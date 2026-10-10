using System;
using static HolyPeak.Audio.Synth;

namespace HolyPeak.Audio
{
    /// <summary>Sounds of the staff, the bell, the creatures and the guardians.</summary>
    public static partial class SfxGen
    {
        private static float Env(float t, float attack, float tau) => MathF.Min(1, t / attack) * Exp(t, tau);

        // ---- the pilgrim -------------------------------------------------------------------

        private static float[] Pogo()
        {
            var b = Mono(0.22f);
            float ph = 0;
            var n = new Noise(13);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 420 + 900 * (1 - MathF.Exp(-t * 30));
                ph += freq / Rate;
                b[i] = (Tri(ph) * 0.35f + Sine(ph * 2) * 0.15f) * Env(t, 0.002f, 0.06f) + f.Lp(n.Next(), 3000) * Exp(t, 0.01f) * 0.5f;
            }
            return b;
        }

        private static float[] Cling()
        {
            var b = Mono(0.16f);
            var n = new Noise(17);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float click = Sine(t * 1900) * Exp(t, 0.008f) * 0.35f;
                float scrape = f.Bp(n.Next(), 2200, 2.5f) * Env(t, 0.004f, 0.05f) * 0.6f;
                b[i] = click + scrape;
            }
            return b;
        }

        private static float[] Glide()
        {
            var b = Mono(0.3f);
            var n = new Noise(19);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float flap = 0.6f + 0.4f * MathF.Sin(t * 60);
                b[i] = f.Bp(n.Next(), 700 + 900 * t / 0.3f, 1.3f) * Env(t, 0.02f, 0.1f) * flap * 0.9f;
            }
            return b;
        }

        private static float[] Swing()
        {
            var b = Mono(0.16f);
            var n = new Noise(23);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 0.16f;
                float cut = 1200 + 4200 * MathF.Sin(MathF.PI * MathF.Min(1, u * 1.4f));
                b[i] = f.Bp(n.Next(), cut, 1.8f) * MathF.Sin(MathF.PI * u) * MathF.Sin(MathF.PI * u) * 0.9f;
            }
            return b;
        }

        private static float[] Hurt()
        {
            var b = Mono(0.45f);
            var n = new Noise(29);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 180 * MathF.Exp(-t * 10) + 45;
                ph += freq / Rate;
                float crunch = MathF.Round(n.Next() * 3) / 3f;
                b[i] = Sine(ph) * Exp(t, 0.12f) * 0.8f + f.Lp(crunch, 2200) * Exp(t, 0.05f) * 0.7f
                       + Pulse(t * 220, 220f / Rate, 0.5f) * Exp(t, 0.03f) * 0.15f;
            }
            return Down(MonoToStereoWithReverb(b, 0.25f, 0.6f, 0.5f));
        }

        private static float[] Hazard()
        {
            var b = Mono(0.3f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 900 * MathF.Exp(-t * 7) + 120;
                ph += freq / Rate;
                b[i] = Pulse(ph, freq / Rate, 0.25f) * Exp(t, 0.09f) * 0.25f;
            }
            return b;
        }

        private static float[] HealStart()
        {
            var b = Mono(0.9f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float swell = MathF.Min(1, t / 0.8f);
                b[i] = (Sine(t * 220 * (1 + swell * 0.5f)) * 0.5f + Sine(t * 330 * (1 + swell * 0.5f)) * 0.3f) * swell * 0.18f * MathF.Min(1, (0.9f - t) / 0.05f);
            }
            return b;
        }

        private static float[] Heal()
        {
            var b = Mono(1.4f);
            int[] notes = { 67, 71, 74, 79 };
            for (int k = 0; k < notes.Length; k++)
            {
                float freq = Midi(notes[k]), start = k * 0.03f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    b[i] += (Sine(t * freq) + 0.25f * Sine(t * freq * 2.01f)) * Env(t, 0.01f, 0.5f) * 0.13f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.55f, 0.85f, 0.3f));
        }

        // ---- the bell ---------------------------------------------------------------------

        /// <summary>The hand-bell: a bright inharmonic strike that rings for a moment.</summary>
        private static float[] HandBell(float vol)
        {
            var b = Mono(1.6f);
            float f0 = 880f;
            (float r, float a, float d)[] partials = { (1f, 1f, 0.7f), (2.01f, 0.5f, 0.5f), (2.76f, 0.45f, 0.35f), (4.07f, 0.25f, 0.2f), (5.43f, 0.15f, 0.12f), (0.5f, 0.3f, 0.9f) };
            var n = new Noise(37);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                foreach (var (r, a, d) in partials)
                    v += Sine(t * f0 * r) * a * Exp(t, d) * (1 + 0.06f * MathF.Sin(t * Tau * (3 + r)));
                float strike = f.Lp(n.Next(), 5000) * Exp(t, 0.006f) * 0.5f;
                b[i] = (v * 0.2f + strike) * MathF.Min(1, t / 0.001f) * vol;
            }
            return Down(MonoToStereoWithReverb(b, 0.45f, 0.82f, 0.3f));
        }

        private static float[] Resonate()
        {
            var b = Mono(1.2f);
            float[] fr = { 523f, 659f, 784f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                for (int k = 0; k < fr.Length; k++) v += Sine(t * fr[k] * (1 + 0.003f * k)) * (0.6f - k * 0.15f);
                b[i] = v * Env(t, 0.05f, 0.45f) * 0.16f * (1 + 0.2f * MathF.Sin(t * Tau * 7));
            }
            return Down(MonoToStereoWithReverb(b, 0.5f, 0.85f, 0.2f));
        }

        private static float[] Glass()
        {
            var b = Mono(1.2f);
            var n = new Noise(41);
            var hp = new OnePole();
            var rng = new Noise(43);
            for (int k = 0; k < 14; k++)
            {
                float start = MathF.Abs(rng.Next()) * 0.25f;
                float freq = 2200 + MathF.Abs(rng.Next()) * 4200;
                float amp = 0.12f + MathF.Abs(rng.Next()) * 0.1f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    b[i] += Sine(t * freq) * Exp(t, 0.08f + amp * 0.3f) * amp;
                }
            }
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += hp.Hp(n.Next(), 3500) * Exp(t, 0.06f) * 0.7f;
            }
            return Down(MonoToStereoWithReverb(b, 0.4f, 0.75f, 0.3f));
        }

        // ---- the world --------------------------------------------------------------------

        private static float[] StoneKnock()
        {
            var b = Mono(0.25f);
            var n = new Noise(47);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (140 + 120 * MathF.Exp(-t * 40)) / Rate;
                b[i] = Sine(ph) * Exp(t, 0.05f) * 0.7f + f.Lp(n.Next(), 1800) * Exp(t, 0.025f) * 0.9f;
            }
            return b;
        }

        private static float[] Secret()
        {
            var b = Mono(1.6f);
            int[] notes = { 69, 73, 76, 81, 85 };
            for (int k = 0; k < notes.Length; k++)
            {
                float freq = Midi(notes[k]), start = 0.12f + k * 0.09f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    b[i] += Sine(t * freq) * Env(t, 0.004f, 0.4f) * 0.15f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.6f, 0.85f, 0.3f));
        }

        private static float[] Coin()
        {
            var b = Mono(0.22f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = t < 0.04f ? 1760 : 2637;
                float fm = Sine(t * freq * 2.5f) * 0.6f * Exp(t, 0.03f);
                b[i] = Sine(t * freq + fm / Tau) * Env(t, 0.001f, 0.07f) * 0.3f;
            }
            return b;
        }

        private static float[] CacheBreak()
        {
            var a = Crystal(1f);
            var c = CrumbleBreak();
            var b = new float[Math.Max(a.Length, c.Length)];
            for (int i = 0; i < b.Length; i++) b[i] = (i < a.Length ? a[i] : 0) + (i < c.Length ? c[i] * 0.7f : 0);
            return b;
        }

        private static float[] Lever()
        {
            var b = Mono(0.35f);
            var n = new Noise(53);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float clunk = Sine(t * 110) * Exp(t, 0.06f) * 0.6f + f.Lp(n.Next(), 1200) * Exp(t, 0.02f) * 0.7f;
                float t2 = t - 0.14f;
                float clank = t2 > 0 ? (Sine(t2 * 740) * 0.5f + Sine(t2 * 1210) * 0.3f) * Exp(t2, 0.05f) * 0.4f : 0;
                b[i] = clunk + clank;
            }
            return b;
        }

        private static float[] Gate()
        {
            var b = Mono(1.3f);
            var n = new Noise(59);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float grind = f.Bp(n.Next(), 160 + 60 * MathF.Sin(t * 37), 3f) * (0.6f + 0.4f * MathF.Sin(t * 90)) * MathF.Min(1, t / 0.1f) * MathF.Min(1, (1.3f - t) / 0.3f);
                float boom = t > 1.0f ? Sine((t - 1f) * 60) * Exp(t - 1f, 0.12f) * 0.7f : 0;
                b[i] = grind * 1.3f + boom;
            }
            return Down(MonoToStereoWithReverb(b, 0.4f, 0.8f, 0.5f));
        }

        private static float[] Rest()
        {
            var b = Mono(2.2f);
            int[] chord = { 55, 62, 67, 71, 74 };
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = Midi(chord[k]), start = k * 0.08f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float fm = Sine(t * freq * 2f) * 0.4f * Exp(t, 0.3f);
                    b[i] += Sine(t * freq + fm / Tau) * Env(t, 0.01f, 0.9f) * 0.1f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.65f, 0.88f, 0.3f));
        }

        private static float[] Buy()
        {
            var b = Mono(0.6f);
            for (int k = 0; k < 4; k++)
            {
                float start = k * 0.045f, freq = 1500 + k * 300;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    b[i] += Sine(t * freq) * Env(t, 0.001f, 0.05f) * 0.2f;
                }
            }
            var chime = Crystal(0.5f);
            for (int i = 0; i < Math.Min(b.Length, chime.Length); i++) b[i] += chime[i] * 0.6f;
            return b;
        }

        private static float[] Deny()
        {
            var b = Mono(0.22f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 110f / Rate;
                b[i] = Pulse(ph, 110f / Rate, 0.4f) * Exp(t, 0.08f) * 0.2f * (t < 0.08f || t > 0.11f ? 1 : 0);
            }
            return b;
        }

        private static float[] Paper()
        {
            var b = Mono(0.3f);
            var n = new Noise(61);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float crinkle = MathF.Abs(MathF.Sin(t * 140)) > 0.7f ? 1 : 0.3f;
                b[i] = f.Hp(n.Next(), 2500, 0.8f) * Env(t, 0.01f, 0.08f) * crinkle * 0.35f;
            }
            return b;
        }

        // ---- creatures --------------------------------------------------------------------

        private static float[] Hit(float weight)
        {
            var b = Mono(0.25f);
            var n = new Noise(67);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = (260 / weight) * MathF.Exp(-t * 25) + 60;
                ph += freq / Rate;
                float body = Sine(ph) * Exp(t, 0.05f * weight) * 0.8f;
                float snap = f.Lp(n.Next(), 4500) * Exp(t, 0.012f) * 1.0f;
                b[i] = body + snap;
            }
            return b;
        }

        private static float[] Kill()
        {
            var h = Hit(1.2f);
            var b = Mono(0.6f);
            var n = new Noise(71);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = (i < h.Length ? h[i] : 0) + f.Bp(n.Next(), 900 * MathF.Exp(-t * 4) + 200, 1.2f) * Exp(t, 0.15f) * 0.6f
                       + Sine(t * 1320) * Exp(t - 0.05f, 0.12f) * (t > 0.05f ? 0.12f : 0);
            }
            return Down(MonoToStereoWithReverb(b, 0.25f, 0.6f, 0.4f));
        }

        private static float[] Clank()
        {
            var b = Mono(0.5f);
            float[] ratios = { 1f, 2.32f, 3.17f, 4.73f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                for (int k = 0; k < ratios.Length; k++) v += Sine(t * 1250 * ratios[k]) * Exp(t, 0.12f / (1 + k)) / (1 + k);
                b[i] = v * MathF.Min(1, t / 0.0005f) * 0.35f;
            }
            return b;
        }

        private static float[] Pop()
        {
            var b = Mono(0.1f);
            var n = new Noise(73);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = f.Lp(n.Next(), 3000) * Exp(t, 0.015f) * 0.7f + Sine(t * (600 - t * 3000)) * Exp(t, 0.02f) * 0.3f;
            }
            return b;
        }

        private static float[] Spit()
        {
            var b = Mono(0.25f);
            var n = new Noise(79);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = f.Bp(n.Next(), 600 + 1500 * t / 0.25f, 4f) * Env(t, 0.01f, 0.07f) * 1.4f;
            }
            return b;
        }

        private static float[] Fire()
        {
            var b = Mono(0.4f);
            var n = new Noise(83);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = f.Lp(n.Next(), 2400 * MathF.Exp(-t * 5) + 300, 1.2f) * Env(t, 0.02f, 0.12f) * 1.1f;
            }
            return b;
        }

        private static float[] Tell()
        {
            var b = Mono(0.35f);
            float ph = 0;
            var n = new Noise(89);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 95 + 25 * MathF.Sin(t * 40);
                ph += freq / Rate;
                b[i] = (Saw(ph, freq / Rate) * 0.3f + f.Lp(n.Next(), 600) * 0.4f) * Env(t, 0.04f, 0.12f) * 0.8f;
            }
            return b;
        }

        private static float[] Notice()
        {
            var b = Mono(0.4f);
            var n = new Noise(97);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = f.Bp(n.Next(), 1800 + 800 * MathF.Sin(t * 20), 6f) * Env(t, 0.03f, 0.12f) * 1.2f + Sine(t * 620) * Exp(t, 0.06f) * 0.2f;
            }
            return b;
        }

        private static float[] Hop()
        {
            var b = Mono(0.2f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 140 + 260 * t / 0.2f;
                ph += freq / Rate;
                b[i] = Sine(ph) * Env(t, 0.005f, 0.07f) * 0.5f;
            }
            return b;
        }

        // ---- guardians --------------------------------------------------------------------

        private static float[] Roar()
        {
            var b = Mono(1.4f);
            var n = new Noise(101);
            var f1 = new Svf();
            var f2 = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Min(1, t / 0.15f) * MathF.Min(1, (1.4f - t) / 0.5f);
                float freq = 70 + 20 * MathF.Sin(t * 9) + 15 * MathF.Sin(t * 31);
                ph += freq / Rate;
                float src = Saw(ph, freq / Rate) * 0.7f + n.Next() * 0.5f;
                float formant = f1.Bp(src, 500 + 200 * MathF.Sin(t * 3), 3f) + f2.Bp(src, 1100, 4f) * 0.5f;
                b[i] = formant * env * 1.2f;
            }
            return Down(MonoToStereoWithReverb(b, 0.4f, 0.8f, 0.5f));
        }

        private static float[] BossDie()
        {
            var b = Mono(3f);
            var n = new Noise(103);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (50 + 80 * MathF.Exp(-t * 3)) / Rate;
                float boom = Sine(ph) * Exp(t, 0.6f) * 0.8f;
                float rumble = f.Lp(n.Next(), 800 * MathF.Exp(-t) + 100) * Exp(t, 0.9f) * 0.9f;
                float shimmer = Sine(t * 1046) * Exp(t - 0.3f, 0.8f) * (t > 0.3f ? 0.06f : 0);
                b[i] = boom + rumble + shimmer;
            }
            return Down(MonoToStereoWithReverb(b, 0.5f, 0.85f, 0.4f));
        }

        private static float[] Crash(float size)
        {
            var b = Mono(0.8f);
            var n = new Noise(107);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (45 + 70 * MathF.Exp(-t * 12)) / Rate;
                b[i] = Sine(ph) * Exp(t, 0.18f * size) * 0.9f + f.Lp(n.Next(), 2600 * MathF.Exp(-t * 6) + 200) * Exp(t, 0.1f * size) * 1.1f;
            }
            return Down(MonoToStereoWithReverb(b, 0.35f, 0.75f, 0.5f));
        }

        private static float[] CrackSnd()
        {
            var b = Mono(0.3f);
            var n = new Noise(109);
            var f = new Svf();
            var rng = new Noise(113);
            float gate = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                if (i % 500 == 0) gate = rng.Next() > 0 ? 1 : 0.2f;
                gate *= 0.998f;
                b[i] = f.Bp(n.Next(), 1500, 1.5f) * gate * Exp(t, 0.12f) * 0.8f;
            }
            return b;
        }

        private static float[] Quake()
        {
            var b = Mono(1.6f);
            var n = new Noise(127);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Min(1, t / 0.3f) * MathF.Min(1, (1.6f - t) / 0.6f);
                b[i] = f.Lp(n.Next(), 220) * env * 2.2f + Sine(t * 42) * env * 0.3f;
            }
            return b;
        }

        private static float[] Vanish(bool appear)
        {
            var b = Mono(0.45f);
            var n = new Noise(131);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = appear ? t / 0.45f : 1 - t / 0.45f;
                b[i] = f.Bp(n.Next(), 500 + 3500 * u, 3f) * MathF.Sin(MathF.PI * t / 0.45f) * 0.7f + Sine(t * (400 + 800 * u)) * MathF.Sin(MathF.PI * t / 0.45f) * 0.1f;
            }
            return Down(MonoToStereoWithReverb(b, 0.5f, 0.8f, 0.3f));
        }

        private static float[] Breath()
        {
            var b = Mono(1.1f);
            var n = new Noise(137);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Min(1, t / 0.05f) * MathF.Min(1, (1.1f - t) / 0.3f);
                b[i] = f.Hp(n.Next(), 2500 + 800 * MathF.Sin(t * 13), 0.9f) * env * 0.5f + f.Bp(n.Next(), 700, 2f) * env * 0.3f;
            }
            return b;
        }

        private static float[] ShieldBreak()
        {
            var g = Glass();
            var bell = HandBell(0.8f);
            var b = new float[Math.Max(g.Length, bell.Length)];
            for (int i = 0; i < b.Length; i++) b[i] = (i < g.Length ? g[i] : 0) + (i < bell.Length ? bell[i] * 0.8f : 0);
            return b;
        }

        private static float[] ShieldUp()
        {
            var b = Mono(0.8f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 0.8f;
                b[i] = (Sine(t * (300 + 500 * u)) * 0.4f + Sine(t * (450 + 750 * u)) * 0.25f) * MathF.Sin(MathF.PI * u) * 0.3f;
            }
            return Down(MonoToStereoWithReverb(b, 0.4f, 0.8f, 0.3f));
        }
    }
}
