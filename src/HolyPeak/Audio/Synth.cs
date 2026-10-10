using System;

namespace HolyPeak.Audio
{
    /// <summary>
    /// Minimal offline synthesizer toolkit: oscillators, envelopes, filters, a plucked string,
    /// FM bells and a Freeverb-style reverb. Everything renders into float buffers.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;
        public const float Tau = MathF.PI * 2f;

        public static float Midi(int note) => 440f * MathF.Pow(2f, (note - 69) / 12f);

        // ---- oscillators (phase in cycles 0..1) ----

        public static float Sine(float ph) => MathF.Sin(ph * Tau);
        public static float Tri(float ph) { ph -= MathF.Floor(ph); return ph < 0.5f ? ph * 4f - 1f : 3f - ph * 4f; }

        private static float PolyBlep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        public static float Saw(float ph, float dt)
        {
            ph -= MathF.Floor(ph);
            return 2f * ph - 1f - PolyBlep(ph, dt);
        }

        public static float Pulse(float ph, float dt, float duty)
        {
            ph -= MathF.Floor(ph);
            float v = ph < duty ? 1f : -1f;
            v += PolyBlep(ph, dt);
            float p2 = ph - duty; p2 -= MathF.Floor(p2);
            v -= PolyBlep(p2, dt);
            return v - (2f * duty - 1f); // remove the DC component of asymmetric pulses
        }

        // ---- envelopes ----

        /// <summary>ADSR amplitude at time t for a note held for <paramref name="hold"/> seconds.</summary>
        public static float Adsr(float t, float hold, float a, float d, float s, float r)
        {
            if (t < 0) return 0;
            float v;
            if (t < a) v = t / a;
            else if (t < a + d) v = 1f - (1f - s) * ((t - a) / d);
            else v = s;
            if (t > hold)
            {
                float rt = t - hold;
                float atRelease = hold < a ? hold / a : (hold < a + d ? 1f - (1f - s) * ((hold - a) / d) : s);
                v = rt >= r ? 0 : atRelease * (1f - rt / r);
            }
            return v;
        }

        public static float Exp(float t, float tau) => t < 0 ? 0 : MathF.Exp(-t / tau);

        // ---- noise ----

        public struct Noise
        {
            private uint _s;
            public Noise(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 8388608f - 1f;
            }
        }

        // ---- filters ----

        public struct OnePole
        {
            private float _z;
            public float Lp(float x, float cutoff)
            {
                float a = 1f - MathF.Exp(-Tau * cutoff / Rate);
                _z += a * (x - _z);
                return _z;
            }
            public float Hp(float x, float cutoff) => x - Lp(x, cutoff);
        }

        /// <summary>Topology-preserving state variable filter.</summary>
        public struct Svf
        {
            private float _ic1, _ic2;
            public void Process(float x, float cutoff, float q, out float lp, out float bp, out float hp)
            {
                cutoff = Math.Clamp(cutoff, 20f, Rate * 0.45f);
                float g = MathF.Tan(MathF.PI * cutoff / Rate);
                float k = 1f / Math.Max(0.05f, q);
                float a1 = 1f / (1f + g * (g + k));
                float a2 = g * a1, a3 = g * a2;
                float v3 = x - _ic2;
                float v1 = a1 * _ic1 + a2 * v3;
                float v2 = _ic2 + a2 * _ic1 + a3 * v3;
                _ic1 = 2f * v1 - _ic1;
                _ic2 = 2f * v2 - _ic2;
                lp = v2; bp = v1; hp = x - k * v1 - v2;
            }
            public float Lp(float x, float c, float q) { Process(x, c, q, out var l, out _, out _); return l; }
            public float Bp(float x, float c, float q) { Process(x, c, q, out _, out var b, out _); return b; }
            public float Hp(float x, float c, float q) { Process(x, c, q, out _, out _, out var h); return h; }
        }

        // ---- buffers ----

        public static float[] Mono(float seconds) => new float[(int)(seconds * Rate)];

