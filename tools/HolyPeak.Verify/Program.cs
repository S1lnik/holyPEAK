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
    /// Proves every room in the game can be completed by searching over real simulation frames.
    /// Any path found is an exact, replayable input sequence (the dedupe only prunes), so a
    /// "SOLVED" result is a hard guarantee. Coarser input granularity (larger K) means the
    /// solution needs less precise timing, which is a rough proxy for human difficulty.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string filter = null;
            string replayDir = null;
            int k = 4;
            int maxNodes = 1_500_000;
            bool embers = true;
            bool ascii = false;
            string pathDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--room": filter = args[++i]; break;
                    case "--replays": replayDir = args[++i]; break;
                    case "--k": k = int.Parse(args[++i]); break;
                    case "--max": maxNodes = int.Parse(args[++i]); break;
                    case "--no-embers": embers = false; break;
                    case "--ascii": ascii = true; break;
                    case "--paths": pathDir = args[++i]; break;
                }
            }

            var rooms = Levels.All;
            int failures = 0;
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"Verifying {rooms.Count} rooms ({Levels.EmberCount} embers), K={k}");
            for (int idx = 0; idx < rooms.Count; idx++)
            {
                var def = rooms[idx];
                if (filter != null && !def.Id.StartsWith(filter)) continue;
                bool dash = def.Chapter >= 1 && !RoomHasShrine(def);
                var room = new Room(def, dash) { RecordEvents = false };
                var solver = new Solver(room, k, maxNodes);

                var t0 = sw.Elapsed;
                var start = room.Save();
                var exitPath = solver.Solve(start, Goal.Exit, out var endSnap, out int nodes);
                var dt = (sw.Elapsed - t0).TotalSeconds;
                string res = exitPath != null
                    ? $"exit OK  {exitPath.Count,5} frames ({exitPath.Count / 60f:0.0}s) nodes={nodes,8}"
                    : $"exit FAIL nodes={nodes}";
                if (exitPath == null) failures++;

                string emberRes = "";
                List<PlayerInput> emberPath = null;
                if (embers && room.HasEmber)
                {
                    room.Load(start);
                    emberPath = solver.Solve(start, Goal.EmberExit, out _, out int n1);
                    if (emberPath != null) emberRes = $" | ember OK {emberPath.Count,5} frames nodes={n1}";
                    else { emberRes = $" | ember FAIL ({n1})"; failures++; }
                }
                Console.WriteLine($"[{idx,2}] {def.Id,-5} {res}{emberRes}  ({dt:0.0}s)");

                if (replayDir != null)
                {
                    Directory.CreateDirectory(replayDir);
                    if (exitPath != null) WriteReplay(Path.Combine(replayDir, def.Id + ".txt"), exitPath);
                    if (emberPath != null) WriteReplay(Path.Combine(replayDir, def.Id + "-ember.txt"), emberPath);
                }
                if ((ascii || pathDir != null) && exitPath != null)
                {
                    var lines = PathOverlay(def, dash, emberPath ?? exitPath);
                    if (ascii) foreach (var l in lines) Console.WriteLine("   " + l);
                    if (pathDir != null) { Directory.CreateDirectory(pathDir); File.WriteAllLines(Path.Combine(pathDir, def.Id + ".txt"), lines); }
                }
            }
            Console.WriteLine(failures == 0 ? $"ALL ROOMS SOLVABLE ({sw.Elapsed.TotalSeconds:0.0}s)" : $"{failures} FAILURE(S)");
            return failures == 0 ? 0 : 1;
        }

        private static bool RoomHasShrine(RoomDef def) => def.Map.Any(r => r.Contains('G'));

        private static void WriteReplay(string path, List<PlayerInput> inputs)
        {
            var sb = new StringBuilder();
            foreach (var i in inputs)
                sb.Append(i.MoveX).Append(' ').Append(i.MoveY).Append(' ')
                  .Append(i.Jump ? 1 : 0).Append(i.JumpPressed ? 1 : 0).Append(i.DashPressed ? 1 : 0).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }

        private static List<string> PathOverlay(RoomDef def, bool dash, List<PlayerInput> inputs)
        {
            var room = new Room(def, dash) { RecordEvents = false };
            var grid = def.Map.Select(r => r.ToCharArray()).ToArray();
            foreach (var inp in inputs)
            {
                room.Update(inp, 1f / 60f);
                var c = room.P.Center;
                int tx = (int)(c.X / 8), ty = (int)(c.Y / 8);
                if (ty >= 0 && ty < grid.Length && tx >= 0 && tx < grid[0].Length && (grid[ty][tx] == '.' || grid[ty][tx] == ':'))
                    grid[ty][tx] = room.P.State == PState.Dash ? '*' : 'o';
            }
            return grid.Select(r => new string(r)).ToList();
        }
    }

    public enum Goal { Exit, EmberExit }

    public sealed class Solver
    {
        private readonly Room _room;
        private readonly int _k;
        private readonly int _maxNodes;
        private readonly int[] _distExit;
        private readonly int[] _distEmber;
        private readonly bool _timeMatters;

        private struct Act
        {
            public sbyte Mx, My;
            public byte Jump; // 0 none, 1 press, 2 hold
            public bool Dash;
        }

        private static readonly Act[] Acts = BuildActs();

        private static Act[] BuildActs()
        {
            var list = new List<Act>();
            for (sbyte mx = -1; mx <= 1; mx++)
                for (byte j = 0; j <= 2; j++)
                    list.Add(new Act { Mx = mx, Jump = j });
            for (sbyte mx = -1; mx <= 1; mx++)
                for (sbyte my = -1; my <= 1; my++)
                {
                    if (mx == 0 && my == 0) continue;
                    list.Add(new Act { Mx = mx, My = my, Dash = true });
                }
            // fast-fall / drop
            list.Add(new Act { Mx = 0, My = 1 });
            return list.ToArray();
        }

        private sealed class Node
        {
            public Room.Snapshot S;
            public Node Parent;
            public int Act;
            public int G;
        }

        public Solver(Room room, int k, int maxNodes)
        {
            _room = room;
            _k = k;
            _maxNodes = maxNodes;
            _timeMatters = room.Pulses.Length > 0 || (room.Def.Wind != 0 && room.Def.WindPeriod > 0);
            _distExit = Bfs(room, exit: true);
            _distEmber = room.HasEmber ? Bfs(room, exit: false) : null;
        }

        private static int[] Bfs(Room room, bool exit)
        {
            int cols = room.Cols, rows = room.Rows;
            var dist = Enumerable.Repeat(int.MaxValue, cols * rows).ToArray();
            var q = new Queue<int>();
            bool Passable(int tx, int ty) => !Room.IsSolidTile(room.TileAt(tx, ty));
            if (exit)
            {
                var ex = room.Def.Exits;
                if ((ex & ExitSide.Top) != 0)
                    for (int tx = 0; tx < cols; tx++) if (Passable(tx, 0)) { dist[tx] = 0; q.Enqueue(tx); }
                for (int ty = 0; ty < rows; ty++)
                {
                    if ((ex & ExitSide.Left) != 0 && Passable(0, ty)) { dist[ty * cols] = 0; q.Enqueue(ty * cols); }
                    if ((ex & ExitSide.Right) != 0 && Passable(cols - 1, ty)) { dist[ty * cols + cols - 1] = 0; q.Enqueue(ty * cols + cols - 1); }
                }
                if (room.HasBell)
                {
                    int bi = (room.BellRect.Center.Y / 8) * cols + room.BellRect.Center.X / 8;
                    dist[bi] = 0; q.Enqueue(bi);
                }
            }
            else
            {
                int ei = ((int)room.Ember.Y / 8) * cols + (int)room.Ember.X / 8;
                dist[ei] = 0; q.Enqueue(ei);
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

        private int H(Goal goal)
        {
            var c = _room.P.Center;
            int tx = Math.Clamp((int)(c.X / 8), 0, _room.Cols - 1);
            int ty = Math.Clamp((int)(c.Y / 8), 0, _room.Rows - 1);
            int i = ty * _room.Cols + tx;
            if (goal == Goal.EmberExit && _room.Ember.State == 0)
            {
                int ei = ((int)_room.Ember.Y / 8) * _room.Cols + (int)_room.Ember.X / 8;
                int a = _distEmber[i], b = _distExit[ei];
                return a == int.MaxValue || b == int.MaxValue ? 999 : a + b;
            }
            int v = _distExit[i];
            return v == int.MaxValue ? 999 : v;
        }

        private bool Reached(Goal goal)
        {
            bool exited = _room.Exited || _room.BellRung;
            if (goal == Goal.Exit) return exited;
            return exited && _room.Ember.State == 1;
        }

        private long Key()
        {
            ref var p = ref _room.P;
            long h = 1469598103934665603L;
            void Mix(long v) { h ^= v; h *= 1099511628211L; }
            Mix(p.X); Mix(p.Y);
            Mix((long)MathF.Round(p.Speed.X / 20f));
            Mix((long)MathF.Round(p.Speed.Y / 20f));
            Mix((long)p.State);
            Mix(p.Dashes);
            Mix(p.VarJumpTimer > 0 ? 1 : 0);
            Mix(p.ForceMoveXTimer > 0 ? 1 : 0);
            Mix((long)(Math.Max(0, p.DashTimer) / 0.05f));
            Mix(p.DashAbility ? 1 : 0);
            for (int i = 0; i < _room.Crumbles.Length; i++) Mix(_room.Crumbles[i].State);
            for (int i = 0; i < _room.Crystals.Length; i++) Mix(_room.Crystals[i].Active ? 1 : 0);
            Mix(_room.HasEmber ? _room.Ember.State : 0);
            if (_timeMatters)
            {
                float period = _room.Pulses.Length > 0 ? _room.Def.PulsePhase * 2 : _room.Def.WindPeriod;
                Mix((long)((_room.Time % period) / 0.1f));
            }
            return h;
        }

        public List<PlayerInput> Solve(Room.Snapshot start, Goal goal, out Room.Snapshot end, out int expanded)
        {
            end = null;
            expanded = 0;
            var open = new PriorityQueue<Node, int>();
            var seen = new HashSet<long>();
            _room.Load(start);
            seen.Add(Key());
            open.Enqueue(new Node { S = start, Act = -1, G = 0 }, H(goal) * 9);
            const float dt = 1f / 60f;

            while (open.Count > 0 && expanded < _maxNodes)
            {
                var node = open.Dequeue();
                expanded++;
                for (int a = 0; a < Acts.Length; a++)
                {
                    var act = Acts[a];
                    _room.Load(node.S);
                    if (act.Dash && !_room.P.CanDash) continue;
                    bool dead = false, done = false;
                    for (int f = 0; f < _k; f++)
                    {
                        var inp = MakeInput(act, f);
                        _room.Update(inp, dt);
                        if (_room.P.State == PState.Dead) { dead = true; break; }
                        if (Reached(goal)) { done = true; break; }
                        if (_room.Exited || _room.BellRung) { dead = true; break; } // left without the ember
                    }
                    if (dead) continue;
                    var child = new Node { Parent = node, Act = a, G = node.G + _k };
                    if (done)
                    {
                        child.S = _room.Save();
                        end = child.S;
                        return Reconstruct(child, start, goal);
                    }
                    if (!seen.Add(Key())) continue;
                    child.S = _room.Save();
                    open.Enqueue(child, child.G + H(goal) * 9);
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
            DashPressed = act.Dash && f == 0,
        };

        private List<PlayerInput> Reconstruct(Node end, Room.Snapshot start, Goal goal)
        {
            var acts = new List<int>();
            for (var n = end; n.Parent != null; n = n.Parent) acts.Add(n.Act);
            acts.Reverse();
            // Re-simulate to trim the final macro to the exact frame the goal was reached.
            var inputs = new List<PlayerInput>();
            _room.Load(start);
            foreach (int a in acts)
            {
                for (int f = 0; f < _k; f++)
                {
                    var inp = MakeInput(Acts[a], f);
                    inputs.Add(inp);
                    _room.Update(inp, 1f / 60f);
                    if (Reached(goal)) return inputs;
                }
            }
            return inputs;
        }
    }
}
