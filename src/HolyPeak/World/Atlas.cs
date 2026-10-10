using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HolyPeak.Audio;

namespace HolyPeak.World
{
    /// <summary>
    /// The whole mountain: every room placed on one grid of cells (a cell is one screen, 40x23 tiles).
    /// Rooms are authored in world.txt (embedded resource):
    /// <code>
    /// @room thorn-gate at=3,5 size=2,1 region=thornwood
    /// hint1: Press {STRIKE} to strike.
    /// @map
    /// ...rows...
    /// @end
    /// </code>
    /// Leaving a room through an open edge enters whichever room owns the neighbouring cell.
    /// </summary>
    public static class Atlas
    {
        public const int CellCols = 40, CellRows = 23, TileSize = 8;
        public const int CellPxW = CellCols * TileSize, CellPxH = CellRows * TileSize;

        public static readonly RegionDef[] Regions =
        {
            new RegionDef { Id = "village", Name = "Pilgrim's Rest", Subtitle = "the village at the foot of the mountain", Style = 0, Song = SongId.Village, Darkness = 0.05f },
            new RegionDef { Id = "thornwood", Name = "Thornwood", Subtitle = "where the forest closed its hand", Style = 1, Song = SongId.Foothills, Darkness = 0.38f },
            new RegionDef { Id = "abbey", Name = "The Sunken Abbey", Subtitle = "its prayers long since fallen silent", Style = 2, Song = SongId.Ruins, Darkness = 0.55f },
            new RegionDef { Id = "frost", Name = "Frostwind Cliffs", Subtitle = "the wind never rests", Style = 3, Song = SongId.Frost, Darkness = 0.3f, Wind = 0.14f },
            new RegionDef { Id = "summit", Name = "The Holy Peak", Subtitle = "where the old bell waits", Style = 4, Song = SongId.Peak, Darkness = 0.2f, Wind = 0.08f },
        };

        public static RegionDef Region(string id)
        {
            foreach (var r in Regions) if (r.Id == id) return r;
            throw new InvalidOperationException($"unknown region '{id}'");
        }

        private static List<RoomDef> _rooms;
        private static Dictionary<string, RoomDef> _byId;
        private static Dictionary<long, RoomDef> _byCell;
        public static int MinCellX, MinCellY, MaxCellX, MaxCellY;

        /// <summary>Non-fatal authoring problems (open edges with no neighbour and similar), reported by the verifier.</summary>
        public static readonly List<string> Problems = new List<string>();

        public static IReadOnlyList<RoomDef> Rooms { get { Ensure(); return _rooms; } }

        public static RoomDef Get(string id) { Ensure(); return _byId.TryGetValue(id, out var r) ? r : null; }

        public static RoomDef AtCell(int cx, int cy)
        {
            Ensure();
            return _byCell.TryGetValue(Key(cx, cy), out var r) ? r : null;
        }

        /// <summary>The room containing a world-space pixel position, or null.</summary>
        public static RoomDef AtWorld(int wx, int wy) => AtCell(FloorDiv(wx, CellPxW), FloorDiv(wy, CellPxH));

        public static RoomDef StartRoom
        {
            get
            {
                foreach (var r in Rooms) if (r.Has('@')) return r;
                return Rooms[0];
            }
        }

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        public static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        private static void Ensure()
        {
            if (_rooms != null) return;
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("world.txt")
                          ?? throw new InvalidOperationException("world.txt resource missing");
            using var reader = new StreamReader(s);
            Load(Parse(reader.ReadToEnd()));
        }

        public static void Load(List<RoomDef> rooms)
        {
            _rooms = rooms;
            _byId = new Dictionary<string, RoomDef>();
            _byCell = new Dictionary<long, RoomDef>();
            MinCellX = MinCellY = int.MaxValue; MaxCellX = MaxCellY = int.MinValue;
            foreach (var r in rooms)
            {
                if (_byId.ContainsKey(r.Id)) throw new InvalidOperationException($"world.txt: duplicate room id {r.Id}");
                _byId[r.Id] = r;
                if (r.Cols != r.CellW * CellCols || r.Rows != r.CellH * CellRows)
                    throw new InvalidOperationException($"world.txt: room {r.Id} is {r.Cols}x{r.Rows}, expected {r.CellW * CellCols}x{r.CellH * CellRows}");
                for (int y = 0; y < r.Rows; y++)
                    if (r.Map[y].Length != r.Cols) throw new InvalidOperationException($"world.txt: room {r.Id} row {y} has length {r.Map[y].Length}");
                for (int cy = 0; cy < r.CellH; cy++)
                    for (int cx = 0; cx < r.CellW; cx++)
                    {
                        long k = Key(r.CellX + cx, r.CellY + cy);
                        if (_byCell.TryGetValue(k, out var other))
                            throw new InvalidOperationException($"world.txt: rooms {other.Id} and {r.Id} overlap at cell {r.CellX + cx},{r.CellY + cy}");
                        _byCell[k] = r;
                    }
                MinCellX = Math.Min(MinCellX, r.CellX); MinCellY = Math.Min(MinCellY, r.CellY);
                MaxCellX = Math.Max(MaxCellX, r.CellX + r.CellW - 1); MaxCellY = Math.Max(MaxCellY, r.CellY + r.CellH - 1);
                Region(r.Region);
            }
            CheckEdges();
        }

