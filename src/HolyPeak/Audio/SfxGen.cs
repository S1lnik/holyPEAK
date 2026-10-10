using System;
using System.Collections.Generic;
using static HolyPeak.Audio.Synth;

namespace HolyPeak.Audio
{
    /// <summary>Procedurally synthesized sound effects. Each returns PCM samples (mono unless noted).</summary>
    public static partial class SfxGen
    {
        public sealed class Clip
        {
            public float[] Data;
            public int Channels = 1;
        }

        public static Dictionary<string, Clip> All()
        {
            var d = new Dictionary<string, Clip>
            {
                // the pilgrim
                ["jump"] = M(Jump(0)),
                ["leap"] = M(Jump(1)),
                ["pogo"] = M(Pogo()),
                ["land"] = M(Land()),
                ["step"] = M(Step()),
                ["cling"] = M(Cling()),
                ["glide"] = M(Glide()),
                ["swing"] = M(Swing()),
                ["hurt"] = M(Hurt()),
                ["hazard"] = M(Hazard()),
                ["death"] = M(Death()),
                ["respawn"] = M(Respawn()),
                ["healstart"] = M(HealStart()),
                ["heal"] = M(Heal()),
                // the bell
                ["toll"] = M(HandBell(1f)),
                ["resonate"] = M(Resonate()),
                ["glass"] = M(Glass()),
                // the world
                ["wallhit"] = M(StoneKnock()),
                ["wallbreak"] = M(CrumbleBreak()),
                ["secret"] = M(Secret()),
                ["coin"] = M(Coin()),
                ["cachehit"] = M(Crystal(0.6f)),
                ["cachebreak"] = M(CacheBreak()),
                ["lever"] = M(Lever()),
                ["gate"] = M(Gate()),
                ["item"] = M(EmberGet()),
                ["ability"] = S(Gift()),
                ["reclaim"] = M(EmberSecure()),
                ["rest"] = M(Rest()),
                ["shop"] = M(Crystal(0.4f)),
                ["buy"] = M(Buy()),
                ["deny"] = M(Deny()),
                ["talk"] = M(Blip(660, 0.04f, 0.2f)),
                ["blip"] = M(Blip(990, 0.015f, 0.18f)),
                ["map"] = M(Paper()),
                // creatures
                ["hit"] = M(Hit(1f)),
                ["kill"] = M(Kill()),
                ["clank"] = M(Clank()),
                ["pop"] = M(Pop()),
                ["spit"] = M(Spit()),
                ["fire"] = M(Fire()),
                ["tell"] = M(Tell()),
                ["notice"] = M(Notice()),
                ["hop"] = M(Hop()),
                ["thud"] = M(Land()),
                ["lunge"] = M(Dash()),
                // guardians
                ["roar"] = M(Roar()),
                ["bosshit"] = M(Hit(1.5f)),
                ["bossdie"] = M(BossDie()),
                ["crash"] = M(Crash(1f)),
                ["slam"] = M(Crash(0.8f)),
                ["crack"] = M(CrackSnd()),
                ["quake"] = M(Quake()),
                ["vanish"] = M(Vanish(false)),
                ["appear"] = M(Vanish(true)),
                ["breath"] = M(Breath()),
                ["shieldbreak"] = M(ShieldBreak()),
                ["shieldup"] = M(ShieldUp()),
                ["bell"] = S(Bell()),
                // interface and air
                ["move"] = M(Blip(880, 0.03f, 0.25f)),
                ["select"] = M(Select(true)),
                ["back"] = M(Select(false)),
                ["pause"] = M(PauseSnd()),
                ["whoosh"] = M(Whoosh()),
                ["wind"] = new Clip { Data = WindLoop(), Channels = 1 },
                ["bird"] = M(Bird()),
                ["drip"] = M(Drip()),
                ["chime"] = M(Chime()),
            };
            return d;
        }

        private static Clip M(float[] d) { Limit(d); FadeEdges(d, 1); return new Clip { Data = d, Channels = 1 }; }
        private static Clip S(float[] d) { Limit(d); FadeEdges(d, 2); return new Clip { Data = d, Channels = 2 }; }

        /// <summary>Soft-limits hot effects so nothing clips, keeping quiet ones untouched.</summary>
        private static void Limit(float[] d)
        {
            float peak = 0;
            foreach (var v in d) peak = MathF.Max(peak, MathF.Abs(v));
            if (peak <= 0.85f) return;
            float drive = 1.2f / peak;
            float norm = 0.9f / MathF.Tanh(peak * drive);
            for (int i = 0; i < d.Length; i++) d[i] = MathF.Tanh(d[i] * drive) * norm;
        }

