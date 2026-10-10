using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HolyPeak.World;

namespace HolyPeak.Verify
{
    /// <summary>
    /// Proves the mountain can be crossed: every route the design relies on (the critical path in
    /// ability order, every secret and every shortcut) is searched over real simulation frames with
    /// creatures disabled. A path found is an exact, replayable input sequence, so "OK" is a hard
    /// guarantee. Checks marked as gates must FAIL: they prove an ability really is required.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string filter = null, replayDir = null, pathDir = null;
            int k = 5, maxNodes = 1_200_000;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--only": filter = args[++i]; break;
                    case "--replays": replayDir = args[++i]; break;
                    case "--paths": pathDir = args[++i]; break;
                    case "--k": k = int.Parse(args[++i]); break;
                    case "--max": maxNodes = int.Parse(args[++i]); break;
                    case "--walk": return Walker.Run(args[++i], k, maxNodes);
                }
            }

            foreach (var p in Atlas.Problems) Console.WriteLine("WORLD: " + p);
            int failures = Atlas.Problems.Count;
            var sw = Stopwatch.StartNew();
            var checks = Checks.All();
            Console.WriteLine($"{Atlas.Rooms.Count} rooms, {checks.Count} checks, K={k}");
            foreach (var c in checks)
            {
                if (filter != null && !c.Name.Contains(filter) && !c.Room.StartsWith(filter)) continue;
                var t0 = sw.Elapsed;
                var room = c.Build();
                var solver = new Solver(room, c, k, c.ExpectFail ? maxNodes / 4 : maxNodes);
                var path = solver.Solve(out int nodes);
                double dt = (sw.Elapsed - t0).TotalSeconds;
                bool ok = c.ExpectFail ? path == null : path != null;
                if (!ok) failures++;
                string what = path != null ? $"{path.Count,5} frames ({path.Count / 60f:0.0}s)" : "no path   ";
                Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {c.Name,-44} {what} nodes={nodes,8} ({dt:0.0}s){(c.ExpectFail ? "  [gate]" : "")}");
                if (path != null && replayDir != null)
                {
                    Directory.CreateDirectory(replayDir);
                    WriteReplay(Path.Combine(replayDir, Safe(c.Name) + ".txt"), path);
                }
                if (path != null && pathDir != null)
                {
                    Directory.CreateDirectory(pathDir);
                    File.WriteAllLines(Path.Combine(pathDir, Safe(c.Name) + ".txt"), PathOverlay(c, path));
                }
            }
            Console.WriteLine(failures == 0 ? $"ALL CHECKS PASS ({sw.Elapsed.TotalSeconds:0.0}s)" : $"{failures} FAILURE(S)");
            return failures == 0 ? 0 : 1;
        }

        private static string Safe(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_').ToArray());

        internal static void WriteReplay(string path, List<PlayerInput> inputs)
        {
            var sb = new StringBuilder();
            foreach (var i in inputs)
                sb.Append(i.MoveX).Append(' ').Append(i.MoveY).Append(' ')
                  .Append(i.Jump ? 1 : 0).Append(i.JumpPressed ? 1 : 0).Append(i.StrikePressed ? 1 : 0)
                  .Append(i.TollPressed ? 1 : 0).Append(i.Heal ? 1 : 0).Append(i.InteractPressed ? 1 : 0).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }

        private static List<string> PathOverlay(Check c, List<PlayerInput> inputs)
        {
            var room = c.Build();
            var grid = room.Def.Map.Select(r => r.ToCharArray()).ToArray();
            foreach (var inp in inputs)
            {
                room.Update(inp, 1f / 60f);
                var p = room.P.Center;
                int tx = (int)(p.X / 8), ty = (int)(p.Y / 8);
                if (ty >= 0 && ty < grid.Length && tx >= 0 && tx < grid[0].Length && (grid[ty][tx] == '.' || grid[ty][tx] == ':' || grid[ty][tx] == 'u'))
                    grid[ty][tx] = room.P.Gliding ? '~' : (room.P.Clinging ? '|' : 'o');
            }
            return grid.Select(r => new string(r)).ToList();
        }
    }

    public enum GoalKind { Exit, Touch, Flag, Bell }

    public sealed class Check
    {
        public string Name, Room;
        public int StartX, StartY;          // tile whose bottom the pilgrim's feet rest on
        public float Vx, Vy;
        public Abilities Ab;
        public string[] Flags = Array.Empty<string>();
        public GoalKind Goal;
        public int Side;                    // exit: 1 up, 2 left, 3 right, 4 down
        public int A0 = int.MinValue, A1 = int.MaxValue;   // exit: tile range along the edge
        public int Gx0, Gy0, Gx1, Gy1;      // touch: tile rect
        public string GoalFlag;
        public bool ExpectFail;

        public Room Build()
        {
            var def = Atlas.Get(Room) ?? throw new InvalidOperationException("no room " + Room);
            var flags = new HashSet<string>(Flags);
            var room = new HolyPeak.World.Room(def, flags, noEnemies: true) { RecordEvents = false };
            var p = new PlayerSim();
            p.Spawn(new Microsoft.Xna.Framework.Point(StartX * 8 + 1, StartY * 8 + 8 - PlayerSim.H), Vx < 0 ? -1 : 1);
            p.Hp = p.MaxHp = 5; p.Damage = 2; p.Ab = Ab;
            p.Speed = new Microsoft.Xna.Framework.Vector2(Vx, Vy);
            p.OnGround = Vy == 0;
            room.P = p;
            return room;
        }
    }

    public sealed class Solver
    {
        private readonly Room _room;
        private readonly Check _c;
        private readonly int _k, _maxNodes;
        private readonly int[] _dist;
        private readonly Act[] _acts;
        private readonly bool _hasResonance;

        private struct Act
        {
            public sbyte Mx, My;
            public byte Jump;      // 0 none, 1 press then hold, 2 hold
            public bool Strike, Toll, Up;
        }

        private sealed class Node
        {
            public Room.Snapshot S;
            public Node Parent;
            public int Act, G;
        }

        public Solver(Room room, Check c, int k, int maxNodes)
        {
            _room = room;
            _c = c;
            _k = k;
            _maxNodes = maxNodes;
            _hasResonance = room.Groups.Any(g => g.Kind == Tile.Resonance);
            var acts = new List<Act>();
            for (sbyte mx = -1; mx <= 1; mx++)
                for (byte j = 0; j <= 2; j++)
                    acts.Add(new Act { Mx = mx, Jump = j });
            bool thorns = room.Tiles.Any(HolyPeak.World.Room.IsThorn);
            bool platforms = room.Tiles.Any(t => t == Tile.Platform);
            bool walls = room.Groups.Any(g => g.Kind == Tile.Breakable) || room.Things.Any(t => t.Kind == ThingKind.Lever || t.Kind == ThingKind.Cache);
            if (c.Ab.HasFlag(Abilities.Bell))
                for (sbyte mx = -1; mx <= 1; mx++)
                {
                    acts.Add(new Act { Mx = mx, Jump = 2, Toll = true });
                    acts.Add(new Act { Mx = mx, Jump = 0, Toll = true });
                }
            if (thorns)
                for (sbyte mx = -1; mx <= 1; mx++) acts.Add(new Act { Mx = mx, My = 1, Strike = true, Jump = 2 });
            if (walls)
                for (sbyte mx = -1; mx <= 1; mx += 2) acts.Add(new Act { Mx = mx, Strike = true });
            if (walls) acts.Add(new Act { My = -1, Strike = true });
            // a gate proves the way is shut without a relic, so it may not pick one up on the way
            if (!c.ExpectFail && room.Things.Any(t => t.Kind == ThingKind.Altar || t.Kind == ThingKind.GreatBell)) acts.Add(new Act { Up = true });
            if (platforms) acts.Add(new Act { My = 1, Jump = 1 });
            _acts = acts.ToArray();
            _dist = Bfs();
        }

        /// <summary>Tile distance to the goal through anything that is not permanent terrain.</summary>
        private int[] Bfs()
        {
            int cols = _room.Cols, rows = _room.Rows;
            var dist = Enumerable.Repeat(int.MaxValue, cols * rows).ToArray();
            var q = new Queue<int>();
            bool Passable(int tx, int ty) => !HolyPeak.World.Room.IsSolidTile(_room.TileAt(tx, ty));
            void Seed(int tx, int ty)
            {
                if (tx < 0 || ty < 0 || tx >= cols || ty >= rows || !Passable(tx, ty)) return;
                int i = ty * cols + tx;
                if (dist[i] == 0) return;
                dist[i] = 0; q.Enqueue(i);
            }
            switch (_c.Goal)
            {
                case GoalKind.Exit:
                {
                    // only edge tiles that really continue into the neighbouring room
                    var d = _room.Def;
                    if (_c.Side == 1 || _c.Side == 4)
                        for (int tx = Math.Max(0, _c.A0); tx <= Math.Min(cols - 1, _c.A1); tx++)
                        {
                            int wy = _c.Side == 1 ? d.WorldY - 4 : d.WorldY + rows * 8 + 4;
                            if (Atlas.OpenAt(d.WorldX + tx * 8 + 4, wy)) Seed(tx, _c.Side == 1 ? 0 : rows - 1);
                        }
                    else
                        for (int ty = Math.Max(0, _c.A0); ty <= Math.Min(rows - 1, _c.A1); ty++)
                        {
                            int wx = _c.Side == 2 ? d.WorldX - 4 : d.WorldX + cols * 8 + 4;
                            if (Atlas.OpenAt(wx, d.WorldY + ty * 8 + 4)) Seed(_c.Side == 2 ? 0 : cols - 1, ty);
                        }
                    break;
                }
                case GoalKind.Touch:
                    for (int ty = _c.Gy0; ty <= _c.Gy1; ty++)
                        for (int tx = _c.Gx0; tx <= _c.Gx1; tx++) Seed(tx, ty);
                    break;
                default:
                    // flags and the bell: head for the altar, lever or bell
                    foreach (var t in _room.Things)
                        if (t.Kind == ThingKind.Altar || t.Kind == ThingKind.Lever || t.Kind == ThingKind.GreatBell)
                            Seed(t.X / 8, (t.Y - 1) / 8);
                    break;
            }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % cols, y = i / cols;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows || !Passable(nx, ny)) continue;
                    int ni = ny * cols + nx;
                    if (dist[ni] != int.MaxValue) continue;
                    dist[ni] = dist[i] + 1;
                    q.Enqueue(ni);
                }
            }
            return dist;
        }

        private int H()
        {
            var c = _room.P.Center;
            int tx = Math.Clamp((int)(c.X / 8), 0, _room.Cols - 1);
            int ty = Math.Clamp((int)(c.Y / 8), 0, _room.Rows - 1);
            int v = _dist[ty * _room.Cols + tx];
            return v == int.MaxValue ? 999 : v;
        }

        private bool Reached()
        {
            switch (_c.Goal)
            {
                case GoalKind.Exit:
                {
                    if (!_room.Exited || _room.ExitDir != _c.Side) return false;
                    var p = _room.P.Center;
                    int a = _c.Side == 1 || _c.Side == 4 ? (int)(p.X / 8) : (int)(p.Y / 8);
                    return a >= (long)_c.A0 - 1 && a <= (long)_c.A1 + 1;
                }
                case GoalKind.Touch:
                {
                    var r = new Microsoft.Xna.Framework.Rectangle(_c.Gx0 * 8, _c.Gy0 * 8, (_c.Gx1 - _c.Gx0 + 1) * 8, (_c.Gy1 - _c.Gy0 + 1) * 8);
                    return r.Intersects(_room.P.Hitbox);
                }
                case GoalKind.Bell:
                    return _room.NearThing >= 0 && _room.Things[_room.NearThing].Kind == ThingKind.GreatBell && _room.P.OnGround;
                default:
                    return _room.Flags.Contains(_c.GoalFlag);
            }
        }

        private bool Failed()
        {
            var p = _room.P;
            if (p.State != PState.Normal || _room.HazardPending || p.Hp < p.MaxHp) return true;
            if (_room.Exited && !(_c.Goal == GoalKind.Exit && Reached())) return true;
            return false;
        }

        private long Key()
        {
            ref var p = ref _room.P;
            long h = 1469598103934665603L;
            void Mix(long v) { h ^= v; h *= 1099511628211L; }
            Mix(p.X); Mix(p.Y);
            Mix((long)MathF.Round(p.Speed.X / 24f));
            Mix((long)MathF.Round(p.Speed.Y / 24f));
            Mix((p.OnGround ? 1 : 0) | (p.Clinging ? 2 : 0) | (p.Gliding ? 4 : 0) | (p.AirToll ? 8 : 0) | (p.Rising ? 16 : 0)
                | (p.ForceMoveTimer > 0 ? 32 : 0) | (p.DropT > 0 ? 64 : 0) | (p.TollCooldownT > 0 ? 128 : 0) | (p.Attack != Strike.None ? 256 : 0));
            if (p.Clinging) Mix((long)(Math.Min(p.ClingTimer, 0.4f) / 0.1f));
            for (int i = 0; i < _room.Groups.Length; i++)
            {
                var g = _room.Groups[i];
                Mix(g.Gone ? 1 : 0);
                if (g.Kind == Tile.Resonance) Mix((long)(g.Timer / 0.25f) * 2 + (g.Pending ? 1 : 0));
                if (g.Kind == Tile.Breakable) Mix(g.Hp);
            }
            for (int i = 0; i < _room.Things.Length; i++) Mix(_room.Things[i].Used ? 1 : 0);
            return h;
        }

        public List<PlayerInput> Solve(out int expanded)
        {
            expanded = 0;
            var start = _room.Save();
            var open = new PriorityQueue<Node, int>();
            var seen = new HashSet<long>();
            seen.Add(Key());
            open.Enqueue(new Node { S = start, Act = -1 }, H() * 8);
            const float dt = 1f / 60f;
            while (open.Count > 0 && expanded < _maxNodes)
            {
                var node = open.Dequeue();
                expanded++;
                for (int a = 0; a < _acts.Length; a++)
                {
                    var act = _acts[a];
                    _room.Load(node.S);
                    if (act.Toll && _room.P.TollCooldownT > 0) continue;
                    bool fail = false, done = false;
                    for (int f = 0; f < _k; f++)
                    {
                        _room.Update(MakeInput(act, f), dt);
                        if (Reached()) { done = true; break; }
                        if (Failed()) { fail = true; break; }
                    }
                    if (fail) continue;
                    var child = new Node { Parent = node, Act = a, G = node.G + _k };
                    if (done) return Reconstruct(child, start);
                    if (!seen.Add(Key())) continue;
                    child.S = _room.Save();
                    open.Enqueue(child, child.G + H() * 8);
                }
            }
            return null;
        }

        private PlayerInput MakeInput(Act act, int f) => new PlayerInput
        {
            MoveX = act.Mx,
            MoveY = act.My,
            Jump = act.Jump != 0,
            JumpPressed = act.Jump == 1 && f == 0,
            StrikePressed = act.Strike && f == 0,
            TollPressed = act.Toll && f == 0,
            InteractPressed = act.Up && f == 0,
        };

        private List<PlayerInput> Reconstruct(Node end, Room.Snapshot start)
        {
            var acts = new List<int>();
            for (var n = end; n.Parent != null; n = n.Parent) acts.Add(n.Act);
            acts.Reverse();
            var inputs = new List<PlayerInput>();
            _room.Load(start);
            foreach (int a in acts)
                for (int f = 0; f < _k; f++)
                {
                    var inp = MakeInput(_acts[a], f);
                    inputs.Add(inp);
                    _room.Update(inp, 1f / 60f);
                    if (Reached()) return inputs;
                }
            return inputs;
        }
    }
}
