using System.Collections.Generic;
using HolyPeak.World;

namespace HolyPeak.Verify
{
    /// <summary>
    /// The routes the mountain's design depends on, in the order a first playthrough meets them.
    /// Coordinates are tiles; a start is the tile the pilgrim's feet stand in.
    /// </summary>
    public static class Checks
    {
        private const Abilities None = Abilities.None, G = Abilities.Grips, GB = Abilities.Grips | Abilities.Bell,
                                GBK = Abilities.Grips | Abilities.Bell | Abilities.Kite;
        private const float Up = -265f;   // the boost given when entering a room from below

        private static Check C(string name, string room, int x, int y, Abilities ab, float vx = 0, float vy = 0, params string[] flags)
            => new Check { Name = name, Room = room, StartX = x, StartY = y, Ab = ab, Vx = vx, Vy = vy, Flags = flags };

        private static Check Exit(this Check c, int side, int a0 = int.MinValue, int a1 = int.MaxValue)
        { c.Goal = GoalKind.Exit; c.Side = side; c.A0 = a0; c.A1 = a1; return c; }

        private static Check Touch(this Check c, int x0, int y0, int x1, int y1)
        { c.Goal = GoalKind.Touch; c.Gx0 = x0; c.Gy0 = y0; c.Gx1 = x1; c.Gy1 = y1; return c; }

        private static Check Touch(this Check c, int x, int y) => c.Touch(x, y, x, y);

        private static Check Flag(this Check c, string flag) { c.Goal = GoalKind.Flag; c.GoalFlag = flag; return c; }

        private static Check Gate(this Check c) { c.ExpectFail = true; return c; }

        private const int U = 1, L = 2, R = 3, D = 4;

