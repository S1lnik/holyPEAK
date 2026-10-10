using System;

namespace HolyPeak.Audio
{
    /// <summary>
    /// The soundtrack. Six pieces share one leitmotif (D-E-F-A ... the "pilgrim's theme"), heard in minor
    /// on the title screen, transfigured in Lydian on the summit, and finally at rest in major for the ending.
    /// </summary>
    public static class Songs
    {
        public static Score Get(SongId id) => id switch
        {
            SongId.Title => Title(),
            SongId.Foothills => Foothills(),
            SongId.Ruins => Ruins(),
            SongId.Frost => Frost(),
            SongId.Peak => Peak(),
            _ => Ending(),
        };

        // ---- helpers ----------------------------------------------------------------------

        private static void Arp(Score s, Inst inst, string[] chords, int startBar, int floor, int voices, int[] pattern, float step,
                                float vel, float pan = 0f, bool alternatePan = false, float accent = 1.15f)
        {
            int perBar = (int)(Score.BeatsPerBar / step);
            for (int b = 0; b < chords.Length; b++)
            {
                var v = Music.Voicing(chords[b], floor, voices);
                for (int k = 0; k < perBar; k++)
                {
                    int note = v[pattern[k % pattern.Length] % v.Length];
                    float beat = (startBar + b) * Score.BeatsPerBar + k * step;
                    float p = alternatePan ? ((k & 1) == 0 ? -0.45f : 0.45f) : pan;
                    s.Add(inst, note, beat, step * 1.6f, vel * (k % (perBar / 2) == 0 ? accent : 1f), p);
                }
            }
        }

        /// <param name="rhythm">8 steps per bar: R root, 5 fifth, O octave, - hold, . rest</param>
        private static void Bass(Score s, Inst inst, string[] chords, int startBar, int floor, string rhythm, float vel)
        {
            for (int b = 0; b < chords.Length; b++)
            {
                int root = Music.BassNote(chords[b], floor);
                var (cr, _, _) = Music.Chord(chords[b]);
                int fifth = floor + ((cr + 7 - floor % 12) + 24) % 12;
                if (fifth < root - 5) fifth += 12;
                for (int k = 0; k < rhythm.Length; k++)
                {
                    char c = rhythm[k];
                    if (c == '-' || c == '.') continue;
                    int len = 1;
                    while (k + len < rhythm.Length && rhythm[k + len] == '-') len++;
                    int note = c == '5' ? fifth : (c == 'O' ? root + 12 : root);
                    float beat = (startBar + b) * Score.BeatsPerBar + k * 0.5f;
                    s.Add(inst, note, beat, len * 0.5f * 0.95f, vel * (k == 0 ? 1.1f : 1f));
                }
            }
        }

        private static void Pads(Score s, Inst inst, string[] chords, int startBar, int floor, int voices, float vel, float bars = 1f)
        {
            for (int b = 0; b < chords.Length; b++)
            {
                var v = Music.Voicing(chords[b], floor, voices);
                float beat = (startBar + b) * Score.BeatsPerBar;
                for (int i = 0; i < v.Length; i++)
                {
                    float pan = (i - (v.Length - 1) / 2f) * 0.35f;
                    s.Add(inst, v[i], beat, Score.BeatsPerBar * bars, vel, pan, detune: (i & 1) == 0 ? 0.0015f : -0.0015f);
                }
            }
        }

        /// <param name="pattern">16 steps per bar: x accent, o soft, . rest</param>
        private static void Drum(Score s, Inst inst, int startBar, int bars, string pattern, float vel, int midi = 60, float pan = 0f)
        {
            for (int b = 0; b < bars; b++)
                for (int k = 0; k < pattern.Length; k++)
                {
                    char c = pattern[k];
                    if (c == '.') continue;
                    float beat = (startBar + b) * Score.BeatsPerBar + k * 0.25f;
                    s.Add(inst, midi, beat, 0.25f, vel * (c == 'x' ? 1f : 0.55f), pan);
                }
        }

        private static string[] Repeat(string[] a, int times)
        {
            var r = new string[a.Length * times];
            for (int i = 0; i < times; i++) Array.Copy(a, 0, r, i * a.Length, a.Length);
            return r;
        }

        private static string[] Concat(params string[][] parts)
        {
            int n = 0;
            foreach (var p in parts) n += p.Length;
            var r = new string[n];
            int i = 0;
            foreach (var p in parts) { Array.Copy(p, 0, r, i, p.Length); i += p.Length; }
            return r;
        }

        // ---- Title: "Holy Peak" (D minor, 70 bpm) -----------------------------------------