        public static void Normalize(float[] buf, float peak = 0.9f)
        {
            float m = 0;
            foreach (var v in buf) m = MathF.Max(m, MathF.Abs(v));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        public static void FadeEdges(float[] buf, int channels, float fadeIn = 0.002f, float fadeOut = 0.01f)
        {
            int frames = buf.Length / channels;
            int fi = (int)(fadeIn * Rate), fo = (int)(fadeOut * Rate);
            for (int i = 0; i < frames; i++)
            {
                float g = 1f;
                if (i < fi) g = i / (float)fi;
                if (i > frames - fo) g = Math.Min(g, (frames - i) / (float)fo);
                if (g < 1f) for (int c = 0; c < channels; c++) buf[i * channels + c] *= g;
            }
        }

        public static byte[] ToPcm16(float[] buf)
        {
            var bytes = new byte[buf.Length * 2];
            for (int i = 0; i < buf.Length; i++)
            {
                float v = Math.Clamp(buf[i], -1f, 1f);
                short s = (short)(v * 32767f);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        public static float SoftClip(float x) => MathF.Tanh(x);

        // ---- Freeverb-style stereo reverb ----

        public sealed class Reverb
        {
            private static readonly int[] CombTuning = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            private static readonly int[] ApTuning = { 556, 441, 341, 225 };
            private const int Spread = 23;
            private readonly float[][] _combL, _combR, _apL, _apR;
            private readonly int[] _ciL, _ciR, _aiL, _aiR;
            private readonly float[] _fsL, _fsR;
            public float RoomSize = 0.84f, Damp = 0.3f;

            public Reverb()
            {
                _combL = new float[8][]; _combR = new float[8][]; _apL = new float[4][]; _apR = new float[4][];
                _ciL = new int[8]; _ciR = new int[8]; _aiL = new int[4]; _aiR = new int[4];
                _fsL = new float[8]; _fsR = new float[8];
                for (int i = 0; i < 8; i++) { _combL[i] = new float[CombTuning[i]]; _combR[i] = new float[CombTuning[i] + Spread]; }
                for (int i = 0; i < 4; i++) { _apL[i] = new float[ApTuning[i]]; _apR[i] = new float[ApTuning[i] + Spread]; }
            }

            public void Process(float inL, float inR, out float outL, out float outR)
            {
                float input = (inL + inR) * 0.015f;
                float l = 0, r = 0;
                for (int i = 0; i < 8; i++)
                {
                    l += Comb(_combL[i], ref _ciL[i], ref _fsL[i], input);
                    r += Comb(_combR[i], ref _ciR[i], ref _fsR[i], input);
                }
                for (int i = 0; i < 4; i++)
                {
                    l = Allpass(_apL[i], ref _aiL[i], l);
                    r = Allpass(_apR[i], ref _aiR[i], r);
                }
                outL = l; outR = r;
            }

            private float Comb(float[] buf, ref int idx, ref float store, float input)
            {
                float output = buf[idx];
                store = output * (1 - Damp) + store * Damp;
                buf[idx] = input + store * RoomSize;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }

            private static float Allpass(float[] buf, ref int idx, float input)
            {
                float bufout = buf[idx];
                float output = -input + bufout;
                buf[idx] = input + bufout * 0.5f;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }
        }

        /// <summary>Applies reverb to a mono buffer in place and returns an interleaved stereo buffer.</summary>
        public static float[] MonoToStereoWithReverb(float[] mono, float wet, float roomSize = 0.8f, float damp = 0.4f, float extraSeconds = 0)
        {
            int frames = mono.Length + (int)(extraSeconds * Rate);
            var st = new float[frames * 2];
            var rv = new Reverb { RoomSize = roomSize, Damp = damp };
            for (int i = 0; i < frames; i++)
            {
                float x = i < mono.Length ? mono[i] : 0;
                rv.Process(x, x, out float l, out float r);
                st[i * 2] = x + l * wet;
                st[i * 2 + 1] = x + r * wet;
            }
            return st;
        }
    }
}
