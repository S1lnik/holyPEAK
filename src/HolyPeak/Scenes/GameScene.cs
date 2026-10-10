using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.World;

namespace HolyPeak.Scenes
{
    /// <summary>
    /// Playing the mountain: one live room at a time, quick transitions between rooms on the world grid,
    /// shrines, the peddler, the map, dialogue, guardians, death and the lingering flame.
    /// </summary>
    public sealed partial class GameScene : Scene
    {
        private enum Phase { Prologue, Playing, Fading, Dying, Ability, Ending }
        private enum Overlay { None, Pause, Map, Dialogue, Shop }

        private static readonly Backdrop[] Backdrops = new Backdrop[5];
        private static readonly int[] PeakHeight = { 50, 58, 72, 86, 0 };
        private static readonly float[] PeakHaze = { 0.45f, 0.5f, 0.34f, 0.38f, 0f };

        private static readonly string[] PrologueLines =
        {
            "They say a bell hangs at the top of the Holy Peak,",
            "silent for a thousand years.",
            "They say that while it sleeps, so does the dawn.",
            "You have walked a long way to wake it.",
        };

        private Room _room;
        private WorldRenderer _wr;
        private Style _st;
        private RegionDef _region;
        private readonly PlayerVisual _pv = new PlayerVisual();
        private readonly Particles _parts = new Particles(3000);
        private readonly Random _shakeRng = new Random(5);
        private readonly HashSet<string> _flags;
        private PlayerSim _carry;

        private Phase _phase = Phase.Playing;
        private Overlay _overlay;
        private float _phaseT, _time;
        private Vector2 _cam, _lastCam;
        private float _shakeT, _shakeMag, _freeze, _lookT;
        private int _lookY;
        private Vector2 _shakeOff;
        private float _fade, _fadeSpeed = 6f;
        private int _fadeDir;
        private Action _onFaded;
        private float _flash;
        private Color _flashColor = Color.White;
        private float _hurtVignette;

        // area title card
        private string _cardName, _cardSub;
        private float _cardT = 99;

        // toasts at the top of the screen
        private readonly List<(string text, Color color, float t)> _toasts = new List<(string, Color, float)>();

        // the toll's visible rings
        private readonly List<(Vector2 pos, float t, float max, Color col)> _rings = new List<(Vector2, float, float, Color)>();

        // guardian
        private string _bossName, _bossTitle;
        private float _bossCardT = 99, _bossBarA;

        // ability cutscene
        private Abilities _gotAbility;

        // automation
        private List<PlayerInput> _replay;
        private int _replayIdx;
        private bool _skipIntro;
        private string _autoRoom, _autoPos;

        // ambient
        private struct Amb { public Vector2 P, V; public float Phase, Size; }
        private Amb[] _amb = Array.Empty<Amb>();

        private readonly System.Diagnostics.Stopwatch _loadWatch = new System.Diagnostics.Stopwatch();
        private SaveData Save => Game.Save;
        private Settings Cfg => Game.Settings;

        public GameScene(bool newGame)
        {
            _flags = new HashSet<string>(Save.Flags);
            if (newGame) _phase = Phase.Prologue;
        }

        // ---- set-up ------------------------------------------------------------------------

        public static GameScene ForAutomation(AutomationOptions o)
        {
            var save = HolyPeakGame.Instance.Save;
            if (o.Give != null)
                foreach (var g in o.Give.Split(',', StringSplitOptions.RemoveEmptyEntries)) save.Flags.Add(g.Contains(':') ? g : "ab:" + g);
            var gs = new GameScene(o.Scene == "prologue") { _skipIntro = o.Scene != "prologue", _autoRoom = o.Room, _autoPos = o.Pos };
            if (o.Replay != null)
            {
                gs._replay = new List<PlayerInput>();
                foreach (var line in File.ReadAllLines(o.Replay))
                {
                    var p = line.Split(' ');
                    if (p.Length < 3) continue;
                    var b = p[2];
                    gs._replay.Add(new PlayerInput
                    {
                        MoveX = sbyte.Parse(p[0]), MoveY = sbyte.Parse(p[1]),
                        Jump = b[0] == '1', JumpPressed = b[1] == '1', StrikePressed = b[2] == '1', TollPressed = b[3] == '1',
                        Heal = b.Length > 4 && b[4] == '1', InteractPressed = b.Length > 5 && b[5] == '1',
                    });
                }
            }
            return gs;
        }

        public override void Begin()
        {
            _carry = new PlayerSim
            {
                Hp = Save.MaxHp, MaxHp = Save.MaxHp, Flasks = Save.MaxFlasks, MaxFlasks = Save.MaxFlasks,
                Damage = Save.Damage, Ab = Progress.AbilitiesFrom(_flags), Facing = 1,
            };
            if (_autoRoom != null)
            {
                var def = Atlas.Get(_autoRoom) ?? throw new InvalidOperationException("no room " + _autoRoom);
                LoadRoom(def, null);
                if (_autoPos != null)
                {
                    var p = _autoPos.Split(',');
                    _room.P.X = int.Parse(p[0]) * 8 + 1;
                    _room.P.Y = int.Parse(p[1]) * 8 + 8 - PlayerSim.H;
                    _room.P.SafeX = _room.P.X; _room.P.SafeY = _room.P.Y;
                    _pv.Reset(_room.P);
                    SnapCamera();
                }
            }
            else if (Save.Started && Save.ShrineRoom != null && Atlas.Get(Save.ShrineRoom) != null)
                LoadRoom(Atlas.Get(Save.ShrineRoom), ThingKind.Shrine);
            else
                LoadRoom(Atlas.StartRoom, null);

            Save.Started = true;
            if (_phase == Phase.Prologue)
            {
                AudioManager.StopMusic(2f);
                _phaseT = 0;
            }
            else
            {
                AudioManager.PlayMusic(_region.Song, 2f);
                if (!_skipIntro) ShowAreaCard();
            }
        }

        public override void OnQuit()
        {
            if (Game.Auto != null && _room?.Boss != null) Console.WriteLine($"[auto] boss {_room.Boss.Id} hp {_room.Boss.Hp}/{_room.Boss.MaxHp} phase {_room.Boss.Phase}");
            if (Game.Auto != null && _room != null) Console.WriteLine($"[auto] end: kills {Save.Kills} embers {Save.Embers} hp {_room.P.Hp}/{_room.P.MaxHp} flasks {_room.P.Flasks} alive {_room.Enemies.Count(e => e.Alive)}/{_room.Enemies.Length}");
            Persist();
        }