        private static Score Title()
        {
            var s = new Score(70, 16) { ReverbSize = 0.9f, DelayBeats = 0.75f };
            string[] ch = { "Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A", "Dm", "Bb", "F", "C", "Gm", "Bb", "A", "Dm" };
            Pads(s, Inst.Pad, ch, 0, 50, 4, 0.9f);
            Pads(s, Inst.Choir, ch[8..], 8, 57, 3, 0.55f);
            Bass(s, Inst.Bass, ch, 0, 38, "R-------", 0.5f);
            Arp(s, Inst.Bell, ch, 0, 62, 5, new[] { 0, 2, 3, 4, 3, 2, 1, 2 }, 0.5f, 0.32f, alternatePan: true);
            foreach (int bar in new[] { 0, 4, 8, 12 }) s.Add(Inst.Timpani, 38, bar * 4, 4, 0.8f);
            s.Add(Inst.Timpani, 33, 7 * 4 + 3, 1, 0.5f);
            s.Add(Inst.Timpani, 33, 14 * 4 + 3, 1, 0.6f);
            s.Line(Inst.Flute, 4 * 4, 0.5f,
                "D5 - - - E5 - F5 - | A5 - - - - - G5 F5 | G5 - - - F5 - D5 - | E5 - - - - - - - | " +
                "D5 - - - E5 - F5 - | A5 - - - D6 - - - | C6 - - - A5 - F5 - | G5 - - - - - - - | " +
                "Bb5 - - - A5 - G5 - | F5 - - - G5 - A5 - | E5 - - - C#5 - - - | D5 - - - - - - -", 0.85f, 0.1f);
            return s;
        }

        // ---- Chapter I: "First Steps" (G major, 104 bpm) -----------------------------------

        private static Score Foothills()
        {
            var s = new Score(104, 32) { ReverbSize = 0.78f, DelayBeats = 0.75f };
            string[] A = { "G", "D/F#", "Em", "C", "G", "D/F#", "C", "D" };
            string[] B = { "Em", "C", "G", "D", "Em", "C", "Am", "D" };
            var all = Concat(A, A, B, A);
            Arp(s, Inst.Pluck, all, 0, 55, 5, new[] { 0, 2, 3, 4, 3, 2, 4, 2 }, 0.5f, 0.75f, pan: -0.25f);
            Bass(s, Inst.Bass, all, 0, 40, "R--R5-O-", 0.75f);
            Pads(s, Inst.Pad, Concat(B, A), 16, 55, 3, 0.45f);
            Drum(s, Inst.Shaker, 0, 32, "o.x.o.x.o.x.o.x.", 0.7f, pan: 0.3f);
            Drum(s, Inst.Kick, 4, 12, "x.......x.......", 0.8f);
            Drum(s, Inst.Rim, 8, 8, "....x.......x...", 0.7f, pan: -0.1f);
            Drum(s, Inst.Kick, 16, 16, "x.....x.x.......", 0.85f);
            Drum(s, Inst.Snare, 16, 16, "....x.......x..o", 0.6f);
            string melA =
                "B4 - D5 - G5 - - - | F#5 - E5 - D5 - - - | E5 - G5 - B5 - A5 G5 | E5 - - - - - D5 - | " +
                "B4 - D5 - G5 - A5 - | B5 - A5 - F#5 - D5 - | E5 - G5 - E5 - D5 C5 | D5 - - - - - - -";
            string melB =
                "G5 - - F#5 E5 - B4 - | C5 - E5 - G5 - - - | D5 - - C5 B4 - G4 - | A4 - - - F#4 - A4 - | " +
                "G5 - - F#5 E5 - B4 - | C5 - E5 - A5 - G5 - | E5 - - D5 C5 - A4 - | D5 - - - F#5 - A5 -";
            s.Line(Inst.Flute, 8 * 4, 0.5f, melA, 0.9f, 0.1f);
            s.Line(Inst.Flute, 16 * 4, 0.5f, melB, 0.9f, 0.1f);
            s.Line(Inst.Flute, 24 * 4, 0.5f, melA, 0.9f, 0.1f);
            s.Line(Inst.Bell, 24 * 4, 0.5f, melA, 0.3f, 0.4f, 12);
            return s;
        }

        // ---- Chapter II: "Those Who Came Before" (A minor, 80 bpm) --------------------------

