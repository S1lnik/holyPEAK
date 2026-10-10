using System;
using System.Collections.Generic;

namespace HolyPeak.World
{
    public enum Tile : byte
    {
        Empty,
        Solid,      // '#'  primary terrain
        Solid2,     // '='  secondary terrain (bricks / ice / marble trim depending on region)
        Back,       // ':'  decorative background wall, not solid
        Platform,   // '-'  one-way platform
        ThornUp,    // '^'
        ThornDown,  // 'v'
        ThornLeft,  // '<'  points left, mounted on a wall to its right
        ThornRight, // '>'  points right, mounted on a wall to its left
        Breakable,  // 'x'  cracked stone, broken by strikes
        BellGlass,  // 'b'  shattered by the bell's toll
        Resonance,  // 'r'  solid for a short while after a toll
        Gate,       // '|'  opened by a lever
        ArenaDoor,  // '!'  closes while a guardian is being fought
        Crumble,    // 'z'  a ledge that falls away soon after it is stood on, and reforms
        Slab,       // 'q'  a heavy slab only the falling star breaks
    }

    [Flags]
    public enum Abilities : byte { None = 0, Grips = 1, Bell = 2, Kite = 4 }

    /// <summary>Static description of one room of the mountain, placed on the world's cell grid.</summary>
    public sealed class RoomDef
    {
        public string Id;
        public int CellX, CellY, CellW = 1, CellH = 1;
        public string Region;
        public string[] Map;
        public readonly Dictionary<string, string> Props = new Dictionary<string, string>();

        public int Cols => Map[0].Length;
        public int Rows => Map.Length;
        public int WorldX => CellX * Atlas.CellPxW;
        public int WorldY => CellY * Atlas.CellPxH;

        public string Prop(string key, string fallback = null) => Props.TryGetValue(key, out var v) ? v : fallback;

        public bool Has(char c)
        {
            foreach (var row in Map) if (row.IndexOf(c) >= 0) return true;
            return false;
        }

        public int Count(char c)
        {
            int n = 0;
            foreach (var row in Map) foreach (char ch in row) if (ch == c) n++;
            return n;
        }

        public override string ToString() => Id;
    }

    public sealed class RegionDef
    {
        public string Id;
        public string Name;
        public string Subtitle;
        public int Style;          // index into Gfx.Style.Regions
        public Audio.SongId Song;
        public float Darkness;     // 0 = fully lit, 1 = pitch black outside of lights
        public float Wind;         // ambient wind loop volume
    }
}