        /// <summary>Tiles that can ever be walked through (gates, cracked walls and glass can open).</summary>
        private static bool Passable(char c) => c != '#' && c != '=';

        /// <summary>True if the world pixel lies in a room tile that can be passed (now or once opened).</summary>
        public static bool OpenAt(int wx, int wy)
        {
            var r = AtWorld(wx, wy);
            if (r == null) return false;
            int tx = (wx - r.WorldX) / TileSize, ty = (wy - r.WorldY) / TileSize;
            return Passable(r.Map[ty][tx]);
        }

        /// <summary>Every open tile on a room's border must continue into an open tile of a neighbouring room.</summary>
        private static void CheckEdges()
        {
            Problems.Clear();
            foreach (var r in _rooms)
            {
                for (int y = 0; y < r.Rows; y++)
                {
                    Check(r, 0, y, -1, 0);
                    Check(r, r.Cols - 1, y, 1, 0);
                }
                for (int x = 0; x < r.Cols; x++)
                {
                    if (r.Prop("sky") == null) Check(r, x, 0, 0, -1);
                    Check(r, x, r.Rows - 1, 0, 1);
                }
            }
        }

        private static void Check(RoomDef r, int tx, int ty, int dx, int dy)
        {
            if (!Passable(r.Map[ty][tx])) return;
            int wx = r.WorldX + (tx + dx) * TileSize + 4, wy = r.WorldY + (ty + dy) * TileSize + 4;
            var n = AtWorld(wx, wy);
            if (n == null) { Problems.Add($"{r.Id}: open edge tile {tx},{ty} leads nowhere"); return; }
            int nx = (wx - n.WorldX) / TileSize, ny = (wy - n.WorldY) / TileSize;
            if (!Passable(n.Map[ny][nx]) && r.Prop("sky") == null) Problems.Add($"{r.Id}: open edge tile {tx},{ty} meets a wall in {n.Id}");
        }

        public static List<RoomDef> Parse(string text)
        {
            var rooms = new List<RoomDef>();
            RoomDef cur = null;
            List<string> rows = null;
            bool inMap = false;
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                string line = raw.TrimEnd('\r');
                if (cur == null)
                {
                    if (!line.StartsWith("@room")) continue;
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    cur = new RoomDef { Id = parts[1] };
                    for (int i = 2; i < parts.Length; i++)
                    {
                        var kv = parts[i].Split('=');
                        switch (kv[0])
                        {
                            case "at": { var p = kv[1].Split(','); cur.CellX = int.Parse(p[0]); cur.CellY = int.Parse(p[1]); break; }
                            case "size": { var p = kv[1].Split(','); cur.CellW = int.Parse(p[0]); cur.CellH = int.Parse(p[1]); break; }
                            case "region": cur.Region = kv[1]; break;
                            default: throw new InvalidOperationException($"world.txt line {lineNo}: unknown room attribute '{kv[0]}'");
                        }
                    }
                    rows = new List<string>();
                    inMap = false;
                    continue;
                }
                if (line.StartsWith("@end"))
                {
                    cur.Map = rows.ToArray();
                    rooms.Add(cur);
                    cur = null;
                    continue;
                }
                if (line.StartsWith("@map")) { inMap = true; continue; }
                if (inMap) { rows.Add(line); continue; }
                if (line.Length == 0 || line.StartsWith("//")) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) throw new InvalidOperationException($"world.txt line {lineNo}: expected 'key: value'");
                cur.Props[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }
            if (cur != null) throw new InvalidOperationException($"world.txt: room {cur.Id} is missing @end (line {lineNo})");
            return rooms;
        }
    }
}
