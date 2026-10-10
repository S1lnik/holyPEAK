using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HolyPeak.Gfx
{
    /// <summary>Hand-authored pixel sprites, stored as text and turned into one texture atlas at startup.</summary>
    public static class Sprites
    {
        public static Texture2D Sheet;
        /// <summary>Same layout as <see cref="Sheet"/> but every opaque pixel is white (for flashes and silhouettes).</summary>
        public static Texture2D SheetWhite;
        public static readonly Dictionary<string, Rectangle> Frames = new Dictionary<string, Rectangle>();

        private static readonly List<(string name, int w, int h, Color[] data)> Images = new List<(string, int, int, Color[])>();

        // ---- the pilgrim ------------------------------------------------------------------

        public static readonly Dictionary<char, Color> PilgrimPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x1b1630),
            ['h'] = G.Hex(0xe2c68c), ['H'] = G.Hex(0xb8955a), ['d'] = G.Hex(0x7d6038),
            ['f'] = G.Hex(0x0d0a14), ['e'] = G.Hex(0xffefb0),
            ['r'] = G.Hex(0x3c4d7e), ['R'] = G.Hex(0x29355e), ['q'] = G.Hex(0x5a6fa6),
            ['t'] = G.Hex(0xd4a85a),
            ['b'] = G.Hex(0x4f3428), ['B'] = G.Hex(0x33211a),
        };

        private static readonly string[] Hood =
        {
            ".oo........",
            "ohhoooo....",
            "ohhhhhhoo..",
            ".ohhhhhhHo.",
            ".ohhHffffo.",
            ".ohHffefeo.",
            ".odHfffffo.",
            "..odHHHHo..",
        };

        private static readonly string[] HoodBlink =
        {
            ".oo........",
            "ohhoooo....",
            "ohhhhhhoo..",
            ".ohhhhhhHo.",
            ".ohhHffffo.",
            ".ohHfffffo.",
            ".odHfffffo.",
            "..odHHHHo..",
        };

        private static readonly string[] HoodUp =   // looking up (strike up / glide)
        {
            "...oo......",
            "..ohhooo...",
            ".ohhhhhhoo.",
            ".ohhHffffo.",
            ".ohHffefeo.",
            ".ohHfffffo.",
            ".odHHHHHo..",
            "..ooooooo..",
        };

        private static readonly string[] Body =
        {
            ".odhhhhhdo.",
            "odhhhhhhhdo",
            "oqrrrrrrrRo",
            "oqrtttttrRo",
            "oqrrrrrrRRo",
            ".oqrrrrRRo.",
            ".ooRRRRRoo.",
        };

        private static readonly string[] BodyFlare =
        {
            ".odhhhhhdo.",
            "odhhhhhhhdo",
            "oqrrrrrrrRo",
            "oqrtttttrRo",
            "oqrrrrrrRRo",
            "oqrrrrrrRRRo",
            "ooRRRRRRRoo.",
        };

        private static readonly string[] BodyCrouch =
        {
            ".odhhhhhdo..",
            "odhhhhhhhdo.",
            "oqrtttttrRo.",
            "oqrrrrrrRRRo",
            "ooRRRRRRRRoo",
        };

        private static readonly Dictionary<string, string[]> Legs = new Dictionary<string, string[]>
        {
            ["stand"] = new[] { "..obo.obo..", "..obo.obo..", "..ooo.ooo.." },
            ["run0"] = new[] { ".obo...obo.", "obo....oBo.", "oo.....ooo." },
            ["run1"] = new[] { "...oboobo..", "...oBobo...", "...oooo...." },
            ["run2"] = new[] { ".obo...obo.", ".oBo....obo", ".ooo.....oo" },
            ["run3"] = new[] { "...obooBo..", "....oboo...", "....ooo...." },
            ["jump"] = new[] { "..obobo....", "...oBo.....", "...ooo....." },
            ["fall"] = new[] { ".obo...obo.", ".oo.....oo.", "..........." },
            ["cling"] = new[] { "....obo.obo", "....oBo.oo.", "....ooo...." },
            ["kneel"] = new[] { ".obbbbo.obo", ".oooooo.ooo" },
        };

        private static void AddPilgrim(string name, string[] head, string[] body, string legs, int headY = 0, int bodyY = 7, int legY = 13, int headX = 0)
        {
            const int w = 12, h = 16;
            var data = new Color[w * h];
            void Blit(string[] rows, int ox, int oy)
            {
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        char ch = rows[y][x];
                        if (ch == '.') continue;
                        int px = x + ox, py = y + oy;
                        if (px < 0 || py < 0 || px >= w || py >= h) continue;
                        data[py * w + px] = PilgrimPal[ch];
                    }
            }
            Blit(Legs[legs], 0, legY);
            Blit(body, 0, bodyY);
            Blit(head, headX, headY);
            Images.Add((name, w, h, data));
        }

        // ---- palettes for creatures ---------------------------------------------------------

        private static readonly Dictionary<char, Color> BugPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x120c10), ['h'] = G.Hex(0x3f5a2e), ['H'] = G.Hex(0x648a3e), ['d'] = G.Hex(0x26361e),
            ['T'] = G.Hex(0xe86a8a), ['e'] = G.Hex(0xffd84a), ['l'] = G.Hex(0x2a1c18),
        };

        private static readonly Dictionary<char, Color> ToadPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x140c14), ['g'] = G.Hex(0x6a4a7a), ['G'] = G.Hex(0x8e6aa2), ['d'] = G.Hex(0x45304f),
            ['b'] = G.Hex(0xd8c0a0), ['e'] = G.Hex(0xfff070), ['m'] = G.Hex(0xe06060),
        };

        private static readonly Dictionary<char, Color> WispPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x1a2a50), ['a'] = G.Hex(0x7fd8ff), ['b'] = G.Hex(0x3a8ad0), ['w'] = G.Hex(0xe8fbff), ['e'] = G.Hex(0x10183a),
        };

        private static readonly Dictionary<char, Color> KnightPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x14101a), ['a'] = G.Hex(0x8a8aa0), ['A'] = G.Hex(0xb8b8cc), ['d'] = G.Hex(0x55556a),
            ['s'] = G.Hex(0xa8743a), ['S'] = G.Hex(0xd9a456), ['k'] = G.Hex(0x6a4420), ['e'] = G.Hex(0xff6a4a),
            ['c'] = G.Hex(0x6a2a3a), ['p'] = G.Hex(0xcfd2e0),
        };

        private static readonly Dictionary<char, Color> PodPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x101410), ['g'] = G.Hex(0x5a8a3a), ['G'] = G.Hex(0x8cc05a), ['d'] = G.Hex(0x355a26),
            ['m'] = G.Hex(0x3a1020), ['y'] = G.Hex(0xe8ff70), ['p'] = G.Hex(0xc05a8a),
        };

        private static readonly Dictionary<char, Color> IcePal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x0c1426), ['i'] = G.Hex(0x9fdcff), ['I'] = G.Hex(0xe0f6ff), ['d'] = G.Hex(0x4a86b8),
            ['e'] = G.Hex(0x3a1050), ['l'] = G.Hex(0x2a3a5a),
        };

        public static void Init()
        {
            Images.Clear();
            Frames.Clear();

            // The pilgrim
            AddPilgrim("idle0", Hood, Body, "stand");
            AddPilgrim("idle1", Hood, Body, "stand", headY: 1, bodyY: 7);
            AddPilgrim("blink", HoodBlink, Body, "stand");
            AddPilgrim("run0", Hood, Body, "run0", headX: 1);
            AddPilgrim("run1", Hood, Body, "run1", headY: 1, bodyY: 8, headX: 1);
            AddPilgrim("run2", Hood, Body, "run2", headX: 1);
            AddPilgrim("run3", Hood, Body, "run3", headY: 1, bodyY: 8, headX: 1);
            AddPilgrim("jump", Hood, Body, "jump");
            AddPilgrim("fall", Hood, BodyFlare, "fall");
            AddPilgrim("cling", Hood, Body, "cling", headX: 1);
            AddPilgrim("glide", HoodUp, BodyFlare, "fall");
            AddPilgrim("lookup", HoodUp, Body, "stand");
            AddPilgrim("kneel", Hood, BodyCrouch, "kneel", headY: 3, bodyY: 10, legY: 14);
            AddPilgrim("hurt", HoodBlink, BodyFlare, "fall", headX: -1);

            // ---- creatures ----
            Add("crawler0", BugPal,
                "...T...T...T..",
                "..oToooToooTo.",
                ".ohhhhhhhhhhho",
                "ohHHhhhhhhhhho",
                "ohHHhhhhhhhheo",
                "ohhhhhhhhhhhho",
                ".oddddddddddo.",
                ".ol.ol..ol.ol.",
                "..............");
            Add("crawler1", BugPal,
                "...T...T...T..",
                "..oToooToooTo.",
                ".ohhhhhhhhhhho",
                "ohHHhhhhhhhhho",
                "ohHHhhhhhhhheo",
                "ohhhhhhhhhhhho",
                ".oddddddddddo.",
                "..ol.ol..ol.ol",
                "..............");

            Add("hopper0", ToadPal,
                "............",
                "............",
                "...oo..oo...",
                "..oeGooGeo..",
                ".oGGGGGGGGo.",
                "oGGggggggGGo",
                "oGgggggggggo",
                "ogbbbbbbbbgo",
                "ogbbbmmbbbgo",
                ".oddddddddo.",
                "oddo....oddo");
            Add("hopper1", ToadPal,   // crouch (the tell)
                "............",
                "............",
                "............",
                "............",
                "...oo..oo...",
                "..oeGooGeo..",
                ".oGGGGGGGGGo",
                "oGGggggggggGo",
                "ogbbbbbbbbbgo",
                "oddddddddddo.",
                "oddddo.odddo");
            Add("hopper2", ToadPal,   // airborne
                "...oo..oo...",
                "..oeGooGeo..",
                ".oGGGGGGGGo.",
                "oGGggggggGGo",
                "oGgggggggggo",
                "ogbbbbbbbbgo",
                "ogbbmmmmbbgo",
                ".oddddddddo.",
                ".od.o..o.do.",
                "od........do",
                "o..........o");

            Add("wisp0", WispPal,
                "....o......",
                "...oao.....",
                "...oaao.o..",
                "..oaaaaoao.",
                ".oaawwwaao.",
                ".oawwwwwao.",
                "oaweewweao.",
                "oawwwwwwao.",
                ".oawwwwao..",
                "..obaaabo..",
                "...obbbo...",
                "....ooo....");
            Add("wisp1", WispPal,
                "......o....",
                ".o...oao...",
                "oao.oaao...",
                ".oaaaaaao..",
                ".oaawwwaao.",
                ".oawwwwwao.",
                "oaweewweao.",
                "oawwwwwwao.",
                ".oawwwwao..",
                "..obaaabo..",
                "...obbbo...",
                "....ooo....");
            Add("wisp2", WispPal,   // the tell: eyes narrowed, flame flaring
                "..o...o....",
                ".oao.oao...",
                ".oaaoaaao..",
                ".oaaaaaaao.",
                "oaawwwwwaao",
                "oawwwwwwwao",
                "oaweewweeao",
                "oawwwwwwwao",
                ".oawwwwwao.",
                "..obaaaabo.",
                "...obbbbo..",
                "....oooo...");

            Add("sentinel0", KnightPal,
                "....oooo......",
                "...oAAAAo.....",
                "..oAaaaaAo....",
                "..oaoeoeao....",
                "..oaaaaaao....",
                "...oddddo.....",
                ".ooccccccoo...",
                "oSScaaaaacao..",
                "oSScaAAaacao..",
                "oSskaaaaacao..",
                "oSskcaaaacao..",
                "oSskccccccoo..",
                "oSssooddoo....",
                "oSsso.ddo.....",
                ".oooo.dd......",
                "....oadoao....",
                "...oado.oado..",
                "...ooo...ooo..");
            Add("sentinel1", KnightPal,
                "....oooo......",
                "...oAAAAo.....",
                "..oAaaaaAo....",
                "..oaoeoeao....",
                "..oaaaaaao....",
                "...oddddo.....",
                ".ooccccccoo...",
                "oSScaaaaacao..",
                "oSScaAAaacao..",
                "oSskaaaaacao..",
                "oSskcaaaacao..",
                "oSskccccccoo..",
                "oSssooddoo....",
                "oSsso.ddo.....",
                ".oooo.dd......",
                ".....oadao....",
                "....oado.oao..",
                "....ooo..ooo..");
            Add("sentinelLow", KnightPal,   // shield lowered: winding up / recovering
                "....oooo......",
                "...oAAAAo.....",
                "..oAaaaaAo....",
                "..oaoeoeao....",
                "..oaaaaaao....",
                "...oddddo.....",
                ".ooccccccoo...",
                ".ocaaaaaacao..",
                ".ocaAAaaacao..",
                ".ocaaaaaacao..",
                "oSScaaaaacao..",
                "oSScccccccoo..",
                "oSskooddoo....",
                "oSsk..ddo.....",
                "oSso..dd......",
                ".oo..oadoao...",
                "....oado.oado.",
                "....ooo...ooo.");

            Add("spitter0", PodPal,
                "....oooo....",
                "...oGGGGo...",
                "..oGGggGGo..",
                ".oGgmmmmgGo.",
                ".oGgmyymgGo.",
                "oGggmmmmggGo",
                "oGgggggggggo",
                "oggpggggpggo",
                ".odddddddo..",
                "..oooooooo..");
            Add("spitter1", PodPal,   // swelling: the tell
                "...oooooo...",
                "..oGGGGGGo..",
                ".oGGgmmgGGo.",
                "oGGgmyymgGGo",
                "oGgmyyyymgGo",
                "oGggmmmmggGo",
                "oGgggggggggo",
                "oggpggggpggo",
                ".odddddddddo",
                "..oooooooo..");

            Add("dropper0", IcePal,   // hanging, wings folded
                ".oooooooooo.",
                "..oIiiiiIo..",
                "..oiiiiiio..",
                "..oieiieio..",
                "...oiiiio...",
                "...odiido...",
                "....oddo....",
                ".....oo.....",
                "............");
            Add("dropper1", IcePal,
                "............",
                "..o......o..",
                ".oIo.oo.oIo.",
                "oiiooIIooiio",
                "oddiiiiiiddo",
                ".odieiieido.",
                "..oiiiiiio..",
                ".ol.ol.ol.lo",
                "............");
            Add("dropper2", IcePal,
                "............",
                "..o......o..",
                ".oIo.oo.oIo.",
                "oiiooIIooiio",
                "oddiiiiiiddo",
                ".odieiieido.",
                "..oiiiiiio..",
                "..lo.lo.lo.l",
                "............");

            AddBossArt();
            AddObjects();
            Pack();
        }

        // ---- guardians ---------------------------------------------------------------------

        private static void AddBossArt()
        {
            var boar = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x120a0c), ['b'] = G.Hex(0x5a3a2e), ['B'] = G.Hex(0x7a543e), ['d'] = G.Hex(0x3a241e),
                ['T'] = G.Hex(0xe86a8a), ['t'] = G.Hex(0xa83e5e), ['w'] = G.Hex(0xf0e6d0), ['e'] = G.Hex(0xff4a2a),
                ['m'] = G.Hex(0x2a5a2a), ['M'] = G.Hex(0x4a8a3a), ['h'] = G.Hex(0x2a1814),
            };
            string[] boarTop =
            {
                "..........T....T.....T....T.......",
                "........tTo..tTo...tTo..tTo.......",
                ".....oootTooootToootTooootToo.....",
                "....oMmmMmmmMmmmmMmmmmMmmmmMoo....",
                "...oMmmmmmmMmmmmmmmMmmmmmmmmmMo...",
                "..oMmmmmmmmmmmmmmmmmmmmmmmmmmmmo..",
                ".obbbbbbbbbbbbbbbbbbbbbbbbbbbbbbo.",
                ".obBBbbbbbbbbbbbbbbbbbbbbbbbbbbbbo",
                "obBBBbbbbbbbbbbbbbbbbbbbbbbbbbBBBo",
                "obBBbbbbbbbbbbbbbbbbbbbbbbbbbBBbBo",
                "obbbbbbbbbbbbbbbbbbbbbbbbbbbBbebbo",
                "obbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbo",
                "odbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbo",
                "oddbbbbbbbbbbbbbbbbbbbbbbbbbbbwwbhho",
                ".oddddddddddddddddddddddddddddwwohho",
                "..oddddddddddddddddddddddddddwwo.oo.",
                "...oooooooooooooooooooooooooooo.....",
            };
            string[] legsA = { "....obbo...obbo......obbo...obbo....", "....obdo...obdo......obdo...obdo....", "....oooo...oooo......oooo...oooo...." };
            string[] legsB = { "...obbo.....obbo....obbo.....obbo...", "..obdo.......obdo..obdo.......obdo..", "..oooo.......oooo..oooo.......oooo.." };
            Images.Add(Compose("thornback0", boar, 38, 24, (boarTop, 1, 2), (legsA, 0, 19)));
            Images.Add(Compose("thornback1", boar, 38, 24, (boarTop, 1, 3), (legsB, 0, 20)));
            Images.Add(Compose("thornbackLow", boar, 38, 24, (boarTop, 1, 6), (new[] { "..oobbooo......oobbooobbooo...oobboo.", "..oooooo.......ooooooooooo....oooooo." }, 0, 22)));

            var abbot = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x0e0a14), ['r'] = G.Hex(0x3a2a4e), ['R'] = G.Hex(0x261a36), ['q'] = G.Hex(0x58406e),
                ['g'] = G.Hex(0xd8b05a), ['G'] = G.Hex(0x9a7430), ['f'] = G.Hex(0x05030a), ['e'] = G.Hex(0xb8f0ff),
                ['w'] = G.Hex(0xe8e0d0), ['W'] = G.Hex(0xb0a898),
            };
            string[] abbotArt =
            {
                ".......oooo.......",
                "......owWWwo......",
                ".....owWggWwo.....",
                "....oqrooooroq....",
                "....oqofffforq....",
                "...oqrofefeforq...",
                "...oqrofffffoRq...",
                "...oqrrooooorRqo..",
                "..oqrrgggggggrRqo.",
                "..oqrrrgGGGgrrRRo.",
                ".oqrrrrrgGgrrrRRo.",
                ".oqrrrrrrgrrrrrRRo",
                ".oqrrrrrrgrrrrrRRo",
                "oqrrrrrrrgrrrrrRRo",
                "oqrrrrrrrgrrrrrRRo",
                "oqrrrrrrrgrrrrrRRRo",
                "oqrrrrrrrgrrrrrRRRo",
                "oqrrrrrrrrrrrrrRRRo",
                "oqrrrrrrrrrrrrrRRRo",
                "oqrrrrrrrrrrrrrRRRo",
                "oqrrrrrrrrrrrrRRRRo",
                ".oqrrrrrrrrrrrRRRo.",
                ".oqrrrrrrrrrrrRRRo.",
                "..oqrrrrrrrrrRRRo..",
                "..oqqrrrrrrrrRRo...",
                "...oqqrRrRrRrRo....",
                "....oo.oRo.oRo.....",
                ".........o...o.....",
            };
            Images.Add(FromArtList("abbot", abbot, abbotArt));

            var wyrm = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x0a1020), ['i'] = G.Hex(0x9fd4f0), ['I'] = G.Hex(0xe2f6ff), ['d'] = G.Hex(0x4a7ea8),
                ['D'] = G.Hex(0x2c5070), ['e'] = G.Hex(0xff5a8a), ['t'] = G.Hex(0xffffff), ['m'] = G.Hex(0x1a0a20),
            };
            Add("wyrmHead", wyrm,
                "....o.....o.............",
                "...oIo...oIo............",
                "...oIIo.oIIo............",
                "..oiIiioiIiiooooo.......",
                ".oiiiiiiiiiiiiiiiooo....",
                "oiiiidddiiiiiiiiiiiioo..",
                "oiiddoeddiiiiiiiiiiiiio.",
                "oiidddddddiiiiiiiiiiiiio",
                "odddddddddddiiiitttiiiio",
                "oDDDdddddddddmmmmmmmmmo.",
                ".oDDDDdddddtmtmtmtmtmo..",
                "..ooDDDDDDdddddddddo....",
                "....oooooooooooooo......");
            Add("wyrmHeadOpen", wyrm,
                "....o.....o.............",
                "...oIo...oIo............",
                "...oIIo.oIIo............",
                "..oiIiioiIiiooooo.......",
                ".oiiiiiiiiiiiiiiiooo....",
                "oiiiidddiiiiiiiiiiiioo..",
                "oiiddoeddiiiiiiiiiiitto.",
                "oiidddddddiiiitttiiimmo.",
                "odddddddddddmmmmmmmmmo..",
                "oDDDdddddddmmmmmmo......",
                ".oDDDDdddddmmmmmmmmmo...",
                "..ooDDDDDDdtmtmtmtmtmo..",
                "....ooooooooooooooooo...");

            var keeper = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x100c18), ['a'] = G.Hex(0x7a7090), ['A'] = G.Hex(0xb0a6c4), ['d'] = G.Hex(0x4a4260),
                ['D'] = G.Hex(0x2e2840), ['g'] = G.Hex(0xf0c04c), ['G'] = G.Hex(0xb8862a), ['l'] = G.Hex(0xfff0b0),
                ['e'] = G.Hex(0xfff6d0), ['f'] = G.Hex(0x05030a), ['c'] = G.Hex(0x8a2a3a), ['C'] = G.Hex(0x5a1a28),
            };
            string[] keeperArt =
            {
                "...........oo..oo...........",
                "..........oglooglo..........",
                "..........oggoogGo..........",
                ".........ooooooooo..........",
                "........oAAAAAAAAAo.........",
                ".......oAaaaaaaaaaDo........",
                ".......oaaffffffaaDo........",
                ".......oaafeffefaaDo........",
                ".......oaaffffffaDDo........",
                "........oaaaaaaaDDo.........",
                "......ooodddddddDooo........",
                "....ooAAAaaaaaaaDDDDoo......",
                "...oAAAaaaaggaaaaaDDDDo.....",
                "..oAAaaaaagllgaaaaaDDDDo....",
                "..oAaaaaaaagglaaaaaaDDDo....",
                ".oAaaccccaaggaaaccccaDDDo...",
                ".oAacCCCcaaaaaacCCCcaDDDo...",
                ".oAaacccaaaaaaaacccaaDDDo...",
                ".oAaaaaaaaggggaaaaaaaDDDo...",
                ".oaaaaaaaaaaaaaaaaaaaDDDo...",
                "..oaaaaaaggggggaaaaaDDDo....",
                "..oDaaaaaaaaaaaaaaaaDDDo....",
                "..oDDaaaaaaaaaaaaaaDDDDo....",
                "...oDDaaaaaaaaaaaaDDDDo.....",
                "...ocDDDDDDDDDDDDDDDDco.....",
                "...occcccccccccccccccco.....",
                "...oCcccccccccccccccCo......",
                "...oCCccccccccccccccCo......",
                "...oCCcccccccccccccCCo......",
                "....oCCccccccccccccCCo......",
                "....oCCccccccccccccCCo......",
                "....oCCcccccoocccccCCo......",
                "....oCCccccoAAoccccCCo......",
                "....oCCcccoAaaAocccCCo......",
                ".....oCCCoAaaaaAoCCCo.......",
                ".....oAAAoAaaaaAoAAAo.......",
                ".....oAaaoaaDDaaoaaAo.......",
                "....oAaaaooaDDaooaaaAo......",
                "....oaaaaao.oo.oaaaaao......",
                "...oAaaaaao....oAaaaaao.....",
                "...oaaaaaDo....oaaaaaDo.....",
                "...ooooooo......ooooooo.....",
            };
            Images.Add(FromArtList("bellkeeper", keeper, keeperArt));
        }

        // ---- objects -----------------------------------------------------------------------

        private static void AddObjects()
        {
            var flamePal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x5a1a10), ['r'] = G.Hex(0xe8452c), ['y'] = G.Hex(0xffb23e), ['w'] = G.Hex(0xfff3c4),
            };
            Add("ember0", flamePal, "..o..", ".oro.", ".ory.", "oryro", "orywo", ".owo.", "..o..");
            Add("ember1", flamePal, "...o.", "..oro", ".oryo", "oryro", "orywo", ".owo.", "..o..");
            Add("ember2", flamePal, ".o...", "oro..", "oyro.", "oryro", "orywo", ".owo.", "..o..");

            var uiFlame = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x2a1410), ['r'] = G.Hex(0xe8452c), ['y'] = G.Hex(0xffb23e), ['w'] = G.Hex(0xfff3c4),
                ['c'] = G.Hex(0xe8dcc0), ['C'] = G.Hex(0xb0a080), ['k'] = G.Hex(0x1b1630), ['g'] = G.Hex(0x5a5068),
            };
            Add("hpFull", uiFlame,
                "...o...",
                "..oyo..",
                ".oywyo.",
                ".orwro.",
                "..oro..",
                ".ocCco.",
                ".occCo.",
                ".occCo.",
                ".occCo.",
                "ooooooo");
            Add("hpEmpty", uiFlame,
                ".......",
                ".......",
                "...k...",
                "...k...",
                "...g...",
                ".ogggo.",
                ".ogggo.",
                ".ogggo.",
                ".ogggo.",
                "ooooooo");

            var flaskPal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1b1630), ['g'] = G.Hex(0xc8e0f0), ['a'] = G.Hex(0xff9a3a), ['A'] = G.Hex(0xffd27a),
                ['c'] = G.Hex(0x8a5a32), ['e'] = G.Hex(0x3a3448),
            };
            Add("flaskFull", flaskPal, ".ooo.", ".oco.", ".ogo.", "ogAgo", "oaAao", "oaaao", ".ooo.");
            Add("flaskEmpty", flaskPal, ".ooo.", ".oco.", ".ogo.", "ogego", "oeeeo", "oeeeo", ".ooo.");

            var vesselPal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1b1630), ['m'] = G.Hex(0xb88a3a), ['M'] = G.Hex(0xf0c86a), ['g'] = G.Hex(0x9ad0e8),
                ['r'] = G.Hex(0xe8452c), ['y'] = G.Hex(0xffb23e), ['w'] = G.Hex(0xfff3c4),
            };
            Add("vessel", vesselPal,
                "...ooo...",
                "..oMmMo..",
                ".ooooooo.",
                ".ogg.ggo.",
                ".og.y.go.",
                ".og.r.go.",
                ".ogywygo.",
                ".ogrwrgo.",
                ".ooooooo.",
                "..oMmMo..",
                "...ooo...");
            Add("balm", flaskPal,
                "..ooo..",
                "..oco..",
                "..ogo..",
                ".oggAo.",
                "ogAAAgo",
                "oaAAAao",
                "oaaaaao",
                "oaaaaao",
                ".ooooo.");

            var cachePal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1a0e0c), ['s'] = G.Hex(0x5a4a46), ['S'] = G.Hex(0x3a2e2c), ['r'] = G.Hex(0xe8452c),
                ['y'] = G.Hex(0xffb23e), ['w'] = G.Hex(0xfff3c4),
            };
            Add("cache0", cachePal,
                "......o.......",
                ".....oyo...o..",
                ".o...oyo..oyo.",
                "oyo.oywro.oyo.",
                "oywooywrooywo.",
                "oyrooyyroowro.",
                ".oyroyrrooyro.",
                ".oyroyroooyo..",
                "oossoyrossoso.",
                "osssssssssssso",
                "oSsssSsssSssso",
                "oSSssSSssSSsso",
                ".oooooooooooo.");
            Add("cache1", cachePal,
                "..............",
                "..............",
                ".........o....",
                ".....o..oyo...",
                "....oyo.oyo...",
                "....oyro.oro..",
                "...oyroooyro..",
                "...oyroooyo...",
                "oossoyrossoso.",
                "osssssssssssso",
                "oSsssSsssSssso",
                "oSSssSSssSSsso",
                ".oooooooooooo.");

            var stone = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1a1620), ['s'] = G.Hex(0x8e8698), ['S'] = G.Hex(0x5e5868), ['l'] = G.Hex(0xc6bed0),
                ['g'] = G.Hex(0xf0c04c), ['G'] = G.Hex(0xb8862a), ['c'] = G.Hex(0xf0e2c0), ['f'] = G.Hex(0xffd070),
                ['m'] = G.Hex(0x5a8a3a), ['t'] = G.Hex(0x3a3440),
            };
            Add("shrine", stone,
                ".......oo.......",
                "......oggo......",
                ".....oglGGo.....",
                ".....oggGGo.....",
                "....ooooooo.....",
                "...ollllllSo....",
                "...ossssssSo....",
                "....ossssSo.....",
                "....osttsSo.....",
                "....ostfsSo.....",
                "....osttsSo.....",
                "....ossssSo.....",
                "....ossssSo.....",
                "....omsssSo.....",
                "....ommssSo.....",
                "...ollllllSSo...",
                "..osssssssssSo..",
                "..osssssssssSo..",
                ".oooooooooooooo.");
            Add("tablet", stone,
                "..oooooooo..",
                ".ollllllllo.",
                "olsssssssSSo",
                "olstststsSSo",
                "olssssssssSo",
                "olststtssSSo",
                "olssssssssSo",
                "olsttstssSSo",
                "olssssssssSo",
                "olstststsSSo",
                "olssssssssSo",
                "omsssssssmSo",
                "ommssssmmSSo",
                "oooooooooooo");
            Add("altar", stone,
                "oooooooooooooooo",
                "ollllllllllllllo",
                "oSgGgGgGgGgGgGSo",
                ".oSsssssssssSSo.",
                "..osssssssssSo..",
                "..osssggsssSSo..",
                "..ossgllgssSSo..",
                "..osssggsssSSo..",
                "..ossssssssSSo..",
                ".olllllllllllSo.",
                "ossssssssssssSSo",
                "oooooooooooooooo");

            var tam = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1a1210), ['p'] = G.Hex(0x8a5a32), ['P'] = G.Hex(0x5e3c22), ['l'] = G.Hex(0xc08a50),
                ['h'] = G.Hex(0x6a7a4a), ['H'] = G.Hex(0x4a5634), ['s'] = G.Hex(0xe0b896), ['S'] = G.Hex(0xb08060),
                ['w'] = G.Hex(0xf0f0f0), ['e'] = G.Hex(0x1a1210), ['r'] = G.Hex(0xc04040), ['y'] = G.Hex(0xffd070),
                ['b'] = G.Hex(0x3a2a20), ['c'] = G.Hex(0x8090b0),
            };
            Add("merchant0", tam,
                "....oooooo..........",
                "...oplllppo.........",
                "..oplpppppPo........",
                "..opppyppPPPo.......",
                "..oppppppPPPPo......",
                ".oprpcpppPPPPo......",
                ".oppppppPPPPPo..oo..",
                ".oppppPPPPPPPoohhho.",
                ".oplpppPPPPPPohhhhHo",
                ".opppppPPPPPohhssSHo",
                ".opppppPPPPPohsesSHo",
                "..opppPPPPPPohsssSo.",
                "..oppPPPPPPohwwwwho.",
                "...oooooooohwwwwhho.",
                "..........ohhhhhHHo.",
                "..........ohhhhhHHo.",
                "..........ohhhhhHHo.",
                "..........obbo.obbo.",
                "..........oooo.oooo.");
            Add("merchant1", tam,
                "....oooooo..........",
                "...oplllppo.........",
                "..oplpppppPo........",
                "..opppyppPPPo.......",
                "..oppppppPPPPo......",
                ".oprpcpppPPPPo......",
                ".oppppppPPPPPo..oo..",
                ".oppppPPPPPPPoohhho.",
                ".oplpppPPPPPPohhhhHo",
                ".opppppPPPPPohhssSHo",
                ".opppppPPPPPohsssSHo",
                "..opppPPPPPPohsssSo.",
                "..oppPPPPPPohwwwwho.",
                "...oooooooohwwwwhho.",
                "..........ohhhhhHHo.",
                "..........ohhhhhHHo.",
                "..........ohhhhhHHo.",
                "..........obbo.obbo.",
                "..........oooo.oooo.");

            var nun = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x14101c), ['w'] = G.Hex(0xe8e4f0), ['W'] = G.Hex(0xa8a2b8), ['k'] = G.Hex(0x2e2a44),
                ['K'] = G.Hex(0x1e1a30), ['s'] = G.Hex(0xe0c0a0), ['e'] = G.Hex(0x2a1a20), ['c'] = G.Hex(0xf0e2c0),
                ['f'] = G.Hex(0xffd070),
            };
            Add("npc0", nun,
                "...oooo...",
                "..owwwwo..",
                ".owkkkkwo.",
                ".owksskwo.",
                ".owksesko.",
                ".owkssskWo",
                "..okkkkko.",
                ".okkkkkKKo",
                ".okkkkkKKo",
                ".okkwkkKKo",
                "okkkwkkKKKo",
                "okkkkkkKKKo",
                "okkkkkkKKKo",
                "okkkkkkKKKo",
                ".okkkkKKKo.",
                ".oooooooo..");
            Add("npc1", nun,
                "..........",
                "...oooo...",
                "..owwwwo..",
                ".owkkkkwo.",
                ".owksskwo.",
                ".owksesko.",
                ".owkssskWo",
                ".okkkkkKKo",
                ".okkkkkKKo",
                ".okkwkkKKo",
                "okkkwkkKKKo",
                "okkkkkkKKKo",
                "okkkkkkKKKo",
                "okkkkkkKKKo",
                ".okkkkKKKo.",
                ".oooooooo..");

            var bellPal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x3a2310), ['g'] = G.Hex(0xf0c04c), ['G'] = G.Hex(0xb8862a), ['l'] = G.Hex(0xfff0b0), ['d'] = G.Hex(0x7a5418),
            };
            Add("bell", bellPal,
                ".......oo.......",
                "......oddo......",
                ".....ollggo.....",
                "....olgggGGo....",
                "...olggggGGGo...",
                "...olggggGGGo...",
                "...olggggGGGo...",
                "..olgggggGGGGo..",
                "..olgggggGGGGo..",
                ".olggggggGGGGGo.",
                ".olggggggGGGGGo.",
                "oollllllllggGGoo",
                "oGGGGGGGGGGGGGGo",
                ".oooooooddooooo.",
                ".......odo......",
                "........o.......");
            Add("handbell", bellPal,
                "..oo..",
                ".oddo.",
                ".olgo.",
                "olggGo",
                "olggGo",
                "ollgGo",
                "oooooo",
                "..oo..");
            Add("kite", new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1b1630), ['c'] = G.Hex(0xf0e6d0), ['C'] = G.Hex(0xc8b898), ['r'] = G.Hex(0xc8443a), ['R'] = G.Hex(0x8a2a28),
            },
                "....oooo....",
                "..oocrrcoo..",
                ".occcrrcccco",
                "occccrRccCCo",
                ".ocCCrRCCCo.",
                "..ooorRooo..",
                ".....oo.....");
            Add("grips", new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x1b1630), ['i'] = G.Hex(0x8a8aa0), ['I'] = G.Hex(0xd0d0e0), ['d'] = G.Hex(0x4a4a60), ['b'] = G.Hex(0x6a4428),
            },
                ".o.o.o..",
                "oIoIoIo.",
                "oiiiiiio",
                "oiIIiiio",
                "obbbbbdo",
                "obbbbbdo",
                ".oooooo.");
        }

        // ---- helpers -----------------------------------------------------------------------

        private static void Add(string name, Dictionary<char, Color> pal, params string[] rows) => Images.Add(FromArt(name, pal, rows));

        private static (string, int, int, Color[]) FromArtList(string name, Dictionary<char, Color> pal, string[] rows) => FromArt(name, pal, rows);

        private static (string, int, int, Color[]) Compose(string name, Dictionary<char, Color> pal, int w, int h, params (string[] rows, int x, int y)[] parts)
        {
            var data = new Color[w * h];
            foreach (var (rows, ox, oy) in parts)
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        int px = x + ox, py = y + oy;
                        if (px < 0 || py < 0 || px >= w || py >= h) continue;
                        if (pal.TryGetValue(rows[y][x], out var c)) data[py * w + px] = c;
                    }
            return (name, w, h, data);
        }

        private static (string, int, int, Color[]) FromArt(string name, Dictionary<char, Color> pal, string[] rows)
        {
            int w = 0;
            foreach (var r in rows) w = Math.Max(w, r.Length);
            int h = rows.Length;
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (pal.TryGetValue(rows[y][x], out var c)) data[y * w + x] = c;
            return (name, w, h, data);
        }

        private static void Pack()
        {
            int sheetW = 256, x = 0, y = 0, rowH = 0;
            var placed = new List<(string, Rectangle, int, Color[])>();
            foreach (var img in Images)
            {
                if (x + img.w > sheetW) { x = 0; y += rowH + 1; rowH = 0; }
                placed.Add((img.name, new Rectangle(x, y, img.w, img.h), img.w, img.data));
                x += img.w + 1;
                rowH = Math.Max(rowH, img.h);
            }
            int sheetH = y + rowH + 1;
            var sheet = new Color[sheetW * sheetH];
            foreach (var (name, r, w, data) in placed)
            {
                for (int yy = 0; yy < r.Height; yy++)
                    for (int xx = 0; xx < r.Width; xx++)
                        sheet[(r.Y + yy) * sheetW + r.X + xx] = data[yy * w + xx];
                Frames[name] = r;
            }
            Sheet = G.Make(sheetW, sheetH, sheet);
            var white = new Color[sheet.Length];
            for (int i = 0; i < sheet.Length; i++) white[i] = sheet[i].A > 0 ? Color.White : Color.Transparent;
            SheetWhite = G.Make(sheetW, sheetH, white);
        }

        public static void Draw(SpriteBatch sb, string frame, Vector2 pos, Color color, Vector2 origin, Vector2 scale, bool flipX = false, float rot = 0f)
        {
            sb.Draw(Sheet, pos, Frames[frame], color, rot, origin, scale, flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        /// <summary>Draws a frame anchored at its bottom centre.</summary>
        public static void DrawFeet(SpriteBatch sb, string frame, Vector2 feet, Color color, bool flipX = false, float rot = 0f, bool white = false)
        {
            var r = Frames[frame];
            sb.Draw(white ? SheetWhite : Sheet, new Vector2(MathF.Round(feet.X), MathF.Round(feet.Y)), r, color, rot, new Vector2(r.Width / 2, r.Height), 1f,
                    flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public static void DrawCentered(SpriteBatch sb, string frame, Vector2 c, Color color, bool flipX = false, float rot = 0f, bool white = false, bool flipY = false)
        {
            var r = Frames[frame];
            var fx = (flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (flipY ? SpriteEffects.FlipVertically : SpriteEffects.None);
            sb.Draw(white ? SheetWhite : Sheet, new Vector2(MathF.Round(c.X), MathF.Round(c.Y)), r, color, rot, new Vector2(r.Width / 2, r.Height / 2), 1f, fx, 0f);
        }

        public static void DrawSilhouette(SpriteBatch sb, string frame, Vector2 pos, Color color, Vector2 origin, Vector2 scale, bool flipX = false)
        {
            sb.Draw(SheetWhite, pos, Frames[frame], color, 0f, origin, scale, flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }
}
