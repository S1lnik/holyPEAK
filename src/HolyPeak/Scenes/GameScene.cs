using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.World;

namespace HolyPeak.Scenes
{
    public sealed class GameScene : Scene
    {
        private enum Phase { Prologue, Card, Playing, Dying, Wipe, Respawning, Gift, Bell }

        private static readonly int[] PeakHeight = { 58, 72, 86, 0 };
        private static readonly float[] PeakHaze = { 0.42f, 0.34f, 0.38f, 0f };
        private static readonly SongId[] ChapterSong = { SongId.Foothills, SongId.Ruins, SongId.Frost, SongId.Peak };
        private static readonly Backdrop[] Backdrops = new Backdrop[4];

        private static readonly string[] PrologueLines =
        {
            "They say a bell hangs at the top of the Holy Peak,",
            "silent for a thousand years.",
            "They say whoever rings it will be heard by the sky itself.",
            "You have walked a long way to find out.",
        };

        private int _roomIndex;
        private Room _room;
        private RoomRenderer _rr;
        private Style _st;
        private int _chapter = -1;
        private readonly PlayerVisual _pv = new PlayerVisual();
        private readonly Particles _parts = new Particles();
        private readonly Random _shakeRng = new Random(5);

        private Phase _phase;
        private float _phaseT, _time;
        private Vector2 _cam;
        private float _shakeT, _shakeMag;
        private Vector2 _shakeOff;
        private float _freeze;
        private bool _pendingJump, _pendingDash;

        // wipe
        private float _wipe;
        private bool _wipeCovering, _wipeReverse;
        private Action _onCovered;
        private Phase _afterWipe = Phase.Playing;

        // ember
        private Vector2 _emberPos;
        private bool _emberGhost;
        private float _emberPopup;

        // overlays
        private float _flash;
        private Color _flashColor = Color.White;
        private ChapterDef _card;
        private float _cardT;
        private bool _showPrologue;

        // pause
        private bool _paused;
        private int _pauseView;   // 0 main, 1 options, 2 controls
        private Menu _pauseMenu, _optionsMenu;
        private float _pauseA;

        // automation replay
        private List<PlayerInput> _replay;
        private int _replayIdx;

        // ambient
        private struct Amb { public Vector2 P, V; public float Phase, Size; }
        private Amb[] _amb = Array.Empty<Amb>();
        private Vector2 _lastCam;
        private float _windStreakT;

        private SaveData Save => Game.Save;
        private Settings Cfg => Game.Settings;

        public GameScene(int roomIndex, bool prologue = false)
        {
            _roomIndex = Math.Clamp(roomIndex, 0, Levels.All.Count - 1);
            _showPrologue = prologue;
        }

