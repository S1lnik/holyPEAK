using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HolyPeak.Core
{
    /// <summary>A logical button bound to several keys and gamepad buttons.</summary>
    public sealed class VButton
    {
        public readonly Keys[] Keys;
        public readonly Buttons[] Pads;
        public bool Down, Pressed, Released;
        private float _repeatTimer;
        public bool Repeated;

        public VButton(Keys[] keys, Buttons[] pads) { Keys = keys; Pads = pads; }

        internal void Update(KeyboardState kb, GamePadState gp, bool padOk, bool extra, float dt, ref bool usedPad, ref bool usedKeys)
        {
            bool down = extra;
            foreach (var k in Keys) if (kb.IsKeyDown(k)) { down = true; usedKeys = true; }
            if (padOk) foreach (var b in Pads) if (gp.IsButtonDown(b)) { down = true; usedPad = true; }
            Set(down, dt);
        }

        internal void Set(bool down, float dt)
        {
            Pressed = down && !Down;
            Released = !down && Down;
            Down = down;
            Repeated = Pressed;
            if (Pressed) _repeatTimer = 0.38f;
            else if (Down)
            {
                _repeatTimer -= dt;
                if (_repeatTimer <= 0) { Repeated = true; _repeatTimer = 0.075f; }
            }
        }

        public void Consume() { Pressed = false; Repeated = false; }
    }

    public static class Input
    {
        public static readonly VButton Jump = new VButton(new[] { Keys.Z, Keys.Space }, new[] { Buttons.A });
        public static readonly VButton Strike = new VButton(new[] { Keys.X, Keys.J }, new[] { Buttons.X });
        public static readonly VButton Toll = new VButton(new[] { Keys.C, Keys.K }, new[] { Buttons.Y, Buttons.RightShoulder, Buttons.RightTrigger });
        public static readonly VButton Heal = new VButton(new[] { Keys.V, Keys.L }, new[] { Buttons.B, Buttons.LeftShoulder, Buttons.LeftTrigger });
        public static readonly VButton Map = new VButton(new[] { Keys.Tab, Keys.M }, new[] { Buttons.Back });
        public static readonly VButton Confirm = new VButton(new[] { Keys.Enter, Keys.Space, Keys.Z, Keys.J }, new[] { Buttons.A, Buttons.Start });
        public static readonly VButton Back = new VButton(new[] { Keys.Escape, Keys.Back, Keys.X, Keys.K }, new[] { Buttons.B, Buttons.Back });
        public static readonly VButton Pause = new VButton(new[] { Keys.Escape, Keys.Enter, Keys.P }, new[] { Buttons.Start });
        public static readonly VButton Fullscreen = new VButton(new[] { Keys.F11 }, Array.Empty<Buttons>());

        public static readonly VButton Up = new VButton(new[] { Keys.Up, Keys.W }, new[] { Buttons.DPadUp });
        public static readonly VButton Down = new VButton(new[] { Keys.Down, Keys.S }, new[] { Buttons.DPadDown });
        public static readonly VButton Left = new VButton(new[] { Keys.Left, Keys.A }, new[] { Buttons.DPadLeft });
        public static readonly VButton Right = new VButton(new[] { Keys.Right, Keys.D }, new[] { Buttons.DPadRight });

        private static readonly VButton[] All = { Jump, Strike, Toll, Heal, Map, Confirm, Back, Pause, Fullscreen, Up, Down, Left, Right };

        /// <summary>True when the most recent input came from a gamepad (used for button prompts).</summary>
        public static bool UsingGamepad { get; private set; }
        /// <summary>True when the player seems to use the WASD + Space/J/K/L layout rather than arrows + Z/X/C/V.</summary>
        public static bool WasdLayout { get; private set; }
        public static bool AnyPressed { get; private set; }

        public static int MoveX => (Right.Down ? 1 : 0) - (Left.Down ? 1 : 0);
        public static int MoveY => (Down.Down ? 1 : 0) - (Up.Down ? 1 : 0);

        private static KeyboardState _prevKb;
        private static readonly Keys[] WasdKeys = { Keys.W, Keys.A, Keys.S, Keys.D, Keys.Space, Keys.J, Keys.K, Keys.L };
        private static readonly Keys[] ArrowKeys = { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Z, Keys.X, Keys.C, Keys.V };

        /// <summary>Buttons forced down for the next update (automation / testing).</summary>
        public static readonly System.Collections.Generic.HashSet<string> Injected = new System.Collections.Generic.HashSet<string>();

        private static VButton ByName(string n) => n switch
        {
            "Jump" => Jump, "Strike" => Strike, "Toll" => Toll, "Heal" => Heal, "Map" => Map,
            "Confirm" => Confirm, "Back" => Back, "Pause" => Pause,
            "Up" => Up, "Down" => Down, "Left" => Left, "Right" => Right, _ => null,
        };

        public static void Update(float dt, bool focused)
        {
            KeyboardState kb = focused ? Keyboard.GetState() : default;
            GamePadState gp = default;
            bool padOk = false;
            try
            {
                gp = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);
                padOk = gp.IsConnected && focused;
            }
            catch (Exception) { padOk = false; }

            bool usedPad = false, usedKeys = false;

            // Analog stick snapped to 8 directions with a generous deadzone so diagonals feel right.
            int sx = 0, sy = 0;
            if (padOk)
            {
                var stick = gp.ThumbSticks.Left;
                if (stick.Length() > 0.35f)
                {
                    usedPad = true;
                    float ang = MathF.Atan2(stick.Y, stick.X);
                    int oct = (int)MathF.Round(ang / (MathF.PI / 4f));
                    oct = ((oct % 8) + 8) % 8;
                    sx = oct switch { 0 => 1, 1 => 1, 7 => 1, 3 => -1, 4 => -1, 5 => -1, _ => 0 };
                    sy = oct switch { 1 => 1, 2 => 1, 3 => 1, 5 => -1, 6 => -1, 7 => -1, _ => 0 };
                }
            }

            foreach (var b in All)
            {
                bool extra = (b == Right && sx > 0) || (b == Left && sx < 0) || (b == Up && sy > 0) || (b == Down && sy < 0);
                foreach (var name in Injected) if (ByName(name) == b) extra = true;
                b.Update(kb, gp, padOk, extra, dt, ref usedPad, ref usedKeys);
            }

            foreach (var k in WasdKeys) if (kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k)) WasdLayout = true;
            foreach (var k in ArrowKeys) if (kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k)) WasdLayout = false;

            Injected.Clear();
            bool anyKey = kb.GetPressedKeyCount() > 0 && kb != _prevKb;
            _prevKb = kb;
            AnyPressed = false;
            foreach (var b in All) if (b.Pressed) AnyPressed = true;
            if (anyKey && !usedPad) UsingGamepad = false;
            if (usedPad && !usedKeys) UsingGamepad = true;
            else if (usedKeys) UsingGamepad = false;
        }

        public static string JumpName => UsingGamepad ? "(A)" : (WasdLayout ? "[Space]" : "[Z]");
        public static string StrikeName => UsingGamepad ? "(X)" : (WasdLayout ? "[J]" : "[X]");
        public static string TollName => UsingGamepad ? "(Y)" : (WasdLayout ? "[K]" : "[C]");
        public static string HealName => UsingGamepad ? "(B)" : (WasdLayout ? "[L]" : "[V]");
        public static string MapName => UsingGamepad ? "(Back)" : "[Tab]";
        public static string ConfirmName => UsingGamepad ? "(A)" : (WasdLayout ? "[Space]" : "[Z]");
        public static string BackName => UsingGamepad ? "(B)" : "[Esc]";
        public static string MoveName => UsingGamepad ? "Stick" : (WasdLayout ? "WASD" : "Arrows");
        public static string UpName => UsingGamepad ? "Up" : (WasdLayout ? "[W]" : "↑");
        public static string DownName => UsingGamepad ? "Down" : (WasdLayout ? "[S]" : "↓");
        public static string PauseName => UsingGamepad ? "(Start)" : "[Esc]";

        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("{JUMP}", JumpName).Replace("{STRIKE}", StrikeName).Replace("{TOLL}", TollName)
                       .Replace("{HEAL}", HealName).Replace("{MAP}", MapName).Replace("{MOVE}", MoveName)
                       .Replace("{UP}", UpName).Replace("{DOWN}", DownName).Replace("{CONFIRM}", ConfirmName)
                       .Replace("{BACK}", BackName).Replace("{PAUSE}", PauseName);
        }
    }
}