        private static float[] Jump(int kind)
        {
            float len = kind == 2 ? 0.22f : 0.14f;
            var b = Mono(len);
            var n = new Noise(11);
            var f = new OnePole();
            float ph = 0;
            float f0 = kind == 1 ? 340 : (kind == 2 ? 220 : 270), f1 = kind == 1 ? 720 : (kind == 2 ? 820 : 560);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float k = MathF.Min(1, t / 0.07f);
                float freq = f0 + (f1 - f0) * (1 - (1 - k) * (1 - k));
                ph += freq / Rate;
                float tone = Pulse(ph, freq / Rate, 0.3f) * 0.22f + Tri(ph * 0.5f) * 0.25f;
                float env = Exp(t, kind == 2 ? 0.07f : 0.045f);
                float puff = f.Lp(n.Next(), 2500) * Exp(t, 0.018f) * 0.6f;
                b[i] = (tone * env + puff) * 0.7f;
            }
            return b;
        }

        private static float[] Land()
        {
            var b = Mono(0.12f);
            var n = new Noise(3);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 130 * MathF.Exp(-t * 18) + 50;
                ph += freq / Rate;
                b[i] = Sine(ph) * Exp(t, 0.035f) * 0.7f + f.Lp(n.Next(), 900) * Exp(t, 0.02f) * 1.1f;
            }
            return b;
        }

        private static float[] Dash()
        {
            var b = Mono(0.32f);
            var n = new Noise(7);
            var svf = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float cut = 3800 * MathF.Exp(-t * 7) + 500;
                float whoosh = svf.Bp(n.Next(), cut, 1.6f) * MathF.Min(1, t / 0.01f) * Exp(t, 0.09f) * 1.6f;
                float freq = 900 * MathF.Exp(-t * 9) + 140;
                ph += freq / Rate;
                float zip = Saw(ph, freq / Rate) * Exp(t, 0.05f) * 0.18f;
                b[i] = whoosh + zip;
            }
            return b;
        }


        private static float[] Death()
        {
            var b = Mono(0.7f);
            var n = new Noise(99);
            var f = new OnePole();
            float ph = 0, ph2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 520 * MathF.Exp(-t * 6) + 60;
                ph += freq / Rate;
                ph2 += 55 / (float)Rate;
                float crush = MathF.Round(Pulse(ph, freq / Rate, 0.5f) * 4) / 4f;
                float tone = crush * Exp(t, 0.12f) * 0.3f;
                float boom = Sine(ph2 * (1 + 2 * MathF.Exp(-t * 20))) * Exp(t, 0.18f) * 0.6f;
                float noise = f.Lp(n.Next(), 3000 * MathF.Exp(-t * 4) + 300) * Exp(t, 0.09f) * 0.9f;
                b[i] = tone + boom + noise;
            }
            var st = MonoToStereoWithReverb(b, 0.35f, 0.7f, 0.5f);
            return Down(st);
        }

        private static float[] Respawn()
        {
            var b = Mono(0.9f);
            int[] notes = { 72, 76, 79, 84, 88 };
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.045f, freq = Midi(notes[k]);
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    b[i] += (Sine(t * freq) * 0.6f + Tri(t * freq * 2) * 0.12f) * Exp(t, 0.18f) * MathF.Min(1, t / 0.004f) * 0.22f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.5f, 0.8f, 0.3f));
        }


        private static float[] Crystal(float vol)
        {
            var b = Mono(0.8f);
            float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
            float baseF = 1568;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                for (int p = 0; p < partials.Length; p++)
                    v += Sine(t * baseF * partials[p]) * Exp(t, 0.25f / (1 + p * 1.5f)) / (1 + p);
                float shimmer = Sine(t * baseF * 1.5f) * Exp(t - 0.05f, 0.12f) * (t > 0.05f ? 0.3f : 0);
                b[i] = (v + shimmer) * MathF.Min(1, t / 0.002f) * 0.3f * vol;
            }
            return Down(MonoToStereoWithReverb(b, 0.45f, 0.75f, 0.2f));
        }

        private static float[] EmberGet()
        {
            var b = Mono(1.4f);
            int[] notes = { 76, 80, 83, 88, 92 };
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.07f, freq = Midi(notes[k]);
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float fm = Sine(t * freq * 3.5f) * 1.2f * Exp(t, 0.1f);
                    b[i] += Sine(t * freq + fm / Tau) * Exp(t, 0.35f) * MathF.Min(1, t / 0.003f) * 0.2f;
                }
            }
            var n = new Noise(5);
            var hp = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += hp.Hp(n.Next(), 6000) * Exp(t, 0.3f) * 0.05f * (0.5f + 0.5f * Sine(t * 23));
            }
            return Down(MonoToStereoWithReverb(b, 0.55f, 0.82f, 0.25f));
        }

        private static float[] EmberSecure()
        {
            var b = Mono(1.6f);
            int[] chord = { 64, 68, 71, 76 };
            foreach (int note in chord)
            {
                float freq = Midi(note);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[i] += (Sine(t * freq) + 0.3f * Sine(t * freq * 2)) * Adsr(t, 0.6f, 0.02f, 0.3f, 0.5f, 0.9f) * 0.09f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.6f, 0.85f, 0.3f));
        }


        private static float[] CrumbleBreak()
        {
            var b = Mono(0.5f);
            var n = new Noise(31);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (90 + 60 * MathF.Exp(-t * 30)) / Rate;
                b[i] = f.Lp(n.Next(), 2400 * MathF.Exp(-t * 5) + 200) * Exp(t, 0.1f) * 1.1f + Sine(ph) * Exp(t, 0.08f) * 0.5f;
            }
            return b;
        }



        private static float[] Step()
        {
            var b = Mono(0.05f);
            var n = new Noise(41);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = f.Bp(n.Next(), 1200, 1.2f) * Exp(t, 0.012f) * 0.8f;
            }
            return b;
        }


        private static float[] Blip(float freq, float len, float vol)
        {
            var b = Mono(len + 0.02f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += freq / Rate;
                b[i] = Pulse(ph, freq / Rate, 0.25f) * Exp(t, len * 0.5f) * vol;
            }
            return b;
        }

        private static float[] Select(bool up)
        {
            var b = Mono(0.2f);
            float a = up ? 659 : 784, c = up ? 988 : 523;
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = t < 0.06f ? a : c;
                ph += freq / Rate;
                float env = t < 0.06f ? Exp(t, 0.04f) : Exp(t - 0.06f, 0.05f);
                b[i] = (Pulse(ph, freq / Rate, 0.25f) * 0.18f + Tri(ph) * 0.2f) * env;
            }
            return b;
        }

        private static float[] PauseSnd()
        {
            var b = Mono(0.25f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = (Sine(t * 392) + 0.4f * Sine(t * 587)) * Exp(t, 0.08f) * 0.25f;
            }
            return b;
        }

        private static float[] Whoosh()
        {
            var b = Mono(0.45f);
            var n = new Noise(61);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Sin(MathF.PI * t / 0.45f);
                b[i] = f.Bp(n.Next(), 400 + 2200 * t / 0.45f, 1.2f) * env * env * 0.4f;
            }
            return b;
        }

        private static float[] Gift()
        {
            var b = Mono(4.5f);
            int[] chord = { 48, 55, 60, 64, 67, 71, 74 };
            var svf = new Svf[chord.Length];
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = Midi(chord[k]);
                float ph1 = 0, ph2 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    ph1 += freq * 1.003f / Rate; ph2 += freq * 0.997f / Rate;
                    float raw = (Saw(ph1, freq / Rate) + Saw(ph2, freq / Rate)) * 0.5f;
                    float cut = 300 + 3000 * Calc01(t / 1.6f);
                    float v = svf[k].Lp(raw, cut, 0.9f);
                    b[i] += v * Adsr(t, 2.2f, 1.2f, 0.5f, 0.8f, 2.0f) * 0.07f;
                }
            }
            // bright bell arpeggio over the swell
            int[] arp = { 79, 83, 86, 91 };
            for (int k = 0; k < arp.Length; k++)
            {
                float start = 0.9f + k * 0.16f, freq = Midi(arp[k]);
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float fm = Sine(t * freq * 3.5f) * Exp(t, 0.2f);
                    b[i] += Sine(t * freq + fm / Tau) * Exp(t, 0.8f) * 0.08f;
                }
            }
            return MonoToStereoWithReverb(b, 0.7f, 0.88f, 0.3f);
        }

        /// <summary>The summit bell: inharmonic partials with slow beating and a very long decay.</summary>
        private static float[] Bell()
        {
            var b = Mono(9f);
            float f0 = 146.8f; // D3
            (float ratio, float amp, float decay)[] partials =
            {
                (0.5f, 0.55f, 4.5f), (1.0f, 1.0f, 3.6f), (1.19f, 0.6f, 2.8f), (1.5f, 0.35f, 2.2f),
                (2.0f, 0.45f, 2.0f), (2.52f, 0.3f, 1.4f), (2.98f, 0.22f, 1.1f), (4.07f, 0.18f, 0.8f), (5.43f, 0.12f, 0.5f),
            };
            var n = new Noise(9);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                foreach (var (ratio, amp, decay) in partials)
                {
                    float beat = 1f + 0.08f * MathF.Sin(t * Tau * (0.7f + ratio * 0.3f));
                    v += Sine(t * f0 * ratio) * amp * Exp(t, decay) * beat;
                }
                float strike = f.Lp(n.Next(), 2500) * Exp(t, 0.012f) * 0.6f;
                b[i] = (v * 0.22f + strike) * MathF.Min(1, t / 0.002f);
            }
            return MonoToStereoWithReverb(b, 0.55f, 0.9f, 0.35f);
        }


        private static float[] WindLoop()
        {
            const float len = 4f;
            var b = Mono(len);
            var n = new Noise(81);
            var svf = new Svf();
            var svf2 = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float mod = 0.5f + 0.5f * MathF.Sin(t / len * Tau * 2) * MathF.Sin(t / len * Tau * 3 + 1);
                float x = n.Next();
                b[i] = svf.Bp(x, 500 + 700 * mod, 2.5f) * (0.4f + 0.6f * mod) + svf2.Bp(x, 1800 + 900 * mod, 4f) * 0.15f;
            }
            // make it loop seamlessly by crossfading the tail into the head
            int xf = Rate / 2;
            for (int i = 0; i < xf; i++)
            {
                float g = i / (float)xf;
                b[i] = b[i] * g + b[b.Length - xf + i] * (1 - g);
            }
            Array.Resize(ref b, b.Length - xf);
            Normalize(b, 0.5f);
            return b;
        }

        /// <summary>A distant two-note bird call.</summary>
        private static float[] Bird()
        {
            var b = Mono(0.9f);
            float[] starts = { 0f, 0.11f, 0.42f };
            float[] f0 = { 2600f, 3100f, 2900f }, f1 = { 3400f, 3900f, 2300f };
            for (int k = 0; k < starts.Length; k++)
            {
                float ph = 0;
                for (int i = (int)(starts[k] * Rate); i < Math.Min(b.Length, (int)((starts[k] + 0.08f) * Rate)); i++)
                {
                    float t = i / (float)Rate - starts[k];
                    float u = t / 0.08f;
                    float freq = f0[k] + (f1[k] - f0[k]) * u + MathF.Sin(t * 180f) * 120f;
                    ph += freq / Rate;
                    b[i] += Sine(ph) * MathF.Sin(MathF.PI * u) * 0.35f;
                }
            }
            return Down(MonoToStereoWithReverb(b, 0.5f, 0.75f, 0.4f));
        }

        /// <summary>A water drop echoing in a stone hall.</summary>
        private static float[] Drip()
        {
            var b = Mono(1.2f);
            float ph = 0;
            for (int i = 0; i < (int)(0.06f * Rate); i++)
            {
                float t = i / (float)Rate;
                float freq = 900 + 1400 * (t / 0.06f);
                ph += freq / Rate;
                b[i] = Sine(ph) * Exp(t, 0.02f) * 0.5f;
            }
            return Down(MonoToStereoWithReverb(b, 0.9f, 0.9f, 0.2f));
        }

        /// <summary>A soft, high temple chime.</summary>
        private static float[] Chime()
        {
            var b = Mono(2.2f);
            float f = 1568f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float m = Sine(t * f * 2.76f) * 0.8f * Exp(t, 0.4f);
                b[i] = (Sine(t * f + m / Tau) * Exp(t, 0.7f) + Sine(t * f * 1.5f) * 0.25f * Exp(t, 0.4f)) * MathF.Min(1, t / 0.003f) * 0.25f;
            }
            return Down(MonoToStereoWithReverb(b, 0.6f, 0.85f, 0.3f));
        }

        private static float Calc01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        /// <summary>Downmix interleaved stereo to mono (so it can be panned at playback).</summary>
        private static float[] Down(float[] st)
        {
            var m = new float[st.Length / 2];
            for (int i = 0; i < m.Length; i++) m[i] = (st[i * 2] + st[i * 2 + 1]) * 0.5f;
            return m;
        }
    }
}
