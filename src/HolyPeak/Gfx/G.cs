using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;

namespace HolyPeak.Gfx
{
    /// <summary>Global graphics helpers and the handful of generic procedural textures everything shares.</summary>
    public static class G
    {
        public const int ViewW = 320, ViewH = 180;

        public static GraphicsDevice Device;
        public static Texture2D Pixel;
        public static Texture2D Glow;       // 64x64 soft radial falloff (premultiplied)
        public static Texture2D GlowSmall;  // 16x16
        public static Texture2D Disc;       // 16x16 hard disc
        public static Texture2D Ring;       // 32x32 ring
        public static Texture2D Soft;       // 64x64 smooth falloff used for lights
        public static Texture2D Vignette;   // 320x180
        public static PixelFont Font;

        public static void Init(GraphicsDevice gd)
        {
            Device = gd;
            Pixel = new Texture2D(gd, 1, 1);
            Pixel.SetData(new[] { Color.White });
            Glow = MakeRadial(64, r => { float t = 1 - r; return t * t * t; });
            GlowSmall = MakeRadial(16, r => { float t = 1 - r; return t * t; });
            Disc = MakeRadial(16, r => r < 0.92f ? 1f : 0f);
            Soft = MakeRadial(64, r => { float t = 1 - r * r; return t * t; });
            Ring = MakeRadial(32, r => r > 0.82f && r < 0.97f ? 1f : 0f);
            Vignette = MakeVignette();
            Font = new PixelFont(gd);
        }

        public static Texture2D Make(int w, int h, Color[] data)
        {
            var t = new Texture2D(Device, w, h);
            t.SetData(data);
            return t;
        }

        private static Texture2D MakeRadial(int size, Func<float, float> f)
        {
            var data = new Color[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / (size / 2f), dy = (y - c) / (size / 2f);
                    float r = MathF.Sqrt(dx * dx + dy * dy);
                    float a = r >= 1 ? 0 : Calc.Clamp01(f(r));
                    data[y * size + x] = Color.White * a;
                }
            return Make(size, size, data);
        }

        private static Texture2D MakeVignette()
        {
            var data = new Color[ViewW * ViewH];
            for (int y = 0; y < ViewH; y++)
                for (int x = 0; x < ViewW; x++)
                {
                    float dx = (x - ViewW / 2f) / (ViewW / 2f), dy = (y - ViewH / 2f) / (ViewH / 2f);
                    float d = MathF.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Calc.Clamp01((d - 0.62f) / 0.6f);
                    a = a * a * 0.55f;
                    data[y * ViewW + x] = Color.Black * a;
                }
            return Make(ViewW, ViewH, data);
        }

        // ---- Primitive drawing --------------------------------------------------------

        public static void Rect(SpriteBatch sb, float x, float y, float w, float h, Color c)
            => sb.Draw(Pixel, new Vector2(x, y), null, c, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);

        public static void Rect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

        public static void RectOutline(SpriteBatch sb, float x, float y, float w, float h, Color c)
        {
            Rect(sb, x, y, w, 1, c);
            Rect(sb, x, y + h - 1, w, 1, c);
            Rect(sb, x, y + 1, 1, h - 2, c);
            Rect(sb, x + w - 1, y + 1, 1, h - 2, c);
        }

        public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color c, float thickness = 1f)
        {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.01f) return;
            sb.Draw(Pixel, a, null, c, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        public static void GlowAt(SpriteBatch sb, Vector2 center, float radius, Color c)
        {
            float s = radius * 2f / Glow.Width;
            sb.Draw(Glow, center, null, c, 0f, new Vector2(Glow.Width / 2f), s, SpriteEffects.None, 0f);
        }

        public static void SoftAt(SpriteBatch sb, Vector2 center, float radius, Color c)
        {
            float s = radius * 2f / Soft.Width;
            sb.Draw(Soft, center, null, c, 0f, new Vector2(Soft.Width / 2f), s, SpriteEffects.None, 0f);
        }

        public static void DiscAt(SpriteBatch sb, Vector2 center, float radius, Color c)
        {
            float s = radius * 2f / Disc.Width;
            sb.Draw(Disc, center, null, c, 0f, new Vector2(Disc.Width / 2f), s, SpriteEffects.None, 0f);
        }

        public static void RingAt(SpriteBatch sb, Vector2 center, float radius, Color c)
        {
            float s = radius * 2f / Ring.Width;
            sb.Draw(Ring, center, null, c, 0f, new Vector2(Ring.Width / 2f), s, SpriteEffects.None, 0f);
        }

        public static Color Hex(uint rgb, float a = 1f)
            => new Color((int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF)) * a;

        public static Color Mul(Color c, float k)
            => new Color((int)Math.Clamp(c.R * k, 0, 255), (int)Math.Clamp(c.G * k, 0, 255), (int)Math.Clamp(c.B * k, 0, 255), c.A);

        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Calc.Clamp01(t));
    }
}