        private static Score Ruins()
        {
            var s = new Score(80, 24) { ReverbSize = 0.9f, ReverbDamp = 0.25f, DelayBeats = 0.75f, DelayFeedback = 0.45f };
            string[] A = { "Am", "G", "F", "E", "Am", "G", "F", "E" };
            string[] B = { "Dm", "Am", "F", "E", "Dm", "Am", "F", "E" };
            var all = Concat(A, A, B);
            Arp(s, Inst.Harp, all, 0, 57, 5, new[] { 0, 2, 4, 2, 3, 1, 4, 2 }, 0.5f, 0.7f, alternatePan: true);
            Pads(s, Inst.Pad, all, 0, 52, 3, 0.55f);
            s.Add(Inst.Drone, 45, 0, 8 * 4, 0.6f);
            s.Add(Inst.Drone, 45, 8 * 4, 8 * 4, 0.6f);
            s.Add(Inst.Drone, 38, 16 * 4, 8 * 4, 0.6f);
            Bass(s, Inst.Bass, all, 0, 33, "R---5---", 0.4f);
            Drum(s, Inst.Tom, 4, 20, "x.....o...x.....", 0.55f, 45, -0.2f);
            Drum(s, Inst.Tom, 8, 16, "........o.....o.", 0.4f, 52, 0.25f);
            Drum(s, Inst.Shaker, 8, 16, "..o...o...o...o.", 0.5f, pan: 0.35f);
            s.Line(Inst.Marimba, 8 * 4, 0.5f,
                "A4 - C5 - E5 - - - | D5 - - - B4 - G4 - | A4 - C5 - F5 - E5 - | E5 - - - G#4 - - - | " +
                "A4 - C5 - E5 - A5 - | G5 - - - F5 - D5 - | F5 - E5 - D5 - C5 - | B4 - - - G#4 - B4 -", 0.9f, -0.1f);
            s.Line(Inst.Marimba, 16 * 4, 0.5f,
                "D5 - F5 - A5 - - - | G5 - E5 - C5 - - - | F5 - E5 - D5 - C5 - | B4 - - - - - - - | " +
                "D5 - F5 - A5 - D6 - | C6 - B5 - A5 - E5 - | F5 - - - E5 - D5 - | E5 - - - - - - -", 0.9f, -0.1f);
            s.Line(Inst.Flute, 16 * 4, 0.5f,
                "r - - - - - - - | r - - - - - - - | r - - - - - - - | G#5 - - - B5 - - - | " +
                "r - - - - - - - | r - - - - - - - | r - - - - - - - | G#5 - - - E5 - - -", 0.45f, 0.4f);
            return s;
        }

        // ---- Chapter III: "Frostwind" (E minor, 126 bpm) ------------------------------------

        private static Score Frost()
        {
            var s = new Score(126, 32) { ReverbSize = 0.82f, DelayBeats = 0.75f, DelayFeedback = 0.3f };
            string[] A = { "Em", "C", "G", "D", "Em", "C", "Am", "B" };
            string[] B = { "C", "D", "Em", "Em", "C", "D", "B", "B" };
            var all = Concat(A, A, B, A);
            Arp(s, Inst.Arp, all, 0, 64, 5, new[] { 0, 1, 2, 3, 4, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1 }, 0.25f, 0.8f, alternatePan: true);
            Bass(s, Inst.SynthBass, all, 0, 40, "RRRORRR5", 0.7f);
            Pads(s, Inst.Pad, Concat(B, A), 16, 52, 3, 0.5f);
            Drum(s, Inst.Hat, 0, 32, "o.x.o.x.o.x.o.x.", 0.6f, pan: 0.25f);
            Drum(s, Inst.Kick, 4, 28, "x.....x...x.....", 0.75f);
            Drum(s, Inst.Snare, 8, 24, "....x.......x...", 0.75f);
            Drum(s, Inst.OpenHat, 8, 24, "..............x.", 0.5f, pan: -0.25f);
            for (int bar = 7; bar < 32; bar += 8) Drum(s, Inst.Snare, bar, 1, "............xoxx", 0.55f);
            string melA =
                "B4 - E5 - G5 - F#5 E5 | G5 - - - E5 - - - | D5 - G5 - B5 - A5 G5 | F#5 - - - D5 - - - | " +
                "B4 - E5 - G5 - B5 - | C6 - B5 - G5 - E5 - | A5 - G5 - E5 - C5 - | D#5 - - - F#5 - - -";
            string melB =
                "E5 - G5 - C6 - - - | D6 - - - A5 - F#5 - | G5 - - - B5 - E6 - | E6 - - - D6 - B5 - | " +
                "C6 - B5 - G5 - E5 - | F#5 - A5 - D6 - C6 - | B5 - - - A5 - F#5 - | D#5 - - - - - - -";
            s.Line(Inst.Lead, 8 * 4, 0.5f, melA, 0.9f, 0.05f);
            s.Line(Inst.Lead, 16 * 4, 0.5f, melB, 0.9f, 0.05f);
            s.Line(Inst.Lead, 24 * 4, 0.5f, melA, 0.9f, 0.05f);
            s.Line(Inst.Bell, 24 * 4, 0.5f, melA, 0.25f, -0.4f, 12);
            return s;
        }

        // ---- Chapter IV: "Old and Patient Light" (D Lydian, 88 bpm) -------------------------

