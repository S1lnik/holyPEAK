using System;
using System.Collections.Generic;
using static HolyPeak.Audio.Synth;

namespace HolyPeak.Audio
{
    public enum SongId { Title, Foothills, Ruins, Frost, Peak, Ending, Village, Boss }

    public enum Inst
    {
        Pluck, Harp, Bell, Marimba, Pad, Choir, Bass, SynthBass, Flute, Lead, Arp, Keys,
        Kick, Snare, Rim, Hat, OpenHat, Shaker, Tom, Timpani, Drone,
    }

    /// <summary>A sequenced piece of music: notes on a beat grid, rendered offline into a seamless stereo loop.</summary>
    public sealed class Score
    {
        public readonly float Bpm;
        public readonly int Bars;
        public const int BeatsPerBar = 4;
        public float BeatSec => 60f / Bpm;
        public float LengthSec => Bars * BeatsPerBar * BeatSec;
        public float ReverbSize = 0.86f, ReverbDamp = 0.35f, Master = 1f;
        public float DelayBeats = 0.75f, DelayFeedback = 0.35f;

        public struct N
        {
            public Inst Inst;
            public int Midi;
            public float Beat, Len, Vel, Pan;
            public float Detune;
        }

        public readonly List<N> Notes = new List<N>();

        public Score(float bpm, int bars) { Bpm = bpm; Bars = bars; }

        public void Add(Inst inst, int midi, float beat, float len, float vel = 1f, float pan = 0f, float detune = 0f)
            => Notes.Add(new N { Inst = inst, Midi = midi, Beat = beat, Len = len, Vel = vel, Pan = pan, Detune = detune });

        /// <summary>
        /// Tracker-style melody: whitespace separated steps; a note name ("F#5"), "r" for rest or "-" to hold.
        /// </summary>
        public void Line(Inst inst, float startBeat, float step, string pattern, float vel = 1f, float pan = 0f, int transpose = 0)
        {
            var toks = pattern.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            while (i < toks.Length)
            {
                string t = toks[i];
                if (t == "r" || t == "-" || t == "|") { i++; continue; }
                int len = 1;
                int j = i + 1;
                while (j < toks.Length && (toks[j] == "-" || toks[j] == "|")) { if (toks[j] == "-") len++; j++; }
                int stepIndex = 0;
                for (int k = 0; k < i; k++) if (toks[k] != "|") stepIndex++;
                Add(inst, Music.NoteNum(t) + transpose, startBeat + stepIndex * step, len * step, vel, pan);
                i = j;
            }
        }
    }

    public static class Music
    {
        private static readonly Dictionary<char, int> Pc = new Dictionary<char, int>
            { ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11 };

        public static int NoteNum(string s)
        {
            int pc = Pc[char.ToUpperInvariant(s[0])];
            int i = 1;
            while (i < s.Length && (s[i] == '#' || s[i] == 'b'))
            {
                pc += s[i] == '#' ? 1 : -1;
                i++;
            }
            int oct = int.Parse(s.Substring(i));
            return (oct + 1) * 12 + pc;
        }

        /// <summary>Parses chord symbols like "Am", "F#m7", "Cmaj7", "D/F#", "Asus4", "G6".</summary>
        public static (int root, int[] tones, int bass) Chord(string sym)
        {
            string main = sym, slash = null;
            int si = sym.IndexOf('/');
            if (si >= 0) { main = sym.Substring(0, si); slash = sym.Substring(si + 1); }
            int pc = Pc[main[0]];
            int i = 1;
            if (i < main.Length && (main[i] == '#' || main[i] == 'b')) { pc += main[i] == '#' ? 1 : -1; i++; }
            string q = main.Substring(i);
            int[] iv = q switch
            {
                "" => new[] { 0, 4, 7 },
                "m" => new[] { 0, 3, 7 },
                "7" => new[] { 0, 4, 7, 10 },
                "m7" => new[] { 0, 3, 7, 10 },
                "maj7" => new[] { 0, 4, 7, 11 },
                "sus2" => new[] { 0, 2, 7 },
                "sus4" => new[] { 0, 5, 7 },
                "add9" => new[] { 0, 4, 7, 14 },
                "madd9" => new[] { 0, 3, 7, 14 },
                "6" => new[] { 0, 4, 7, 9 },
                "dim" => new[] { 0, 3, 6 },
                _ => new[] { 0, 4, 7 },
            };
            int bass = pc;
            if (slash != null)
            {
                bass = Pc[slash[0]];
                if (slash.Length > 1) bass += slash[1] == '#' ? 1 : -1;
            }
            return (((pc % 12) + 12) % 12, iv, ((bass % 12) + 12) % 12);
        }

