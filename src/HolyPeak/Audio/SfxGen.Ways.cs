using System;
using System.Collections.Generic;
using static HolyPeak.Audio.Synth;

namespace HolyPeak.Audio
{
    /// <summary>Sounds of the pilgrim's ways: vaults, ropes, lanterns, crumbling ledges, spells and charms.</summary>
    public static partial class SfxGen
    {
        private static void AddWays(Dictionary<string, Clip> d)
        {
            d["mantle"] = M(Mantle());
            d["vault"] = M(Vault());
            d["ropegrab"] = M(RopeGrab());
            d["ropejump"] = M(RopeJump());
            d["lantern"] = M(LanternRing());
            d["crumble"] = M(Crumble(false));
            d["crumblefall"] = M(Crumble(true));
            d["reform"] = M(Reform());
            d["cast"] = M(Cast());
            d["bolt"] = M(Bolt());
            d["psalm"] = M(Psalm());
            d["dive"] = M(Dive());
            d["starland"] = M(StarLand());
            d["spellhit"] = M(SpellHit());
            d["nograce"] = M(NoGrace());
            d["bramble"] = M(BrambleBurst());
            d["charm"] = S(CharmFound());
            d["equip"] = M(Equip(true));
            d["unequip"] = M(Equip(false));
        }

        // ---- moving -------------------------------------------------------------------------

        /// <summary>Hands catching a ledge and a quick pull up.</summary>
        private static float[] Mantle()
        {
            var b = Mono(0.18f);
            var n = new Noise(101);
            var f = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 180 + 260 * MathF.Min(1, t / 0.1f);
                ph += freq / Rate;
                float grip = f.Bp(n.Next(), 1500, 2f) * Exp(t, 0.02f) * 0.6f;
                b[i] = Tri(ph) * Env(t, 0.004f, 0.05f) * 0.3f + grip;
            }
            return b;
        }

