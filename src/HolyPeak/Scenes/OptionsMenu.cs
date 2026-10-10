using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;

namespace HolyPeak.Scenes
{
    /// <summary>Options and controls screens shared by the title screen and the pause menu.</summary>
    public static class OptionsMenu
    {
        private static readonly ControlsMenu ControlsScreen = new ControlsMenu();

        /// <summary>Draws the options menu inside its framed panel.</summary>
        public static void Draw(SpriteBatch sb, Menu m, float alpha, Color accent)
        {
            float h = m.Height + 16;
            float top = MathF.Round(G.ViewH / 2f - h / 2f);
            Fx.Panel(sb, 64, top, G.ViewW - 128, h, alpha, accent);
            m.Draw(sb, new Vector2(G.ViewW / 2f, G.ViewH / 2f + 2), alpha, accent, 172);
        }

        public static Menu Build(Action back, Action showControls)
        {
            var g = HolyPeakGame.Instance;
            var s = g.Settings;
            var m = new Menu { Title = "Options", LineHeight = 11, Back = () => { g.SaveSettings(); back(); } };

            string Bar(int v) => new string('|', v) + new string('.', 10 - v);
            string OnOff(bool b) => b ? "On" : "Off";

            m.Option("Music", () => Bar(s.MusicVolume), d => { s.MusicVolume = Math.Clamp(s.MusicVolume + d, 0, 10); g.ApplyAudioSettings(); });
            m.Option("Sound", () => Bar(s.SfxVolume), d =>
            {
                s.SfxVolume = Math.Clamp(s.SfxVolume + d, 0, 10);
                g.ApplyAudioSettings();
                AudioManager.Play("jump", 0.8f);
            });
            m.Option("Fullscreen", () => OnOff(s.Fullscreen), d => { s.Fullscreen = !s.Fullscreen; g.ApplyVideo(); }, 3);
            m.Option("Window Size", () => $"{s.WindowScale}x", d =>
            {
                s.WindowScale = Math.Clamp(s.WindowScale + d, 2, 8);
                if (!s.Fullscreen) g.ApplyVideo();
            });
            m.Option("Screen Shake", () => OnOff(s.ScreenShake), d => s.ScreenShake = !s.ScreenShake);
            m.Option("Speedrun Timer", () => OnOff(s.ShowTimer), d => s.ShowTimer = !s.ShowTimer);
            m.Option("Game Speed", () => $"{s.GameSpeed * 10}%", d => s.GameSpeed = Math.Clamp(s.GameSpeed + d, 5, 10), 3);
            m.Option("Invincibility", () => OnOff(s.Invincible), d => s.Invincible = !s.Invincible);
            m.Add("Controls", () => { ControlsScreen.Open(); showControls(); }, spacing: 3);
            m.Add("Back", m.Back);
            return m;
        }

        /// <summary>The current bindings as text: action, keyboard keys, gamepad buttons.</summary>
        public static (string action, string keys, string pad)[] Controls
        {
            get
            {
                var rows = new (string, string, string)[Input.Bindings.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    var b = Input.Bindings[i];
                    var keys = new List<string>();
                    foreach (var k in b.Keys) if (k != Keys.None) keys.Add(Input.KeyLabel(k));
                    var pads = new List<string> { Input.PadLabel(b.Pad) };
                    pads.AddRange(ControlsMenu.PadExtras(b));
                    rows[i] = (b.Label, keys.Count > 0 ? string.Join(" / ", keys) : "-", string.Join(" / ", pads));
                }
                return rows;
            }
        }

        /// <summary>Runs the controls screen; returns true when the player leaves it.</summary>
        public static bool UpdateControls(float dt) => ControlsScreen.Update(dt);

        public static void DrawControls(SpriteBatch sb, float alpha, Color accent) => ControlsScreen.Draw(sb, alpha, accent);
    }

    /// <summary>
    /// The controls screen: every gameplay action with two keys and a pad button, rebound in place.
    /// Up/Down picks the action, Left/Right the column, Confirm listens for the new key or button.
    /// </summary>
    public sealed class ControlsMenu
    {
        private static readonly float[] ColX = { 138, 186, 248 };
        private static readonly float[] ColW = { 44, 44, 64 };
        private static readonly string[] Heads = { "Keys", "Alt", "Pad" };
        private const float Top = 8, PanelH = 162, RowH = 10, FirstRow = Top + 25;
        private const string Tip = "Strike while holding Down in the air to bounce.";

        private int _row, _col, _bottom;   // _bottom picks "Reset to defaults" (0) or "Back" (1) on the last row
        private bool _listening;
        private int _ignore;               // frames to ignore input for after a capture
        private float _time, _msgT;
        private string _msg;
        private bool _msgBad;

