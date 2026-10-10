using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.Scenes;

namespace HolyPeak
{
    /// <summary>Command-line driven automation used for screenshots and replay checks during development.</summary>
    public sealed class AutomationOptions
    {
        public string ShotPath;          // write PNG(s) here
        public int Frames = 120;         // frames to run before the (last) screenshot
        public int Every;                // also capture every N frames (numbered files)
        public string Scene = "title";   // title | game | ending | prologue
        public string Room;              // room id for the game scene
        public string Pos;               // tile position "x,y" to place the pilgrim in that room
        public string Replay;            // input replay file for the game scene
        public string Give;              // flags to grant, e.g. "grips,bell,kite,boss:thornback"
        public string Keys;              // scripted presses: "frame:Button,frame:Button"
        public bool Invincible;          // assist invincibility (for watching guardians)
    }

    public sealed class HolyPeakGame : Game
    {
        public static HolyPeakGame Instance { get; private set; }

        public readonly GraphicsDeviceManager Graphics;
        public Settings Settings;
        public SaveData Save;
        public readonly AutomationOptions Auto;
        public readonly bool AutoAudio;

        private SpriteBatch _sb;
        private RenderTarget2D _worldRt, _uiRt, _captureRt, _lightRt;

        /// <summary>The light map the current scene drew this frame (same size and space as the world target).</summary>
        public RenderTarget2D LightTarget => _lightRt;
        private Scene _scene, _next;
        private float _fade = 1f;       // 1 = black
        private float _fadeSpeed = 2.5f;
        private Color _fadeColor = Color.Black;
        private bool _fadingOut;
        private int _frame;
        private bool _quitRequested;
        private bool _resizing;

        public Scene Scene => _scene;
        private static readonly RasterizerState ScissorState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

        public HolyPeakGame(AutomationOptions auto)
        {
            Instance = this;
            Auto = auto;
            Graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = G.ViewW * 4,
                PreferredBackBufferHeight = G.ViewH * 4,
                SynchronizeWithVerticalRetrace = auto == null,
                HardwareModeSwitch = false,
                GraphicsProfile = GraphicsProfile.Reach,
            };
            // Automated runs draw every simulated frame (no frame skipping) and run as fast as possible,
            // unless audio is being recorded, which needs real time.
            AutoAudio = auto != null && Environment.GetEnvironmentVariable("HOLYPEAK_AUTO_AUDIO") == "1";
            IsFixedTimeStep = auto == null || AutoAudio;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
            IsMouseVisible = false;
            Window.Title = "Holy Peak";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += (_, _) =>
            {
                if (Graphics.IsFullScreen || _resizing) return;
                var b = Window.ClientBounds;
                if (b.Width <= 0 || b.Height <= 0) return;
                if (b.Width == Graphics.PreferredBackBufferWidth && b.Height == Graphics.PreferredBackBufferHeight) return;
                _resizing = true;
                Graphics.PreferredBackBufferWidth = b.Width;
                Graphics.PreferredBackBufferHeight = b.Height;
                Graphics.ApplyChanges();
                _resizing = false;
            };
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            string dataDir = Environment.GetEnvironmentVariable("HOLYPEAK_DATA");
            if (!string.IsNullOrEmpty(dataDir)) Storage.OverrideFolder = dataDir;
            else if (Auto != null) Storage.OverrideFolder = Path.Combine(Path.GetTempPath(), "holypeak-auto-" + Environment.ProcessId);
            Settings = Storage.Load<Settings>("settings.json");
            Save = Storage.Load<SaveData>("save.json");
            if (Auto != null && Auto.Invincible) Settings.Invincible = true;
            ApplyVideo();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _sb = new SpriteBatch(GraphicsDevice);
            G.Init(GraphicsDevice);
            Sprites.Init();
            _worldRt = new RenderTarget2D(GraphicsDevice, G.ViewW + 2, G.ViewH + 2);
            _lightRt = new RenderTarget2D(GraphicsDevice, G.ViewW + 2, G.ViewH + 2);
            _uiRt = new RenderTarget2D(GraphicsDevice, G.ViewW, G.ViewH);
            if (Auto == null || AutoAudio) AudioManager.Init();
            ApplyAudioSettings();

            Scene first;
            if (Auto != null)
            {
                _captureRt = new RenderTarget2D(GraphicsDevice, G.ViewW * 4, G.ViewH * 4);
                first = Auto.Scene switch
                {
                    "game" => GameScene.ForAutomation(Auto),
                    "ending" => new EndingScene(),
                    "prologue" => GameScene.ForAutomation(Auto),
                    _ => new TitleScene(),
                };
                _fade = 0;
            }
            else first = new TitleScene();
            _scene = first;
            _scene.Begin();
        }

        public void ApplyVideo()
        {
            if (Settings.Fullscreen)
            {
                var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                Graphics.PreferredBackBufferWidth = dm.Width;
                Graphics.PreferredBackBufferHeight = dm.Height;
                Graphics.IsFullScreen = true;
            }
            else
            {
                int s = Math.Clamp(Settings.WindowScale, 2, 8);
                try
                {
                    var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    while (s > 2 && (G.ViewW * s > dm.Width || G.ViewH * s > dm.Height - 60)) s--;
                }
                catch (Exception) { }
                Graphics.IsFullScreen = false;
                Graphics.PreferredBackBufferWidth = G.ViewW * s;
                Graphics.PreferredBackBufferHeight = G.ViewH * s;
            }
            Graphics.ApplyChanges();
        }

