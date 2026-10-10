using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.World;

namespace HolyPeak.Scenes
{
    public sealed class TitleScene : Scene
    {
        private static readonly RasterizerState Scissor = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        private enum View { Main, Options, Controls, Credits, ConfirmNew }

        private static Backdrop _bd;
        private float _time, _intro;
        private View _view = View.Main;
        private Menu _main, _confirm, _options;
        private readonly PlayerVisual _pv = new PlayerVisual();
        private PlayerSim _pilgrim;
        private readonly Particles _parts = new Particles(256);
        private Vector2[] _snow;
        private float[] _snowS;

        private SaveData Save => Game.Save;

        public override void Begin()
        {
            _bd ??= new Backdrop(Style.Title, 104, 0.3f, 0.05f, 0.45f);
            AudioManager.PlayMusic(SongId.Title, 2.5f);
            AudioManager.SetWind(0.12f);
            BuildMenus();
            _pilgrim.Spawn(new Point(52, 150), 1);
            _pv.HasBell = Save.Flags.Contains("ab:bell");
            _pv.Reset(_pilgrim);
            var rng = new Rng(77);
            _snow = new Vector2[120];
            _snowS = new float[_snow.Length];
            for (int i = 0; i < _snow.Length; i++) { _snow[i] = new Vector2(rng.Range(0, 340), rng.Range(0, 200)); _snowS[i] = rng.Range(0.3f, 1f); }
        }

        private void BuildMenus()
        {
            _main = new Menu();
            bool started = Save.Started;
            if (started) _main.Add(Save.Completed ? "Return to the Mountain" : "Continue", Continue);
            _main.Add("New Journey", () =>
            {
                if (Save.Started) { _view = View.ConfirmNew; _confirm.Index = 1; }
                else NewJourney();
            });
            _main.Add("Options", () =>
            {
                _view = View.Options;
                _options = OptionsMenu.Build(() => _view = View.Main, () => _view = View.Controls);
            });
            _main.Add("Credits", () => _view = View.Credits);
            _main.Add("Quit", () => Game.RequestQuit());
            _main.Back = null;
            _main.SelectFirstEnabled();

            _confirm = new Menu { Title = "Begin a new journey?", Back = () => _view = View.Main };
            _confirm.Add("Yes, start over", NewJourney);
            _confirm.Add("No", () => _view = View.Main);
        }

        private void Continue()
        {
            Game.ChangeScene(new GameScene(false), 0.8f);
        }

        private void NewJourney()
        {
            Game.Save = new SaveData { Started = true };
            Game.WriteSave();
            AudioManager.StopMusic(1.5f);
            Game.ChangeScene(new GameScene(true), 1.0f);
        }

        public override void Update(float dt)
        {
            _time += dt;
            _intro = Math.Min(1, _intro + dt * 0.5f);
            _pv.Update(_pilgrim, dt, 30f + MathF.Sin(_time * 0.7f) * 20f, _parts, Style.Title);
            _parts.Update(dt);
            for (int i = 0; i < _snow.Length; i++)
            {
                _snow[i] += new Vector2(10f + MathF.Sin(_time + i) * 6f, 14f + _snowS[i] * 16f) * _snowS[i] * dt;
                if (_snow[i].Y > 190) _snow[i] = new Vector2(_snow[i].X - 30, -6);
                if (_snow[i].X > 330) _snow[i].X -= 340;
                if (_snow[i].X < -10) _snow[i].X += 340;
            }
            if (Game.Transitioning || _intro < 0.35f) return;

            switch (_view)
            {
                case View.Main: _main.Update(dt); break;
                case View.ConfirmNew: _confirm.Update(dt); break;
                case View.Options: _options.Update(dt); break;
                case View.Controls:
                case View.Credits:
                    if (Input.Confirm.Pressed || Input.Back.Pressed)
                    {
                        Input.Confirm.Consume(); Input.Back.Consume();
                        AudioManager.Play("back", 0.7f);
                        _view = _view == View.Controls ? View.Options : View.Main;
                    }
                    break;
            }
        }