        private static int Rows => Input.Bindings.Length;

        public void Open()
        {
            _row = 0; _col = 0; _bottom = 1;
            _listening = false;
            _msgT = 0;
            _ignore = 1;
        }

        /// <summary>Returns true when the player leaves the screen.</summary>
        public bool Update(float dt)
        {
            _time += dt;
            _msgT -= dt;
            if (_ignore > 0) { _ignore--; Input.ConsumeAll(); return false; }
            if (_listening) { Listen(); return false; }

            int dy = Input.Down.Repeated ? 1 : (Input.Up.Repeated ? -1 : 0);
            if (dy != 0)
            {
                _row = (_row + dy + Rows + 1) % (Rows + 1);
                AudioManager.Play("move", 0.7f);
            }
            int dx = Input.Right.Repeated ? 1 : (Input.Left.Repeated ? -1 : 0);
            if (dx != 0)
            {
                if (_row == Rows) _bottom = 1 - _bottom;
                else _col = (_col + dx + 3) % 3;
                AudioManager.Play("move", 0.7f, dx * 0.2f);
            }

            if (_row < Rows && _col < 2 && (Input.KeyPressed(Keys.Delete) || Input.KeyPressed(Keys.Back)))
                Clear();
            else if (Input.Confirm.Pressed)
            {
                Input.Confirm.Consume(); Input.Jump.Consume();
                if (_row == Rows && _bottom == 1) { AudioManager.Play("back", 0.8f); return true; }
                AudioManager.Play("select", 0.8f);
                if (_row < Rows) { _listening = true; _msgT = 0; }
                else
                {
                    Input.ResetBindings();
                    Saved("Defaults restored.");
                }
            }
            else if (Input.Back.Pressed)
            {
                Input.Back.Consume(); Input.Strike.Consume();
                AudioManager.Play("back", 0.8f);
                return true;
            }
            return false;
        }

        /// <summary>Waits for the key (or pad button, in the pad column) to bind to the selected cell.</summary>
        private void Listen()
        {
            var b = Input.Bindings[_row];
            if (_col == 2)
            {
                if (Input.KeyPressed(Keys.Escape)) { Cancel(); return; }
                var p = Input.PadHit;
                if (p == Buttons.None) return;
                Input.SetPad(b, p, out var swapped);
                Done(null, swapped);
                return;
            }

            var k = Input.KeyHit;
            // Esc cancels, except for Pause, which may take it
            if ((k == Keys.Escape && b.Button != Input.Pause) || Input.PadHit is Buttons.B or Buttons.Back) { Cancel(); return; }
            if (k == Keys.None) return;
            if (k == Keys.Delete || k == Keys.Back) { Clear(); return; }
            string refused = Input.SetKey(b, _col, k, out var other);
            Done(refused, other);
        }

        private void Clear()
        {
            var b = Input.Bindings[_row];
            if (b.Keys[_col] == Keys.None) { Cancel(); return; }
            Done(Input.SetKey(b, _col, Keys.None, out _), null);
        }

        private void Done(string refused, Binding swapped)
        {
            _listening = false;
            _ignore = 2;
            Input.ConsumeAll();
            if (refused != null)
            {
                Say(refused, true);
                AudioManager.Play("deny", 0.7f);
                return;
            }
            AudioManager.Play("select", 0.8f);
            Saved(swapped != null ? "Swapped with " + swapped.Label + "." : null);
        }

        private void Cancel()
        {
            _listening = false;
            _ignore = 2;
            Input.ConsumeAll();
            AudioManager.Play("back", 0.7f);
        }

        private void Saved(string message)
        {
            var g = HolyPeakGame.Instance;
            Input.StoreBindings(g.Settings);
            g.SaveSettings();
            if (message != null) Say(message, false);
            else _msgT = 0;
        }

        private void Say(string message, bool bad)
        {
            _msg = message;
            _msgBad = bad;
            _msgT = 2.5f;
        }

        /// <summary>The fixed extra pad buttons an action also answers to (RT for spells, LB/LT for healing).</summary>
        public static IEnumerable<string> PadExtras(Binding b)
        {
            foreach (var p in b.Button.Pads)
                if (p != b.Pad && p != Buttons.DPadUp && p != Buttons.DPadDown && p != Buttons.DPadLeft && p != Buttons.DPadRight)
                    yield return Input.PadLabel(p);
        }