        /// <summary>Chord tones voiced upward from a floor note.</summary>
        public static int[] Voicing(string sym, int floor, int count)
        {
            var (root, iv, _) = Chord(sym);
            var res = new List<int>();
            int baseNote = floor + ((root - floor % 12) + 12) % 12;
            int oct = 0;
            while (res.Count < count)
            {
                foreach (int v in iv)
                {
                    if (res.Count >= count) break;
                    res.Add(baseNote + (v % 12) + oct * 12 + (v >= 12 ? 12 : 0));
                }
                oct++;
            }
            res.Sort();
            return res.ToArray();
        }

        public static int BassNote(string sym, int floor)
        {
            var (_, _, bass) = Chord(sym);
            return floor + ((bass - floor % 12) + 12) % 12;
        }

        // ---- rendering --------------------------------------------------------------------

        public static float[] Render(Score s)
        {
            float tail = 4f;
            int loopFrames = (int)(s.LengthSec * Rate);
            int frames = loopFrames + (int)(tail * Rate);
            var dryL = new float[frames]; var dryR = new float[frames];
            var revL = new float[frames]; var revR = new float[frames];
            var dlyL = new float[frames]; var dlyR = new float[frames];
            // Render notes in parallel batches, then mix them in order (deterministic output).
            const int batch = 48;
            var notes = s.Notes;
            var outs = new float[batch][];
            var lens = new int[batch];
            var sends = new (float rev, float dly)[batch];
            for (int b0 = 0; b0 < notes.Count; b0 += batch)
            {
                int count = Math.Min(batch, notes.Count - b0);
                System.Threading.Tasks.Parallel.For(0, count, k =>
                {
                    var n = notes[b0 + k];
                    float dur = n.Len * s.BeatSec;
                    var buf = outs[k];
                    int need = (int)((dur + 3f) * Rate) + 1024;
                    if (buf == null || buf.Length < need) outs[k] = buf = new float[Math.Max(need, Rate * 4)];
                    else Array.Clear(buf, 0, buf.Length);
                    lens[k] = Voice(n.Inst, Midi(n.Midi) * (1 + n.Detune), dur, n.Vel, buf, n.Midi * 7919 + (int)(n.Beat * 13), out sends[k].rev, out sends[k].dly);
                });
                for (int k = 0; k < count; k++)
                {
                    var n = notes[b0 + k];
                    int off = (int)(n.Beat * s.BeatSec * Rate);
                    float pan = Math.Clamp(n.Pan, -1f, 1f);
                    float gl = MathF.Cos((pan + 1) * MathF.PI / 4), gr = MathF.Sin((pan + 1) * MathF.PI / 4);
                    float revSend = sends[k].rev, dlySend = sends[k].dly;
                    var tmp = outs[k];
                    int len = lens[k];
                    for (int i = 0; i < len; i++)
                    {
                        int idx = off + i;
                        if (idx >= frames) break;
                        float v = tmp[i];
                        float l = v * gl, r = v * gr;
                        dryL[idx] += l; dryR[idx] += r;
                        revL[idx] += l * revSend; revR[idx] += r * revSend;
                        dlyL[idx] += l * dlySend; dlyR[idx] += r * dlySend;
                    }
                }
            }

            // ping-pong delay feeding the reverb as well
            int dl = (int)(s.DelayBeats * s.BeatSec * Rate);
            var bufL = new float[dl]; var bufR = new float[dl];
            int p = 0;
            for (int i = 0; i < frames; i++)
            {
                float outL = bufL[p], outR = bufR[p];
                bufL[p] = dlyL[i] + outR * s.DelayFeedback;
                bufR[p] = dlyR[i] * 0.3f + outL * s.DelayFeedback;
                p = (p + 1) % dl;
                dryL[i] += outL * 0.55f; dryR[i] += outR * 0.55f;
                revL[i] += outL * 0.3f; revR[i] += outR * 0.3f;
            }

            var rv = new Reverb { RoomSize = s.ReverbSize, Damp = s.ReverbDamp };
            var mix = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                rv.Process(revL[i], revR[i], out float wl, out float wr);
                mix[i * 2] = dryL[i] + wl * 2.2f;
                mix[i * 2 + 1] = dryR[i] + wr * 2.2f;
            }

