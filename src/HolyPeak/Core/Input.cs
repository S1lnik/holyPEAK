using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HolyPeak.Core
{
    /// <summary>A logical button bound to several keys and gamepad buttons.</summary>
    public sealed class VButton
    {
        public Keys[] Keys { get; internal set; }
        public Buttons[] Pads { get; internal set; }
        public bool Down, Pressed, Released;
        private float _repeatTimer;
        public bool Repeated;

        public VButton(Keys[] keys = null, Buttons[] pads = null)
        {
            Keys = keys ?? Array.Empty<Keys>();
            Pads = pads ?? Array.Empty<Buttons>();
        }

        internal void Update(bool extra, float dt, ref bool usedPad, ref bool usedKeys)
        {
            bool down = extra;
            foreach (var k in Keys) if (Input.KeyDown(k)) { down = true; usedKeys = true; }
            foreach (var b in Pads) if (Input.PadDown(b)) { down = true; usedPad = true; }
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

        /// <summary>After a rebind: a key that is already held counts as held, so it cannot fire a fresh press.</summary>
        internal void Sync()
        {
            foreach (var k in Keys) if (Input.KeyDown(k)) Down = true;
            foreach (var b in Pads) if (Input.PadDown(b)) Down = true;
            Pressed = Repeated = false;
            _repeatTimer = 0.38f;
        }

        public void Consume() { Pressed = false; Repeated = false; }
    }

    /// <summary>A rebindable gameplay action: two keyboard slots and one gamepad button.</summary>
    public sealed class Binding
    {
        public readonly string Id, Label;
        public readonly VButton Button;
        public readonly Keys[] DefaultKeys;
        public readonly Buttons DefaultPad;
        /// <summary>Jump, Pause and movement must always keep at least one key.</summary>
        public readonly bool Required;
        /// <summary>
        /// Fixed extras that apply unless another action is bound to them: the arrows and D-pad (so menus can always
        /// be navigated), RT for spells, LB/LT for healing, Enter/Start for pausing.
        /// </summary>
        public readonly Keys[] ExtraKeys;
        public readonly Buttons[] ExtraPads;

        /// <summary>The two keyboard slots; Keys.None is an empty slot.</summary>
        public readonly Keys[] Keys = new Keys[2];
        public Buttons Pad;

        public Binding(string id, string label, VButton button, Keys key1, Keys key2, Buttons pad, bool required = false,
                       Keys[] extraKeys = null, Buttons[] extraPads = null)
        {
            Id = id; Label = label; Button = button;
            DefaultKeys = new[] { key1, key2 };
            DefaultPad = pad;
            Required = required;
            ExtraKeys = extraKeys ?? Array.Empty<Keys>();
            ExtraPads = extraPads ?? Array.Empty<Buttons>();
        }
    }

    public static class Input
    {
        public static readonly VButton Jump = new VButton();
        public static readonly VButton Strike = new VButton();
        public static readonly VButton Toll = new VButton();
        public static readonly VButton Cast = new VButton();
        public static readonly VButton Heal = new VButton();
        public static readonly VButton Map = new VButton();
        public static readonly VButton Confirm = new VButton();
        public static readonly VButton Back = new VButton();
        public static readonly VButton Pause = new VButton();
        public static readonly VButton Fullscreen = new VButton(new[] { Keys.F11 });

        public static readonly VButton Up = new VButton();
        public static readonly VButton Down = new VButton();
        public static readonly VButton Left = new VButton();
        public static readonly VButton Right = new VButton();

        private static readonly VButton[] All = { Jump, Strike, Toll, Cast, Heal, Map, Confirm, Back, Pause, Fullscreen, Up, Down, Left, Right };

        /// <summary>The rebindable gameplay actions, in the order the controls screen lists them.</summary>
        public static readonly Binding[] Bindings =
        {
            new Binding("Left", "Left", Left, Keys.Left, Keys.A, Buttons.DPadLeft, true, new[] { Keys.Left }, new[] { Buttons.DPadLeft }),
            new Binding("Right", "Right", Right, Keys.Right, Keys.D, Buttons.DPadRight, true, new[] { Keys.Right }, new[] { Buttons.DPadRight }),
            new Binding("Up", "Up", Up, Keys.Up, Keys.W, Buttons.DPadUp, true, new[] { Keys.Up }, new[] { Buttons.DPadUp }),
            new Binding("Down", "Down", Down, Keys.Down, Keys.S, Buttons.DPadDown, true, new[] { Keys.Down }, new[] { Buttons.DPadDown }),
            new Binding("Jump", "Jump", Jump, Keys.Z, Keys.Space, Buttons.A, true),
            new Binding("Strike", "Strike", Strike, Keys.X, Keys.J, Buttons.X),
            new Binding("Toll", "Ring the bell", Toll, Keys.C, Keys.K, Buttons.Y),
            new Binding("Cast", "Cast a spell", Cast, Keys.F, Keys.I, Buttons.RightShoulder, extraPads: new[] { Buttons.RightTrigger }),
            new Binding("Heal", "Heal (hold)", Heal, Keys.V, Keys.L, Buttons.B, extraPads: new[] { Buttons.LeftShoulder, Buttons.LeftTrigger }),
            new Binding("Map", "Map", Map, Keys.Tab, Keys.M, Buttons.Back),
            new Binding("Pause", "Pause", Pause, Keys.Escape, Keys.P, Buttons.Start, true, new[] { Keys.Enter }, new[] { Buttons.Start }),
        };

        // Menu buttons that work whatever the bindings are, so a bad layout can never lock the player out.
        private static readonly Keys[] ConfirmKeys = { Keys.Enter };
        private static readonly Buttons[] ConfirmPads = { Buttons.A, Buttons.Start };
        private static readonly Keys[] BackKeys = { Keys.Escape, Keys.Back };
        private static readonly Buttons[] BackPads = { Buttons.B, Buttons.Back };

        /// <summary>Gamepad buttons a gameplay action may be bound to (the left stick always moves).</summary>
        public static readonly Buttons[] PadButtons =
        {
            Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.LeftShoulder, Buttons.RightShoulder,
            Buttons.LeftTrigger, Buttons.RightTrigger, Buttons.Back, Buttons.Start, Buttons.LeftStick, Buttons.RightStick,
            Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        };

        static Input() => ResetBindings();

        /// <summary>True when the most recent input came from a gamepad (used for button prompts).</summary>
        public static bool UsingGamepad { get; private set; }
        public static bool AnyPressed { get; private set; }

        public static int MoveX => (Right.Down ? 1 : 0) - (Left.Down ? 1 : 0);
        public static int MoveY => (Down.Down ? 1 : 0) - (Up.Down ? 1 : 0);

        private static KeyboardState _kb, _prevKb;
        private static Buttons _pad, _prevPad;
        private static int _lastSlot;   // the keyboard slot (0 or 1) the player last pressed a gameplay key from

        /// <summary>
        /// Buttons forced down for the next update (automation / testing): virtual button names ("Jump", "Cast"...),
        /// or raw keys and pad buttons as "Key.F" and "Pad.LeftShoulder".
        /// </summary>
        public static readonly HashSet<string> Injected = new HashSet<string>();

        private static VButton ByName(string n) => n switch
        {
            "Jump" => Jump, "Strike" => Strike, "Toll" => Toll, "Cast" => Cast, "Heal" => Heal, "Map" => Map,
            "Confirm" => Confirm, "Back" => Back, "Pause" => Pause,
            "Up" => Up, "Down" => Down, "Left" => Left, "Right" => Right, _ => null,
        };

        internal static bool KeyDown(Keys k) => _kb.IsKeyDown(k);
        internal static bool PadDown(Buttons b) => (_pad & b) != 0;

        /// <summary>True on the frame the key went down.</summary>
        public static bool KeyPressed(Keys k) => _kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k);

        /// <summary>A key that went down this frame, or <see cref="Keys.None"/> (for capturing a new binding).</summary>
        public static Keys KeyHit
        {
            get
            {
                if (_kb.GetPressedKeyCount() == 0) return Keys.None;
                foreach (var k in _kb.GetPressedKeys()) if (!_prevKb.IsKeyDown(k)) return k;
                return Keys.None;
            }
        }

        /// <summary>A bindable pad button that went down this frame, or <see cref="Buttons.None"/>.</summary>
        public static Buttons PadHit
        {
            get
            {
                foreach (var b in PadButtons) if ((_pad & b) != 0 && (_prevPad & b) == 0) return b;
                return Buttons.None;
            }
        }

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

            // injected raw keys and pad buttons behave exactly like real ones
            List<Keys> keys = null;
            Buttons pad = Buttons.None;
            foreach (var name in Injected)
            {
                if (name.StartsWith("Key.") && Enum.TryParse(name.Substring(4), out Keys k)) (keys ??= new List<Keys>(kb.GetPressedKeys())).Add(k);
                else if (name.StartsWith("Pad.") && Enum.TryParse(name.Substring(4), out Buttons b)) pad |= b;
            }
            if (keys != null) kb = new KeyboardState(keys.ToArray());
            if (padOk) foreach (var b in PadButtons) if (gp.IsButtonDown(b)) pad |= b;
            _prevKb = _kb; _kb = kb;
            _prevPad = _pad; _pad = pad;

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
                b.Update(extra, dt, ref usedPad, ref usedKeys);
            }

            // prompts show the keys of whichever slot the player actually uses
            // (not Pause or Map: Esc and Tab are reached for from either layout)
            foreach (var b in Bindings)
                if (b.Button != Pause && b.Button != Map)
                    for (int s = 0; s < 2; s++)
                        if (b.Keys[s] != Keys.None && KeyPressed(b.Keys[s])) _lastSlot = s;

            Injected.Clear();
            bool anyKey = _kb.GetPressedKeyCount() > 0 && _kb != _prevKb;
            AnyPressed = false;
            foreach (var b in All) if (b.Pressed) AnyPressed = true;
            if (anyKey && !usedPad) UsingGamepad = false;
            if (usedPad && !usedKeys) UsingGamepad = true;
            else if (usedKeys) UsingGamepad = false;
        }

        public static void ConsumeAll()
        {
            foreach (var b in All) b.Consume();
        }

        // ---- bindings ---------------------------------------------------------------------------

        public static Binding BindingOf(VButton button)
        {
            foreach (var b in Bindings) if (b.Button == button) return b;
            return null;
        }

        private static Binding OwnerOfKey(Keys k) => k == Keys.None ? null : Array.Find(Bindings, b => b.Keys[0] == k || b.Keys[1] == k);
        private static Binding OwnerOfPad(Buttons p) => p == Buttons.None ? null : Array.Find(Bindings, b => b.Pad == p);

        /// <summary>Why a key may not be bound to an action, or null when it may.</summary>
        private static string Refusal(Binding b, Keys k) => k switch
        {
            Keys.Enter => "Enter is kept for menus.",
            Keys.F11 => "F11 is kept for fullscreen.",
            Keys.Escape when b.Button != Pause => "Esc can only pause.",
            Keys.Back or Keys.Delete => KeyLabel(k) + " clears a key.",
            _ => null,
        };

        public static void ResetBindings()
        {
            foreach (var b in Bindings)
            {
                b.Keys[0] = b.DefaultKeys[0];
                b.Keys[1] = b.DefaultKeys[1];
                b.Pad = b.DefaultPad;
            }
            Rebuild();
        }

        /// <summary>
        /// Puts a key in one of an action's slots (<see cref="Keys.None"/> clears it). A key another action already uses
        /// is swapped: that action's slot gets this slot's previous key. Returns why it was refused, or null.
        /// </summary>
        public static string SetKey(Binding b, int slot, Keys key, out Binding swapped)
        {
            swapped = null;
            Keys old = b.Keys[slot];
            if (key == old) return null;
            if (key == Keys.None)
            {
                if (b.Required && b.Keys[1 - slot] == Keys.None) return b.Label + " needs a key.";
                b.Keys[slot] = Keys.None;
                Rebuild();
                return null;
            }
            string no = Refusal(b, key);
            if (no != null) return no;
            var other = OwnerOfKey(key);
            if (other != null)
            {
                int os = other.Keys[0] == key ? 0 : 1;
                if (other != b && old == Keys.None && other.Required && other.Keys[1 - os] == Keys.None)
                    return other.Label + " needs a key.";
                other.Keys[os] = old;
                if (other != b) swapped = other;
            }
            b.Keys[slot] = key;
            Rebuild();
            return null;
        }

        /// <summary>Binds a pad button, swapping with the action that had it.</summary>
        public static void SetPad(Binding b, Buttons pad, out Binding swapped)
        {
            swapped = null;
            if (pad == b.Pad) return;
            var other = OwnerOfPad(pad);
            if (other != null) { other.Pad = b.Pad; swapped = other; }
            b.Pad = pad;
            Rebuild();
        }

        /// <summary>Loads the bindings stored in the settings. Missing or invalid entries fall back to the defaults.</summary>
        public static void ApplyBindings(Settings s)
        {
            var missing = new List<(Binding b, int slot)>();
            var missingPad = new List<Binding>();
            foreach (var b in Bindings)
            {
                string[] names = null;
                s?.Keys?.TryGetValue(b.Id, out names);
                for (int slot = 0; slot < 2; slot++)
                {
                    string n = names != null && slot < names.Length ? names[slot] : "?";
                    b.Keys[slot] = Keys.None;
                    if (string.IsNullOrEmpty(n)) continue;   // deliberately empty
                    if (TryKey(n, out var k) && Refusal(b, k) == null) b.Keys[slot] = k;
                    else missing.Add((b, slot));
                }

                string p = null;
                s?.Pads?.TryGetValue(b.Id, out p);
                b.Pad = Buttons.None;
                if (p == "") continue;
                if (TryPad(p, out var pad)) b.Pad = pad;
                else missingPad.Add(b);
            }

            // defaults fill the gaps, unless the player already put that key elsewhere
            foreach (var (b, slot) in missing)
                if (OwnerOfKey(b.DefaultKeys[slot]) == null) b.Keys[slot] = b.DefaultKeys[slot];
            foreach (var b in missingPad)
                if (OwnerOfPad(b.DefaultPad) == null) b.Pad = b.DefaultPad;

            // a hand-edited file may use a key twice: the first action keeps it
            var seenKeys = new HashSet<Keys>();
            var seenPads = new HashSet<Buttons>();
            foreach (var b in Bindings)
            {
                for (int slot = 0; slot < 2; slot++)
                    if (b.Keys[slot] != Keys.None && !seenKeys.Add(b.Keys[slot])) b.Keys[slot] = Keys.None;
                if (b.Pad != Buttons.None && !seenPads.Add(b.Pad)) b.Pad = Buttons.None;
            }

            // a table that leaves movement, jumping or pausing without a key is unusable: start over
            foreach (var b in Bindings)
                if (b.Required && b.Keys[0] == Keys.None && b.Keys[1] == Keys.None)
                {
                    foreach (var r in Bindings) { r.Keys[0] = r.DefaultKeys[0]; r.Keys[1] = r.DefaultKeys[1]; }
                    break;
                }
            Rebuild();
        }

        /// <summary>Writes the current bindings into the settings (enum names, "" for an empty slot).</summary>
        public static void StoreBindings(Settings s)
        {
            s.Keys = new Dictionary<string, string[]>();
            s.Pads = new Dictionary<string, string>();
            foreach (var b in Bindings)
            {
                s.Keys[b.Id] = new[] { Stored(b.Keys[0]), Stored(b.Keys[1]) };
                s.Pads[b.Id] = b.Pad == Buttons.None ? "" : b.Pad.ToString();
            }
        }

        private static string Stored(Keys k) => k == Keys.None ? "" : k.ToString();

        // exact enum names only, so numbers or typos in a hand-edited file fall back to the defaults
        private static bool TryKey(string name, out Keys k) => Enum.TryParse(name, out k) && k != Keys.None && k.ToString() == name;
        private static bool TryPad(string name, out Buttons b) => Enum.TryParse(name, out b) && Array.IndexOf(PadButtons, b) >= 0 && b.ToString() == name;

        /// <summary>Rebuilds every virtual button from the bindings; the menu buttons follow Jump and Strike.</summary>
        private static void Rebuild()
        {
            var keys = new List<Keys>();
            var pads = new List<Buttons>();
            foreach (var b in Bindings)
            {
                keys.Clear(); pads.Clear();
                foreach (var k in b.Keys) if (k != Keys.None) keys.Add(k);
                foreach (var k in b.ExtraKeys) if (!keys.Contains(k) && OwnerOfKey(k) == null) keys.Add(k);
                if (b.Pad != Buttons.None) pads.Add(b.Pad);
                foreach (var p in b.ExtraPads) if (!pads.Contains(p) && OwnerOfPad(p) == null) pads.Add(p);
                b.Button.Keys = keys.ToArray();
                b.Button.Pads = pads.ToArray();
            }

            var jump = BindingOf(Jump);
            var strike = BindingOf(Strike);
            Confirm.Keys = Merge(ConfirmKeys, jump.Keys);
            Back.Keys = Merge(BackKeys, strike.Keys);
            // a pad button that is fixed for the other menu action stays there (B always backs out, A always confirms)
            Confirm.Pads = Array.IndexOf(BackPads, jump.Pad) < 0 ? Merge(ConfirmPads, new[] { jump.Pad }) : ConfirmPads;
            Back.Pads = Array.IndexOf(ConfirmPads, strike.Pad) < 0 ? Merge(BackPads, new[] { strike.Pad }) : BackPads;

            foreach (var b in All) b.Sync();
        }

        private static T[] Merge<T>(T[] fixedOnes, T[] bound) where T : struct, Enum
        {
            var r = new List<T>(fixedOnes);
            foreach (var x in bound) if (Convert.ToInt32(x) != 0 && !r.Contains(x)) r.Add(x);
            return r.ToArray();
        }

        // ---- names --------------------------------------------------------------------------------

        /// <summary>Short key name for the controls table ("Z", "Space", "←", "-" for none).</summary>
        public static string KeyLabel(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return k.ToString();
            if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num" + (int)(k - Keys.NumPad0);
            return k switch
            {
                Keys.None => "-",
                Keys.Left => "←", Keys.Right => "→", Keys.Up => "↑", Keys.Down => "↓",
                Keys.Escape => "Esc", Keys.Back => "Bksp", Keys.Delete => "Del", Keys.Insert => "Ins",
                Keys.PageUp => "PgUp", Keys.PageDown => "PgDn", Keys.CapsLock => "Caps",
                Keys.LeftShift or Keys.RightShift => "Shift",
                Keys.LeftControl or Keys.RightControl => "Ctrl",
                Keys.LeftAlt or Keys.RightAlt => "Alt",
                Keys.LeftWindows or Keys.RightWindows => "Win",
                Keys.OemSemicolon => ";", Keys.OemQuotes => "'", Keys.OemComma => ",", Keys.OemPeriod => ".",
                Keys.OemQuestion => "/", Keys.OemMinus => "-", Keys.OemPlus => "=", Keys.OemTilde => "~",
                Keys.OemOpenBrackets => "[", Keys.OemCloseBrackets => "]", Keys.OemPipe or Keys.OemBackslash => "\\",
                Keys.Multiply => "Num*", Keys.Add => "Num+", Keys.Subtract => "Num-", Keys.Divide => "Num/", Keys.Decimal => "Num.",
                _ => k.ToString(),
            };
        }

        /// <summary>Short pad button name for the controls table ("A", "RB", "D-pad ↑").</summary>
        public static string PadLabel(Buttons b) => b switch
        {
            Buttons.None => "-",
            Buttons.LeftShoulder => "LB", Buttons.RightShoulder => "RB",
            Buttons.LeftTrigger => "LT", Buttons.RightTrigger => "RT",
            Buttons.LeftStick => "L3", Buttons.RightStick => "R3",
            Buttons.DPadUp => "D-pad ↑", Buttons.DPadDown => "D-pad ↓",
            Buttons.DPadLeft => "D-pad ←", Buttons.DPadRight => "D-pad →",
            _ => b.ToString(),
        };

        /// <summary>Key name for prompts: "[Z]", "[Space]", bare arrows.</summary>
        public static string KeyName(Keys k) => k switch
        {
            Keys.None => "[unbound]",
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => KeyLabel(k),
            _ => "[" + KeyLabel(k) + "]",
        };

        /// <summary>Pad button name for prompts: "(A)", "(RB)", "D-pad ↑".</summary>
        public static string PadName(Buttons b) => b switch
        {
            Buttons.None => "(unbound)",
            Buttons.DPadUp or Buttons.DPadDown or Buttons.DPadLeft or Buttons.DPadRight => PadLabel(b),
            _ => "(" + PadLabel(b) + ")",
        };

        /// <summary>The key of an action that prompts show: the slot the player last used, else the other one.</summary>
        private static Keys PromptKey(VButton button)
        {
            var b = BindingOf(button);
            return b.Keys[_lastSlot] != Keys.None ? b.Keys[_lastSlot] : b.Keys[1 - _lastSlot];
        }

        private static string Prompt(VButton button) => UsingGamepad ? PadName(BindingOf(button).Pad) : KeyName(PromptKey(button));

        /// <summary>Up and down on a pad: the stick works too, so the default D-pad binding shows as a plain arrow.</summary>
        private static string DirName(VButton button, Buttons dpad, string arrow)
        {
            if (!UsingGamepad) return KeyName(PromptKey(button));
            var pad = BindingOf(button).Pad;
            return pad == dpad ? arrow : PadName(pad);
        }

        public static string JumpName => Prompt(Jump);
        public static string StrikeName => Prompt(Strike);
        public static string TollName => Prompt(Toll);
        public static string CastName => Prompt(Cast);
        public static string HealName => Prompt(Heal);
        public static string MapName => Prompt(Map);
        public static string PauseName => Prompt(Pause);
        public static string ConfirmName => UsingGamepad ? "(A)" : KeyName(PromptKey(Jump));
        public static string BackName => UsingGamepad ? "(B)" : "[Esc]";
        public static string UpName => DirName(Up, Buttons.DPadUp, "↑");
        public static string DownName => DirName(Down, Buttons.DPadDown, "↓");

        public static string MoveName
        {
            get
            {
                if (UsingGamepad) return "Stick";
                Keys u = PromptKey(Up), l = PromptKey(Left), d = PromptKey(Down), r = PromptKey(Right);
                if (u == Keys.Up && l == Keys.Left && d == Keys.Down && r == Keys.Right) return "Arrows";
                if (u == Keys.W && l == Keys.A && d == Keys.S && r == Keys.D) return "WASD";
                string[] n = { KeyLabel(u), KeyLabel(l), KeyLabel(d), KeyLabel(r) };
                bool single = Array.TrueForAll(n, x => x.Length == 1);
                return single ? string.Concat(n) : string.Join("/", n);
            }
        }

        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("{JUMP}", JumpName).Replace("{STRIKE}", StrikeName).Replace("{TOLL}", TollName)
                       .Replace("{CAST}", CastName).Replace("{HEAL}", HealName).Replace("{MAP}", MapName).Replace("{MOVE}", MoveName)
                       .Replace("{UP}", UpName).Replace("{DOWN}", DownName).Replace("{CONFIRM}", ConfirmName)
                       .Replace("{BACK}", BackName).Replace("{PAUSE}", PauseName);
        }
    }
}