        public override void DrawWorld(SpriteBatch sb)
        {
            SubPixel = Vector2.Zero;
            var cam = new Vector2(_time * 4f, 0);
            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(1, 1, 0));
            _bd.Draw(sb, cam, new Point(G.ViewW, G.ViewH), _time);

            // a rocky outcrop for the pilgrim to stand on
            Color rock = G.Hex(0x0d1028), rockL = G.Hex(0x272c55), snowC = G.Hex(0xdfe8ff);
            for (int x = 0; x < 120; x++)
            {
                float h = 160 - MathF.Max(0, 26 - MathF.Abs(x - 54) * 0.55f) + MathF.Sin(x * 0.7f) * 1.2f + (x > 90 ? (x - 90) * 0.9f : 0);
                G.Rect(sb, x, MathF.Round(h), 1, G.ViewH - h, rock);
                G.Rect(sb, x, MathF.Round(h), 1, 2, x < 70 ? snowC : rockL);
            }
            _pilgrim.Y = 160 - 26 - PlayerSim.H + 1;   // the summit of the outcrop
            _pilgrim.X = 51;
            _pv.Draw(sb, _pilgrim);
            _parts.Draw(sb, false);

            for (int i = 0; i < _snow.Length; i++)
            {
                var p = _snow[i];
                float s = _snowS[i];
                G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), s > 0.8f ? 2 : 1, s > 0.8f ? 2 : 1, Color.White * (0.35f + s * 0.5f));
            }
            sb.Draw(G.Vignette, Vector2.Zero, Color.White);
            sb.End();
        }

        public override void DrawUI(SpriteBatch sb)
        {
            var font = G.Font;
            Color gold = G.Hex(0xffd88a), accent = G.Hex(0xffc96b);
            sb.Begin(samplerState: SamplerState.PointClamp);
            float a = Calc.Clamp01(_intro * 1.6f);

            bool logo = _view == View.Main || _view == View.ConfirmNew;
            if (logo)
            {
                // logo
                float ly = 26 + (1 - Calc.EaseOutCubic(a)) * -10;
                string title = "HOLY PEAK";
                float w = font.Measure(title) * 4;
                var p = new Vector2(MathF.Round(G.ViewW / 2f - w / 2f), MathF.Round(ly));
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        font.Draw(sb, title, p + new Vector2(ox * 2, oy * 2), G.Hex(0x1a1030) * a, 4f);
                font.Draw(sb, title, p + new Vector2(0, 4), G.Hex(0x7a4a2a) * a, 4f);
                font.Draw(sb, title, p, G.Hex(0xf0b44a) * a, 4f);
                sb.End();
                var gd = sb.GraphicsDevice;
                var oldScissor = gd.ScissorRectangle;
                gd.ScissorRectangle = new Rectangle(0, (int)p.Y, G.ViewW, 13);
                sb.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Scissor);
                font.Draw(sb, title, p, G.Hex(0xffe7a6) * a, 4f);
                sb.End();
                gd.ScissorRectangle = new Rectangle(0, (int)p.Y, G.ViewW, 4);
                sb.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Scissor);
                font.Draw(sb, title, p, G.Hex(0xfffbe8) * a, 4f);
                sb.End();
                gd.ScissorRectangle = oldScissor;
                sb.Begin(samplerState: SamplerState.PointClamp);
                float sweep = (_time * 0.35f % 2.2f) * (w + 80) - 40;
                gd.ScissorRectangle = new Rectangle((int)(p.X + sweep), 0, 6, G.ViewH);
                sb.End();
                sb.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Scissor);
                font.Draw(sb, title, p, Color.White * (0.55f * a), 4f);
                sb.End();
                gd.ScissorRectangle = oldScissor;
                sb.Begin(samplerState: SamplerState.PointClamp);
                font.DrawCentered(sb, "a pilgrimage up the silent mountain", new Vector2(G.ViewW / 2f, ly + 34), new Color(200, 195, 225) * a, Color.Black * a);
            }

            float ma = Calc.Clamp01((_intro - 0.35f) * 3f);
            switch (_view)
            {
                case View.Main:
                    sb.Draw(G.Glow, new Vector2(G.ViewW / 2f + 50, 124), null, new Color(4, 4, 14) * (0.75f * ma), 0f,
                            new Vector2(G.Glow.Width / 2f), new Vector2(220f / G.Glow.Width, 120f / G.Glow.Height), SpriteEffects.None, 0f);
                    _main.Draw(sb, new Vector2(G.ViewW / 2f + 50, 122), ma, accent);
                    if (Save.Started)
                    {
                        var flags = new System.Collections.Generic.HashSet<string>(Save.Flags);
                        var room = Save.Room != null ? Atlas.Get(Save.Room) : null;
                        string where = room != null ? Atlas.Region(room.Region).Name : Atlas.Regions[0].Name;
                        string info = Save.Completed
                            ? $"The bell has rung · {Progress.Percent(flags)}% found"
                            : $"{where} · {Progress.Percent(flags)}% found";
                        font.DrawCentered(sb, info, new Vector2(G.ViewW / 2f + 50, 164), new Color(150, 150, 180) * ma);
                    }
                    break;
                case View.ConfirmNew:
                    Fx.Panel(sb, G.ViewW / 2f - 90 + 50, 92, 180, 58, ma, accent);
                    _confirm.Draw(sb, new Vector2(G.ViewW / 2f + 50, 122), ma, accent);
                    break;
                case View.Options:
                    OptionsMenu.Draw(sb, _options, ma, accent);
                    break;
                case View.Controls:
                    OptionsMenu.DrawControls(sb, ma, accent);
                    break;
                case View.Credits:
                    DrawCredits(sb, ma, accent);
                    break;
            }

            font.Draw(sb, "v2.0", new Vector2(G.ViewW - 20, G.ViewH - 10), new Color(120, 120, 150) * ma);
            string help = Input.Format(_view == View.Main ? "{CONFIRM} select" : "{CONFIRM} select   " + Input.BackName + " back");
            font.Draw(sb, help, new Vector2(4, G.ViewH - 10), new Color(120, 120, 150) * ma);
            sb.End();
        }

        public static void DrawCredits(SpriteBatch sb, float a, Color accent)
        {
            var font = G.Font;
            Fx.Panel(sb, 30, 14, G.ViewW - 60, G.ViewH - 28, a, accent);
            float y = 24;
            font.DrawCentered(sb, "HOLY PEAK", new Vector2(G.ViewW / 2f, y), accent * a, Color.Black * a, 2f);
            y += 28;
            (string text, bool head)[] lines =
            {
                ("Design, code, pixel art, music & sound", true),
                ("Claude", false),
                ("", false),
                ("Every sprite, tile, note and sound effect", false),
                ("is generated by code at startup.", false),
                ("Every chamber is proven passable by a solver.", false),
                ("", false),
                ("Built with MonoGame and C#", true),
            };
            foreach (var (text, head) in lines)
            {
                if (text.Length > 0)
                    font.DrawCentered(sb, text, new Vector2(G.ViewW / 2f, y), (head ? new Color(170, 165, 200) : new Color(225, 220, 235)) * a);
                y += text.Length == 0 ? 6 : 11;
            }
            font.DrawCentered(sb, "Thank you for climbing.", new Vector2(G.ViewW / 2f, y + 6), accent * a);
            font.DrawCentered(sb, Input.Format("{CONFIRM} back"), new Vector2(G.ViewW / 2f, G.ViewH - 28), new Color(150, 150, 180) * a);
        }
    }
}