        private static Score Peak()
        {
            var s = new Score(88, 32) { ReverbSize = 0.92f, ReverbDamp = 0.25f, DelayBeats = 0.75f, DelayFeedback = 0.4f };
            string[] A = { "D", "E/D", "Bm", "G", "D", "E/D", "G", "A" };
            string[] B = { "Bm", "G", "D", "A", "Bm", "G", "Em", "A" };
            var all = Concat(A, A, B, A);
            Pads(s, Inst.Choir, all, 0, 57, 3, 0.6f);
            Pads(s, Inst.Pad, Concat(B, A), 16, 50, 3, 0.45f);
            Arp(s, Inst.Bell, all, 0, 66, 5, new[] { 0, 1, 2, 4, 3, 2, 4, 1 }, 0.5f, 0.3f, alternatePan: true);
            Arp(s, Inst.Harp, Concat(A, B, A), 8, 50, 5, new[] { 0, 2, 4, 2, 3, 2, 4, 1 }, 0.5f, 0.4f, pan: -0.3f);
            Bass(s, Inst.Bass, all, 0, 38, "R---R-5-", 0.6f);
            foreach (int bar in new[] { 0, 8, 16, 24 }) s.Add(Inst.Timpani, 38, bar * 4, 4, 0.8f);
            for (int bar = 7; bar < 32; bar += 8)
                for (int k = 0; k < 6; k++) s.Add(Inst.Timpani, 33, bar * 4 + 2.5f + k * 0.25f, 0.25f, 0.25f + k * 0.08f);
            Drum(s, Inst.Kick, 16, 16, "x.......x.......", 0.75f);
            Drum(s, Inst.Rim, 16, 8, "....x.......x...", 0.55f);
            Drum(s, Inst.Snare, 24, 8, "....x.......x...", 0.6f);
            Drum(s, Inst.Shaker, 16, 16, "o.x.o.x.o.x.o.x.", 0.5f, pan: 0.35f);
            string melA =
                "D5 - - - E5 - F#5 - | A5 - - - - - G#5 F#5 | F#5 - - - E5 - D5 - | B4 - - - - - - - | " +
                "D5 - - - E5 - F#5 - | A5 - - - D6 - - - | B5 - - - A5 - G5 - | A5 - - - - - - -";
            string melB =
                "F#5 - - - B5 - A5 - | G5 - - - D5 - - - | F#5 - - - A5 - D6 - | C#6 - - - - - - - | " +
                "D6 - - - C#6 - B5 - | B5 - - - A5 - G5 - | G5 - - - F#5 - E5 - | E5 - - - C#5 - E5 -";
            s.Line(Inst.Flute, 8 * 4, 0.5f, melA, 0.9f, 0.1f);
            s.Line(Inst.Flute, 16 * 4, 0.5f, melB, 0.95f, 0.1f);
            s.Line(Inst.Flute, 24 * 4, 0.5f, melA, 0.95f, 0.1f);
            s.Line(Inst.Lead, 24 * 4, 0.5f, melA, 0.35f, -0.3f, -12);
            return s;
        }

        // ---- Ending: "The Bell" (D major, 66 bpm) ------------------------------------------

        private static Score Ending()
        {
            var s = new Score(66, 16) { ReverbSize = 0.9f, DelayBeats = 1f, DelayFeedback = 0.35f };
            string[] ch = { "D", "Bm", "G", "A", "D", "Bm", "Em", "A", "G", "A", "F#m", "Bm", "G", "A", "D", "D" };
            Arp(s, Inst.Keys, ch, 0, 50, 5, new[] { 0, 2, 3, 4, 3, 2, 1, 2 }, 0.5f, 0.55f, pan: -0.15f);
            Pads(s, Inst.Pad, ch, 0, 55, 3, 0.55f);
            Bass(s, Inst.Bass, ch, 0, 38, "R---5---", 0.5f);
            s.Line(Inst.Flute, 0, 0.5f,
                "D5 - - - E5 - F#5 - | A5 - - - - - G5 F#5 | G5 - - - F#5 - D5 - | E5 - - - - - - - | " +
                "D5 - - - E5 - F#5 - | A5 - - - D6 - - - | B5 - - - A5 - G5 - | A5 - - - - - - - | " +
                "B5 - - - A5 - G5 - | A5 - - - E5 - C#5 - | F#5 - - - E5 - C#5 - | D5 - - - B4 - - - | " +
                "G5 - - - F#5 - E5 - | E5 - - - F#5 - G5 - | F#5 - - - E5 - D5 - | D5 - - - - - - -", 0.85f, 0.1f);
            s.Line(Inst.Bell, 12 * 4, 0.5f,
                "B5 - - - A5 - G5 - | E5 - - - F#5 - G5 - | A5 - - - - - - - | D6 - - - - - - -", 0.25f, 0.4f);
            return s;
        }
    }
}