        public static List<Check> All() => new List<Check>
        {
            // ---- the first walk: village to Thornback (no abilities) ----
            C("village: east road", "v-east", 34, 20, None).Exit(L),
            C("village: square", "v-square", 78, 20, None).Exit(L),
            C("village: west gate", "v-west", 38, 20, None).Exit(L),
            C("thornwood: forest edge", "t-edge", 38, 20, None).Exit(L),
            C("thornwood: brambles to hollow", "t-brambles", 38, 20, None).Exit(D),
            C("thornwood: brambles to smithy", "t-brambles", 38, 20, None).Exit(L),
            C("thornwood: hollow to the den", "t-hollow", 51, 1, None, 0, 60).Exit(L),
            C("thornwood: hollow back up to the smithy", "t-hollow", 1, 20, None).Exit(U, 10, 15),
            C("thornwood: smithy from below", "t-smithy", 12, 22, None, 0, Up).Exit(R),
            C("thornwood: smithy down to hollow", "t-smithy", 30, 20, None).Exit(D),
            C("thornwood: hollow flask (cracked wall)", "t-hollow", 51, 1, None, 0, 60).Touch(75, 20),
            C("thornwood: the den", "t-den", 38, 20, None).Exit(L),
            C("thornwood: the anvil (grips)", "t-anvil", 38, 20, None).Touch(8, 20),
            C("gate: smithy high door needs grips", "t-smithy", 30, 20, None).Exit(L).Gate(),
            C("gate: anvil chimney needs grips", "t-anvil", 8, 20, None).Exit(U).Gate(),

            // ---- with the Iron Grips ----
            C("thornwood: up the anvil chimney", "t-anvil", 8, 20, G).Exit(U),
            C("thornwood: smith's chimney", "t-chimney", 19, 22, G, 0, Up).Exit(R),
            C("thornwood: root bridge", "t-bridge", 1, 8, G).Exit(R),
            C("thornwood: smithy from the bridge", "t-smithy", 1, 8, G).Exit(R),
            C("thornwood: brambles eastward", "t-brambles", 1, 20, G).Exit(R),
            C("thornwood: forest edge eastward", "t-edge", 1, 20, G).Exit(R),
            C("village: west gate eastward", "v-west", 1, 20, G).Exit(R),
            C("village: climb the cleft", "v-square", 1, 20, G).Exit(U, 63, 67),
            C("gate: cleft needs grips", "v-square", 1, 20, None).Exit(U, 63, 67).Gate(),
            C("thornwood: smithy up to the canopy", "t-smithy", 30, 20, G).Exit(U, 28, 33),
            C("thornwood: canopy cache", "t-canopy", 30, 22, G, 0, Up).Touch(20, 7),
            C("abbey: steps", "a-steps", 25, 22, G, 0, Up).Exit(R),
            C("abbey: gate hall", "a-gate", 1, 8, G).Exit(R),
            C("abbey: gate lever", "a-gate", 1, 8, G).Flag("lever:a-gate"),
            C("abbey: gate to the chapter house", "a-gate", 1, 8, G).Exit(U),
            C("village: down the grate (shortcut)", "v-east", 19, 1, G, 0, 60, "lever:a-gate").Exit(L),
            C("abbey: nave", "a-nave", 1, 20, G).Exit(R),
            C("abbey: down the crypt to the bell", "a-crypt", 1, 20, G).Touch(20, 43),
            C("abbey: chapter house", "a-chapter", 6, 22, G, 0, Up).Exit(R),
            C("gate: cloister needs the bell", "a-cloister", 1, 12, G).Exit(R).Gate(),

            // ---- with the Bell of Resonance ----
            C("abbey: out of the crypt", "a-crypt", 20, 43, GB).Exit(L, 16, 20),
            C("abbey: crypt to the ossuary", "a-crypt", 20, 43, GB).Exit(L, 39, 43),
            C("abbey: ossuary vessel", "a-ossuary", 38, 20, GB).Touch(7, 15),
            C("abbey: out of the ossuary", "a-ossuary", 7, 15, GB).Exit(R),
            C("abbey: nave westward", "a-nave", 78, 20, GB).Exit(L),
            C("abbey: gate hall up", "a-gate", 38, 20, GB).Exit(U),
            C("abbey: cloister", "a-cloister", 1, 12, GB).Exit(R),
            C("abbey: cloister glass floor (shortcut)", "a-cloister", 1, 12, GB).Exit(D),
            C("abbey: drop into the nave", "a-nave", 62, 1, GB, 0, 60).Exit(L),
            C("abbey: sanctum (after the abbot)", "a-sanctum", 1, 12, GB).Exit(U),
            C("abbey: bell tower", "a-tower", 19, 45, GB, 0, Up).Exit(U),
            C("thornwood: canopy vessel", "t-canopy", 30, 22, GB, 0, Up).Touch(70, 14),
            C("thornwood: out of the canopy", "t-canopy", 70, 14, GB).Exit(D),
            C("frost: windswept ledge", "f-ledge", 19, 22, GB, 0, Up).Exit(L),
            C("frost: icefall", "f-icefall", 38, 20, GB).Exit(L),
            C("frost: crevasse", "f-crevasse", 78, 15, GB).Exit(L),
            C("frost: kite cliff altar", "f-cliff", 38, 15, GB).Touch(6, 9),
            C("gate: windway needs the kite", "f-cliff", 6, 9, GB).Exit(U).Gate(),

            // ---- with the Pilgrim's Kite ----
            C("frost: ride up from the cliff", "f-cliff", 6, 9, GBK).Exit(U),
            C("frost: windway", "f-windway", 19, 22, GBK, 0, -150).Exit(R),
            C("frost: windway cache", "f-windway", 19, 22, GBK, 0, -150).Touch(12, 6),
            C("frost: wyrm's hollow", "f-hollow", 1, 20, GBK).Exit(R),
            C("frost: windward ascent", "f-ascent", 1, 20, GBK).Exit(U),
            C("gate: ascent needs the kite", "f-ascent", 1, 20, GB).Exit(U).Gate(),
            C("frost: icefall vessel", "f-icefall", 20, 20, GBK).Touch(36, 3),
            C("village: smoke to the belfry", "v-square", 40, 20, GBK).Exit(U, 7, 10),
            C("village: belfry flask", "v-belfry", 8, 22, GBK, 0, -150).Touch(31, 9),
            C("village: out of the belfry", "v-belfry", 31, 9, GBK).Exit(D),
            C("summit: high shrine", "s-shrine", 22, 22, GBK, 0, -150).Exit(L),
            C("summit: stairs of light", "s-stairs", 78, 20, GBK).Exit(U),
            C("summit: keeper's gate (after the keeper)", "s-gate", 19, 22, GBK, 0, Up).Exit(U),
            C("summit: the great bell", "s-bell", 19, 22, GBK, 0, Up).Touch(28, 20),
        };
    }
}