        public static GameScene ForAutomation(AutomationOptions o)
        {
            int idx = 0;
            if (o.Room != null)
                for (int i = 0; i < Levels.All.Count; i++) if (Levels.All[i].Id == o.Room) idx = i;
            var g = new GameScene(idx);
            if (o.Dash || Levels.All[idx].Chapter >= 1) HolyPeakGame.Instance.Save.HasDash = !HasShrine(Levels.All[idx]) || o.Dash;
            var files = new List<string>();
            if (o.Replay != null) files.Add(o.Replay);
            if (o.ReplayDir != null)
                for (int i = idx; i < Levels.All.Count; i++)
                {
                    string id = Levels.All[i].Id;
                    string ember = Path.Combine(o.ReplayDir, id + "-ember.txt");
                    files.Add(o.EmberRoute && File.Exists(ember) ? ember : Path.Combine(o.ReplayDir, id + ".txt"));
                }
            if (files.Count > 0) g._replay = new List<PlayerInput>();
            foreach (var file in files)
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var p = line.Split(' ');
                    if (p.Length < 3) continue;
                    var b = p[2];
                    g._replay.Add(new PlayerInput
                    {
                        MoveX = sbyte.Parse(p[0]), MoveY = sbyte.Parse(p[1]),
                        Jump = b[0] == '1', JumpPressed = b[1] == '1', DashPressed = b[2] == '1',
                    });
                }
            }
            g._skipIntro = true;
            return g;
        }

        private bool _skipIntro;

        private static bool HasShrine(RoomDef d)
        {
            foreach (var r in d.Map) if (r.IndexOf('G') >= 0) return true;
            return false;
        }

        public override void Begin()
        {
            LoadRoom(_roomIndex);
            if (_skipIntro) { _phase = Phase.Playing; AudioManager.PlayMusic(ChapterSong[_chapter]); }
            else if (_showPrologue)
            {
                _phase = Phase.Prologue;
                _phaseT = 0;
                AudioManager.StopMusic(2f);
            }
            else ShowCard();
        }

        public override void OnQuit() => PersistProgress();

        private void PersistProgress()
        {
            Save.Started = true;
            Save.RoomIndex = _roomIndex;
            if (Cfg.AssistActive) Save.UsedAssist = true;
            Game.WriteSave();
        }

        // ---- room management -------------------------------------------------------------

        private void LoadRoom(int index)
        {
            if (index != _roomIndex) _roomDeaths = 0;
            _roomIndex = index;
            var def = Levels.All[index];
            if (Game.Auto != null) Console.WriteLine($"[auto] room {def.Id} (embers {Save.Embers.Count}, deaths {Save.Deaths}, t={Save.PlayTime:0.0})");
            bool newChapter = def.Chapter != _chapter;
            _chapter = def.Chapter;
            _st = Style.Chapters[_chapter];
            Backdrops[_chapter] ??= new Backdrop(_st, PeakHeight[_chapter], 0.64f, PeakHaze[_chapter]);

            _rr?.Dispose();
            _room = new Room(def, Save.HasDash)
            {
                InfiniteDash = Cfg.InfiniteDash,
                Invincible = Cfg.Invincible,
            };
            if (_room.HasShrine && Save.HasDash) _room.ShrineUsed = true;
            _rr = new RoomRenderer(_room, _st);
            _emberGhost = _room.HasEmber && Save.Embers.Contains(def.Id);
            _rr.EmberGhost = _emberGhost;
            _emberPos = _room.HasEmber ? new Vector2(_room.Ember.X, _room.Ember.Y) : Vector2.Zero;
            _rr.EmberPos = _emberPos;
            _parts.Clear();
            _pv.Reset(_room.P);
            _pv.GiftBlend = 1f;
            _pv.Golden = Save.Embers.Count >= Levels.EmberCount;
            SnapCamera();
            if (newChapter) InitAmbient();
            if (_chapter > Save.MaxChapter) Save.MaxChapter = _chapter;
            Save.RoomIndex = index;
            Save.Started = true;
            Game.WriteSave();
            AudioManager.SetWind(def.Wind != 0 ? 0.25f : (_chapter == 2 ? 0.12f : 0f));
        }

        private void SnapCamera()
        {
            _cam = CameraTarget();
            _lastCam = _cam;
        }

        private Vector2 CameraTarget()
        {
            var c = _room.P.Center;
            float look = Math.Clamp(_room.P.Speed.X * 0.12f, -18f, 18f);
            var t = new Vector2(c.X + look - G.ViewW / 2f, c.Y - G.ViewH / 2f - 8);
            t.X = Math.Clamp(t.X, 0, Math.Max(0, _room.Width - G.ViewW));
            t.Y = Math.Clamp(t.Y, 0, Math.Max(0, _room.Height - G.ViewH));
            return t;
        }

        private void ShowCard()
        {
            _phase = Phase.Card;
            _card = Levels.Chapters[_chapter];
            _cardT = 0;
            AudioManager.PlayMusic(ChapterSong[_chapter], 2f);
            AudioManager.Play("chapter", 0.7f);
        }

        // ---- update ----------------------------------------------------------------------

        public override void Update(float dt)
        {
            _time += dt;
            UpdateRumble(dt);
            _flash = Math.Max(0, _flash - dt * 2.5f);
            _emberPopup = Math.Max(0, _emberPopup - dt);
            if (!_paused) _tipT = Math.Max(0, _tipT - dt);
            _pauseA = Calc.Approach(_pauseA, _paused ? 1 : 0, dt * 6);

            if (_paused) { UpdatePause(dt); return; }

            if (_shakeT > 0)
            {
                _shakeT -= dt;
                float m = _shakeMag * Calc.Clamp01(_shakeT / 0.2f);
                _shakeOff = new Vector2(MathF.Round((float)(_shakeRng.NextDouble() * 2 - 1) * m), MathF.Round((float)(_shakeRng.NextDouble() * 2 - 1) * m));
            }
            else _shakeOff = Vector2.Zero;

            UpdateWipe(dt);

            switch (_phase)
            {
                case Phase.Prologue: UpdatePrologue(dt); break;
                case Phase.Card: UpdateCard(dt); break;
                case Phase.Playing: UpdatePlaying(dt); break;
                case Phase.Dying: UpdateDying(dt); break;
                case Phase.Respawning:
                    _phaseT += dt;
                    UpdateVisualsOnly(dt);
                    if (_phaseT > 0.25f) _phase = Phase.Playing;
                    break;
                case Phase.Wipe: UpdateVisualsOnly(dt); break;
                case Phase.Gift: UpdateGift(dt); break;
                case Phase.Bell: UpdateBell(dt); break;
            }
            UpdateAmbient(dt);
        }

        private void UpdateVisualsOnly(float dt)
        {
            _parts.Update(dt);
            _pv.Update(_room.P, dt, _room.WindNow, _parts, _st);
            _cam = Vector2.Lerp(_cam, CameraTarget(), Calc.Damp(5f, dt));
            UpdateEmberFollow(dt);
        }

        private void UpdatePrologue(float dt)
        {
            _phaseT += dt;
            float total = 2.2f + PrologueLines.Length * 2.3f;
            if ((_phaseT > 0.6f && (Input.Confirm.Pressed || Input.Jump.Pressed)) || _phaseT > total)
            {
                Input.Confirm.Consume(); Input.Jump.Consume();
                ShowCard();
            }
        }

        private void UpdateCard(float dt)
        {
            _cardT += dt;
            if (_cardT > 0.5f && _cardT < 2.6f && (Input.Confirm.Pressed || Input.Jump.Pressed || Input.Dash.Pressed))
            {
                Input.Jump.Consume(); Input.Confirm.Consume(); Input.Dash.Consume();
                _cardT = 2.6f;
            }
            if (_cardT > 2.6f) UpdateVisualsOnly(dt);
            if (_cardT >= 3.4f) _phase = Phase.Playing;
        }

        private PlayerInput ReadInput()
        {
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
                DashPressed = Input.Dash.Pressed,
            };
        }

        private void UpdatePlaying(float dt)
        {
            if ((Input.Pause.Pressed || (!Game.IsActive && Game.Auto == null)) && _replay == null) { OpenPause(); return; }
            if (Input.Restart.Pressed && _replay == null)
            {
                _room.P.Kill(_room, new Vector2(0, -1));
                ProcessEvents();
                return;
            }

            var inp = ReadInput();
            if (_freeze > 0)
            {
                _freeze -= dt;
                _pendingJump |= inp.JumpPressed;
                _pendingDash |= inp.DashPressed;
                if (_replay != null) _replayIdx--; // replays are frame-exact: hold the frame
                return;
            }
            inp.JumpPressed |= _pendingJump;
            inp.DashPressed |= _pendingDash;
            _pendingJump = _pendingDash = false;

            float speed = Cfg.GameSpeed / 10f;
            _room.Update(inp, dt * speed);
            Save.PlayTime += dt;
            ProcessEvents();
            if (_phase != Phase.Playing) return;

            _parts.Update(dt * speed);
            _pv.Update(_room.P, dt * speed, _room.WindNow, _parts, _st);
            _cam = Vector2.Lerp(_cam, CameraTarget(), Calc.Damp(5f, dt));
            UpdateEmberFollow(dt);
            UpdateWindFx(dt);
            UpdateAmbience(dt);
        }

        private float _ambT = 4f;

        /// <summary>Occasional distant sounds that give each chapter its air.</summary>
        private void UpdateAmbience(float dt)
        {
            _ambT -= dt;
            if (_ambT > 0) return;
            var rng = _parts.Rng;
            _ambT = rng.Range(4f, 10f);
            string snd = _chapter switch { 0 => "bird", 1 => "drip", 3 => "chime", _ => null };
            if (snd == null) return;
            AudioManager.Play(snd, rng.Range(0.12f, 0.25f), rng.Range(-0.25f, 0.2f), rng.Range(-0.8f, 0.8f));
        }

        private void UpdateEmberFollow(float dt)
        {
            if (!_room.HasEmber) return;
            if (_room.Ember.State == 1)
            {
                var target = _room.P.Center + new Vector2(-_room.P.Facing * 9, -10 + MathF.Sin(_time * 3) * 2);
                _emberPos = Vector2.Lerp(_emberPos, target, Calc.Damp(7f, dt));
            }
            else _emberPos = Vector2.Lerp(_emberPos, new Vector2(_room.Ember.X, _room.Ember.Y), Calc.Damp(6f, dt));
            _rr.EmberPos = _emberPos;
            if (_room.Ember.State == 1 && _parts.Rng.Chance(dt * 20))
                _parts.Emit(_emberPos + _parts.Rng.InCircle(3), new Vector2(0, -12), G.Hex(0xffb23e), 0.5f, 1, additive: true);
        }

        private void UpdateWindFx(float dt)
        {
            float w = _room.WindNow;
            AudioManager.SetWind(_room.Def.Wind != 0 ? 0.15f + MathF.Abs(w) / 70f * 0.5f : (_chapter == 2 ? 0.12f : 0f));
            if (MathF.Abs(w) < 8) return;
            _windStreakT -= dt * MathF.Abs(w) / 40f;
            while (_windStreakT <= 0)
            {
                _windStreakT += 0.05f;
                float x = w > 0 ? _cam.X - 10 : _cam.X + G.ViewW + 10;
                var pos = new Vector2(x, _cam.Y + _parts.Rng.Range(0, G.ViewH));
                _parts.Emit(pos, new Vector2(w * 6f, _parts.Rng.Range(-4, 4)), Color.White * 0.35f, 1.4f, 1, PShape.Line);
            }
        }

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

        public override void End()
        {
            _rumbleT = 0;
            if (Game.Auto == null)
                try { Microsoft.Xna.Framework.Input.GamePad.SetVibration(PlayerIndex.One, 0, 0); } catch (Exception) { }
        }

        private void Shake(float time, float mag)
        {
            if (!Cfg.ScreenShake) return;
            bool active = _shakeT > 0;
            _shakeMag = active ? Math.Max(_shakeMag, mag) : mag;
            _shakeT = Math.Max(_shakeT, time);
        }

        private void ProcessEvents()
        {
            var rng = _parts.Rng;
            foreach (var e in _room.Events)
            {
                switch (e.Type)
                {
                    case Ev.Death: StartDeath(); break;
                    case Ev.Exit: StartExit(); break;
                    case Ev.Gift: StartGift(e.Pos); break;
                    case Ev.Bell: StartBell(); break;
                    case Ev.Dash:
                        _freeze = 0.05f;
                        Rumble(0.25f, 0.08f);
                        _pv.OnEvent(e, _room.P, _parts, _st, Cfg.ScreenShake, Shake);
                        break;
                    case Ev.Spring:
                        _pv.Scale = e.Dir.Y < 0 ? new Vector2(0.7f, 1.4f) : new Vector2(1.4f, 0.7f);
                        AudioManager.Play("spring", 0.7f, pitchVariance: 0.05f);
                        for (int i = 0; i < 8; i++)
                            _parts.Emit(e.Pos, new Vector2(rng.Range(-40, 40), rng.Range(-50, -10)) * (e.Dir.X == 0 ? 1 : 0.6f), _st.Light2 * 0.8f, 0.4f, 1, drag: 4);
                        break;
                    case Ev.Crystal:
                        _freeze = 0.05f;
                        _flash = 0.12f; _flashColor = G.Hex(0xbff6ff);
                        AudioManager.Play("crystal", 0.7f);
                        _parts.Burst(e.Pos, 14, G.Hex(0x7ff0ff), 30, 90, 0.5f, 1, 0, 3);
                        _parts.Burst(e.Pos, 6, Color.White, 10, 40, 0.4f, 6, 0, 3, PShape.Glow, additive: true);
                        break;
                    case Ev.CrystalRespawn:
                        AudioManager.Play("crystalback", 0.5f);
                        _parts.Burst(e.Pos, 8, G.Hex(0x7ff0ff), 10, 30, 0.4f, 1, 0, 3);
                        break;
                    case Ev.EmberTouch:
                        AudioManager.Play("ember", 0.75f);
                        _parts.Burst(e.Pos, 16, G.Hex(0xffb23e), 20, 70, 0.6f, 1, -20, 2, additive: true);
                        _flash = 0.1f; _flashColor = G.Hex(0xffd9a0);
                        break;
                    case Ev.CrumbleShake:
                        AudioManager.Play("crumble", 0.45f, pitchVariance: 0.1f);
                        break;
                    case Ev.CrumbleBreak:
                    {
                        AudioManager.Play("break", 0.55f, pitchVariance: 0.1f);
                        var c = _room.Crumbles[e.Index];
                        for (int i = 0; i < c.W / 2; i++)
                            _parts.Emit(new Vector2(c.X + rng.Range(0, c.W), c.Y + rng.Range(0, 8)), new Vector2(rng.Range(-20, 20), rng.Range(-30, 10)),
                                        rng.Chance(0.5f) ? _st.Alt : _st.AltDark, 0.8f, rng.Chance(0.3f) ? 2 : 1, gravity: 300);
                        break;
                    }
                    case Ev.CrumbleRestore:
                        AudioManager.Play("restore", 0.2f);
                        break;
                    case Ev.PulseSwitch:
                        if (_room.Pulses.Length > 0)
                        {
                            AudioManager.Play(e.Index == 1 ? "tick" : "tock", 0.45f);
                            foreach (var b in _room.Pulses)
                                if (b.Group == e.Index && IsOnScreen(b.X, b.Y))
                                    _parts.Emit(new Vector2(b.X + b.W / 2f, b.Y + b.H / 2f), Vector2.Zero, (b.Group == 1 ? RoomRenderer.PulseA : RoomRenderer.PulseB) * 0.5f, 0.25f, b.W * 0.7f, PShape.Glow, sizeEnd: b.W * 1.2f, additive: true);
                        }
                        break;
                    default:
                        _pv.OnEvent(e, _room.P, _parts, _st, Cfg.ScreenShake, Shake);
                        break;
                }
            }
            _room.Events.Clear();
        }

        private bool IsOnScreen(float x, float y) =>
            x > _cam.X - 16 && x < _cam.X + G.ViewW + 16 && y > _cam.Y - 16 && y < _cam.Y + G.ViewH + 16;

        // ---- death / respawn ---------------------------------------------------------------

        private int _roomDeaths;
        private float _tipT;
        private bool _tipShown;

        private void StartDeath()
        {
            _roomDeaths++;
            if (_roomDeaths >= 12 && !_tipShown && !Cfg.AssistActive)
            {
                _tipShown = true;
                _tipT = 6f;
            }
            if (Game.Auto != null) Console.WriteLine($"[auto] DEATH in {_room.Def.Id} at {_room.P.X},{_room.P.Y}");
            _phase = Phase.Dying;
            _phaseT = 0;
            Save.Deaths++;
            var c = _room.P.Center;
            c.Y = Math.Min(c.Y, _room.Height - 4);
            AudioManager.Play("death", 0.8f);
            Shake(0.3f, 3f);
            _freeze = 0;
            Color col = _pv.ScarfColor;
            Color dark = G.Hex(0x1b1630);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Calc.Tau;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                _parts.Emit(c, dir * 110f, dark, 0.6f, 4.8f, PShape.Disc, drag: 5f, end: dark * 0f, sizeEnd: 2f);
                _parts.Emit(c, dir * 110f, col, 0.6f, 3.5f, PShape.Disc, drag: 5f, end: col * 0f, sizeEnd: 1f);
            }
            Rumble(0.6f, 0.25f);
            _parts.Emit(c, Vector2.Zero, Color.White, 0.25f, 4, PShape.Glow, sizeEnd: 40, additive: true);
            _parts.Burst(c, 12, Color.White, 20, 80, 0.5f, 1, 0, 3);
        }

        private void UpdateDying(float dt)
        {
            _phaseT += dt;
            _parts.Update(dt);
            if (_phaseT > 0.5f && _wipe <= 0 && !_wipeCovering)
            {
                StartWipe(() =>
                {
                    _room.Respawn();
                    _room.Events.Clear();
                    _pv.Reset(_room.P);
                    _parts.Clear();
                    _emberPos = _room.HasEmber ? new Vector2(_room.Ember.X, _room.Ember.Y) : Vector2.Zero;
                    _rr.EmberPos = _emberPos;
                    SnapCamera();
                    var c = _room.P.Center;
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Calc.Tau;
                        var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        _parts.Emit(c + dir * 36f, -dir * 130f, G.Hex(0x1b1630), 0.28f, 2.5f, PShape.Disc, end: G.Hex(0x1b1630) * 0.5f, sizeEnd: 4);
                        _parts.Emit(c + dir * 36f, -dir * 130f, _pv.ScarfColor, 0.28f, 1.5f, PShape.Disc, end: Color.White, sizeEnd: 3);
                    }
                    AudioManager.Play("respawn", 0.5f);
                }, Phase.Respawning);
            }
        }

        private void StartWipe(Action onCovered, Phase after)
        {
            _wipeCovering = true;
            _wipeReverse = !_wipeReverse;
            _onCovered = onCovered;
            _afterWipe = after;
        }

        private void UpdateWipe(float dt)
        {
            if (_wipeCovering)
            {
                _wipe = Math.Min(1, _wipe + dt * 3.6f);
                if (_wipe >= 1)
                {
                    _wipeCovering = false;
                    var cb = _onCovered;
                    _onCovered = null;
                    cb?.Invoke();
                    if (_phase != Phase.Card) { _phase = _afterWipe; _phaseT = 0; }
                }
            }
            else if (_wipe > 0 && _phase != Phase.Card) _wipe = Math.Max(0, _wipe - dt * 3.6f);
        }

        // ---- room exit -------------------------------------------------------------------

        private void StartExit()
        {
            _phase = Phase.Wipe;
            AudioManager.Play("whoosh", 0.5f);
            bool securedEmber = _room.HasEmber && _room.Ember.State == 1 && !_emberGhost;
            if (securedEmber)
            {
                Save.Embers.Add(_room.Def.Id);
                if (Save.Embers.Count >= Levels.EmberCount) _pv.Golden = true;
                _emberPopup = 3f;
                AudioManager.Play("embersecure", 0.7f);
            }
            int next = _roomIndex + 1;
            if (next >= Levels.All.Count) next = _roomIndex;
            StartWipe(() =>
            {
                int prevChapter = _chapter;
                LoadRoom(next);
                if (_chapter != prevChapter)
                {
                    _wipe = 0;
                    ShowCard();
                }
            }, Phase.Playing);
        }

        // ---- the shrine --------------------------------------------------------------------

        private void StartGift(Vector2 at)
        {
            _phase = Phase.Gift;
            _phaseT = 0;
            Save.HasDash = true;
            Game.WriteSave();
            _pv.GiftBlend = 0f;
            AudioManager.Play("gift", 0.85f);
            AudioManager.Duck(0.25f);
            _flash = 0.6f; _flashColor = G.Hex(0xfff3d0);
            Shake(0.4f, 2f);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Calc.Tau;
                _parts.Emit(at, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 120f, G.Hex(0xfff0c0), 0.9f, 2, PShape.Disc, drag: 3, sizeEnd: 0.5f, additive: true);
            }
        }

        private void UpdateGift(float dt)
        {
            _phaseT += dt;
            _parts.Update(dt);
            _rr.ShrineGlow = Calc.Clamp01(1 - _phaseT);
            if (_phaseT > 1.0f && _phaseT < 2.2f)
            {
                _pv.GiftBlend = Calc.Clamp01((_phaseT - 1.0f) / 1.0f);
                if (_parts.Rng.Chance(dt * 30))
                    _parts.Emit(_room.P.Center + _parts.Rng.InCircle(10), new Vector2(0, -20), PlayerVisual.ScarfReady, 0.6f, 1, additive: true);
            }
            if (_phaseT >= 2.2f) _pv.GiftBlend = 1f;
            _pv.Update(_room.P, dt, 0, _parts, _st);
            if (_phaseT > 2.8f && (Input.Confirm.Pressed || Input.Jump.Pressed || Input.Dash.Pressed || (_replay != null && _phaseT > 3.2f)))
            {
                Input.Confirm.Consume(); Input.Jump.Consume(); Input.Dash.Consume();
                AudioManager.Play("select", 0.7f);
                AudioManager.Duck(1f);
                _room.Events.Clear();
                _phase = Phase.Playing;
            }
        }

        // ---- the bell ----------------------------------------------------------------------

        private void StartBell()
        {
            if (Game.Auto != null) Console.WriteLine($"[auto] BELL rung (embers {Save.Embers.Count}, deaths {Save.Deaths}, t={Save.PlayTime:0.0})");
            _phase = Phase.Bell;
            _phaseT = 0;
            // The pilgrim lets go of the controls, lands and looks up at the bell.
            _room.P.Facing = _room.BellRect.Center.X >= _room.P.Center.X ? 1 : -1;
            _rr.BellSwing = 0.45f;
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
            if (Cfg.AssistActive) Save.UsedAssist = true;
            Game.WriteSave();
        }

        private void UpdateBell(float dt)
        {
            _phaseT += dt;
            if (_room.P.State == PState.Dash) _room.P.State = PState.Normal;
            _room.Update(new PlayerInput { MoveX = 0 }, dt);
            foreach (var e in _room.Events)
                if (e.Type == Ev.Land) _pv.OnEvent(e, _room.P, _parts, _st, Cfg.ScreenShake, Shake);
            _room.Events.Clear();
            _parts.Update(dt);
            _rr.BellTime += dt;
            _rr.BellSwing = 0.45f * MathF.Exp(-_phaseT * 0.4f);
            _pv.Update(_room.P, dt, 0, _parts, _st);
            var bell = new Vector2(_room.BellRect.Center.X, _room.BellRect.Bottom - 22);
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

        // ---- pause -------------------------------------------------------------------------

        private void OpenPause()
        {
            _paused = true;
            _pauseView = 0;
            AudioManager.Play("pause", 0.7f);
            AudioManager.Duck(0.35f);
            _pauseMenu = new Menu { Back = ClosePause };
            _pauseMenu.Add("Resume", ClosePause);
            _pauseMenu.Add("Restart Room", () =>
            {
                ClosePause();
                _room.P.Kill(_room, new Vector2(0, -1));
                ProcessEvents();
            });
            _pauseMenu.Add("Options", () =>
            {
                _pauseView = 1;
                _optionsMenu = OptionsMenu.Build(() => { _pauseView = 0; ApplyAssist(); }, () => _pauseView = 2);
            });
            _pauseMenu.Add("Save & Return to Title", () =>
            {
                PersistProgress();
                AudioManager.Duck(1f);
                Game.ChangeScene(new TitleScene(), 0.6f);
            });
        }

        private void ApplyAssist()
        {
            _room.InfiniteDash = Cfg.InfiniteDash;
            _room.Invincible = Cfg.Invincible;
        }

        private void ClosePause()
        {
            _paused = false;
            AudioManager.Duck(1f);
            ApplyAssist();
            Input.Jump.Consume(); Input.Dash.Consume(); Input.Confirm.Consume();
        }

        private void UpdatePause(float dt)
        {
            if (Game.Transitioning) return;
            switch (_pauseView)
            {
                case 0:
                    if (Input.Pause.Pressed && !Input.Confirm.Pressed) { AudioManager.Play("back", 0.7f); ClosePause(); return; }
                    _pauseMenu.Update(dt);
                    break;
                case 1:
                    _optionsMenu.Update(dt);
                    break;
                case 2:
                    if (Input.Confirm.Pressed || Input.Back.Pressed)
                    {
                        Input.Confirm.Consume(); Input.Back.Consume();
                        AudioManager.Play("back", 0.7f);
                        _pauseView = 1;
                    }
                    break;
            }
        }

        // ---- ambient particles ----------------------------------------------------------------

        private void InitAmbient()
        {
            var rng = new Rng(_chapter * 31 + 7);
            int n = _chapter switch { 0 => 22, 1 => 30, 2 => 110, _ => 40 };
            _amb = new Amb[n];
            for (int i = 0; i < n; i++)
            {
                _amb[i] = new Amb
                {
                    P = new Vector2(rng.Range(0, G.ViewW), rng.Range(0, G.ViewH)),
                    Phase = rng.Range(0, 100),
                    Size = rng.Range(0.4f, 1f),
                };
                _amb[i].V = _chapter switch
                {
                    0 => new Vector2(rng.Range(-6, 6), rng.Range(-4, 4)),
                    1 => new Vector2(rng.Range(4, 12), rng.Range(6, 14)),
                    2 => new Vector2(rng.Range(-4, 4), rng.Range(18, 40)),
                    _ => new Vector2(rng.Range(-3, 3), rng.Range(-12, -4)),
                };
            }
        }

        private void UpdateAmbient(float dt)
        {
            var d = _cam - _lastCam;
            _lastCam = _cam;
            float wind = _room?.WindNow ?? 0;
            for (int i = 0; i < _amb.Length; i++)
            {
                ref var a = ref _amb[i];
                float depth = 0.4f + a.Size * 0.6f;
                a.P -= d * depth;
                var v = a.V;
                if (_chapter == 0) v += new Vector2(MathF.Sin(_time * 0.7f + a.Phase) * 6, MathF.Cos(_time * 0.9f + a.Phase) * 5);
                if (_chapter == 1) v.X += MathF.Sin(_time * 1.3f + a.Phase) * 10;
                v.X += wind * (_chapter == 2 ? 1.6f : 0.8f) * depth;
                a.P += v * dt * depth;
                a.P.X = ((a.P.X % (G.ViewW + 20)) + G.ViewW + 20) % (G.ViewW + 20);
                a.P.Y = ((a.P.Y % (G.ViewH + 20)) + G.ViewH + 20) % (G.ViewH + 20);
            }
        }

        private void DrawAmbient(SpriteBatch sb, bool additive)
        {
            for (int i = 0; i < _amb.Length; i++)
            {
                var a = _amb[i];
                var p = a.P - new Vector2(10, 10);
                switch (_chapter)
                {
                    case 0:
                        if (!additive) break;
                        {
                            float tw = 0.5f + 0.5f * MathF.Sin(_time * 2.3f + a.Phase * 3);
                            G.GlowAt(sb, p, 5 * a.Size + 2, _st.Ambient * (0.35f * tw));
                            G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), 1, 1, _st.Ambient * tw);
                        }
                        break;
                    case 1:
                        if (additive) break;
                        G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), a.Size > 0.8f ? 2 : 1, 1, (a.Size > 0.7f ? _st.Accent1 : _st.Ambient) * 0.55f);
                        break;
                    case 2:
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

        // ---- drawing -----------------------------------------------------------------------

        private static Backdrop _prologueBd;

        public override void DrawWorld(SpriteBatch sb)
        {
            if (_phase == Phase.Prologue)
            {
                // The far-off peak under the stars while the story is told.
                _prologueBd ??= new Backdrop(Style.Title, 70, 0.5f, 0.2f, 0.55f);
                SubPixel = Vector2.Zero;
                sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(1, 1, 0));
                _prologueBd.Draw(sb, new Vector2(_phaseT * 3f, 0), new Point(G.ViewW, G.ViewH), _time, 0.8f);
                sb.Draw(G.Vignette, Vector2.Zero, Color.White);
                sb.End();
                return;
            }
            var camFloor = new Vector2(MathF.Floor(_cam.X), MathF.Floor(_cam.Y));
            SubPixel = _cam - camFloor;
            var cam = camFloor + _shakeOff;
            var screen = Matrix.CreateTranslation(1, 1, 0);
            var world = Matrix.CreateTranslation(-cam.X + 1, -cam.Y + 1, 0);

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: screen);
            Backdrops[_chapter].Draw(sb, cam, new Point(_room.Width, _room.Height), _time);
            sb.End();

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: world);
            _rr.DrawBack(sb);
            _rr.DrawTerrain(sb);
            _rr.DrawObjects(sb, _time, _shakeRng);
            _rr.DrawHints(sb, _room.P.Center, _time);
            bool showPlayer = _phase != Phase.Dying && !(_phase == Phase.Wipe && _room.P.State == PState.Dead) &&
                              !(_phase == Phase.Respawning && _phaseT < 0.2f) && _phase != Phase.Prologue;
            if (showPlayer) _pv.Draw(sb, _room.P);
            _rr.DrawPickups(sb, _time);
            _parts.Draw(sb, false);
            sb.End();

            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive, transformMatrix: world);
            _rr.DrawGlows(sb, _time);
            _parts.Draw(sb, true);
            sb.End();

            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: screen);
            DrawAmbient(sb, false);
            sb.Draw(G.Vignette, Vector2.Zero, Color.White);
            sb.End();
            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive, transformMatrix: screen);
            DrawAmbient(sb, true);
            sb.End();
        }

        public override void DrawUI(SpriteBatch sb)
        {
            var font = G.Font;
            sb.Begin(samplerState: SamplerState.PointClamp);

            if (_flash > 0) G.Rect(sb, 0, 0, G.ViewW, G.ViewH, _flashColor * (_flash * 0.8f));

            Fx.Wipe(sb, Calc.EaseInOutCubic(_wipe), _wipeReverse, Color.Black);

            // HUD
            if (Cfg.ShowTimer && _phase != Phase.Prologue)
            {
                string t = Calc.FormatTime(Save.PlayTime);
                G.Rect(sb, 0, 2, font.Measure(t) + 8, 11, Color.Black * 0.55f);
                font.Draw(sb, t, new Vector2(4, 4), Color.White);
            }
            if (_emberPopup > 0)
            {
                float k = Calc.EaseOutCubic(Math.Min(1, (3f - _emberPopup) * 4)) * Calc.Clamp01(_emberPopup * 3);
                string s = $"{Save.Embers.Count} / {Levels.EmberCount}";
                float w = font.Measure(s) + 22;
                float x = G.ViewW - w * k - 2;
                G.Rect(sb, x, 4, w + 4, 14, Color.Black * 0.6f);
                var f = Sprites.Frames["ember" + ((int)(_time * 10) % 3)];
                sb.Draw(Sprites.Sheet, new Vector2(x + 4, 6), f, Color.White);
                font.Draw(sb, s, new Vector2(x + 15, 7), G.Hex(0xffd9a0));
            }

            if (_tipT > 0 && _phase == Phase.Playing)
            {
                float ta = Calc.Clamp01(_tipT) * Calc.Clamp01((6f - _tipT) * 2f);
                string tip = "Tip: assist options live in the pause menu.";
                float tw = font.Measure(tip) + 12;
                G.Rect(sb, G.ViewW / 2f - tw / 2f, G.ViewH - 18, tw, 13, Color.Black * (0.6f * ta));
                font.DrawCentered(sb, tip, new Vector2(G.ViewW / 2f, G.ViewH - 16), new Color(230, 225, 240) * ta);
            }

            if (_phase == Phase.Prologue) DrawPrologue(sb);
            if (_phase == Phase.Card) DrawCard(sb);
            if (_phase == Phase.Gift) DrawGiftPanel(sb);
            if (_phase == Phase.Bell)
            {
                float a = Calc.Clamp01((_phaseT - 4.5f) / 2f);
                G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.White * a);
            }

            if (_pauseA > 0.01f) DrawPause(sb);
            sb.End();
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

        private void DrawCard(SpriteBatch sb)
        {
            var font = G.Font;
            float bg = _cardT < 2.6f ? 1f : 1f - Calc.Clamp01((_cardT - 2.6f) / 0.8f);
            float textA = Calc.Clamp01((_cardT - 0.15f) / 0.6f) * bg;
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.Black * bg);
            Color accent = _st.Accent1;
            float cy = 66 - (1 - Calc.EaseOutCubic(Calc.Clamp01(_cardT / 0.9f))) * 6;
            font.DrawCentered(sb, _card.Subtitle.ToUpperInvariant(), new Vector2(G.ViewW / 2f, cy), accent * textA);
            font.DrawCentered(sb, _card.Title, new Vector2(G.ViewW / 2f, cy + 14), Color.White * textA, Color.Black * textA, 2f);
            Fx.Ornament(sb, new Vector2(G.ViewW / 2f, cy + 42), 60 * Calc.EaseOutCubic(Calc.Clamp01((_cardT - 0.3f) / 0.8f)), accent * textA);
            font.DrawCentered(sb, _card.Epigraph, new Vector2(G.ViewW / 2f, cy + 52), new Color(200, 195, 215) * (Calc.Clamp01((_cardT - 0.7f) / 0.7f) * bg));
            int got = 0, total = Levels.EmberCountInChapter(_chapter);
            foreach (var r in Levels.All) if (r.Chapter == _chapter && Save.Embers.Contains(r.Id)) got++;
            if (got > 0)
                font.DrawCentered(sb, $"Embers {got} / {total}", new Vector2(G.ViewW / 2f, cy + 74), G.Hex(0xffd9a0) * (textA * 0.8f));
        }

        private void DrawGiftPanel(SpriteBatch sb)
        {
            var font = G.Font;
            float a = Calc.Clamp01((_phaseT - 2.2f) / 0.5f);
            if (a <= 0) return;
            float y = 118 + (1 - Calc.EaseOutCubic(a)) * 8;
            Fx.Panel(sb, 40, y, G.ViewW - 80, 50, a, PlayerVisual.ScarfReady);
            font.DrawCentered(sb, "The Wind's Gift", new Vector2(G.ViewW / 2f, y + 6), PlayerVisual.ScarfReady * a, Color.Black * a);
            font.DrawCentered(sb, Input.Format("Press {DASH} to dash. Aim with {MOVE}."), new Vector2(G.ViewW / 2f, y + 20), Color.White * a);
            font.DrawCentered(sb, "Your scarf glows red while your dash is ready.", new Vector2(G.ViewW / 2f, y + 31), new Color(200, 200, 215) * a);
            if (_phaseT > 2.8f)
                font.DrawCentered(sb, Input.Format("{CONFIRM}"), new Vector2(G.ViewW / 2f + 0, y + 54), Color.White * (0.5f + 0.3f * MathF.Sin(_time * 4)));
        }

        private void DrawPause(SpriteBatch sb)
        {
            var font = G.Font;
            float a = _pauseA;
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, new Color(6, 6, 16) * (0.7f * a));
            Color accent = _st.Accent1;
            if (_pauseView == 2) { OptionsMenu.DrawControls(sb, a, accent); return; }
            if (_pauseView == 1)
            {
                OptionsMenu.Draw(sb, _optionsMenu, a, accent);
                return;
            }
            var ch = Levels.Chapters[_chapter];
            font.DrawCentered(sb, "Paused", new Vector2(G.ViewW / 2f, 26), Color.White * a, Color.Black * a, 2f);
            int roomNo = _roomIndex - Levels.FirstRoomOfChapter(_chapter) + 1;
            font.DrawCentered(sb, $"{ch.Subtitle} · {ch.Title} · Room {roomNo}/{Levels.RoomCountInChapter(_chapter)}",
                new Vector2(G.ViewW / 2f, 52), accent * a);
            int got = 0;
            foreach (var r in Levels.All) if (r.Chapter == _chapter && Save.Embers.Contains(r.Id)) got++;
            string stats = $"Deaths {Save.Deaths}   Embers {Save.Embers.Count}/{Levels.EmberCount}   {Calc.FormatTime(Save.PlayTime, false)}";
            font.DrawCentered(sb, stats, new Vector2(G.ViewW / 2f, 64), new Color(190, 190, 210) * a);
            Fx.Ornament(sb, new Vector2(G.ViewW / 2f, 80), 70, accent * (0.6f * a));
            _pauseMenu.Draw(sb, new Vector2(G.ViewW / 2f, 118), a, accent);
        }
    }
}