        public void Draw(SpriteBatch sb, float alpha, Color accent)
        {
            var f = G.Font;
            Color dim = new Color(150, 150, 170) * alpha, shadow = Color.Black * alpha;
            Fx.Panel(sb, 24, Top, G.ViewW - 48, PanelH, alpha, accent);
            f.DrawCentered(sb, "Controls", new Vector2(G.ViewW / 2f, Top + 4), accent * alpha, shadow);
            for (int c = 0; c < 3; c++) f.DrawCentered(sb, Heads[c], new Vector2(ColX[c], FirstRow - 11), dim);

            float y = FirstRow;
            for (int i = 0; i < Rows; i++, y += RowH)
            {
                var b = Input.Bindings[i];
                bool sel = i == _row;
                if (sel) G.Rect(sb, 30, y - 2, G.ViewW - 60, RowH, accent * (0.08f * alpha));
                f.DrawShadowed(sb, b.Label, new Vector2(36, y), (sel ? Color.White : new Color(200, 200, 215)) * alpha, shadow);
                for (int c = 0; c < 3; c++) DrawCell(sb, b, c, sel && c == _col, y, alpha, accent);
            }

            // reset / back
            y += 2;
            string[] items = { "Reset to defaults", "Back" };
            float gap = 36, w0 = f.Measure(items[0]), w1 = f.Measure(items[1]);
            float x = G.ViewW / 2f - (w0 + gap + w1) / 2f;
            for (int i = 0; i < 2; i++)
            {
                float w = i == 0 ? w0 : w1;
                bool sel = _row == Rows && _bottom == i;
                if (sel)
                {
                    G.Rect(sb, x - 6, y - 2, w + 12, 10, accent * (0.22f * alpha));
                    G.Rect(sb, x - 6, y + 8, w + 12, 1, accent * (0.6f * alpha));
                    float off = 9 + MathF.Sin(_time * 6f) * 1.5f;
                    f.Draw(sb, ">", new Vector2(x - off - 4, y), accent * alpha);
                    f.Draw(sb, "<", new Vector2(x + w + off, y), accent * alpha);
                }
                f.DrawShadowed(sb, items[i], new Vector2(x, y), (sel ? Color.White : new Color(200, 200, 215)) * alpha, shadow);
                x += w + gap;
            }

            // one line below: the prompt while listening, then any message, otherwise the tip
            var at = new Vector2(G.ViewW / 2f, Top + PanelH - 13);
            if (_listening)
            {
                var b = Input.Bindings[_row];
                string what = _col == 2 ? "Press a button for " : "Press a key for ";
                string cancel = _col == 2 || b.Button != Input.Pause ? " (Esc to cancel)" : "";
                float pulse = 0.75f + 0.25f * MathF.Sin(_time * 8f);
                f.DrawCentered(sb, what + b.Label + "..." + cancel, at, accent * (pulse * alpha), shadow);
            }
            else if (_msgT > 0)
                f.DrawCentered(sb, _msg, at, (_msgBad ? new Color(255, 130, 110) : accent) * alpha, shadow);
            else
                f.DrawCentered(sb, Tip, at, new Color(170, 170, 195) * alpha);
        }

        private void DrawCell(SpriteBatch sb, Binding b, int col, bool sel, float y, float alpha, Color accent)
        {
            var f = G.Font;
            float cx = ColX[col], w = ColW[col];
            if (sel)
            {
                float a = _listening ? 0.3f + 0.15f * MathF.Sin(_time * 8f) : 0.22f;
                G.Rect(sb, cx - w / 2f, y - 2, w, 10, accent * (a * alpha));
                G.Rect(sb, cx - w / 2f, y + 8, w, 1, accent * (0.6f * alpha));
            }
            if (sel && _listening)
            {
                f.DrawCentered(sb, "...", new Vector2(cx, y), Color.White * alpha);
                return;
            }

            bool bound = col < 2 ? b.Keys[col] != Keys.None : b.Pad != Buttons.None;
            string text = col < 2 ? Input.KeyLabel(b.Keys[col]) : Input.PadLabel(b.Pad);
            Color c = !bound ? new Color(110, 110, 130) : sel ? accent : new Color(220, 220, 235);
            if (col < 2)
            {
                f.DrawCentered(sb, text, new Vector2(cx, y), c * alpha, Color.Black * alpha);
                return;
            }

            // fixed extras (RT, LB, LT) follow the bound button, dimmed
            string extra = "";
            foreach (var e in PadExtras(b)) extra += " +" + e;
            float tw = f.Measure(text), ew = extra.Length > 0 ? f.Measure(extra) + 1 : 0;
            float x = MathF.Round(cx - (tw + ew) / 2f);
            f.DrawShadowed(sb, text, new Vector2(x, y), c * alpha, Color.Black * alpha);
            if (ew > 0) f.Draw(sb, extra, new Vector2(x + tw + 1, y), new Color(130, 130, 155) * alpha);
        }
    }
}
