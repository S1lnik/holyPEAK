using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.World;

namespace HolyPeak.Scenes
{
    public sealed partial class GameScene
    {
        private static readonly BlendState Multiply = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One, AlphaBlendFunction = BlendFunction.Add,
        };

        // ---- overlays: pause, map, dialogue, shop ---------------------------------------------

        private Menu _pauseMenu, _optionsMenu;
        private int _pauseView;   // 0 main, 1 options, 2 controls
        private float _overlayA;
        private Lore.Speech _speech;
        private int _speechLine;
        private float _speechT;
        private Action _afterSpeech;
        private int _shopIndex;
        private float _shopDeny;

        private void OpenPause()
        {
            _overlay = Overlay.Pause;
            _pauseView = 0;
            AudioManager.Play("pause", 0.7f);
            AudioManager.Duck(0.35f);
            _pauseMenu = new Menu { Back = CloseOverlay };
            _pauseMenu.Add("Resume", CloseOverlay);
            _pauseMenu.Add("Map", () => { _overlay = Overlay.Map; AudioManager.Play("map", 0.6f); });
            _pauseMenu.Add("Options", () =>
            {
                _pauseView = 1;
                _optionsMenu = OptionsMenu.Build(() => { _pauseView = 0; ApplyAssist(); }, () => _pauseView = 2);
            });
            _pauseMenu.Add("Save & Return to Title", () =>
            {
                Persist();
                AudioManager.Duck(1f);
                Game.ChangeScene(new TitleScene(), 0.6f);
            });
        }

        private void OpenMap()
        {
            _overlay = Overlay.Map;
            AudioManager.Play("map", 0.6f);
            AudioManager.Duck(0.5f);
        }

        private void OpenDialogue(Lore.Speech speech, Action after = null)
        {
            _overlay = Overlay.Dialogue;
            _speech = speech;
            _speechLine = 0;
            _speechT = 0;
            _afterSpeech = after;
            AudioManager.Play("talk", 0.6f);
        }

        private void OpenShop()
        {
            OpenDialogue(Lore.Npc("tam", _flags), () =>
            {
                _overlay = Overlay.Shop;
                _shopIndex = 0;
                AudioManager.Play("shop", 0.6f);
            });
        }

        private void ApplyAssist()
        {
            _room.Invincible = Cfg.Invincible;
        }

        private void CloseOverlay()
        {
            _overlay = Overlay.None;
            AudioManager.Duck(1f);
            ApplyAssist();
            Input.Jump.Consume(); Input.Strike.Consume(); Input.Confirm.Consume(); Input.Up.Consume(); Input.Toll.Consume();
        }

