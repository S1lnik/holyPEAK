using System;
using System.Collections.Generic;

namespace HolyPeak.World
{
    public enum Tile : byte
    {
        Empty,
        Solid,      // '#'  primary terrain
        Solid2,     // '='  secondary terrain (bricks / ice / marble trim depending on chapter)
        Back,       // ':'  decorative background wall, not solid
        JumpThru,   // '-'  one-way platform
        SpikeUp,    // '^'
        SpikeDown,  // 'v'
        SpikeLeft,  // '<'  points left, mounted on a wall to its right
        SpikeRight, // '>'  points right, mounted on a wall to its left
    }

    [Flags]
    public enum ExitSide : byte { None = 0, Top = 1, Left = 2, Right = 4 }

    /// <summary>Static description of one screen of the mountain.</summary>
    public sealed class RoomDef
    {
        public string Id;
        public int Chapter;
        public string[] Map;
        /// <summary>Optional tutorial text shown at the 'h' marker. Tokens {JUMP} {DASH} {MOVE} are replaced with bindings.</summary>
        public string Hint;
        /// <summary>Second optional tutorial text shown at the 'k' marker.</summary>
        public string Hint2;
        /// <summary>Horizontal wind in px/s (negative = blows left). 0 = calm.</summary>
        public float Wind;
        /// <summary>If &gt; 0 the wind fades in and out with this period (seconds).</summary>
        public float WindPeriod;
        /// <summary>Edges the player may leave through. All other edges (except the bottom pit) are invisible walls.</summary>
        public ExitSide Exits = ExitSide.Right;
        /// <summary>Seconds each pulse group stays solid.</summary>
        public float PulsePhase = 1.4f;

        public int Cols => Map[0].Length;
        public int Rows => Map.Length;
    }

    public sealed class ChapterDef
    {
        public int Index;
        public string Title;
        public string Subtitle;
        public string Epigraph;
    }

    public static class MapValidation
    {
        public static void Validate(IReadOnlyList<RoomDef> rooms)
        {
            foreach (var r in rooms)
            {
                int w = r.Map[0].Length;
                int spawns = 0;
                for (int y = 0; y < r.Map.Length; y++)
                {
                    if (r.Map[y].Length != w)
                        throw new InvalidOperationException($"Room {r.Id}: row {y} has length {r.Map[y].Length}, expected {w}");
                    foreach (char c in r.Map[y]) if (c == 'P') spawns++;
                }
                if (spawns != 1) throw new InvalidOperationException($"Room {r.Id}: expected exactly one spawn 'P', found {spawns}");
                if (w < 40 || r.Map.Length < 23) throw new InvalidOperationException($"Room {r.Id}: room must be at least 40x23 (is {w}x{r.Map.Length})");
            }
        }
    }
}