        /// <summary>The staff planted with a wooden knock, then the rush of the vault.</summary>
        private static float[] Vault()
        {
            var b = Mono(0.4f);
            var n = new Noise(103);
            var lp = new OnePole();
            var f = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (220 - 80 * MathF.Min(1, t / 0.05f)) / Rate;
                float knock = (Sine(ph) * 0.6f + lp.Lp(n.Next(), 1800) * 0.5f) * Exp(t, 0.025f);
                float u = MathF.Min(1, MathF.Max(0, (t - 0.03f) / 0.32f));
                float rush = f.Bp(n.Next(), 600 + 2600 * u, 1.4f) * MathF.Sin(MathF.PI * u) * 0.8f;
                b[i] = knock * 0.8f + rush;
            }
            return b;
        }

        /// <summary>A rope creaks as it takes the pilgrim's weight.</summary>
        private static float[] RopeGrab()
        {
            var b = Mono(0.3f);
            var n = new Noise(107);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float creak = f.Bp(n.Next(), 420 + 120 * MathF.Sin(t * 70), 6f) * (0.6f + 0.4f * MathF.Sin(t * Tau * 38)) * Env(t, 0.01f, 0.09f);
                float slap = Sine(t * 140) * Exp(t, 0.02f) * 0.4f;
                b[i] = creak * 1.2f + slap;
            }
            return b;
        }

        private static float[] RopeJump()
        {
            var b = Mono(0.3f);
            var n = new Noise(109);
            var f = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 0.3f;
                ph += (300 + 500 * MathF.Min(1, t / 0.08f)) / Rate;
                float whoosh = f.Bp(n.Next(), 900 + 2400 * u, 1.6f) * MathF.Sin(MathF.PI * u) * 0.7f;
                b[i] = whoosh + Tri(ph) * Exp(t, 0.05f) * 0.2f;
            }
            return b;
        }

        /// <summary>A struck lantern: a bright brass ding and a puff of air.</summary>
        private static float[] LanternRing()
        {
            var b = Mono(0.7f);
            var n = new Noise(113);
            var f = new OnePole();
            (float r, float a, float d)[] partials = { (1f, 1f, 0.25f), (2.4f, 0.5f, 0.15f), (3.9f, 0.3f, 0.08f), (5.2f, 0.2f, 0.05f) };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0;
                foreach (var (r, a, d) in partials) v += Sine(t * 1320 * r) * a * Exp(t, d);
                b[i] = v * 0.18f * MathF.Min(1, t / 0.001f) + f.Lp(n.Next(), 4000) * Exp(t, 0.012f) * 0.45f;
            }
            return Down(MonoToStereoWithReverb(b, 0.35f, 0.75f, 0.3f));
        }

        /// <summary>Loose stone rattling (and, when it falls, tumbling away).</summary>
        private static float[] Crumble(bool fall)
        {
            var b = Mono(fall ? 0.6f : 0.35f);
            var n = new Noise(fall ? 127u : 131u);
            var rng = new Noise(137);
            var f = new OnePole();
            int grains = fall ? 26 : 12;
            for (int k = 0; k < grains; k++)
            {
                float start = MathF.Abs(rng.Next()) * (fall ? 0.4f : 0.3f);
                float cut = 600 + MathF.Abs(rng.Next()) * (fall ? 1400 : 2600);
                float amp = 0.25f + MathF.Abs(rng.Next()) * 0.35f;
                var g = new OnePole();
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    if (t > 0.06f) break;
                    b[i] += g.Lp(n.Next(), cut) * Exp(t, 0.012f) * amp;
                }
            }
            if (fall)
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[i] += f.Lp(n.Next(), 300) * Env(t, 0.01f, 0.18f) * 0.9f;
                }
            return b;
        }

        /// <summary>A ledge pulling itself back together: a soft rising grind.</summary>
        private static float[] Reform()
        {
            var b = Mono(0.4f);
            var n = new Noise(139);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 0.4f;
                b[i] = f.Bp(n.Next(), 300 + 900 * u, 3f) * MathF.Sin(MathF.PI * u) * 0.5f + Sine(t * (400 + 300 * u)) * MathF.Sin(MathF.PI * u) * 0.08f;
            }
            return b;
        }

        // ---- spells -------------------------------------------------------------------------

        /// <summary>Grace leaving the lantern: a quick swish and a sparkle.</summary>
        private static float[] Cast()
        {
            var b = Mono(0.3f);
            var n = new Noise(149);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float swish = f.Bp(n.Next(), 2500 + 3000 * t / 0.3f, 2f) * Env(t, 0.01f, 0.06f) * 0.6f;
                float sparkle = Sine(t * 2637) * Exp(t, 0.08f) * 0.12f + Sine(t * 3520) * Exp(MathF.Max(0, t - 0.04f), 0.07f) * (t > 0.04f ? 0.1f : 0);
                b[i] = swish + sparkle;
            }
            return b;
        }

        /// <summary>The lantern bolt: a roaring flame that rises in pitch as it flies.</summary>
        private static float[] Bolt()
        {
            var b = Mono(0.5f);
            var n = new Noise(151);
            var f = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 0.5f;
                ph += (330 + 330 * u) / Rate;
                float roar = f.Bp(n.Next(), 700 + 1500 * u, 1.2f) * Env(t, 0.01f, 0.18f) * 0.8f;
                b[i] = roar + (Tri(ph) * 0.5f + Sine(ph * 2) * 0.3f) * Env(t, 0.005f, 0.12f) * 0.25f;
            }
            return Down(MonoToStereoWithReverb(b, 0.3f, 0.7f, 0.3f));
        }

        /// <summary>The rising psalm: a bright chord that swells up like a column of light.</summary>
        private static float[] Psalm()
        {
            var b = Mono(1.4f);
            int[] chord = { 67, 71, 74, 79, 83 };
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = Midi(chord[k]);
                float start = k * 0.025f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float vib = 1 + 0.004f * MathF.Sin(t * Tau * 5.5f);
                    b[i] += (Sine(t * freq * vib) + 0.3f * Sine(t * freq * 2 * vib)) * Env(t, 0.03f, 0.45f) * 0.09f;
                }
            }
            var n = new Noise(157);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += f.Bp(n.Next(), 1800 + 4000 * MathF.Min(1, t / 0.3f), 1.5f) * Env(t, 0.02f, 0.15f) * 0.4f;
            }
            return Down(MonoToStereoWithReverb(b, 0.55f, 0.88f, 0.25f));
        }

        /// <summary>The falling star: a whistle diving down.</summary>
        private static float[] Dive()
        {
            var b = Mono(0.45f);
            var n = new Noise(163);
            var f = new Svf();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 1800 * MathF.Exp(-t * 3.2f) + 300;
                ph += freq / Rate;
                b[i] = Sine(ph) * Env(t, 0.01f, 0.25f) * 0.25f + f.Bp(n.Next(), freq * 1.5f, 2.5f) * Env(t, 0.01f, 0.2f) * 0.5f;
            }
            return b;
        }

        /// <summary>The star lands: a deep boom with a ring of light.</summary>
        private static float[] StarLand()
        {
            var b = Mono(1.2f);
            var n = new Noise(167);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (110 - 60 * MathF.Min(1, t / 0.3f)) / Rate;
                float boom = Sine(ph) * Exp(t, 0.22f) * 0.8f;
                float crack = f.Lp(n.Next(), 2600) * Exp(t, 0.03f) * 0.7f;
                float ring = (Sine(t * 1046) + Sine(t * 1568) * 0.6f) * Env(t, 0.01f, 0.35f) * 0.1f;
                b[i] = boom + crack + ring;
            }
            return Down(MonoToStereoWithReverb(b, 0.4f, 0.8f, 0.3f));
        }

        private static float[] SpellHit()
        {
            var b = Mono(0.22f);
            var n = new Noise(173);
            var f = new OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float crackle = f.Hp(n.Next(), 2500) * Exp(t, 0.025f) * (0.6f + 0.4f * Sine(t * 120));
                b[i] = crackle * 0.8f + Sine(t * 1760 * (1 - t)) * Exp(t, 0.05f) * 0.2f;
            }
            return b;
        }

        /// <summary>Not enough grace: the lantern gutters.</summary>
        private static float[] NoGrace()
        {
            var b = Mono(0.3f);
            var n = new Noise(179);
            var f = new OnePole();
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += (200 - 90 * t / 0.3f) / Rate;
                b[i] = Tri(ph) * Env(t, 0.005f, 0.09f) * 0.3f + f.Lp(n.Next(), 900) * Env(t, 0.005f, 0.06f) * 0.4f;
            }
            return b;
        }

        private static float[] BrambleBurst()
        {
            var b = Mono(0.4f);
            var n = new Noise(181);
            var f = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float rustle = f.Bp(n.Next(), 3000 - 1800 * MathF.Min(1, t / 0.2f), 1.5f) * Env(t, 0.003f, 0.1f);
                float snap = (((int)(t * 90) & 1) == 0 ? 1 : 0.4f);
                b[i] = rustle * snap * 0.9f;
            }
            return b;
        }

        // ---- charms -------------------------------------------------------------------------

        /// <summary>A charm found: a quick, glassy arpeggio.</summary>
        private static float[] CharmFound()
        {
            var b = Mono(2f);
            int[] notes = { 72, 76, 79, 84, 88 };
            for (int k = 0; k < notes.Length; k++)
            {
                float freq = Midi(notes[k]), start = k * 0.09f;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float fm = Sine(t * freq * 2) * Exp(t, 0.15f) * 0.6f;
                    b[i] += Sine(t * freq + fm / Tau) * Env(t, 0.003f, 0.5f) * 0.12f;
                }
            }
            return MonoToStereoWithReverb(b, 0.6f, 0.86f, 0.3f);
        }

        private static float[] Equip(bool on)
        {
            var b = Mono(0.3f);
            var n = new Noise(191);
            var f = new OnePole();
            float f0 = on ? 1318 : 880, f1 = on ? 1760 : 659;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float click = f.Hp(n.Next(), 3000) * Exp(t, 0.006f) * 0.5f;
                float tone = Sine(t * f0) * Exp(t, 0.05f) * 0.15f + (t > 0.06f ? Sine(t * f1) * Exp(t - 0.06f, 0.09f) * 0.15f : 0);
                b[i] = click + tone;
            }
            return b;
        }
    }
}