        public override void End()
        {
            _rumbleT = 0;
            if (Game.Auto == null)
                try { Microsoft.Xna.Framework.Input.GamePad.SetVibration(PlayerIndex.One, 0, 0); } catch (Exception) { }
        }

        /// <summary>Copies everything worth keeping into the save file and writes it.</summary>
        private void Persist()
        {
            Save.Started = true;
            Save.Flags = _flags.ToList();
            if (_room != null)
            {
                Save.Room = _room.Def.Id;
                Save.MaxHp = _room.P.MaxHp;
                Save.MaxFlasks = _room.P.MaxFlasks;
                Save.Damage = _room.P.Damage;
            }
            if (Cfg.AssistActive) Save.UsedAssist = true;
            Game.WriteSave();
        }

        // ---- rooms -------------------------------------------------------------------------

        private void LoadRoom(RoomDef def, ThingKind? spawnAt, Vector2? local = null, Vector2? speed = null)
        {
            _loadWatch.Restart();
            var carry = _room?.P ?? _carry;
            _wr?.Dispose();
            _room = new Room(def, _flags, noEnemies: Game.Auto?.Peaceful == true)
            {
                Invincible = Cfg.Invincible,
                EmberCharm = _flags.Contains("bought:charm"),
            };
            if (Save.ShadeRoom == def.Id && Save.ShadeEmbers > 0) _room.AddShade(Save.ShadeX, Save.ShadeY);
            Point? spawn = null;
            if (!local.HasValue) spawn = spawnAt.HasValue ? _room.SpawnAtThing(spawnAt.Value) : (_room.StartPoint ?? _room.SpawnAtThing(ThingKind.Shrine));
            Travel.Arrive(_room, carry, local, speed ?? Vector2.Zero, spawn);

            var region = Atlas.Region(def.Region);
            bool newRegion = _region != region;
            _region = region;
            _st = Style.Regions[region.Style];
            Backdrops[region.Style] ??= new Backdrop(_st, PeakHeight[region.Style], 0.64f, PeakHaze[region.Style]);
            _wr = new WorldRenderer(_room, _st) { ShrineActive = Save.ShrineRoom == def.Id };
            _pv.HasBell = _room.P.Has(Abilities.Bell);
            _pv.Reset(_room.P);
            _parts.Clear();
            _rings.Clear();
            SnapCamera();
            if (newRegion)
            {
                InitAmbient();
                if (_phase != Phase.Prologue && !_skipIntro) ShowAreaCard();
                if (_phase != Phase.Prologue) AudioManager.PlayMusic(region.Song, 1.5f);
                AudioManager.SetWind(region.Wind);
            }
            if (!Save.Visited.Contains(def.Id)) Save.Visited.Add(def.Id);
            Save.Room = def.Id;
            if (Game.Auto != null) Console.WriteLine($"[auto] room {def.Id} ({region.Id}) hp {_room.P.Hp}/{_room.P.MaxHp} embers {Save.Embers} built in {_loadWatch.ElapsedMilliseconds} ms");
        }

        private void ShowAreaCard()
        {
            _cardName = _region.Name;
            _cardSub = _region.Subtitle;
            _cardT = 0;
        }

        private void SnapCamera()
        {
            _cam = CameraTarget();
            _lastCam = _cam;
        }

        private Vector2 CameraTarget()
        {
            var c = _room.P.Center;
            float look = Math.Clamp(_room.P.Speed.X * 0.15f, -22f, 22f);
            float lookY = _lookT > 0.45f ? Math.Sign(_lookY) * 48f * Calc.SmoothStep((_lookT - 0.45f) / 0.4f) : 0;
            var t = new Vector2(c.X + look - G.ViewW / 2f, c.Y - G.ViewH / 2f - 6 + lookY);
            t.X = Math.Clamp(t.X, 0, Math.Max(0, _room.Width - G.ViewW));
            t.Y = Math.Clamp(t.Y, 0, Math.Max(0, _room.Height - G.ViewH));
            return t;
        }

        // ---- update ------------------------------------------------------------------------

