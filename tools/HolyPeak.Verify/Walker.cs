using System;
using System.Collections.Generic;
using System.Diagnostics;
using HolyPeak.World;

namespace HolyPeak.Verify
{
    /// <summary>
    /// Walks the whole critical path in one go: from the east road, through every region, to the
    /// great bell, solving each room in turn and crossing between rooms with the game's own
    /// transition code. The result is one replay the game can play from start to finish
    /// (with --peaceful, since creatures are left out of the search).
    /// </summary>
    public static class Walker
    {
        private const int U = 1, L = 2, R = 3, D = 4;

        private static readonly (string room, Func<Check> goal)[] Route =
        {
            ("v-east", () => Exit(L)), ("v-square", () => Exit(L)), ("v-west", () => Exit(L)), ("t-edge", () => Exit(L)),
            ("t-brambles", () => Exit(D)), ("t-hollow", () => Exit(L)), ("t-den", () => Exit(L)),
            ("t-anvil", () => Flag("ab:grips")), ("t-anvil", () => Exit(U)), ("t-chimney", () => Exit(R)), ("t-bridge", () => Exit(R)),
            ("t-smithy", () => Exit(R)), ("t-brambles", () => Exit(R)), ("t-edge", () => Exit(R)), ("v-west", () => Exit(R)),
            ("v-square", () => Exit(U, 63, 67)), ("a-steps", () => Exit(R)), ("a-gate", () => Exit(R)), ("a-nave", () => Exit(R)),
            ("a-crypt", () => Flag("ab:bell")), ("a-crypt", () => Exit(L, 16, 20)), ("a-nave", () => Exit(L)), ("a-gate", () => Exit(U)),
            ("a-chapter", () => Exit(R)), ("a-cloister", () => Exit(R)), ("a-sanctum", () => Exit(U)), ("a-tower", () => Exit(U)),
            ("f-ledge", () => Exit(L)), ("f-icefall", () => Exit(L)), ("f-crevasse", () => Exit(L)),
            ("f-cliff", () => Flag("ab:kite")), ("f-cliff", () => Exit(U)), ("f-windway", () => Exit(R)), ("f-hollow", () => Exit(R)),
            ("f-ascent", () => Exit(U, 60, 65)), ("s-shrine", () => Exit(L)), ("s-stairs", () => Exit(U, 17, 22)), ("s-gate", () => Big(Exit(U))),
            ("s-bell", () => Bell()),
        };

        private static Check Exit(int side, int a0 = int.MinValue, int a1 = int.MaxValue)
            => new Check { Goal = GoalKind.Exit, Side = side, A0 = a0, A1 = a1 };
        private static Check Flag(string f) => new Check { Goal = GoalKind.Flag, GoalFlag = f };
        private static Check Bell() => new Check { Goal = GoalKind.Bell };
        private static Check Big(Check c) { c.Budget = 4_000_000; return c; }

        public static int Run(string outPath, int k, int maxNodes)
        {
            var sw = Stopwatch.StartNew();
            var flags = new HashSet<string>();
            var room = new Room(Atlas.StartRoom, flags, noEnemies: true) { RecordEvents = false };
            var carry = new PlayerSim { Hp = 5, MaxHp = 5, Grace = 99, MaxGrace = 99, Damage = 4, Facing = 1 };
            Travel.Arrive(room, carry, null, Microsoft.Xna.Framework.Vector2.Zero, room.StartPoint);
            var all = new List<PlayerInput>();
            foreach (var (id, make) in Route)
            {
                if (room.Def.Id != id)
                {
                    Console.WriteLine($"WALK FAIL: expected to be in {id} but in {room.Def.Id}");
                    return 1;
                }
                var goal = make();
                goal.Name = id; goal.Room = id; goal.Ab = room.P.Ab; goal.Spells = room.P.Sp; goal.Charms = room.P.Ch;
                var solver = new Solver(room, goal, k, goal.Budget > 0 ? goal.Budget : maxNodes);
                var path = solver.Solve(out int nodes);
                if (path == null)
                {
                    Console.WriteLine($"WALK FAIL in {id} ({goal.Goal}) after {nodes} nodes");
                    Program.WriteReplay(outPath, all);
                    return 1;
                }
                all.AddRange(path);
                Console.WriteLine($"  {id,-12} {goal.Goal,-5} {path.Count,5} frames  total {all.Count / 60f,6:0.0}s  nodes={nodes}");
                if (goal.Goal == GoalKind.Bell)
                {
                    var ring = new PlayerInput { InteractPressed = true };
                    all.Add(ring);
                    break;
                }
                if (goal.Goal == GoalKind.Exit)
                {
                    var next = Travel.Next(room, out var local, out var speed);
                    var p = room.P;
                    room = new Room(next, flags, noEnemies: true) { RecordEvents = false };
                    Travel.Arrive(room, p, local, speed, null);
                }
            }
            Program.WriteReplay(outPath, all);
            Console.WriteLine($"WALK OK: {all.Count} frames ({all.Count / 60f:0.0}s of play) in {sw.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }
    }
}
