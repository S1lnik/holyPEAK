using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;

namespace HolyPeak.Scenes
{
    public abstract class Scene
    {
        protected static HolyPeakGame Game => HolyPeakGame.Instance;

        /// <summary>Fractional camera offset in [0,1) used to smooth scrolling of the low-res world layer.</summary>
        public Vector2 SubPixel { get; protected set; }

        /// <summary>Scenes with darkness draw their lights into <see cref="HolyPeakGame.LightTarget"/> before the world.</summary>
        public virtual bool UsesLight => false;
        public virtual Color LightAmbient => Color.White;
        public virtual void DrawLight(SpriteBatch sb) { }

        public virtual void Begin() { }
        public virtual void End() { }
        public virtual void OnQuit() { }
        public abstract void Update(float dt);
        public abstract void DrawWorld(SpriteBatch sb);
        public abstract void DrawUI(SpriteBatch sb);
    }

    public sealed class MenuItem
    {
        public string Label;
        public Func<string> Value;
        public Action Confirm;
        public Action<int> Change;
        public Func<bool> Enabled = () => true;
        public float Spacing;   // extra gap before this item
    }

    /// <summary>Vertical menu with keyboard / gamepad navigation, value cycling and animated selection.</summary>
    public sealed class Menu
    {
        public readonly List<MenuItem> Items = new List<MenuItem>();
        public int Index;
        public Action Back;
        public string Title;
        public int LineHeight = 12;
        private float[] _sel = Array.Empty<float>();
        private float _time;

        public Menu Add(string label, Action confirm, Func<bool> enabled = null, float spacing = 0)
        {
            Items.Add(new MenuItem { Label = label, Confirm = confirm, Enabled = enabled ?? (() => true), Spacing = spacing });
            return this;
        }

        public Menu Option(string label, Func<string> value, Action<int> change, float spacing = 0)
        {
            Items.Add(new MenuItem { Label = label, Value = value, Change = change, Confirm = () => change(1), Spacing = spacing });
            return this;
        }

        public void SelectFirstEnabled()
        {
            Index = 0;
            while (Index < Items.Count - 1 && !Items[Index].Enabled()) Index++;
        }

        public void Update(float dt)
        {
            _time += dt;
            if (_sel.Length != Items.Count) _sel = new float[Items.Count];
            if (Items.Count == 0) return;

            int dir = Input.Down.Repeated ? 1 : (Input.Up.Repeated ? -1 : 0);
            if (dir != 0)
            {
                int start = Index;
                do { Index = (Index + dir + Items.Count) % Items.Count; }
                while (!Items[Index].Enabled() && Index != start);
                if (Index != start) AudioManager.Play("move", 0.7f);
            }

            var it = Items[Index];
            if (it.Change != null)
            {
                if (Input.Right.Repeated) { it.Change(1); AudioManager.Play("move", 0.7f, 0.2f); }
                else if (Input.Left.Repeated) { it.Change(-1); AudioManager.Play("move", 0.7f, -0.2f); }
            }
            if (Input.Confirm.Pressed && it.Enabled())
            {
                Input.Confirm.Consume();
                Input.Jump.Consume();
                Input.Strike.Consume();
                AudioManager.Play("select", 0.8f);
                it.Confirm?.Invoke();
            }
            else if (Input.Back.Pressed && Back != null)
            {
                Input.Back.Consume();
                Input.Strike.Consume();
                AudioManager.Play("back", 0.8f);
                Back();
            }

            for (int i = 0; i < _sel.Length; i++)
                _sel[i] = Calc.Approach(_sel[i], i == Index ? 1f : 0f, dt * 7f);
        }

        public float Height
        {
            get
            {
                float h = Title != null ? 16 : 0;
                foreach (var it in Items) h += LineHeight + it.Spacing;
                return h;
            }
        }

