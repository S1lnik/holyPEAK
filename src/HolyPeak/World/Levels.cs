using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace HolyPeak.World
{
    /// <summary>
    /// The mountain. Rooms are authored in levels.txt (embedded resource) as ASCII maps:
    ///   #  terrain            =  secondary terrain     :  background wall     -  one-way platform
    ///   ^ v &lt; &gt;  spikes          C  crumbling block       S  spring            { }  wall springs
    ///   D  dash crystal       E  ember (collectible)   1 2  pulse blocks      P  player spawn
    ///   G  shrine (dash)      B  summit bell           h k  tutorial text anchors
    /// </summary>
    public static class Levels
    {
        public static readonly ChapterDef[] Chapters =
        {
            new ChapterDef { Index = 0, Title = "The Foothills", Subtitle = "Chapter I", Epigraph = "Every pilgrimage begins with a single step." },
            new ChapterDef { Index = 1, Title = "The Forgotten Ruins", Subtitle = "Chapter II", Epigraph = "Those who climbed before you left only stones." },
            new ChapterDef { Index = 2, Title = "Frostwind Cliffs", Subtitle = "Chapter III", Epigraph = "The wind never rests. Neither will you." },
            new ChapterDef { Index = 3, Title = "The Holy Peak", Subtitle = "Chapter IV", Epigraph = "The summit hums with an old and patient light." },
        };

        private static List<RoomDef> _all;

        public static IReadOnlyList<RoomDef> All
        {
            get
            {
                if (_all == null)
                {
                    using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("levels.txt")
                                  ?? throw new InvalidOperationException("levels.txt resource missing");
                    using var reader = new StreamReader(s);
                    _all = Parse(reader.ReadToEnd());
                    MapValidation.Validate(_all);
                }
                return _all;
            }
        }

        public static List<RoomDef> Parse(string text)
        {
            var rooms = new List<RoomDef>();
            RoomDef cur = null;
            List<string> rows = null;
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                string line = raw.TrimEnd('\r');
                if (cur == null)
                {
                    if (line.StartsWith("@room"))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        cur = new RoomDef { Id = parts[1] };
                        for (int i = 2; i < parts.Length; i++)
                        {
                            var kv = parts[i].Split('=');
                            if (kv[0] == "chapter") cur.Chapter = int.Parse(kv[1], CultureInfo.InvariantCulture);
                        }
                        rows = new List<string>();
                    }
                    continue;
                }
                if (line.StartsWith("@end"))
                {
                    cur.Map = rows.ToArray();
                    rooms.Add(cur);
                    cur = null;
                    continue;
                }
                if (line.StartsWith("//") || line.Length == 0) continue;
                if (line.StartsWith("hint:")) { cur.Hint = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("hint2:")) { cur.Hint2 = line.Substring(6).Trim(); continue; }
                if (line.StartsWith("wind:"))
                {
                    var p = line.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    cur.Wind = float.Parse(p[0], CultureInfo.InvariantCulture);
                    if (p.Length > 1) cur.WindPeriod = float.Parse(p[1], CultureInfo.InvariantCulture);
                    continue;
                }
                if (line.StartsWith("exit:"))
                {
                    cur.Exits = ExitSide.None;
                    foreach (var e in line.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        cur.Exits |= e switch { "top" => ExitSide.Top, "left" => ExitSide.Left, "right" => ExitSide.Right, "none" => ExitSide.None, _ => throw new InvalidOperationException($"levels.txt: bad exit '{e}' in room {cur.Id}") };
                    continue;
                }
                if (line.StartsWith("pulse:")) { cur.PulsePhase = float.Parse(line.Substring(6).Trim(), CultureInfo.InvariantCulture); continue; }
                rows.Add(line);
            }
            if (cur != null) throw new InvalidOperationException($"levels.txt: room {cur.Id} is missing @end (line {lineNo})");
            return rooms;
        }

        public static int FirstRoomOfChapter(int chapter)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Chapter == chapter) return i;
            return 0;
        }

        public static bool HasEmber(RoomDef r)
        {
            foreach (var row in r.Map) if (row.IndexOf('E') >= 0) return true;
            return false;
        }

        public static int EmberCount
        {
            get { int n = 0; foreach (var r in All) if (HasEmber(r)) n++; return n; }
        }

        public static int EmberCountInChapter(int chapter)
        {
            int n = 0;
            foreach (var r in All) if (r.Chapter == chapter && HasEmber(r)) n++;
            return n;
        }

        public static int RoomCountInChapter(int chapter)
        {
            int n = 0;
            foreach (var r in All) if (r.Chapter == chapter) n++;
            return n;
        }
    }
}