        private void UpdateOverlay(float dt)
        {
            _overlayA = Calc.Approach(_overlayA, 1, dt * 6);
            if (Game.Transitioning) return;
            switch (_overlay)
            {
                case Overlay.Pause:
                    switch (_pauseView)
                    {
                        case 0:
                            if (Input.Pause.Pressed && !Input.Confirm.Pressed) { AudioManager.Play("back", 0.7f); CloseOverlay(); return; }
                            _pauseMenu.Update(dt);
                            break;
                        case 1: _optionsMenu.Update(dt); break;
                        case 2:
                            if (Input.Confirm.Pressed || Input.Back.Pressed)
                            {
                                Input.Confirm.Consume(); Input.Back.Consume();
                                AudioManager.Play("back", 0.7f);
                                _pauseView = 1;
                            }
                            break;
                    }
                    break;

                case Overlay.Map:
                    if (Input.Map.Pressed || Input.Back.Pressed || Input.Pause.Pressed)
                    {
                        Input.Back.Consume(); Input.Pause.Consume();
                        AudioManager.Play("back", 0.6f);
                        CloseOverlay();
                    }
                    break;

                case Overlay.Dialogue:
                {
                    _speechT += dt;
                    string line = Input.Format(_speech.Lines[_speechLine]);
                    int shown = (int)(_speechT * 55);
                    if (shown < line.Length && (int)((_speechT - dt) * 55) / 3 != shown / 3 && line[Math.Min(shown, line.Length - 1)] != ' ')
                        AudioManager.Play("blip", 0.18f, _speech.Speaker == "Old Tam" ? -0.3f : 0.1f, pitchVariance: 0.08f);
                    bool advance = Input.Confirm.Pressed || Input.Jump.Pressed || Input.Strike.Pressed || Input.Up.Pressed;
                    if (Input.Back.Pressed && !Input.Strike.Pressed) { CloseOverlay(); _afterSpeech?.Invoke(); _afterSpeech = null; return; }
                    if (!advance) break;
                    Input.Confirm.Consume(); Input.Jump.Consume(); Input.Strike.Consume(); Input.Up.Consume();
                    if (shown < line.Length) { _speechT = 999; break; }
                    _speechLine++;
                    _speechT = 0;
                    if (_speechLine >= _speech.Lines.Length)
                    {
                        CloseOverlay();
                        var after = _afterSpeech;
                        _afterSpeech = null;
                        after?.Invoke();
                    }
                    else AudioManager.Play("talk", 0.4f);
                    break;
                }

                case Overlay.Shop:
                {
                    _shopDeny = Math.Max(0, _shopDeny - dt);
                    var wares = Lore.Wares;
                    int n = wares.Length + 1;
                    if (Input.Down.Repeated) { _shopIndex = (_shopIndex + 1) % n; AudioManager.Play("move", 0.6f); }
                    if (Input.Up.Repeated) { _shopIndex = (_shopIndex + n - 1) % n; AudioManager.Play("move", 0.6f); }
                    if (Input.Back.Pressed && !Input.Strike.Pressed) { Input.Back.Consume(); AudioManager.Play("back", 0.7f); CloseOverlay(); break; }
                    if (Input.Confirm.Pressed || Input.Jump.Pressed)
                    {
                        Input.Confirm.Consume(); Input.Jump.Consume();
                        if (_shopIndex == wares.Length) { AudioManager.Play("back", 0.7f); CloseOverlay(); break; }
                        Buy(wares[_shopIndex]);
                    }
                    break;
                }
            }
        }

        private bool WareAvailable(Lore.Ware w) => !_flags.Contains("bought:" + w.Id) && (w.Requires == null || _flags.Contains("bought:" + w.Requires));

        private void Buy(Lore.Ware w)
        {
            if (!WareAvailable(w) || Save.Embers < w.Price)
            {
                AudioManager.Play("deny", 0.7f);
                _shopDeny = 0.4f;
                return;
            }
            Save.Embers -= w.Price;
            _flags.Add("bought:" + w.Id);
            ref var p = ref _room.P;
            switch (w.Id)
            {
                case "whet1": case "whet2": p.Damage++; break;
                case "flask": p.MaxFlasks++; p.Flasks++; break;
                case "heart": p.MaxHp++; p.Hp++; break;
                case "charm": _room.EmberCharm = true; break;
            }
            AudioManager.Play("buy", 0.8f);
            Persist();
        }

        // ---- lighting --------------------------------------------------------------------

        public override bool UsesLight => _phase != Phase.Prologue && _room != null;

        public override Color LightAmbient
        {
            get
            {
                float dark = _region?.Darkness ?? 0;
                string prop = _room?.Def.Prop("dark");
                if (prop != null) dark = float.Parse(prop, System.Globalization.CultureInfo.InvariantCulture);
                if (_phase == Phase.Ending) dark *= Calc.Clamp01(1 - _phaseT / 2f);
                return Color.Lerp(Color.White, _st?.Shadow ?? Color.Black, dark);
            }
        }

        private Matrix WorldMatrix(out Vector2 cam)
        {
            var camFloor = new Vector2(MathF.Floor(_cam.X), MathF.Floor(_cam.Y));
            SubPixel = _cam - camFloor;
            cam = camFloor + _shakeOff;
            return Matrix.CreateTranslation(-cam.X + 1, -cam.Y + 1, 0);
        }