        public override void Update(float dt)
        {
            _time += dt;
            UpdateRumble(dt);
            _flash = Math.Max(0, _flash - dt * 2.5f);
            _hurtVignette = Math.Max(0, _hurtVignette - dt * 1.5f);
            _cardT += dt;
            _bossCardT += dt;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.t += dt;
                if (t.t > 3.2f) _toasts.RemoveAt(i); else _toasts[i] = t;
            }
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i];
                r.t += dt;
                if (r.t > 0.45f) _rings.RemoveAt(i); else _rings[i] = r;
            }

            UpdateFade(dt);
            if (_overlay != Overlay.None) { UpdateOverlay(dt); return; }

            if (_shakeT > 0)
            {
                _shakeT -= dt;
                float m = _shakeMag * Calc.Clamp01(_shakeT / 0.2f);
                _shakeOff = new Vector2(MathF.Round((float)(_shakeRng.NextDouble() * 2 - 1) * m), MathF.Round((float)(_shakeRng.NextDouble() * 2 - 1) * m));
            }
            else _shakeOff = Vector2.Zero;

            switch (_phase)
            {
                case Phase.Prologue: UpdatePrologue(dt); break;
                case Phase.Playing: UpdatePlaying(dt); break;
                case Phase.Fading: UpdateVisualsOnly(dt); break;
                case Phase.Dying: UpdateDying(dt); break;
                case Phase.Ability: UpdateAbility(dt); break;
                case Phase.Ending: UpdateEnding(dt); break;
            }
            UpdateAmbient(dt);
        }

        private void UpdateFade(float dt)
        {
            if (_fadeDir > 0)
            {
                _fade = Math.Min(1, _fade + dt * _fadeSpeed);
                if (_fade >= 1)
                {
                    _fadeDir = -1;
                    var cb = _onFaded;
                    _onFaded = null;
                    cb?.Invoke();
                }
            }
            else if (_fadeDir < 0)
            {
                _fade = Math.Max(0, _fade - dt * _fadeSpeed);
                if (_fade <= 0) _fadeDir = 0;
            }
        }

        private void FadeThrough(float seconds, Action mid)
        {
            _fadeDir = 1;
            _fadeSpeed = 1f / Math.Max(0.05f, seconds);
            _onFaded = mid;
        }

        private void UpdateVisualsOnly(float dt)
        {
            _parts.Update(dt);
            _pv.Update(_room.P, dt, 0, _parts, _st);
            _wr.Update(dt);
            _cam = Vector2.Lerp(_cam, CameraTarget(), Calc.Damp(6f, dt));
        }

        private void UpdatePrologue(float dt)
        {
            _phaseT += dt;
            float total = 2.2f + PrologueLines.Length * 2.3f;
            if ((_phaseT > 0.6f && (Input.Confirm.Pressed || Input.Jump.Pressed)) || _phaseT > total)
            {
                Input.Confirm.Consume(); Input.Jump.Consume();
                _phase = Phase.Playing;
                AudioManager.PlayMusic(_region.Song, 3f);
                ShowAreaCard();
            }
        }

        private int _botT;
        private bool _botJump;

        /// <summary>Automation only: a crude fighter that walks up to the nearest foe and hits it.</summary>
        private PlayerInput BotInput()
        {
            _botT++;
            var inp = new PlayerInput();
            var p = _room.P;
            var c = p.Center;
            Vector2? target = null;
            var b = _room.Boss;
            if (b != null && b.Active && !b.Dead && !b.InIntro)
                target = b.Hurtboxes.Count > 0 ? b.Hurtboxes[0].Center.ToVector2() : new Vector2(b.X, b.Y - 16);
            else
            {
                float best = 1e9f;
                foreach (var e in _room.Enemies)
                    if (e.Alive && Vector2.DistanceSquared(e.Center, c) < best) { best = Vector2.DistanceSquared(e.Center, c); target = e.Center; }
            }
            // answer the great bell's rings, and break the keeper's ward
            foreach (var pr in _room.Projectiles)
                if (pr.Active && pr.Type == ProjType.Ring && Math.Abs(Vector2.Distance(c, new Vector2(pr.X, pr.Y)) - pr.R) < 14) inp.TollPressed = true;
            if (b is Bellkeeper k && k.ShieldUp && Vector2.Distance(c, new Vector2(k.X, k.Y - 20)) < 60) inp.TollPressed = _botT % 20 == 0;
            if (target == null) return inp;
            float dx = target.Value.X - c.X, dy = target.Value.Y - c.Y;
            inp.MoveX = (sbyte)(Math.Abs(dx) > 16 ? Math.Sign(dx) : (Math.Sign(dx) != p.Facing && Math.Abs(dx) > 3 ? Math.Sign(dx) : 0));
            if (dy < -20 && p.OnGround && Math.Abs(dx) < 30 && _botT % 20 == 0) { inp.JumpPressed = true; _botJump = true; }
            if (p.OnGround && !inp.JumpPressed) _botJump = false;
            inp.Jump = _botJump;
            bool near = Math.Abs(dx) < 28 && Math.Abs(dy) < 40;
            if (near && _botT % 12 == 0)
            {
                inp.StrikePressed = true;
                if (dy < -14 && Math.Abs(dx) < 20) inp.MoveY = -1;
                else if (dy > 14 && !p.OnGround) inp.MoveY = 1;
            }
            return inp;
        }

        private PlayerInput ReadInput()
        {
            if (Game.Auto?.AutoFight == true) return BotInput();
            if (_replay != null)
            {
                var r = _replayIdx < _replay.Count ? _replay[_replayIdx] : default;
                _replayIdx++;
                return r;
            }
            return new PlayerInput
            {
                MoveX = (sbyte)Input.MoveX,
                MoveY = (sbyte)Input.MoveY,
                Jump = Input.Jump.Down,
                JumpPressed = Input.Jump.Pressed,
                StrikePressed = Input.Strike.Pressed,
                TollPressed = Input.Toll.Pressed,
                Heal = Input.Heal.Down,
                InteractPressed = Input.Up.Pressed,
            };
        }

        private PlayerInput _held;

        private void UpdatePlaying(float dt)
        {
            if (_fadeDir > 0) { UpdateVisualsOnly(dt); return; }
            bool fighting = _room.Boss != null && _room.Boss.Active && !_room.Boss.Finished;
            if ((Input.Pause.Pressed || (!Game.IsActive && Game.Auto == null)) && _replay == null) { OpenPause(); return; }
            if (Input.Map.Pressed && _replay == null && !fighting) { OpenMap(); return; }

            var inp = ReadInput();
            if (_room.Boss != null && _room.Boss.InIntro) inp = new PlayerInput();

            if (_freeze > 0)
            {
                _freeze -= dt;
                // Presses during hit-stop are not lost.
                _held.JumpPressed |= inp.JumpPressed;
                _held.StrikePressed |= inp.StrikePressed;
                _held.TollPressed |= inp.TollPressed;
                _held.InteractPressed |= inp.InteractPressed;
                if (_replay != null) _replayIdx--;
                return;
            }
            inp.JumpPressed |= _held.JumpPressed;
            inp.StrikePressed |= _held.StrikePressed;
            inp.TollPressed |= _held.TollPressed;
            inp.InteractPressed |= _held.InteractPressed;
            _held = default;

            // Hold up or down while standing still to look around.
            int lookDir = _room.P.OnGround && inp.MoveX == 0 && Math.Abs(_room.P.Speed.X) < 1 && _room.NearThing < 0 && !_room.P.Healing ? inp.MoveY : 0;
            if (lookDir != 0 && lookDir == _lookY) _lookT += dt; else { _lookT = 0; _lookY = lookDir; }

            float sdt = dt * Cfg.GameSpeed / 10f;
            _room.Update(inp, sdt);
            Save.PlayTime += dt;
            ProcessEvents();
            if (_phase != Phase.Playing) return;

            _parts.Update(sdt);
            _pv.Update(_room.P, sdt, _region.Wind * 40f, _parts, _st);
            _wr.Update(sdt);
            _cam = Vector2.Lerp(_cam, CameraTarget(), Calc.Damp(6f, dt));
            UpdateAmbience(dt);

            if (_room.Exited) StartTransition();
        }

        private float _ambT = 4f;

        /// <summary>Occasional distant sounds that give each region its air.</summary>
        private void UpdateAmbience(float dt)
        {
            _ambT -= dt;
            if (_ambT > 0) return;
            var rng = _parts.Rng;
            _ambT = rng.Range(4f, 10f);
            string snd = _region.Style switch { 0 => "bird", 1 => "bird", 2 => "drip", 4 => "chime", _ => null };
            if (snd == null) return;
            AudioManager.Play(snd, rng.Range(0.1f, 0.22f), rng.Range(-0.35f, 0.15f), rng.Range(-0.8f, 0.8f));
        }

        // ---- transitions -------------------------------------------------------------------

        private void StartTransition()
        {
            var next = Travel.Next(_room, out var local, out var speed);
            if (next == null) { _room.Exited = false; return; }
            _phase = Phase.Fading;
            AudioManager.Play("whoosh", 0.22f);
            FadeThrough(0.1f, () =>
            {
                LoadRoom(next, null, local, speed);
                _phase = Phase.Playing;
                _fadeSpeed = 1f / 0.16f;
            });
        }

        // ---- rumble / shake ----------------------------------------------------------------

        private float _rumbleT;

        private void Rumble(float strength, float seconds)
        {
            if (Game.Auto != null) return;
            try
            {
                if (Microsoft.Xna.Framework.Input.GamePad.GetState(PlayerIndex.One).IsConnected)
                {
                    Microsoft.Xna.Framework.Input.GamePad.SetVibration(PlayerIndex.One, strength, strength * 0.6f);
                    _rumbleT = seconds;
                }
            }
            catch (Exception) { }
        }

        private void UpdateRumble(float dt)
        {
            if (_rumbleT <= 0) return;
            _rumbleT -= dt;
            if (_rumbleT <= 0)
                try { Microsoft.Xna.Framework.Input.GamePad.SetVibration(PlayerIndex.One, 0, 0); } catch (Exception) { }
        }

        private void Shake(float time, float mag)
        {
            if (!Cfg.ScreenShake) return;
            bool active = _shakeT > 0;
            _shakeMag = active ? Math.Max(_shakeMag, mag) : mag;
            _shakeT = Math.Max(_shakeT, time);
        }

        private void Freeze(float t) => _freeze = Math.Max(_freeze, t);

        private void Toast(string text, Color? color = null) => _toasts.Add((text, color ?? Color.White, 0));

        // ---- events ------------------------------------------------------------------------

        private void ProcessEvents()
        {
            var rng = _parts.Rng;
            bool resonated = false;
            foreach (var e in _room.Events)
            {
                switch (e.Type)
                {
                    case Ev.Jump: case Ev.Leap: case Ev.Land: case Ev.Cling: case Ev.Swing: case Ev.Pogo:
                    case Ev.GlideOpen: case Ev.HealStart: case Ev.Heal:
                        _pv.OnEvent(e, _room.P, _parts, _st);
                        if (e.Type == Ev.Pogo) Freeze(0.02f);
                        break;

                    case Ev.Hurt:
                        Freeze(0.16f);
                        Shake(0.3f, 3f);
                        _hurtVignette = 1f;
                        Rumble(0.7f, 0.25f);
                        AudioManager.Play("hurt", 0.85f);
                        for (int i = 0; i < 12; i++)
                        {
                            float a = rng.Range(0, Calc.Tau);
                            _parts.Emit(e.Pos, new Vector2(MathF.Cos(a), MathF.Sin(a)) * rng.Range(40, 120), G.Hex(0x1b1630), 0.5f, 2, PShape.Square, drag: 4);
                        }
                        _parts.Emit(e.Pos, Vector2.Zero, Color.White, 0.2f, 4, PShape.Glow, sizeEnd: 30, additive: true);
                        break;

                    case Ev.Death: StartDeath(e.Pos); break;
                    case Ev.Hazard: StartHazard(); break;

                    case Ev.Toll:
                        AudioManager.Play("toll", 0.8f, pitchVariance: 0.02f);
                        _rings.Add((e.Pos, 0, PlayerSim.TollRadius, WorldRenderer.NoteColor));
                        _parts.Emit(e.Pos, Vector2.Zero, WorldRenderer.NoteColor * 0.5f, 0.3f, 6, PShape.Glow, sizeEnd: 60, additive: true);
                        if (e.Index == 1)
                            for (int i = 0; i < 6; i++)
                                _parts.Emit(e.Pos + new Vector2(rng.Range(-4, 4), 6), new Vector2(rng.Range(-30, 30), rng.Range(10, 40)), WorldRenderer.NoteColor * 0.8f, 0.4f, 1, drag: 3, additive: true);
                        break;

                    case Ev.GlassBreak:
                    {
                        AudioManager.Play("glass", 0.85f, pitchVariance: 0.05f);
                        Shake(0.2f, 2f);
                        var g = _room.Groups[e.Index];
                        for (int ty = g.Y0; ty <= g.Y1; ty++)
                            for (int tx = g.X0; tx <= g.X1; tx++)
                            {
                                if (_room.GroupOf[ty * _room.Cols + tx] != g.Index) continue;
                                for (int k = 0; k < 4; k++)
                                    _parts.Emit(new Vector2(tx * 8 + rng.Range(0, 8), ty * 8 + rng.Range(0, 8)), new Vector2(rng.Range(-60, 60), rng.Range(-80, 10)),
                                                rng.Chance(0.5f) ? WorldRenderer.GlassColor : Color.White, rng.Range(0.5f, 1f), rng.Chance(0.3f) ? 2 : 1, gravity: 320);
                            }
                        break;
                    }
                    case Ev.Resonate:
                        if (!resonated) AudioManager.Play("resonate", 0.6f);
                        resonated = true;
                        _parts.Emit(e.Pos, Vector2.Zero, WorldRenderer.ResonanceColor * 0.6f, 0.4f, 6, PShape.Glow, sizeEnd: 40, additive: true);
                        break;

                    case Ev.WallHit:
                        AudioManager.Play("wallhit", 0.7f, pitchVariance: 0.08f);
                        _wr.OnWallHit(_room.Time);
                        Freeze(0.03f);
                        Shake(0.08f, 1f);
                        for (int i = 0; i < 6; i++)
                            _parts.Emit(e.Pos, new Vector2(-e.Dir.X * rng.Range(20, 70) + rng.Range(-20, 20), rng.Range(-60, -10)), rng.Chance(0.5f) ? _st.Light : _st.Base, 0.6f, 1, gravity: 300);
                        break;
                    case Ev.WallBreak:
                    {
                        AudioManager.Play("wallbreak", 0.9f);
                        AudioManager.Play("secret", 0.5f);
                        Shake(0.35f, 3f);
                        Freeze(0.06f);
                        Rumble(0.5f, 0.3f);
                        var r = _room.Groups[e.Index].Rect;
                        for (int i = 0; i < r.Width * r.Height / 10; i++)
                            _parts.Emit(new Vector2(r.X + rng.Range(0, r.Width), r.Y + rng.Range(0, r.Height)), new Vector2(rng.Range(-60, 60), rng.Range(-90, 0)),
                                        rng.Chance(0.5f) ? _st.Base : _st.Dark, rng.Range(0.6f, 1.2f), rng.Chance(0.3f) ? 2 : 1, gravity: 360);
                        _wr.OnWallBroken(e.Index);
                        break;
                    }
                    case Ev.Clank:
                        AudioManager.Play("clank", 0.8f, pitchVariance: 0.06f);
                        Freeze(0.05f);
                        _parts.Burst(e.Pos, 8, G.Hex(0xfff0c0), 40, 110, 0.25f, 1, 0, 3, additive: true);
                        break;
                    case Ev.ThornHit:
                        _parts.Burst(e.Pos, 5, _st.SpikeLight, 20, 60, 0.25f, 1, 0, 3);
                        break;

                    case Ev.Ember:
                        Save.Embers += e.Index;
                        AudioManager.Play("coin", 0.32f, 0.1f + Math.Min(0.4f, e.Index * 0.03f), pitchVariance: 0.08f);
                        _parts.Emit(e.Pos, new Vector2(0, -20), G.Hex(0xffd070), 0.3f, 1, additive: true);
                        break;
                    case Ev.CacheHit:
                        AudioManager.Play("cachehit", 0.75f, pitchVariance: 0.06f);
                        Freeze(0.04f);
                        Shake(0.1f, 1.5f);
                        _parts.Burst(e.Pos, 8, G.Hex(0xffb23e), 30, 90, 0.4f, 1, 200, 2);
                        break;
                    case Ev.CacheBreak:
                        AudioManager.Play("cachebreak", 0.85f);
                        Freeze(0.07f);
                        Shake(0.25f, 2.5f);
                        _parts.Burst(e.Pos, 20, G.Hex(0xffb23e), 40, 130, 0.6f, 1, 250, 2);
                        _parts.Emit(e.Pos, Vector2.Zero, G.Hex(0xffb23e) * 0.6f, 0.35f, 6, PShape.Glow, sizeEnd: 50, additive: true);
                        break;

                    case Ev.Lever:
                        AudioManager.Play("lever", 0.8f);
                        Freeze(0.05f);
                        if (_room.Def.Prop("gatekey", _room.Def.Id) != _room.Def.Id || !_room.Def.Has('|'))
                        {
                            AudioManager.Play("gate", 0.6f, -0.2f);
                            Toast("Somewhere, a gate grinds open.", _st.Light2);
                        }
                        Persist();
                        break;
                    case Ev.GateOpen:
                        AudioManager.Play("gate", 0.8f);
                        Shake(0.6f, 1f);
                        break;

                    case Ev.Item:
                        AudioManager.Play("item", 0.85f);
                        Freeze(0.12f);
                        _flash = 0.4f; _flashColor = G.Hex(0xffe0b0);
                        _parts.Burst(e.Pos, 20, G.Hex(0xffd070), 30, 100, 0.8f, 1, -20, 2, additive: true);
                        Toast(e.Index == 0 ? "Vessel of Vigil: your vigil grows." : "Balm Flask: one more flask of balm.", G.Hex(0xffd9a0));
                        Persist();
                        break;
                    case Ev.Ability: StartAbility(e); break;
                    case Ev.Interact: Interact(e.Index); break;
                    case Ev.ShadeReclaim:
                        Save.Embers += Save.ShadeEmbers;
                        Toast($"Your lingering flame returns: {Save.ShadeEmbers} embers.", G.Hex(0xc8b0ff));
                        Save.ShadeRoom = null; Save.ShadeEmbers = 0;
                        AudioManager.Play("reclaim", 0.8f);
                        _flash = 0.3f; _flashColor = G.Hex(0xb090ff);
                        _parts.Burst(e.Pos, 24, G.Hex(0xa080ff), 30, 100, 0.7f, 1, -20, 2, additive: true);
                        Persist();
                        break;

                    case Ev.ProjectilePop:
                        _parts.Burst(e.Pos, 6, PopColor((ProjType)e.Index), 20, 60, 0.3f, 1, 0, 3);
                        AudioManager.Play("pop", 0.3f, pitchVariance: 0.15f, pan: Pan(e.Pos));
                        break;

                    case Ev.EnemyHit:
                        if (e.Index < 0)
                        {
                            _parts.Burst(e.Pos, 4, WorldRenderer.NoteColor, 20, 50, 0.3f, 1, 0, 3, additive: true);
                            break;
                        }
                        AudioManager.Play("hit", 0.75f, pitchVariance: 0.08f);
                        Freeze(0.045f);
                        Shake(0.08f, 1f);
                        HitSpark(e.Pos, e.Dir);
                        break;
                    case Ev.EnemyDie:
                        AudioManager.Play("kill", 0.85f, pitchVariance: 0.06f);
                        Freeze(0.07f);
                        Shake(0.12f, 1.5f);
                        HitSpark(e.Pos, e.Dir);
                        _parts.Burst(e.Pos, 14, EnemyColor((EnemyType)e.Index), 30, 110, 0.6f, 2, 200, 2);
                        _parts.Emit(e.Pos, Vector2.Zero, Color.White * 0.7f, 0.2f, 4, PShape.Glow, sizeEnd: 28, additive: true);
                        Save.Kills++;
                        break;
                    case Ev.EnemyTell:
                        AudioManager.Play(e.Index == 100 ? "notice" : "tell", 0.4f, pitchVariance: 0.1f, pan: Pan(e.Pos));
                        break;
                    case Ev.EnemyJump: AudioManager.Play("hop", 0.4f, pitchVariance: 0.1f, pan: Pan(e.Pos)); break;
                    case Ev.EnemyLand:
                        AudioManager.Play("thud", 0.35f, pitchVariance: 0.1f, pan: Pan(e.Pos));
                        _parts.Burst(e.Pos, 4, G.Mix(_st.Light2, _st.Base, 0.4f), 10, 30, 0.3f, 1, 0, 4, spread: MathF.PI, dir: -MathF.PI / 2);
                        break;
                    case Ev.EnemyLunge: AudioManager.Play("lunge", 0.55f, pitchVariance: 0.08f, pan: Pan(e.Pos)); break;
                    case Ev.Shoot: AudioManager.Play(e.Index == 1 ? "fire" : "spit", 0.5f, pitchVariance: 0.1f, pan: Pan(e.Pos)); break;

                    case Ev.BossStart: StartBoss(); break;
                    case Ev.BossHit:
                        AudioManager.Play("bosshit", 0.8f, pitchVariance: 0.08f);
                        Freeze(0.04f);
                        Shake(0.08f, 1.2f);
                        HitSpark(e.Pos, e.Dir);
                        break;
                    case Ev.BossDie:
                        AudioManager.Play("bossdie", 1f);
                        AudioManager.StopMusic(2f);
                        Freeze(0.4f);
                        Shake(0.8f, 4f);
                        Rumble(1f, 0.6f);
                        _flash = 0.8f; _flashColor = Color.White;
                        _parts.Burst(e.Pos, 40, Color.White, 40, 160, 1f, 2, 0, 2, additive: true);
                        break;
                    case Ev.BossCue: BossCue(e); break;
                }
            }
            _room.Events.Clear();
        }

        private float Pan(Vector2 p) => Math.Clamp((p.X - _cam.X - G.ViewW / 2f) / (G.ViewW / 2f), -1, 1) * 0.6f;

        private void HitSpark(Vector2 at, Vector2 dir)
        {
            var rng = _parts.Rng;
            float baseA = dir == Vector2.Zero ? -MathF.PI / 2 : MathF.Atan2(dir.Y, dir.X);
            for (int i = 0; i < 6; i++)
            {
                float a = baseA + rng.Range(-0.7f, 0.7f);
                _parts.Emit(at, new Vector2(MathF.Cos(a), MathF.Sin(a)) * rng.Range(80, 180), Color.White, 0.18f, 1, PShape.Line, drag: 6, additive: true);
            }
            _parts.Emit(at, Vector2.Zero, Color.White * 0.8f, 0.1f, 3, PShape.Glow, sizeEnd: 16, additive: true);
        }

        private static Color EnemyColor(EnemyType t) => t switch
        {
            EnemyType.Crawler => G.Hex(0x648a3e), EnemyType.Hopper => G.Hex(0x8e6aa2), EnemyType.Wisp => G.Hex(0x7fd8ff),
            EnemyType.Sentinel => G.Hex(0xb8b8cc), EnemyType.Spitter => G.Hex(0x8cc05a), _ => G.Hex(0x9fdcff),
        };

        private static Color PopColor(ProjType t) => t switch
        {
            ProjType.Glob => G.Hex(0x8cc05a), ProjType.Shard or ProjType.Icicle => G.Hex(0xe2f6ff),
            ProjType.Note => WorldRenderer.NoteColor, ProjType.Rock => G.Hex(0x8a7a70), _ => G.Hex(0xffb23e),
        };

        // ---- interacting with things -------------------------------------------------------

        private void Interact(int index)
        {
            var t = _room.Things[index];
            switch (t.Kind)
            {
                case ThingKind.Shrine: Rest(t); break;
                case ThingKind.Merchant: OpenShop(); break;
                case ThingKind.Npc: OpenDialogue(Lore.Npc(_room.Def.Prop("npc", "elder"), _flags)); break;
                case ThingKind.Tablet: OpenDialogue(Lore.Tablet(_room.Def.Prop("tablet", "The words have worn away."))); break;
                case ThingKind.GreatBell: StartEnding(); break;
            }
        }

        private void Rest(in Thing t)
        {
            ref var p = ref _room.P;
            p.Hp = p.MaxHp;
            p.Flasks = p.MaxFlasks;
            Save.ShrineRoom = _room.Def.Id;
            _wr.ShrineActive = true;
            AudioManager.Play("rest", 0.8f);
            _flash = 0.25f; _flashColor = G.Hex(0xffe8b0);
            var c = new Vector2(t.X, t.Y - 12);
            _parts.Burst(c, 18, G.Hex(0xffd070), 20, 70, 0.9f, 1, -30, 2, additive: true);
            _parts.Emit(c, Vector2.Zero, G.Hex(0xffd070) * 0.5f, 0.6f, 8, PShape.Glow, sizeEnd: 70, additive: true);
            Toast("You rest. The shrine will remember you.", G.Hex(0xffe8c0));
            Persist();
        }

        // ---- hazards, death and the lingering flame -----------------------------------------

        private void StartHazard()
        {
            AudioManager.Play("hazard", 0.6f);
            _phase = Phase.Fading;
            FadeThrough(0.22f, () =>
            {
                _room.HazardRespawn();
                _pv.Reset(_room.P);
                SnapCamera();
                _phase = Phase.Playing;
                _fadeSpeed = 1f / 0.25f;
            });
        }

        private Vector2 _deathAt;

        private void StartDeath(Vector2 at)
        {
            if (Game.Auto != null) Console.WriteLine($"[auto] DEATH in {_room.Def.Id} at {_room.P.X},{_room.P.Y}");
            _phase = Phase.Dying;
            _phaseT = 0;
            _deathAt = at;
            Save.Deaths++;
            AudioManager.Play("death", 0.9f);
            AudioManager.Duck(0.3f);
            Shake(0.5f, 3f);
            Rumble(0.8f, 0.4f);
        }

        private void UpdateDying(float dt)
        {
            _phaseT += dt;
            _parts.Update(dt);
            _wr.Update(dt);
            var rng = _parts.Rng;
            if (_phaseT < 1.2f && rng.Chance(dt * 60))
            {
                var c = _deathAt + rng.InCircle(6);
                _parts.Emit(c, new Vector2(rng.Range(-10, 10), rng.Range(-50, -20)), rng.Chance(0.5f) ? G.Hex(0xff9a3a) : G.Hex(0x8a5aff), 0.8f, 1, additive: true);
            }
            if (_phaseT > 1.6f && _fadeDir == 0)
            {
                FadeThrough(0.6f, () =>
                {
                    // The pilgrim's embers stay behind as a lingering flame on the last safe ground.
                    if (Save.Embers > 0)
                    {
                        Save.ShadeRoom = _room.Def.Id;
                        Save.ShadeX = _room.P.SafeX + PlayerSim.W / 2;
                        Save.ShadeY = _room.P.SafeY + PlayerSim.H;
                        Save.ShadeEmbers = Save.Embers;
                        Save.Embers = 0;
                    }
                    var p = _room.P;
                    p.Hp = p.MaxHp;
                    p.Flasks = p.MaxFlasks;
                    p.Invuln = 0;
                    _room.P = p;
                    var shrine = Save.ShrineRoom != null ? Atlas.Get(Save.ShrineRoom) : null;
                    _region = null;   // replay the area card and the music
                    LoadRoom(shrine ?? Atlas.StartRoom, shrine != null ? ThingKind.Shrine : null);
                    _phase = Phase.Playing;
                    _fadeSpeed = 1f / 0.8f;
                    AudioManager.Duck(1f);
                    AudioManager.Play("respawn", 0.6f);
                    Persist();
                });
            }
        }

        // ---- abilities ---------------------------------------------------------------------

        private void StartAbility(in GameEvent e)
        {
            _phase = Phase.Ability;
            _phaseT = 0;
            _gotAbility = (Abilities)e.Index;
            _pv.HasBell = _room.P.Has(Abilities.Bell);
            AudioManager.Play("ability", 0.9f);
            AudioManager.Duck(0.25f);
            _flash = 0.7f; _flashColor = G.Hex(0xfff3d0);
            Shake(0.4f, 2f);
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Calc.Tau;
                _parts.Emit(e.Pos, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 120f, G.Hex(0xfff0c0), 0.9f, 2, PShape.Disc, drag: 3, sizeEnd: 0.5f, additive: true);
            }
            if (Game.Auto != null) Console.WriteLine($"[auto] ABILITY {_gotAbility}");
            Persist();
        }

        private void UpdateAbility(float dt)
        {
            _phaseT += dt;
            _parts.Update(dt);
            _pv.Update(_room.P, dt, 0, _parts, _st);
            if (_phaseT < 2f && _parts.Rng.Chance(dt * 30))
                _parts.Emit(_room.P.Center + _parts.Rng.InCircle(12), new Vector2(0, -24), G.Hex(0xfff0c0), 0.6f, 1, additive: true);
            if (_phaseT > 1.6f && (Input.Confirm.Pressed || Input.Jump.Pressed || Input.Strike.Pressed || (_replay != null && _phaseT > 2f)))
            {
                Input.Confirm.Consume(); Input.Jump.Consume(); Input.Strike.Consume();
                AudioManager.Play("select", 0.7f);
                AudioManager.Duck(1f);
                _room.Events.Clear();
                _phase = Phase.Playing;
            }
        }

        // ---- guardians ---------------------------------------------------------------------

        private void StartBoss()
        {
            var b = _room.Boss;
            _bossName = b.Name;
            _bossTitle = b.Title;
            _bossCardT = 0;
            AudioManager.PlayMusic(SongId.Boss, 0.6f);
            AudioManager.Play("gate", 0.7f);
            Shake(0.5f, 1.5f);
            if (Game.Auto != null) Console.WriteLine($"[auto] BOSS {b.Id} wakes");
        }

        private void BossCue(in GameEvent e)
        {
            var rng = _parts.Rng;
            Color dust = G.Mix(_st.Light2, _st.Base, 0.4f);
            switch (e.Index)
            {
                case 1: case 20: case 40: case 61:
                    AudioManager.Play("roar", 0.9f, e.Index == 20 ? 0.3f : (e.Index == 40 ? -0.2f : (e.Index == 61 ? -0.35f : 0f)));
                    Shake(0.7f, 2.5f);
                    Rumble(0.6f, 0.5f);
                    break;
                case 2: AudioManager.Play("tell", 0.7f, -0.4f); break;
                case 3: case 65: AudioManager.Play("lunge", 0.8f, -0.3f); break;
                case 4:
                    AudioManager.Play("crash", 0.9f);
                    Shake(0.4f, 4f);
                    Rumble(0.8f, 0.3f);
                    _parts.Burst(e.Pos, 14, dust, 30, 100, 0.6f, 2, 200, 2, spread: MathF.PI, dir: e.Dir.X > 0 ? MathF.PI : 0);
                    break;
                case 5: AudioManager.Play("hop", 0.8f, -0.4f); break;
                case 6: case 64:
                    AudioManager.Play("slam", 0.95f);
                    Shake(0.35f, 3.5f);
                    Rumble(0.7f, 0.3f);
                    _parts.Burst(e.Pos, 16, dust, 30, 110, 0.5f, 2, 150, 3, spread: MathF.PI, dir: -MathF.PI / 2);
                    break;
                case 7: AudioManager.Play("crack", 0.6f, pitchVariance: 0.1f); break;
                case 21: AudioManager.Play("vanish", 0.6f); _parts.Burst(e.Pos, 10, G.Hex(0x58406e), 20, 60, 0.5f, 2, -20, 2); break;
                case 22: AudioManager.Play("appear", 0.6f); _parts.Burst(e.Pos, 10, G.Hex(0xb8f0ff), 20, 60, 0.4f, 1, 0, 3, additive: true); break;
                case 23: case 63: AudioManager.Play("tell", 0.7f, -0.2f); break;
                case 24: case 66: AudioManager.Play("chime", 0.8f, -0.3f); break;
                case 25: AudioManager.Play("fire", 0.45f, 0.3f); break;
                case 26: AudioManager.Play("swing", 0.9f, -0.5f); break;
                case 27: AudioManager.Play("roar", 0.5f, 0.5f); break;
                case 28: AudioManager.Play("toll", 0.55f, 0.4f); break;
                case 41: AudioManager.Play("quake", 0.9f); Shake(1.0f, 2.5f); Rumble(0.5f, 0.8f); break;
                case 42:
                    AudioManager.Play("crack", 0.6f, -0.3f);
                    for (int i = 0; i < 10; i++) _parts.Emit(e.Pos + new Vector2(rng.Range(-10, 10), 0), new Vector2(rng.Range(-10, 10), rng.Range(-60, -20)), Color.White, 0.6f, 1, gravity: 100);
                    break;
                case 43:
                    AudioManager.Play("wallbreak", 0.8f, -0.2f);
                    Shake(0.3f, 3f);
                    _parts.Burst(e.Pos, 24, Color.White, 40, 140, 0.7f, 2, 250, 2, spread: MathF.PI, dir: -MathF.PI / 2);
                    break;
                case 44: AudioManager.Play("roar", 0.6f, 0.1f); break;
                case 45: AudioManager.Play("breath", 0.85f); break;
                case 60:
                    AudioManager.Play("shieldbreak", 1f);
                    Freeze(0.1f);
                    Shake(0.3f, 2.5f);
                    _parts.Burst(e.Pos, 30, WorldRenderer.GlassColor, 50, 150, 0.7f, 1, 200, 2);
                    break;
                case 62: AudioManager.Play("shieldup", 0.8f); break;
                case 67:
                    AudioManager.Play("bell", 0.9f, 0.2f);
                    Shake(0.6f, 2f);
                    _flash = 0.25f; _flashColor = WorldRenderer.NoteColor;
                    break;
                case 999:
                {
                    var b = _room.Boss;
                    Toast(Lore.BossLine(b.Id), G.Hex(0xffe8c0));
                    AudioManager.Play("gate", 0.8f);
                    AudioManager.PlayMusic(_region.Song, 3f);
                    if (Game.Auto != null) Console.WriteLine($"[auto] BOSS {b.Id} defeated");
                    Persist();
                    break;
                }
            }
        }

        // ---- the great bell ----------------------------------------------------------------

        private void StartEnding()
        {
            if (Game.Auto != null) Console.WriteLine($"[auto] GREAT BELL rung (deaths {Save.Deaths}, t={Save.PlayTime:0.0})");
            _phase = Phase.Ending;
            _phaseT = 0;
            _wr.BellSwing = 0.45f;
            _flags.Add("rung");
            AudioManager.Play("bell", 1f);
            AudioManager.StopMusic(4f);
            AudioManager.SetWind(0);
            _flash = 1f; _flashColor = Color.White;
            Shake(0.6f, 2.5f);
            bool first = !Save.Completed;
            Save.Completed = true;
            if (first)
            {
                Save.FirstClearTime = Save.PlayTime;
                Save.FirstClearDeaths = Save.Deaths;
            }
            Persist();
        }

        private void UpdateEnding(float dt)
        {
            _phaseT += dt;
            _parts.Update(dt);
            _wr.BellTime += dt;
            _wr.BellSwing = 0.45f * MathF.Exp(-_phaseT * 0.4f);
            _pv.Update(_room.P, dt, 0, _parts, _st);
            Vector2 bell = Vector2.Zero;
            foreach (var t in _room.Things) if (t.Kind == ThingKind.GreatBell) bell = new Vector2(t.X, t.Y - 30);
            if (_parts.Rng.Chance(dt * 25))
            {
                float a = _parts.Rng.Range(0, Calc.Tau);
                _parts.Emit(bell, new Vector2(MathF.Cos(a), MathF.Sin(a)) * _parts.Rng.Range(20, 60), G.Hex(0xffe6a0), 2.5f, 1, drag: 0.5f, additive: true);
            }
            if ((int)(_phaseT * 1.2f) != (int)((_phaseT - dt) * 1.2f) && _phaseT < 4)
                _parts.Emit(bell, Vector2.Zero, G.Hex(0xfff0c8) * 0.6f, 1.8f, 6, PShape.Glow, sizeEnd: 260, additive: true);
            if (_phaseT > 6.5f && !Game.Transitioning)
                Game.ChangeScene(new EndingScene(), 0.6f, Color.White);
        }

        // ---- ambient particles -------------------------------------------------------------

        private void InitAmbient()
        {
            int s = _st.Id;
            var rng = new Rng(s * 31 + 7);
            int n = s switch { 0 => 22, 1 => 34, 2 => 30, 3 => 110, _ => 40 };
            _amb = new Amb[n];
            for (int i = 0; i < n; i++)
            {
                _amb[i] = new Amb
                {
                    P = new Vector2(rng.Range(0, G.ViewW), rng.Range(0, G.ViewH)),
                    Phase = rng.Range(0, 100),
                    Size = rng.Range(0.4f, 1f),
                };
                _amb[i].V = s switch
                {
                    0 => new Vector2(rng.Range(-6, 6), rng.Range(-4, 4)),
                    1 => new Vector2(rng.Range(-3, 3), rng.Range(2, 7)),
                    2 => new Vector2(rng.Range(4, 12), rng.Range(6, 14)),
                    3 => new Vector2(rng.Range(-4, 4), rng.Range(18, 40)),
                    _ => new Vector2(rng.Range(-3, 3), rng.Range(-12, -4)),
                };
            }
        }

        private void UpdateAmbient(float dt)
        {
            if (_region == null) return;
            var d = _cam - _lastCam;
            _lastCam = _cam;
            float wind = _region.Wind * 160f;
            int s = _st.Id;
            for (int i = 0; i < _amb.Length; i++)
            {
                ref var a = ref _amb[i];
                float depth = 0.4f + a.Size * 0.6f;
                a.P -= d * depth;
                var v = a.V;
                if (s == 0 || s == 1) v += new Vector2(MathF.Sin(_time * 0.7f + a.Phase) * 6, MathF.Cos(_time * 0.9f + a.Phase) * 5);
                if (s == 2) v.X += MathF.Sin(_time * 1.3f + a.Phase) * 10;
                v.X += wind * (s == 3 ? 1.6f : 0.8f) * depth;
                a.P += v * dt * depth;
                a.P.X = ((a.P.X % (G.ViewW + 20)) + G.ViewW + 20) % (G.ViewW + 20);
                a.P.Y = ((a.P.Y % (G.ViewH + 20)) + G.ViewH + 20) % (G.ViewH + 20);
            }
        }

        private void DrawAmbient(SpriteBatch sb, bool additive)
        {
            int s = _st.Id;
            for (int i = 0; i < _amb.Length; i++)
            {
                var a = _amb[i];
                var p = a.P - new Vector2(10, 10);
                switch (s)
                {
                    case 0:
                    case 1:
                        if (!additive) break;
                        {
                            float tw = 0.5f + 0.5f * MathF.Sin(_time * 2.3f + a.Phase * 3);
                            Color c = s == 1 && i % 3 == 0 ? G.Hex(0xc8a0ff) : _st.Ambient;
                            G.GlowAt(sb, p, 5 * a.Size + 2, c * (0.35f * tw));
                            G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), 1, 1, c * tw);
                        }
                        break;
                    case 2:
                        if (additive) break;
                        G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), a.Size > 0.8f ? 2 : 1, 1, (a.Size > 0.7f ? _st.Accent1 : _st.Ambient) * 0.5f);
                        break;
                    case 3:
                        if (additive) break;
                        G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), a.Size > 0.85f ? 2 : 1, a.Size > 0.85f ? 2 : 1, Color.White * (0.4f + a.Size * 0.5f));
                        break;
                    default:
                        if (!additive) break;
                        {
                            float tw = 0.6f + 0.4f * MathF.Sin(_time * 3f + a.Phase);
                            G.GlowAt(sb, p, 4 * a.Size + 1, _st.Ambient * (0.3f * tw));
                            G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), 1, 1, _st.Ambient * (0.8f * tw));
                        }
                        break;
                }
            }
        }
    }
}