            // Fold the tail back onto the start so the loop is seamless.
            var loop = new float[loopFrames * 2];
            Array.Copy(mix, loop, loop.Length);
            for (int i = loopFrames * 2; i < mix.Length; i++)
            {
                int j = i - loopFrames * 2;
                if (j < loop.Length) loop[j] += mix[i];
            }

            // Gentle master: normalise then soft-limit.
            float peak = 0;
            foreach (var v in loop) peak = MathF.Max(peak, MathF.Abs(v));
            float gain = peak > 0 ? 1.15f / peak : 1f;
            for (int i = 0; i < loop.Length; i++) loop[i] = MathF.Tanh(loop[i] * gain) * 0.82f * s.Master;
            return loop;
        }

        /// <summary>Synthesises one note into <paramref name="o"/>; returns the number of samples written.</summary>
        private static int Voice(Inst inst, float f, float dur, float vel, float[] o, int seed, out float rev, out float dly)
        {
            rev = 0.25f; dly = 0f;
            int n;
            var noise = new Noise((uint)(seed * 2654435761u + 17));
            switch (inst)
            {
                case Inst.Pluck:
                case Inst.Harp:
                {
                    bool harp = inst == Inst.Harp;
                    float t60 = harp ? 2.6f : 1.6f;
                    n = Math.Min(o.Length, (int)((Math.Min(dur, t60) + 0.4f) * Rate));
                    Pluck(o, n, f, t60, harp ? 0.45f : 0.6f, ref noise);
                    float g = vel * (harp ? 0.32f : 0.34f);
                    var hp = new OnePole();
                    for (int i = 0; i < n; i++) o[i] = hp.Hp(o[i], 60) * g * Release(i, n, 0.05f);
                    rev = harp ? 0.5f : 0.3f; dly = harp ? 0.25f : 0.12f;
                    return n;
                }
                case Inst.Bell:
                {
                    n = Math.Min(o.Length, (int)((dur + 2.2f) * Rate));
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float idx = 2.2f * Exp(t, 0.25f) + 0.4f;
                        float m = Sine(t * f * 3.5f) * idx;
                        o[i] = Sine(t * f + m / Tau) * Exp(t, 0.9f) * MathF.Min(1, t / 0.002f) * vel * 0.18f;
                    }
                    rev = 0.55f; dly = 0.3f;
                    return n;
                }
                case Inst.Marimba:
                {
                    n = Math.Min(o.Length, (int)(1.2f * Rate));
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        o[i] = (Sine(t * f) * Exp(t, 0.42f) + Sine(t * f * 4f) * 0.35f * Exp(t, 0.05f) + Sine(t * f * 9.9f) * 0.08f * Exp(t, 0.015f))
                               * MathF.Min(1, t / 0.001f) * vel * 0.3f;
                    }
                    rev = 0.45f; dly = 0.28f;
                    return n;
                }
                case Inst.Pad:
                {
                    float rel = 1.2f;
                    n = Math.Min(o.Length, (int)((dur + rel) * Rate));
                    var svf = new Svf();
                    float p1 = noise.Next(), p2 = noise.Next(), p3 = noise.Next();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        p1 += f * 1.0045f / Rate; p2 += f * 0.9955f / Rate; p3 += f * 0.5f / Rate;
                        float raw = Saw(p1, f / Rate) + Saw(p2, f / Rate) + Tri(p3) * 0.8f;
                        float cut = 700 + 500 * MathF.Sin(t * 0.7f + seed) * 0.5f + f * 1.2f;
                        o[i] = svf.Lp(raw, cut, 0.7f) * Adsr(t, dur, 0.6f, 0.4f, 0.85f, rel) * vel * 0.06f;
                    }
                    rev = 0.6f;
                    return n;
                }
                case Inst.Choir:
                {
                    float rel = 1.4f;
                    n = Math.Min(o.Length, (int)((dur + rel) * Rate));
                    var f1 = new Svf(); var f2 = new Svf(); var f3 = new Svf();
                    float ph = 0, ph2 = 0.3f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float vib = 1 + 0.004f * MathF.Sin(t * Tau * 5.2f) * MathF.Min(1, t / 0.6f);
                        ph += f * vib / Rate; ph2 += f * vib * 1.006f / Rate;
                        float raw = Saw(ph, f / Rate) + Saw(ph2, f / Rate);
                        float v = f1.Bp(raw, 730, 6f) * 1.0f + f2.Bp(raw, 1090, 7f) * 0.6f + f3.Bp(raw, 2440, 9f) * 0.25f;
                        o[i] = v * Adsr(t, dur, 0.7f, 0.3f, 0.9f, rel) * vel * 0.16f;
                    }
                    rev = 0.7f;
                    return n;
                }
                case Inst.Bass:
                {
                    n = Math.Min(o.Length, (int)((dur + 0.12f) * Rate));
                    float ph = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f / Rate;
                        float v = Tri(ph) * 0.7f + Sine(ph) * 0.6f + Sine(ph * 2) * 0.12f * Exp(t, 0.08f);
                        o[i] = v * Adsr(t, dur, 0.006f, 0.15f, 0.75f, 0.1f) * vel * 0.4f;
                    }
                    rev = 0.08f;
                    return n;
                }
                case Inst.SynthBass:
                {
                    n = Math.Min(o.Length, (int)((dur + 0.1f) * Rate));
                    var svf = new Svf();
                    float ph = 0, ph2 = 0.5f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f / Rate; ph2 += f * 1.005f / Rate;
                        float raw = Saw(ph, f / Rate) + Pulse(ph2, f / Rate, 0.5f) * 0.6f;
                        float v = svf.Lp(raw, 260 + 1100 * Exp(t, 0.06f), 1.4f) + Sine(ph * 0.5f) * 0.12f;
                        o[i] = v * Adsr(t, dur, 0.004f, 0.1f, 0.7f, 0.08f) * vel * 0.2f;
                    }
                    rev = 0.05f;
                    return n;
                }
                case Inst.Flute:
                {
                    n = Math.Min(o.Length, (int)((dur + 0.3f) * Rate));
                    float ph = 0;
                    var bp = new Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float vib = 1 + 0.006f * MathF.Sin(t * Tau * 5.4f) * Calc01((t - 0.18f) / 0.3f);
                        ph += f * vib / Rate;
                        float v = Sine(ph) + 0.22f * Sine(ph * 2) + 0.08f * Sine(ph * 3);
                        float breath = bp.Bp(noise.Next(), f * 2, 2f) * 0.25f * (0.4f + Exp(t, 0.06f));
                        o[i] = (v + breath) * Adsr(t, dur, 0.05f, 0.12f, 0.82f, 0.22f) * vel * 0.2f;
                    }
                    rev = 0.5f; dly = 0.22f;
                    return n;
                }
                case Inst.Lead:
                {
                    n = Math.Min(o.Length, (int)((dur + 0.25f) * Rate));
                    var svf = new Svf();
                    float ph = 0, ph2 = 0.25f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float vib = 1 + 0.007f * MathF.Sin(t * Tau * 5.8f) * Calc01((t - 0.15f) / 0.25f);
                        ph += f * vib / Rate; ph2 += f * vib * 1.003f / Rate;
                        float raw = Pulse(ph, f / Rate, 0.3f) * 0.6f + Saw(ph2, f / Rate) * 0.5f;
                        float v = svf.Lp(raw, 1400 + 2200 * Exp(t, 0.15f) + f, 1.1f);
                        o[i] = v * Adsr(t, dur, 0.01f, 0.2f, 0.7f, 0.18f) * vel * 0.13f;
                    }
                    rev = 0.4f; dly = 0.3f;
                    return n;
                }
                case Inst.Arp:
                {
                    n = Math.Min(o.Length, (int)(0.35f * Rate));
                    var svf = new Svf();
                    float ph = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f / Rate;
                        float v = svf.Lp(Pulse(ph, f / Rate, 0.25f), 1800 + 2500 * Exp(t, 0.03f), 1.2f);
                        o[i] = v * Exp(t, 0.09f) * MathF.Min(1, t / 0.002f) * vel * 0.12f;
                    }
                    rev = 0.3f; dly = 0.35f;
                    return n;
                }
                case Inst.Keys:
                {
                    n = Math.Min(o.Length, (int)((Math.Min(dur, 3f) + 0.6f) * Rate));
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float idx = 1.6f * Exp(t, 0.35f) + 0.25f;
                        float m = Sine(t * f) * idx;
                        float tine = Sine(t * f * 14f) * Exp(t, 0.01f) * 0.15f;
                        float body = Sine(t * f + m / Tau);
                        float env = Exp(t, 1.4f) * (t > dur ? Exp(t - dur, 0.12f) : 1f) * MathF.Min(1, t / 0.002f);
                        o[i] = (body + tine) * env * vel * 0.22f * (1 + 0.06f * MathF.Sin(t * Tau * 4.5f));
                    }
                    rev = 0.45f; dly = 0.1f;
                    return n;
                }
                case Inst.Kick:
                {
                    n = (int)(0.4f * Rate);
                    float ph = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += (45 + 120 * MathF.Exp(-t * 32)) / Rate;
                        o[i] = (Sine(ph) * Exp(t, 0.16f) + noise.Next() * Exp(t, 0.003f) * 0.3f) * vel * 0.55f;
                    }
                    rev = 0.05f;
                    return n;
                }
                case Inst.Snare:
                case Inst.Rim:
                {
                    bool rim = inst == Inst.Rim;
                    n = (int)(0.3f * Rate);
                    var svf = new Svf();
                    float ph = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += (rim ? 420 : 190) / (float)Rate;
                        float body = Sine(ph) * Exp(t, rim ? 0.015f : 0.04f) * (rim ? 0.5f : 0.6f);
                        float sn = svf.Bp(noise.Next(), rim ? 3500 : 2200, rim ? 3f : 0.9f) * Exp(t, rim ? 0.02f : 0.09f) * (rim ? 0.5f : 1.2f);
                        o[i] = (body + sn) * vel * 0.32f;
                    }
                    rev = rim ? 0.25f : 0.3f;
                    return n;
                }
                case Inst.Hat:
                case Inst.OpenHat:
                case Inst.Shaker:
                {
                    float tau = inst == Inst.Hat ? 0.018f : (inst == Inst.OpenHat ? 0.12f : 0.035f);
                    n = (int)((tau * 6 + 0.02f) * Rate);
                    var svf = new Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float x = noise.Next();
                        float v = inst == Inst.Shaker ? svf.Bp(x, 5500, 1.5f) * MathF.Min(1, t / 0.008f) : svf.Hp(x, 7500, 0.8f);
                        o[i] = v * Exp(t, tau) * vel * (inst == Inst.Shaker ? 0.2f : 0.16f);
                    }
                    rev = 0.15f;
                    return n;
                }
                case Inst.Tom:
                {
                    n = (int)(0.6f * Rate);
                    float ph = 0;
                    var lp = new OnePole();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f * (1 + 0.6f * MathF.Exp(-t * 25)) / Rate;
                        o[i] = (Sine(ph) * Exp(t, 0.16f) + lp.Lp(noise.Next(), 1200) * Exp(t, 0.01f) * 0.6f) * vel * 0.45f;
                    }
                    rev = 0.35f;
                    return n;
                }
                case Inst.Timpani:
                {
                    n = Math.Min(o.Length, (int)(2.5f * Rate));
                    var lp = new OnePole();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float ff = f * (1 + 0.03f * MathF.Exp(-t * 6));
                        float v = Sine(t * ff) * Exp(t, 0.9f) + Sine(t * ff * 1.5f) * 0.4f * Exp(t, 0.5f) + Sine(t * ff * 1.98f) * 0.25f * Exp(t, 0.35f);
                        float mallet = lp.Lp(noise.Next(), 900) * Exp(t, 0.02f);
                        o[i] = (v + mallet) * MathF.Min(1, t / 0.003f) * vel * 0.45f;
                    }
                    rev = 0.55f;
                    return n;
                }
                case Inst.Drone:
                {
                    float rel = 2f;
                    n = Math.Min(o.Length, (int)((dur + rel) * Rate));
                    var svf = new Svf();
                    float ph = 0, ph2 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f / Rate; ph2 += f * 1.5f * 1.002f / Rate;
                        float raw = Saw(ph, f / Rate) + Saw(ph2, f / Rate) * 0.5f;
                        o[i] = svf.Lp(raw, 300 + 120 * MathF.Sin(t * 0.5f), 1.2f) * Adsr(t, dur, 1.5f, 0.5f, 0.9f, rel) * vel * 0.1f;
                    }
                    rev = 0.5f;
                    return n;
                }
            }
            n = 0;
            return n;
        }

        private static float Release(int i, int n, float sec)
        {
            int r = (int)(sec * Rate);
            return i > n - r ? (n - i) / (float)r : 1f;
        }

        private static float Calc01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        /// <summary>Karplus-Strong plucked string with fractional-delay tuning.</summary>
        private static void Pluck(float[] o, int n, float freq, float t60, float bright, ref Noise noise)
        {
            float period = Rate / freq;
            int N = Math.Max(2, (int)MathF.Floor(period - 0.5f - 0.1f));
            float d = period - 0.5f - N;
            float C = (1 - d) / (1 + d);
            var line = new float[N];
            var lp = new OnePole();
            float cutoff = 800 + bright * 7000;
            for (int i = 0; i < N; i++) line[i] = lp.Lp(noise.Next(), cutoff);
            // remove DC from the excitation
            float mean = 0;
            for (int i = 0; i < N; i++) mean += line[i];
            mean /= N;
            for (int i = 0; i < N; i++) line[i] -= mean;
            float g = MathF.Pow(0.001f, 1f / (t60 * freq));
            int ptr = 0;
            float prev = 0, apX = 0, apY = 0;
            for (int i = 0; i < n; i++)
            {
                float y = line[ptr];
                float avg = 0.5f * (y + prev) * g;
                prev = y;
                float ap = C * avg + apX - C * apY;
                apX = avg; apY = ap;
                line[ptr] = ap;
                if (++ptr >= N) ptr = 0;
                o[i] = y;
            }
        }
    }
}