        public override void DrawLight(SpriteBatch sb)
        {
            var world = WorldMatrix(out var cam);
            sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive, transformMatrix: world);
            if (_room.P.State != PState.Dead || _phase == Phase.Dying && _phaseT < 0.6f)
            {
                float k = _pv.LanternGlow;
                WorldRenderer.Light(sb, _pv.LanternPos, 84 * k, G.Hex(0xffe2b0) * 0.85f);
                WorldRenderer.Light(sb, _room.P.Center, 34, Color.White * 0.5f);
            }
            _wr.DrawLights(sb, _time, cam);
            foreach (var r in _rings) WorldRenderer.Light(sb, r.pos, r.max * 1.6f, WorldRenderer.NoteColor * (0.6f * (1 - r.t / 0.45f)));
            if (_flash > 0) WorldRenderer.Light(sb, _room.P.Center, 300, _flashColor * _flash);
            sb.End();
        }

        // ---- world -----------------------------------------------------------------------

        private static Backdrop _prologueBd;

        public override void DrawWorld(SpriteBatch sb)
        {
            if (_phase == Phase.Prologue)
            {
                _prologueBd ??= new Backdrop(Style.Title, 70, 0.5f, 0.2f, 0.55f);
                SubPixel = Vector2.Zero;
                sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(1, 1, 0));
                _prologueBd.Draw(sb, new Vector2(_phaseT * 3f, 0), new Point(G.ViewW, G.ViewH), _time, 0.8f);
                sb.Draw(G.Vignette, Vector2.Zero, Color.White);
                sb.End();
                return;
            }
            var world = WorldMatrix(out var cam);
            var screen = Matrix.CreateTranslation(1, 1, 0);

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: screen);
            Backdrops[_region.Style].Draw(sb, cam, new Point(_room.Width, _room.Height), _time);
            sb.End();

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: world);
            _wr.DrawBack(sb);
            _wr.DrawThings(sb, _time, _room.P.Ab);
            _wr.DrawTerrain(sb);
            _wr.DrawDynamic(sb, _time);
            _wr.DrawEnemies(sb, _time);
            _wr.DrawBoss(sb, _time);
            bool showPlayer = _phase != Phase.Dying && _room.P.State != PState.Dead && !(_phase == Phase.Fading && _room.HazardPending);
            if (_phase == Phase.Dying && _phaseT < 0.35f)
                Sprites.DrawFeet(sb, "hurt", _room.P.Feet, Color.White * (1 - _phaseT / 0.35f), _room.P.Facing < 0, white: true);
            if (showPlayer) _pv.Draw(sb, _room.P);
            _wr.DrawDrops(sb, _time);
            _wr.DrawProjectiles(sb, _time);
            _parts.Draw(sb, false);
            _wr.DrawHints(sb, _room.P.Center, _time);
            DrawPrompt(sb);
            sb.End();

            // darkness: multiply the scene by the light map
            if (Game.LightTarget != null && UsesLight)
            {
                sb.Begin(samplerState: SamplerState.PointClamp, blendState: Multiply);
                sb.Draw(Game.LightTarget, Vector2.Zero, Color.White);
                sb.End();
            }

            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive, transformMatrix: world);
            _wr.DrawGlows(sb, _time);
            if (showPlayer) _pv.DrawGlow(sb, _room.P);
            _parts.Draw(sb, true);
            foreach (var r in _rings)
            {
                float k = Calc.EaseOutCubic(r.t / 0.3f);
                float rad = 4 + (r.max - 4) * k;
                float a = 1 - Calc.Clamp01(r.t / 0.45f);
                int n = (int)(rad * 2.2f) + 10;
                for (int i = 0; i < n; i++)
                {
                    float ang = i / (float)n * Calc.Tau;
                    G.Rect(sb, MathF.Round(r.pos.X + MathF.Cos(ang) * rad), MathF.Round(r.pos.Y + MathF.Sin(ang) * rad), 1, 1, r.col * a);
                }
            }
            sb.End();

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: screen);
            DrawAmbient(sb, false);
            sb.Draw(G.Vignette, Vector2.Zero, Color.White);
            sb.End();
            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive, transformMatrix: screen);
            DrawAmbient(sb, true);
            sb.End();
        }

        /// <summary>"↑ Rest", "↑ Talk"... above whatever the pilgrim can use.</summary>
        private void DrawPrompt(SpriteBatch sb)
        {
            if (_room.NearThing < 0 || _phase != Phase.Playing || _overlay != Overlay.None) return;
            var t = _room.Things[_room.NearThing];
            string verb = t.Kind switch
            {
                ThingKind.Shrine => "Rest", ThingKind.Merchant => "Trade", ThingKind.Npc => "Talk", ThingKind.Tablet => "Read",
                ThingKind.Altar => "Take", ThingKind.GreatBell => "Ring the bell", _ => null,
            };
            if (verb == null) return;
            string s = Input.Format("{UP} " + verb);
            float w = G.Font.Measure(s);
            float y = t.Box.Top - 12 + MathF.Sin(_time * 4) * 1;
            var pos = new Vector2(MathF.Round(t.X - w / 2), MathF.Round(y));
            G.Rect(sb, pos.X - 3, pos.Y - 2, w + 6, 11, Color.Black * 0.55f);
            G.Font.Draw(sb, s, pos, Color.White);
        }

        // ---- interface --------------------------------------------------------------------

        public override void DrawUI(SpriteBatch sb)
        {
            var font = G.Font;
            sb.Begin(samplerState: SamplerState.PointClamp);

            if (_flash > 0) G.Rect(sb, 0, 0, G.ViewW, G.ViewH, _flashColor * (_flash * 0.6f));
            if (_hurtVignette > 0 || (_room != null && _room.P.Hp == 1 && _room.P.State == PState.Normal))
            {
                float a = Math.Max(_hurtVignette, _room.P.Hp == 1 ? 0.35f + 0.2f * MathF.Sin(_time * 5) : 0);
                sb.Draw(G.Vignette, new Rectangle(0, 0, G.ViewW, G.ViewH), G.Hex(0xff2040) * a);
            }

            if (_phase != Phase.Prologue && _room != null)
            {
                DrawHud(sb);
                DrawAreaCard(sb);
                DrawBossCard(sb);
                DrawToasts(sb);
            }

            if (_phase == Phase.Prologue) DrawPrologue(sb);
            if (_phase == Phase.Ability) DrawAbilityPanel(sb);
            if (_phase == Phase.Ending)
            {
                float a = Calc.Clamp01((_phaseT - 4.5f) / 2f);
                G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.White * a);
            }

            if (_fade > 0) G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.Black * Calc.SmoothStep(_fade));

            if (_overlay == Overlay.None) _overlayA = 0;
            switch (_overlay)
            {
                case Overlay.Pause: DrawPause(sb); break;
                case Overlay.Map: DrawMap(sb); break;
                case Overlay.Dialogue: DrawDialogue(sb); break;
                case Overlay.Shop: DrawShop(sb); break;
            }
            sb.End();
        }

        private float _bossTrail = 1;

        private void DrawHud(SpriteBatch sb)
        {
            var font = G.Font;
            var p = _room.P;
            float hudA = _phase == Phase.Ending ? Calc.Clamp01(1 - _phaseT) : 1f;
            if (hudA <= 0) return;

            // vigil flames
            for (int i = 0; i < p.MaxHp; i++)
            {
                bool lit = i < p.Hp;
                var f = Sprites.Frames[lit ? "hpFull" : "hpEmpty"];
                float bob = lit && p.Hp == 1 ? MathF.Sin(_time * 10) * 0.6f : 0;
                sb.Draw(Sprites.Sheet, new Vector2(5 + i * 8, 4 + bob), f, Color.White * hudA);
            }
            // balm flasks, the next one filling while healing
            for (int i = 0; i < p.MaxFlasks; i++)
            {
                var f = Sprites.Frames[i < p.Flasks ? "flaskFull" : "flaskEmpty"];
                sb.Draw(Sprites.Sheet, new Vector2(6 + i * 7, 16), f, Color.White * hudA);
            }
            if (p.Healing)
            {
                float k = p.HealT / PlayerSim.HealTime;
                G.Rect(sb, 6, 25, 30, 2, Color.Black * 0.6f);
                G.Rect(sb, 6, 25, 30 * k, 2, PlayerVisual.Lantern);
            }
            // embers
            var ef = Sprites.Frames["ember" + ((int)(_time * 8) % 3)];
            sb.Draw(Sprites.Sheet, new Vector2(6, 29), ef, Color.White * hudA);
            font.DrawShadowed(sb, Save.Embers.ToString(), new Vector2(14, 30), G.Hex(0xffd9a0) * hudA, Color.Black * hudA);

            if (Cfg.ShowTimer)
            {
                string t = Calc.FormatTime(Save.PlayTime);
                float w = font.Measure(t);
                G.Rect(sb, G.ViewW - w - 8, 2, w + 8, 11, Color.Black * 0.55f);
                font.Draw(sb, t, new Vector2(G.ViewW - w - 4, 4), Color.White);
            }

            // guardian health
            var b = _room.Boss;
            bool show = b != null && b.Active && !b.Finished;
            _bossBarA = Calc.Approach(_bossBarA, show ? 1 : 0, 1 / 30f);
            if (_bossBarA > 0.01f && b != null)
            {
                float a = _bossBarA;
                float frac = b.HpFraction;
                _bossTrail = frac > _bossTrail ? frac : Calc.Approach(_bossTrail, frac, 1 / 60f * 0.4f);
                float bw = 180, bx = (G.ViewW - bw) / 2, by = 15;
                for (int i = 0; i < 26; i++) G.Rect(sb, 0, i, G.ViewW, 1, Color.Black * (0.5f * a * (1 - i / 26f)));
                font.DrawCentered(sb, b.Name, new Vector2(G.ViewW / 2f, 3), G.Hex(0xffe8c0) * a, Color.Black * a);
                G.Rect(sb, bx - 1, by - 1, bw + 2, 6, Color.Black * (0.8f * a));
                G.Rect(sb, bx, by, bw * _bossTrail, 4, G.Hex(0xffe8c0) * (0.6f * a));
                G.Rect(sb, bx, by, bw * frac, 4, G.Hex(0xd8443c) * a);
                G.Rect(sb, bx, by, bw * frac, 1, G.Hex(0xff8a7a) * a);
                if (b is Bellkeeper k && k.ShieldUp && k.Phase < 3) G.RectOutline(sb, bx - 2, by - 2, bw + 4, 8, WorldRenderer.GlassColor * (0.7f * a));
            }
        }

        private void DrawAreaCard(SpriteBatch sb)
        {
            if (_cardT > 4.2f || _cardName == null) return;
            var font = G.Font;
            float a = Calc.Clamp01(_cardT / 0.7f) * Calc.Clamp01((4.2f - _cardT) / 0.9f);
            float y = 46;
            font.DrawCentered(sb, _cardName, new Vector2(G.ViewW / 2f, y), Color.White * a, Color.Black * a, 2f);
            Fx.Ornament(sb, new Vector2(G.ViewW / 2f, y + 22), 70 * Calc.EaseOutCubic(Calc.Clamp01(_cardT / 1.2f)), _st.Accent1 * a);
            font.DrawCentered(sb, _cardSub, new Vector2(G.ViewW / 2f, y + 28), new Color(210, 205, 225) * a, Color.Black * (a * 0.7f));
        }

        private void DrawBossCard(SpriteBatch sb)
        {
            if (_bossCardT > 3.4f || _bossName == null) return;
            var font = G.Font;
            float a = Calc.Clamp01((_bossCardT - 0.3f) / 0.5f) * Calc.Clamp01((3.4f - _bossCardT) / 0.6f);
            float y = G.ViewH - 62;
            font.DrawCentered(sb, _bossTitle, new Vector2(G.ViewW / 2f, y), new Color(210, 200, 220) * a, Color.Black * a);
            font.DrawCentered(sb, _bossName, new Vector2(G.ViewW / 2f, y + 11), G.Hex(0xffe8c0) * a, Color.Black * a, 2f);
        }

        private void DrawToasts(SpriteBatch sb)
        {
            var font = G.Font;
            float y = _bossBarA > 0.1f ? 28 : 8;
            foreach (var (text, color, t) in _toasts)
            {
                float a = Calc.Clamp01(t / 0.3f) * Calc.Clamp01((3.2f - t) / 0.6f);
                float w = font.Measure(text) + 12;
                G.Rect(sb, G.ViewW / 2f - w / 2f, y - 2, w, 12, Color.Black * (0.6f * a));
                font.DrawCentered(sb, text, new Vector2(G.ViewW / 2f, y), color * a);
                y += 14;
            }
        }

        private void DrawPrologue(SpriteBatch sb)
        {
            var font = G.Font;
            float fadeIn = Calc.Clamp01(_phaseT / 2.5f);
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.Black * (1f - 0.45f * fadeIn));
            float y = 58;
            for (int i = 0; i < PrologueLines.Length; i++)
            {
                float t = _phaseT - 0.8f - i * 2.1f;
                float a = Calc.Clamp01(t / 1.2f);
                if (a <= 0) continue;
                font.DrawCentered(sb, PrologueLines[i], new Vector2(G.ViewW / 2f, y + (1 - Calc.EaseOutCubic(a)) * 4), new Color(225, 220, 235) * a);
                y += i == 1 ? 22 : 14;
            }
            float hintA = Calc.Clamp01((_phaseT - 2f) / 1f) * (0.4f + 0.2f * MathF.Sin(_time * 3));
            font.DrawCentered(sb, Input.Format("{CONFIRM} continue"), new Vector2(G.ViewW / 2f, G.ViewH - 18), Color.White * hintA);
        }

        private void DrawAbilityPanel(SpriteBatch sb)
        {
            var font = G.Font;
            float a = Calc.Clamp01((_phaseT - 1.0f) / 0.5f);
            if (a <= 0) return;
            var (name, lines) = Lore.AbilityInfo(_gotAbility);
            float h = 26 + lines.Length * 11;
            float y = 108 + (1 - Calc.EaseOutCubic(a)) * 8;
            Color accent = G.Hex(0xffd9a0);
            Fx.Panel(sb, 30, y, G.ViewW - 60, h, a, accent);
            font.DrawCentered(sb, name, new Vector2(G.ViewW / 2f, y + 6), accent * a, Color.Black * a);
            for (int i = 0; i < lines.Length; i++)
                font.DrawCentered(sb, Input.Format(lines[i]), new Vector2(G.ViewW / 2f, y + 20 + i * 11), Color.White * a);
            if (_phaseT > 1.6f)
                font.DrawCentered(sb, Input.Format("{CONFIRM}"), new Vector2(G.ViewW / 2f, y + h + 3), Color.White * (0.5f + 0.3f * MathF.Sin(_time * 4)));
        }

        private void DrawPause(SpriteBatch sb)
        {
            var font = G.Font;
            float a = _overlayA;
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, new Color(6, 6, 16) * (0.72f * a));
            Color accent = _st.Accent1;
            if (_pauseView == 2) { OptionsMenu.DrawControls(sb, a, accent); return; }
            if (_pauseView == 1) { OptionsMenu.Draw(sb, _optionsMenu, a, accent); return; }
            font.DrawCentered(sb, "Paused", new Vector2(G.ViewW / 2f, 24), Color.White * a, Color.Black * a, 2f);
            font.DrawCentered(sb, _region.Name, new Vector2(G.ViewW / 2f, 50), accent * a);
            string stats = $"Deaths {Save.Deaths}   Found {Progress.Percent(_flags)}%   {Calc.FormatTime(Save.PlayTime, false)}";
            font.DrawCentered(sb, stats, new Vector2(G.ViewW / 2f, 62), new Color(190, 190, 210) * a);
            Fx.Ornament(sb, new Vector2(G.ViewW / 2f, 78), 70, accent * (0.6f * a));
            _pauseMenu.Draw(sb, new Vector2(G.ViewW / 2f, 118), a, accent);
        }

        private void DrawDialogue(SpriteBatch sb)
        {
            var font = G.Font;
            float a = _overlayA;
            float x = 20, y = G.ViewH - 58, w = G.ViewW - 40, h = 48;
            Fx.Panel(sb, x, y, w, h, a, _st.Accent1);
            float ty = y + 8;
            if (_speech.Speaker != null)
            {
                font.DrawShadowed(sb, _speech.Speaker, new Vector2(x + 8, y + 6), _st.Accent1 * a, Color.Black * a);
                ty += 11;
            }
            string line = Input.Format(_speech.Lines[_speechLine]);
            int shown = Math.Min(line.Length, (int)(_speechT * 55));
            // wrap the full line so words don't jump between rows while typing
            var full = font.Wrap(line, (int)w - 16);
            int left = shown;
            for (int i = 0; i < full.Count && left > 0; i++)
            {
                string part = full[i].Length <= left ? full[i] : full[i].Substring(0, left);
                font.Draw(sb, part, new Vector2(x + 8, ty + i * 10), new Color(235, 230, 240) * a);
                left -= full[i].Length + 1;
            }
            if (shown >= line.Length && ((int)(_time * 3) & 1) == 0)
                G.Rect(sb, x + w - 10, y + h - 8, 4, 3, _st.Accent1 * a);
        }

        private void DrawShop(SpriteBatch sb)
        {
            var font = G.Font;
            float a = _overlayA;
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, new Color(6, 6, 16) * (0.6f * a));
            Color accent = G.Hex(0xffd9a0);
            float px = 34, py = 18, pw = G.ViewW - 68, ph = G.ViewH - 36;
            Fx.Panel(sb, px, py, pw, ph, a, accent);
            font.DrawCentered(sb, "Old Tam's Wares", new Vector2(G.ViewW / 2f, py + 6), accent * a, Color.Black * a);
            var ef = Sprites.Frames["ember" + ((int)(_time * 8) % 3)];
            string have = Save.Embers.ToString();
            float hw = font.Measure(have);
            sb.Draw(Sprites.Sheet, new Vector2(px + pw - hw - 18, py + 5), ef, Color.White * a);
            font.Draw(sb, have, new Vector2(px + pw - hw - 9, py + 6), (_shopDeny > 0 ? G.Hex(0xff6a5a) : G.Hex(0xffd9a0)) * a);

            var wares = Lore.Wares;
            float y = py + 22;
            for (int i = 0; i <= wares.Length; i++)
            {
                bool sel = i == _shopIndex;
                if (sel) G.Rect(sb, px + 6, y - 2, pw - 12, 11, accent * (0.18f * a));
                if (i == wares.Length)
                {
                    font.Draw(sb, "Leave", new Vector2(px + 14, y), (sel ? Color.White : new Color(180, 180, 195)) * a);
                    break;
                }
                var w = wares[i];
                bool sold = _flags.Contains("bought:" + w.Id);
                bool locked = !sold && w.Requires != null && !_flags.Contains("bought:" + w.Requires);
                Color c = sold || locked ? new Color(110, 105, 120) : (sel ? Color.White : new Color(205, 200, 215));
                font.Draw(sb, locked ? "???" : w.Name, new Vector2(px + 14, y), c * a);
                string price = sold ? "sold" : (locked ? "" : w.Price.ToString());
                float pwid = font.Measure(price);
                font.Draw(sb, price, new Vector2(px + pw - 14 - pwid, y), (sold ? new Color(110, 105, 120) : (Save.Embers >= w.Price ? accent : G.Hex(0xb07060))) * a);
                y += 12;
            }
            string desc = _shopIndex < wares.Length
                ? (WareAvailable(wares[_shopIndex]) || _flags.Contains("bought:" + wares[_shopIndex].Id) ? wares[_shopIndex].Desc : "Tam isn't ready to part with this yet.")
                : "Come back with fuller pockets!";
            var lines = font.Wrap(desc, (int)pw - 24);
            for (int i = 0; i < lines.Count; i++)
                font.DrawCentered(sb, lines[i], new Vector2(G.ViewW / 2f, py + ph - 26 + i * 10), new Color(190, 185, 205) * a);
            font.DrawCentered(sb, Input.Format("{CONFIRM} buy   {BACK} leave"), new Vector2(G.ViewW / 2f, py + ph - 4), accent * (0.7f * a));
        }

        // ---- the map -----------------------------------------------------------------------

        private static readonly Dictionary<string, Texture2D> MapTiles = new Dictionary<string, Texture2D>();
        private const int MapCellW = 20, MapCellH = 11;

        private static Texture2D MapTexture(RoomDef r)
        {
            if (MapTiles.TryGetValue(r.Id, out var t)) return t;
            int w = r.CellW * MapCellW, h = r.CellH * MapCellH;
            var data = new Color[w * h];
            var st = Style.Regions[Atlas.Region(r.Region).Style];
            Color wall = G.Mix(st.Light, st.Light2, 0.35f), air = G.Mix(st.Back, st.Shadow, 0.3f) * 0.85f;
            for (int my = 0; my < h; my++)
                for (int mx = 0; mx < w; mx++)
                {
                    int tx0 = mx * r.Cols / w, tx1 = Math.Max(tx0, (mx + 1) * r.Cols / w - 1);
                    int ty0 = my * r.Rows / h, ty1 = Math.Max(ty0, (my + 1) * r.Rows / h - 1);
                    int solid = 0, total = 0;
                    for (int ty = ty0; ty <= ty1; ty++)
                        for (int tx = tx0; tx <= tx1; tx++)
                        {
                            char c = r.Map[ty][tx];
                            total++;
                            if (c == '#' || c == '=' || c == 'x') solid++;
                        }
                    data[my * w + mx] = solid * 2 > total ? wall : air;
                }
            t = G.Make(w, h, data);
            MapTiles[r.Id] = t;
            return t;
        }

        private void DrawMap(SpriteBatch sb)
        {
            var font = G.Font;
            float a = _overlayA;
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, new Color(10, 10, 22) * a);
            for (int y = 0; y < G.ViewH; y += 3)
                for (int x = (y / 3) % 2 * 3; x < G.ViewW; x += 6) G.Rect(sb, x, y, 1, 1, new Color(40, 40, 70) * (0.5f * a));
            sb.Draw(G.Vignette, new Rectangle(0, 0, G.ViewW, G.ViewH), Color.White * a);
            int cw = Atlas.MaxCellX - Atlas.MinCellX + 1, ch = Atlas.MaxCellY - Atlas.MinCellY + 1;
            float ox = MathF.Round((G.ViewW - cw * MapCellW) / 2f), oy = MathF.Round((G.ViewH - ch * MapCellH) / 2f) + 4;
            bool surveyed = _flags.Contains("bought:map");
            foreach (var r in Atlas.Rooms)
            {
                bool seen = Save.Visited.Contains(r.Id);
                if (!seen && !surveyed) continue;
                var tex = MapTexture(r);
                var pos = new Vector2(ox + (r.CellX - Atlas.MinCellX) * MapCellW, oy + (r.CellY - Atlas.MinCellY) * MapCellH);
                sb.Draw(tex, pos, Color.White * (a * (seen ? 1f : 0.35f)));
                G.RectOutline(sb, pos.X, pos.Y, tex.Width, tex.Height, Color.Black * (0.5f * a));
                if (!seen) continue;
                // icons
                foreach (var (c, col) in new[] { ('G', G.Hex(0xffd070)), ('M', G.Hex(0x9ad0ff)) })
                    for (int y = 0; y < r.Rows; y++)
                    {
                        int x = r.Map[y].IndexOf(c);
                        if (x < 0) continue;
                        float ix = pos.X + x * tex.Width / (float)r.Cols, iy = pos.Y + y * tex.Height / (float)r.Rows;
                        G.Rect(sb, MathF.Round(ix) - 1, MathF.Round(iy) - 2, 3, 3, col * a);
                    }
                string boss = r.Prop("boss");
                if (boss != null && !_flags.Contains("boss:" + boss))
                    G.Rect(sb, pos.X + tex.Width / 2 - 1, pos.Y + tex.Height / 2 - 1, 3, 3, G.Hex(0xd8443c) * a);
                if (Save.ShadeRoom == r.Id)
                    G.Rect(sb, pos.X + Save.ShadeX * tex.Width / (float)(r.Cols * 8) - 1, pos.Y + Save.ShadeY * tex.Height / (float)(r.Rows * 8) - 2, 3, 3, G.Hex(0xa080ff) * a);
            }
            // you are here
            var def = _room.Def;
            var me = new Vector2(ox + (def.CellX - Atlas.MinCellX) * MapCellW + _room.P.Center.X / 8f * MapCellW / Atlas.CellCols,
                                 oy + (def.CellY - Atlas.MinCellY) * MapCellH + _room.P.Center.Y / 8f * MapCellH / Atlas.CellRows);
            if (((int)(_time * 3) & 1) == 0) G.Rect(sb, MathF.Round(me.X) - 1, MathF.Round(me.Y) - 1, 3, 3, Color.White * a);
            font.DrawCentered(sb, _region.Name, new Vector2(G.ViewW / 2f, 6), _st.Accent1 * a, Color.Black * a);
            float ly = G.ViewH - 11;
            G.Rect(sb, 40, ly + 2, 3, 3, G.Hex(0xffd070) * a); font.Draw(sb, "shrine", new Vector2(46, ly), new Color(190, 190, 210) * a);
            G.Rect(sb, 90, ly + 2, 3, 3, G.Hex(0x9ad0ff) * a); font.Draw(sb, "peddler", new Vector2(96, ly), new Color(190, 190, 210) * a);
            G.Rect(sb, 146, ly + 2, 3, 3, G.Hex(0xd8443c) * a); font.Draw(sb, "guardian", new Vector2(152, ly), new Color(190, 190, 210) * a);
            G.Rect(sb, 206, ly + 2, 3, 3, G.Hex(0xa080ff) * a); font.Draw(sb, "your flame", new Vector2(212, ly), new Color(190, 190, 210) * a);
        }
    }
}
