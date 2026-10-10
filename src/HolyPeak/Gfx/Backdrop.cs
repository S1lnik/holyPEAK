using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;

namespace HolyPeak.Gfx
{
    /// <summary>
    /// Parallax scenery for a chapter: dithered sky, the Holy Peak itself on the horizon,
    /// three mountain ranges and a chapter-specific silhouette layer (forest, ruins, ice spires, cloud sea).
    /// Everything is generated procedurally once and cached.
    /// </summary>
    public sealed class Backdrop
    {
        private const int LayerW = 640;
        private readonly Style _st;
        private readonly Texture2D _sky, _peak, _far, _mid, _near, _sil, _clouds;
        private readonly Vector2[] _stars;
        private readonly float[] _starPhase;
        private readonly float _peakX;
        private readonly Texture2D _gradient;

        /// <param name="peakHeight">pixel height of the Holy Peak on the horizon (0 = hidden)</param>
        /// <param name="peakHaze">how much the peak dissolves into the sky (distance)</param>
        public Backdrop(Style st, int peakHeight, float peakX = 0.62f, float peakHaze = 0.3f, float night = 0f)
        {
            _st = st;
            _peakX = peakX;
            int seed = 1000 + st.Id * 97;
            _sky = MakeSky(st);
            _peak = peakHeight > 0 ? MakePeak(st, seed, peakHeight, peakHaze, night) : null;
            _far = MakeRange(st, seed + 1, 120, 70, 5, G.Mix(st.MountFar, st.Haze, 0.25f), true, 0.45f);
            _mid = MakeRange(st, seed + 2, 120, 52, 7, st.MountMid, st.Id != 3, 0.25f);
            _near = MakeRange(st, seed + 3, 110, 38, 9, st.MountNear, false, 0.1f);
            var gd = new Color[48];
            for (int i = 0; i < gd.Length; i++) { float t = i / (float)(gd.Length - 1); gd[i] = Color.White * (MathF.Pow(1 - t, 1.6f) * MathF.Min(1, t * 6)); }
            _gradient = G.Make(1, gd.Length, gd);
            _sil = st.Id switch
            {
                0 => MakeForest(st, seed + 4),
                1 => MakeRuins(st, seed + 4),
                2 => MakeSpires(st, seed + 4),
                3 => MakeCloudSea(st, seed + 4, 0.35f),
                _ => MakeForest(st, seed + 4),
            };
            if (st.Id == 3) _clouds = MakeCloudSea(st, seed + 9, 0.0f);
            var rng = new Rng(seed);
            _stars = new Vector2[st.Stars ? 110 : 0];
            _starPhase = new float[_stars.Length];
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i] = new Vector2(rng.Range(0, G.ViewW), rng.Range(0, G.ViewH * 0.62f));
                _starPhase[i] = rng.Range(0, 100f);
            }
        }

        /// <param name="cam">camera top-left in world pixels</param>
        /// <param name="room">room size in pixels</param>
        public void Draw(SpriteBatch sb, Vector2 cam, Point room, float time, float brightness = 1f)
        {
            // Vertical progress through tall rooms lifts the horizon a little.
            float vy = room.Y > G.ViewH ? (cam.Y / Math.Max(1, room.Y - G.ViewH)) : 1f; // 0 = top, 1 = bottom
            float lift = (1 - vy) * 28f;
            Color tint = Color.White * brightness;
            tint.A = 255;

            sb.Draw(_sky, Vector2.Zero, tint);

            // stars
            for (int i = 0; i < _stars.Length; i++)
            {
                float tw = 0.55f + 0.45f * MathF.Sin(time * (1.2f + (i % 5) * 0.4f) + _starPhase[i]);
                var p = _stars[i] + new Vector2(-cam.X * 0.01f, lift * 0.2f);
                p.X = ((p.X % G.ViewW) + G.ViewW) % G.ViewW;
                float a = tw * (i % 7 == 0 ? 1f : 0.6f) * brightness;
                G.Rect(sb, MathF.Floor(p.X), MathF.Floor(p.Y), 1, 1, Color.White * a);
                if (i % 13 == 0) G.GlowAt(sb, new Vector2(MathF.Floor(p.X) + 0.5f, MathF.Floor(p.Y) + 0.5f), 4, Color.White * (a * 0.25f));
            }

            // celestial body / glow per chapter
            DrawLight(sb, cam, lift, time, brightness);

            // the Holy Peak, always on the horizon, getting closer each chapter
            if (_peak != null)
            {
                var pos = new Vector2(MathF.Round(G.ViewW * _peakX - cam.X * 0.015f), MathF.Round(G.ViewH - 44 + lift * 0.3f));
                sb.Draw(_peak, pos, null, tint, 0f, new Vector2(_peak.Width / 2, _peak.Height), 1f, SpriteEffects.None, 0f);
                var summit = pos + new Vector2(0, -_peak.Height + 2);
                float pulse = 0.75f + 0.25f * MathF.Sin(time * 1.7f);
                G.GlowAt(sb, summit, 10 * pulse, _st.PeakGlow * (0.45f * brightness));
                G.Rect(sb, summit.X, summit.Y, 1, 1, _st.PeakGlow * brightness);
            }

            DrawLayer(sb, _far, cam.X * 0.05f, G.ViewH - _far.Height + 26 + lift * 0.45f, tint);
            DrawLayer(sb, _mid, cam.X * 0.11f, G.ViewH - _mid.Height + 40 + lift * 0.7f, tint);
            if (_st.Id == 3)
                DrawLayer(sb, _clouds, cam.X * 0.18f + time * 3f, G.ViewH - _clouds.Height + 52 + lift, tint * 0.8f);
            DrawLayer(sb, _near, cam.X * 0.2f, G.ViewH - _near.Height + 54 + lift * 0.9f, tint);
            DrawLayer(sb, _sil, cam.X * 0.34f + (_st.Id == 3 ? time * 6f : 0), G.ViewH - _sil.Height + 62 + lift * 1.15f, tint);

            // haze toward the horizon
            for (int i = 0; i < 6; i++)
            {
                float y = G.ViewH - 60 + i * 12 + lift;
                G.Rect(sb, 0, y, G.ViewW, 12, _st.Haze * (0.03f + i * 0.012f));
            }
        }

        private void DrawLight(SpriteBatch sb, Vector2 cam, float lift, float time, float b)
        {
            switch (_st.Id)
            {
                case 0: // dawn sun peeking over the horizon
                {
                    var c = new Vector2(70 - cam.X * 0.015f, G.ViewH - 52 + lift * 0.3f);
                    G.GlowAt(sb, c, 120, G.Hex(0xffd6a0) * (0.35f * b));
                    G.GlowAt(sb, c, 40, G.Hex(0xfff0c8) * (0.45f * b));
                    G.DiscAt(sb, c, 11, G.Hex(0xfff3d6) * b);
                    break;
                }
                case 1: // huge setting sun
                {
                    var c = new Vector2(250 - cam.X * 0.015f, G.ViewH - 48 + lift * 0.3f);
                    G.GlowAt(sb, c, 140, G.Hex(0xff9a5a) * (0.35f * b));
                    G.DiscAt(sb, c, 22, G.Hex(0xffc27a) * b);
                    for (int i = 0; i < 4; i++) G.Rect(sb, c.X - 24, c.Y - 6 + i * 5, 48, 1 + i, G.Hex(0xc7637a) * (0.55f * b));
                    break;
                }
                case 2: // cold moon
                case 4:
                {
                    var c = new Vector2(_st.Id == 4 ? 64 : 262 - cam.X * 0.01f, 34 + lift * 0.15f);
                    G.GlowAt(sb, c, 60, G.Hex(0xcfe6ff) * (0.25f * b));
                    G.DiscAt(sb, c, 9, G.Hex(0xf2f8ff) * b);
                    G.DiscAt(sb, c + new Vector2(3, -2), 8, _st.Sky[1] * b);
                    break;
                }
                default: // curtains of golden aurora above the summit
                {
                    Color gold = G.Hex(0xffd88a), violet = G.Hex(0xb48cff);
                    for (int x = 0; x < G.ViewW; x++)
                    {
                        float wx = x + cam.X * 0.03f;
                        float y = 14 + MathF.Sin(wx * 0.021f + time * 0.25f) * 10 + MathF.Sin(wx * 0.057f - time * 0.4f) * 5 + lift * 0.2f;
                        float h = 34 + MathF.Sin(wx * 0.043f + time * 0.5f) * 12;
                        float a = (0.10f + 0.07f * MathF.Sin(wx * 0.09f + time * 0.8f) + 0.04f * MathF.Sin(wx * 0.31f - time)) * b;
                        if (a <= 0) continue;
                        Color c = Color.Lerp(gold, violet, 0.5f + 0.5f * MathF.Sin(wx * 0.013f + time * 0.15f));
                        sb.Draw(_gradient, new Vector2(x, MathF.Round(y)), null, c * a, 0f, Vector2.Zero, new Vector2(1, h / _gradient.Height), SpriteEffects.None, 0f);
                    }
                    break;
                }
            }
        }

        private static void DrawLayer(SpriteBatch sb, Texture2D tex, float scroll, float y, Color tint)
        {
            float x = -(scroll % tex.Width);
            if (x > 0) x -= tex.Width;
            y = MathF.Round(y);
            for (; x < G.ViewW; x += tex.Width) sb.Draw(tex, new Vector2(MathF.Round(x), y), tint);
        }

        // ---- generation -------------------------------------------------------------------

        private static readonly int[,] Bayer =
        {
            { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 },
        };

        private static Texture2D MakeSky(Style st)
        {
            int w = G.ViewW, h = G.ViewH;
            var data = new Color[w * h];
            int n = st.Sky.Length - 1;
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1) * n;
                for (int x = 0; x < w; x++)
                {
                    float d = (Bayer[y & 3, x & 3] + 0.5f) / 16f - 0.5f;
                    float tt = Math.Clamp(t + d * 0.18f, 0, n - 0.0001f);
                    int i = (int)tt;
                    float f = tt - i;
                    // quantize the blend into steps for a crisp banded look
                    f = MathF.Round(f * 6f) / 6f;
                    data[y * w + x] = Color.Lerp(st.Sky[i], st.Sky[i + 1], f);
                }
            }
            return G.Make(w, h, data);
        }

        private static float PNoise(float x, int period, int seed)
        {
            int xi = (int)MathF.Floor(x);
            float f = x - xi;
            int a = ((xi % period) + period) % period, b = (((xi + 1) % period) + period) % period;
            float u = f * f * (3 - 2 * f);
            return Calc.Lerp(Calc.Hash(a, seed), Calc.Hash(b, seed), u);
        }

        private static float PFbm(float x, int period, int seed, int oct)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                s += PNoise(x, period, seed + i * 31) * amp;
                norm += amp; amp *= 0.5f; x *= 2; period *= 2;
            }
            return s / norm;
        }

        private static Texture2D MakeRange(Style st, int seed, int h, int amp, int period, Color col, bool snow, float haze)
        {
            int w = LayerW;
            var data = new Color[w * h];
            var height = new float[w];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * period;
                float r1 = 1 - MathF.Abs(PFbm(u, period, seed, 2) * 2 - 1);
                float r2 = 1 - MathF.Abs(PFbm(u * 2.3f + 1.7f, period * 2, seed + 5, 2) * 2 - 1);
                float n = PFbm(u * 5f, period * 5, seed + 9, 2);
                float v = r1 * r1 * 0.75f + r2 * 0.2f + n * 0.08f;
                height[x] = h - 6 - v * amp;
            }
            Color lit = G.Mix(col, G.Hex(0xffffff), 0.12f), shade = G.Mul(col, 0.82f);
            Color snowLit = G.Mix(G.Hex(0xf4f7ff), col, 0.18f), snowShade = G.Mix(G.Hex(0xc6d0ea), col, 0.35f);
            for (int x = 0; x < w; x++)
            {
                float hx = height[x];
                float slope = height[(x + 2) % w] - height[(x - 2 + w) % w];
                bool litSide = slope > 0;   // terrain rising to the left faces the light
                float snowLine = h - 6 - amp * (0.62f + PNoise(x / 9f, w / 9, seed + 3) * 0.12f);
                for (int y = Math.Max(0, (int)hx); y < h; y++)
                {
                    Color c = litSide ? lit : shade;
                    if (MathF.Abs(slope) < 0.6f) c = col;
                    if (snow && y < snowLine)
                    {
                        bool gully = Calc.Hash(x, y / 4, seed) > 0.86f && y > hx + 4;
                        c = gully ? (litSide ? lit : shade) : (litSide ? snowLit : snowShade);
                    }
                    float depthT = Calc.Clamp01((y - hx) / (float)(h - hx + 1));
                    c = G.Mix(c, st.Haze, haze * 0.6f + depthT * (0.25f + haze * 0.4f));
                    data[y * w + x] = c;
                }
            }
            return G.Make(w, h, data);
        }

        private static Texture2D MakePeak(Style st, int seed, int h, float haze, float night)
        {
            int w = (int)(h * 2.4f);
            var data = new Color[w * h];
            float cx = w / 2f;
            var height = new float[w];
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / (w / 2f);
                float env = MathF.Max(0, 1 - MathF.Pow(MathF.Abs(dx), 0.8f));
                float shoulder = MathF.Max(0, 0.55f - MathF.Abs(dx + 0.38f) * 1.6f) * 0.35f + MathF.Max(0, 0.5f - MathF.Abs(dx - 0.45f) * 1.8f) * 0.25f;
                float ridge = (1 - MathF.Abs(Calc.Fbm(x / 7f, 3.1f, 3, seed) * 2 - 1)) * 0.12f;
                float v = MathF.Max(env, shoulder) + ridge * env;
                height[x] = h - v * (h - 3);
            }
            height[(int)cx] = Math.Min(height[(int)cx], 2);
            Color lit = G.Mix(st.PeakFar, G.Hex(0xffffff), 0.1f), shade = st.PeakNear;
            Color snowLit = G.Mix(G.Hex(0xffffff), st.PeakFar, 0.12f), snowShade = G.Mix(G.Hex(0xd4dcf0), st.PeakNear, 0.4f);
            for (int x = 0; x < w; x++)
            {
                float hx = height[x];
                float slope = height[Math.Min(w - 1, x + 2)] - height[Math.Max(0, x - 2)];
                bool litSide = slope > 0;
                for (int y = Math.Max(0, (int)hx); y < h; y++)
                {
                    float rel = y / (float)h;
                    float snowLine = 0.42f + Calc.Fbm(x / 5f, 0.5f, 3, seed + 4) * 0.35f;
                    bool snow = rel < snowLine && !(Calc.Hash(x, y / 4, seed + 1) > 0.88f && rel > 0.15f);
                    if (!snow && rel < snowLine + 0.12f && Calc.Hash(x, y / 3, seed + 5) > 0.7f) snow = true;
                    Color c = snow ? (litSide ? snowLit : snowShade) : (litSide ? lit : shade);
                    // rock streaks running down the faces
                    if (!snow && Calc.Hash(x, y / 5, seed + 2) > 0.9f) c = G.Mul(c, 0.85f);
                    float fade = Calc.Clamp01((rel - 0.45f) / 0.55f);
                    c = G.Mix(c, st.Haze, haze + fade * 0.45f);
                    if (night > 0) c = G.Mix(c, st.Sky[1], night * (snow ? 0.55f : 1f));
                    data[y * w + x] = c;
                }
            }
            // the bell tower on the summit
            int bx = (int)cx;
            Color tower = G.Mul(st.PeakNear, 0.6f);
            for (int y = 0; y < 3; y++)
                for (int x = -1; x <= 1; x++)
                    if (!(y == 0 && x != 0)) data[y * w + bx + x] = tower;
            return G.Make(w, h, data);
        }

        private static Texture2D MakeForest(Style st, int seed)
        {
            int w = LayerW, h = 110;
            var data = new Color[w * h];
            var rng = new Rng(seed);
            float Ground(int x) => h - 26 - PFbm(x / (float)w * 6, 6, seed, 3) * 18;
            Color c = st.Silhouette, c2 = G.Mix(st.Silhouette, st.MountNear, 0.5f);
            for (int x = 0; x < w; x++)
                for (int y = (int)Ground(x); y < h; y++) data[y * w + x] = c;
            // two rows of pines, the back row lighter
            for (int pass = 0; pass < 2; pass++)
            {
                Color pc = pass == 0 ? c2 : c;
                for (int i = 0; i < 70; i++)
                {
                    int tx = rng.Range(0, w);
                    int th = rng.Range(14, 38) - pass * 4;
                    float gy = Ground(tx) + (pass == 0 ? -4 : 2);
                    for (int y = 0; y < th; y++)
                    {
                        float t = y / (float)th;
                        int half = (int)(t * th * 0.3f + 1);
                        int tier = (int)(t * 4);
                        half -= (int)((t * 4 - tier) * 2);
                        for (int dx = -half; dx <= half; dx++)
                        {
                            int xx = ((tx + dx) % w + w) % w;
                            int yy = (int)(gy - th + y);
                            if (yy >= 0 && yy < h) data[yy * w + xx] = pc;
                        }
                    }
                }
            }
            return G.Make(w, h, data);
        }

        private static Texture2D MakeRuins(Style st, int seed)
        {
            int w = LayerW, h = 120;
            var data = new Color[w * h];
            var rng = new Rng(seed);
            Color c = st.Silhouette, light = G.Mix(st.Silhouette, st.Haze, 0.12f);
            float Ground(int x) => h - 22 - PFbm(x / (float)w * 5, 5, seed, 3) * 14;
            void Fill(int x0, int y0, int x1, int y1, Color col)
            {
                for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
                    for (int x = x0; x < x1; x++) data[y * w + ((x % w) + w) % w] = col;
            }
            for (int x = 0; x < w; x++) Fill(x, (int)Ground(x), x + 1, h, c);
            for (int i = 0; i < 14; i++)
            {
                int x = i * (w / 14) + rng.Range(0, 20);
                int g = (int)Ground(x);
                int kind = rng.Range(0, 3);
                if (kind == 0)
                {
                    // column, sometimes broken
                    int ch = rng.Range(30, 70);
                    int top = g - ch;
                    Fill(x, top, x + 6, g, c);
                    Fill(x - 2, top - 3, x + 8, top, c);
                    Fill(x + 1, top, x + 2, g, light);
                    if (rng.Chance(0.5f)) Fill(x + 2, top - 3 - rng.Range(2, 8), x + 6, top - 3, c);
                }
                else if (kind == 1)
                {
                    // arch
                    int aw = rng.Range(26, 40), ah = rng.Range(36, 60);
                    int top = g - ah;
                    Fill(x, top, x + 5, g, c);
                    Fill(x + aw - 5, top, x + aw, g, c);
                    for (int a = 0; a <= aw; a++)
                    {
                        float t = a / (float)aw * MathF.PI;
                        int yy = top - (int)(MathF.Sin(t) * aw * 0.35f);
                        Fill(x + a, yy - 4, x + a + 1, yy + 2, c);
                    }
                    if (rng.Chance(0.5f)) Fill(x + aw / 2, top - 30, x + aw, top - 6, Color.Transparent);
                }
                else
                {
                    // fallen blocks
                    for (int b = 0; b < 4; b++)
                    {
                        int bw = rng.Range(6, 14), bh = rng.Range(4, 9);
                        int bx = x + rng.Range(-10, 20);
                        Fill(bx, g - bh, bx + bw, g, c);
                    }
                }
            }
            return G.Make(w, h, data);
        }

        private static Texture2D MakeSpires(Style st, int seed)
        {
            int w = LayerW, h = 130;
            var data = new Color[w * h];
            var rng = new Rng(seed);
            Color c = st.Silhouette, edge = G.Mix(st.Silhouette, st.Haze, 0.35f);
            float Ground(int x) => h - 20 - PFbm(x / (float)w * 7, 7, seed, 3) * 16;
            for (int x = 0; x < w; x++)
                for (int y = (int)Ground(x); y < h; y++) data[y * w + x] = c;
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Range(0, w);
                int sh = rng.Range(30, 95);
                int sw = rng.Range(5, 12);
                int g = (int)Ground(x) + 2;
                for (int y = 0; y < sh; y++)
                {
                    float t = y / (float)sh;
                    int half = (int)(sw * t * 0.6f) + 1;
                    int jag = (int)(Calc.Hash(x, y / 3, seed) * 2);
                    for (int dx = -half + jag; dx <= half; dx++)
                    {
                        int xx = ((x + dx) % w + w) % w, yy = g - sh + y;
                        if (yy < 0 || yy >= h) continue;
                        data[yy * w + xx] = dx == -half + jag ? edge : c;
                    }
                }
            }
            return G.Make(w, h, data);
        }

        private static Texture2D MakeCloudSea(Style st, int seed, float darken)
        {
            int w = LayerW, h = 70;
            var data = new Color[w * h];
            var rng = new Rng(seed);
            Color top = G.Mix(st.Silhouette, G.Hex(0xffffff), 0.3f), mid = st.Silhouette, low = G.Mix(st.Silhouette, st.MountNear, 0.35f + darken);
            var bumps = new (int x, int y, int r)[60];
            for (int i = 0; i < bumps.Length; i++) bumps[i] = (rng.Range(0, w), rng.Range(10, 34), rng.Range(8, 20));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float best = -1;
                    foreach (var b in bumps)
                    {
                        float dx = Math.Min(Math.Abs(x - b.x), w - Math.Abs(x - b.x));
                        float dy = y - b.y;
                        float d = b.r - MathF.Sqrt(dx * dx + dy * dy);
                        if (d > best) best = d;
                    }
                    if (y > 34) best = Math.Max(best, y - 34);
                    if (best < 0) continue;
                    Color c = best < 2 ? top : (y > 44 ? low : mid);
                    if (darken > 0) c = G.Mix(c, st.MountMid, darken);
                    data[y * w + x] = c;
                }
            return G.Make(w, h, data);
        }
    }
}