        public void Draw(SpriteBatch sb, Vector2 center, float alpha, Color accent, float width = 170)
        {
            var font = G.Font;
            float y = center.Y - Height / 2f;
            if (Title != null)
            {
                font.DrawCentered(sb, Title, new Vector2(center.X, y), accent * alpha, Color.Black * alpha);
                y += 16;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                y += it.Spacing;
                float s = i < _sel.Length ? _sel[i] : 0f;
                bool en = it.Enabled();
                Color c = en ? Color.Lerp(new Color(200, 200, 215), Color.White, s) : new Color(90, 90, 105);
                if (s > 0.01f)
                {
                    float w = (it.Value != null ? width : font.Measure(it.Label) + 22) * Calc.EaseOutCubic(s);
                    G.Rect(sb, center.X - w / 2f, y - 2, w, 11, accent * (0.22f * s * alpha));
                    G.Rect(sb, center.X - w / 2f, y + 9, w, 1, accent * (0.6f * s * alpha));
                }
                if (it.Value != null)
                {
                    float lx = center.X - width / 2f + 6 + s * 2;
                    font.DrawShadowed(sb, it.Label, new Vector2(lx, y), c * alpha, Color.Black * alpha);
                    string v = it.Value();
                    string shown = i == Index ? "← " + v + " →" : v;
                    float vw = font.Measure(shown);
                    font.DrawShadowed(sb, shown, new Vector2(center.X + width / 2f - 6 - vw, y), (i == Index ? accent : c) * alpha, Color.Black * alpha);
                }
                else
                {
                    float bob = i == Index ? MathF.Sin(_time * 6f) * 0.5f : 0;
                    font.DrawCentered(sb, it.Label, new Vector2(center.X, y + bob), c * alpha, Color.Black * alpha);
                    if (i == Index && s > 0.5f)
                    {
                        float w = font.Measure(it.Label);
                        float off = 6 + MathF.Sin(_time * 6f) * 1.5f;
                        font.Draw(sb, ">", new Vector2(center.X - w / 2f - off - 4, y), accent * alpha);
                        font.Draw(sb, "<", new Vector2(center.X + w / 2f + off, y), accent * alpha);
                    }
                }
                y += LineHeight;
            }
        }
    }

    /// <summary>Shared screen-space effects: diamond wipes, panels, flashes.</summary>
    public static class Fx
    {
        /// <summary>Diamond wipe. p = 0 clear, 1 fully covered. <paramref name="reverse"/> sweeps from the other corner.</summary>
        public static void Wipe(SpriteBatch sb, float p, bool reverse, Color color)
        {
            if (p <= 0) return;
            if (p >= 1) { G.Rect(sb, 0, 0, G.ViewW, G.ViewH, color); return; }
            const int cell = 20;
            for (int gy = -1; gy <= G.ViewH / cell + 1; gy++)
                for (int gx = -1; gx <= G.ViewW / cell + 1; gx++)
                {
                    float d = (gx * cell + gy * cell * 0.6f) / (G.ViewW + G.ViewH * 0.6f);
                    if (reverse) d = 1 - d;
                    float t = Calc.Clamp01(p * 2.2f - d * 1.2f);
                    if (t <= 0) continue;
                    float size = t * cell * 1.45f;
                    var c = new Vector2(gx * cell + cell / 2f, gy * cell + cell / 2f);
                    sb.Draw(G.Pixel, c, null, color, MathF.PI / 4f, new Vector2(0.5f), size, SpriteEffects.None, 0f);
                }
        }

        public static void Panel(SpriteBatch sb, float x, float y, float w, float h, float alpha, Color border)
        {
            G.Rect(sb, x, y, w, h, new Color(10, 10, 22) * (0.86f * alpha));
            G.RectOutline(sb, x, y, w, h, border * (0.8f * alpha));
            G.RectOutline(sb, x + 2, y + 2, w - 4, h - 4, border * (0.25f * alpha));
        }

        public static void Ornament(SpriteBatch sb, Vector2 center, float halfWidth, Color c)
        {
            G.Rect(sb, center.X - halfWidth, center.Y, halfWidth - 4, 1, c);
            G.Rect(sb, center.X + 4, center.Y, halfWidth - 4, 1, c);
            sb.Draw(G.Pixel, center + new Vector2(0, 0.5f), null, c, MathF.PI / 4f, new Vector2(0.5f), 3.2f, SpriteEffects.None, 0f);
        }
    }
}