        public void ApplyAudioSettings()
        {
            AudioManager.MusicVolume = Settings.MusicVolume / 10f;
            AudioManager.SfxVolume = Settings.SfxVolume / 10f;
        }

        public void SaveSettings() => Storage.Write("settings.json", Settings);
        public void WriteSave() => Storage.Write("save.json", Save);

        /// <summary>Fades to black, swaps scenes, fades back in.</summary>
        public void ChangeScene(Scene next, float fadeSeconds = 0.5f, Color? color = null)
        {
            if (_next != null) return;
            _fadeColor = color ?? Color.Black;
            _next = next;
            _fadingOut = true;
            _fadeSpeed = 1f / Math.Max(0.05f, fadeSeconds);
        }

        public bool Transitioning => _next != null;

        public void RequestQuit() => _quitRequested = true;

        private System.Collections.Generic.Dictionary<int, string[]> _keyScript;

        protected override void Update(GameTime gameTime)
        {
            float dt = 1f / 60f;
            if (Auto?.Keys != null)
            {
                if (_keyScript == null)
                {
                    _keyScript = new System.Collections.Generic.Dictionary<int, string[]>();
                    foreach (var part in Auto.Keys.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = part.Split(':');
                        int f = int.Parse(kv[0]);
                        _keyScript[f] = _keyScript.TryGetValue(f, out var old) ? Append(old, kv[1]) : new[] { kv[1] };
                    }
                }
                if (_keyScript.TryGetValue(_frame, out var names)) foreach (var n in names) Input.Injected.Add(n);
            }
            Input.Update(dt, IsActive || Auto != null);

            if (Input.Fullscreen.Pressed)
            {
                Settings.Fullscreen = !Settings.Fullscreen;
                ApplyVideo();
                SaveSettings();
            }

            AudioManager.Update(dt);

            if (_next != null && _fadingOut)
            {
                _fade = Math.Min(1, _fade + dt * _fadeSpeed);
                if (_fade >= 1)
                {
                    _scene?.End();
                    _scene = _next;
                    _next = null;
                    _fadingOut = false;
                    _scene.Begin();
                }
            }
            else if (_fade > 0) _fade = Math.Max(0, _fade - dt * _fadeSpeed);

            _scene?.Update(dt);

            if (_quitRequested) Exit();

            if (Auto != null)
            {
                _frame++;
                if (_frame > Auto.Frames + 2) Exit();   // safety net if the final Draw was skipped
            }
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var gd = GraphicsDevice;

            // 0) light map for scenes with darkness
            if (_scene != null && _scene.UsesLight)
            {
                gd.SetRenderTarget(_lightRt);
                gd.Clear(_scene.LightAmbient);
                _scene.DrawLight(_sb);
            }

            // 1) world at native resolution (+1px margin for sub-pixel camera smoothing)
            gd.SetRenderTarget(_worldRt);
            gd.Clear(Color.Black);
            _scene?.DrawWorld(_sb);

            // 2) UI at native resolution
            gd.SetRenderTarget(_uiRt);
            gd.Clear(Color.Transparent);
            _scene?.DrawUI(_sb);
            if (_fade > 0)
            {
                _sb.Begin(samplerState: SamplerState.PointClamp);
                G.Rect(_sb, 0, 0, G.ViewW, G.ViewH, _fadeColor * Calc.SmoothStep(_fade));
                _sb.End();
            }

            // 3) composite, integer-scaled and letterboxed
            bool capture = Auto != null && Auto.ShotPath != null &&
                           (_frame == Auto.Frames || (Auto.Every > 0 && _frame % Auto.Every == 0 && _frame <= Auto.Frames));
            RenderTarget2D target = capture ? _captureRt : null;
            gd.SetRenderTarget(target);
            int bw = target?.Width ?? gd.PresentationParameters.BackBufferWidth;
            int bh = target?.Height ?? gd.PresentationParameters.BackBufferHeight;
            gd.Clear(Color.Black);
            int scale = Math.Max(1, Math.Min(bw / G.ViewW, bh / G.ViewH));
            int vw = G.ViewW * scale, vh = G.ViewH * scale;
            int ox = (bw - vw) / 2, oy = (bh - vh) / 2;
            var sub = _scene?.SubPixel ?? Vector2.Zero;

            gd.ScissorRectangle = new Rectangle(ox, oy, vw, vh);
            _sb.Begin(samplerState: SamplerState.PointClamp, rasterizerState: ScissorState);
            _sb.Draw(_worldRt, new Vector2(ox - (1 + sub.X) * scale, oy - (1 + sub.Y) * scale), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            _sb.Draw(_uiRt, new Rectangle(ox, oy, vw, vh), Color.White);
            _sb.End();

            if (capture)
            {
                gd.SetRenderTarget(null);
                string path = Auto.ShotPath;
                if (Auto.Every > 0 && _frame != Auto.Frames)
                    path = Path.Combine(Path.GetDirectoryName(path) ?? ".", $"{Path.GetFileNameWithoutExtension(path)}_{_frame:00000}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                using (var fs = File.Create(path)) _captureRt.SaveAsPng(fs, _captureRt.Width, _captureRt.Height);
                if (_frame >= Auto.Frames) Exit();
            }
            base.Draw(gameTime);
        }

        private static string[] Append(string[] a, string b)
        {
            var r = new string[a.Length + 1];
            a.CopyTo(r, 0);
            r[a.Length] = b;
            return r;
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            _scene?.OnQuit();
            base.OnExiting(sender, args);
        }
    }
}
