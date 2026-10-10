using System;
using Microsoft.Xna.Framework;

namespace HolyPeak.Core
{
    /// <summary>Small math helpers shared by the simulation, rendering and audio code.</summary>
    public static class Calc
    {
        public const float Tau = MathF.PI * 2f;

        public static float Approach(float value, float target, float maxDelta)
        {
            return value > target ? Math.Max(value - maxDelta, target) : Math.Min(value + maxDelta, target);
        }

        public static Vector2 Approach(Vector2 value, Vector2 target, float maxDelta)
        {
            Vector2 d = target - value;
            float len = d.Length();
            if (len <= maxDelta || len < 1e-5f) return target;
            return value + d / len * maxDelta;
        }

        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static Color LerpColor(Color a, Color b, float t) => Color.Lerp(a, b, Clamp01(t));

        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float Damp(float sharpness, float dt) => 1f - MathF.Exp(-sharpness * dt);

        public static float EaseOutCubic(float t) { t = Clamp01(t); float u = 1 - t; return 1 - u * u * u; }
        public static float EaseInCubic(float t) { t = Clamp01(t); return t * t * t; }
        public static float EaseInOutCubic(float t)
        {
            t = Clamp01(t);
            return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
        }
        public static float EaseOutBack(float t)
        {
            t = Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
        }
        public static float EaseOutElastic(float t)
        {
            t = Clamp01(t);
            if (t == 0 || t == 1) return t;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * (Tau / 3f)) + 1;
        }
        public static float SmoothStep(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }

        /// <summary>Deterministic integer hash → [0,1).</summary>
        public static float Hash(int x, int y, int seed = 0)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        public static float Hash(int x, int seed = 0) => Hash(x, 0x51ED27, seed);

        /// <summary>Smooth value noise in [0,1).</summary>
        public static float ValueNoise(float x, float y, int seed = 0)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float y, int octaves, int seed = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        public static string FormatTime(double seconds, bool withMillis = true)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(seconds);
            string core = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes}:{ts.Seconds:00}";
            return withMillis ? $"{core}.{ts.Milliseconds:000}" : core;
        }

        public static string Roman(int n) => n switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => n.ToString()
        };
    }

    /// <summary>Tiny deterministic xorshift RNG (System.Random is fine too, but this one is cheap to seed per-effect).</summary>
    public sealed class Rng
    {
        private uint _s;
        public Rng(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; }
        public uint NextUInt() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public float NextFloat() => (NextUInt() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * NextFloat();
        public int Range(int a, int bExclusive) => a + (int)(NextUInt() % (uint)Math.Max(1, bExclusive - a));
        public bool Chance(float p) => NextFloat() < p;
        public float Sign() => (NextUInt() & 1) == 0 ? -1f : 1f;
        public Vector2 InCircle(float r)
        {
            float a = NextFloat() * Calc.Tau, d = MathF.Sqrt(NextFloat()) * r;
            return new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d);
        }
    }
}
