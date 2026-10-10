using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HolyPeak.Gfx
{
    /// <summary>Hand-authored pixel sprites, stored as text and turned into textures at startup.</summary>
    public static class Sprites
    {
        public static Texture2D Sheet;
        /// <summary>Same layout as <see cref="Sheet"/> but every opaque pixel is white (for tinted silhouettes).</summary>
        public static Texture2D SheetWhite;
        public static readonly Dictionary<string, Rectangle> Frames = new Dictionary<string, Rectangle>();

        // Player palette
        private static readonly Dictionary<char, Color> PlayerPal = new Dictionary<char, Color>
        {
            ['o'] = G.Hex(0x1b1630),
            ['c'] = G.Hex(0x3a5a96),
            ['C'] = G.Hex(0x283e6e),
            ['L'] = G.Hex(0x5f86c8),
            ['s'] = G.Hex(0xf4cba6),
            ['S'] = G.Hex(0xd59c7c),
            ['e'] = G.Hex(0x1b1630),
            ['b'] = G.Hex(0x5a3a30),
            ['B'] = G.Hex(0x3b2420),
            ['t'] = G.Hex(0xd8c39a), // belt / rope
            ['w'] = G.Hex(0xffffff),
        };

        private static readonly string[] Head =
        {
            "..oooo...",
            ".oLLLco..",
            "oLLcccCo.",
            "oLcco.seo",
            "oLcco.sso",
            ".oCcoooo.",
        };

        private static readonly string[] Body =
        {
            ".oLcccCo.",
            "oLcccccCo",
            "oLtttttCo",
            "oCcccccCo",
            ".oCCCCCo.",
        };

        private static readonly string[] BodyFall =
        {
            ".oLcccCo.",
            "oLcccccCo",
            "oLtttttCo",
            "oCcccccCCo",
            "oCCCCCCCo.",
        };

        private static readonly Dictionary<string, string[]> Legs = new Dictionary<string, string[]>
        {
            ["stand"] = new[] { ".obo.obo.", ".ooo.ooo." },
            ["run0"] = new[] { ".obo.obo.", "oooo..oo." },
            ["run1"] = new[] { "..obob...", "..oooo..." },
            ["run2"] = new[] { ".obo.obo.", ".oo..oooo" },
            ["run3"] = new[] { "...obob..", "...oooo.." },
            ["jump"] = new[] { "..obobo..", "...ooo..." },
            ["fall"] = new[] { "obo...obo", "oo.....oo" },
        };

        public static void Init()
        {
            var images = new List<(string name, int w, int h, Color[] data)>();

            void AddPlayer(string name, string[] body, string legs, int bob, bool lean = false, bool slide = false)
            {
                const int w = 10, h = 13;
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
                            data[py * w + px] = PlayerPal[ch];
                        }
                }
                var l = Legs[legs];
                Blit(l, 0, 11);
                Blit(body, 0, 6 + bob);
                Blit(Head, lean ? 1 : 0, bob);
                if (slide)
                {
                    // hand pressed against the wall
                    data[7 * w + 9] = PlayerPal['s'];
                    data[6 * w + 9] = PlayerPal['o'];
                    data[8 * w + 9] = PlayerPal['o'];
                }
                images.Add((name, w, h, data));
            }

            AddPlayer("idle0", Body, "stand", 0);
            AddPlayer("idle1", Body, "stand", 1);
            AddPlayer("run0", Body, "run0", 0, lean: true);
            AddPlayer("run1", Body, "run1", 1, lean: true);
            AddPlayer("run2", Body, "run2", 0, lean: true);
            AddPlayer("run3", Body, "run3", 1, lean: true);
            AddPlayer("jump", Body, "jump", 0);
            AddPlayer("fall", BodyFall, "fall", 0);
            AddPlayer("slide", Body, "jump", 0, slide: true);
            AddPlayer("dash", Body, "fall", 1, lean: true);

            images.Add(FromArt("crystal", new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x15324a), ['a'] = G.Hex(0x7ff0ff), ['b'] = G.Hex(0x37b6e6), ['c'] = G.Hex(0x1f7fb8), ['w'] = G.Hex(0xffffff),
            },
                "....o....",
                "...owo...",
                "..owaao..",
                ".owaabbo.",
                "owaaabbco",
                "oaaabbbco",
                ".obbbbco.",
                "..obbco..",
                "...oco...",
                "....o....",
                "........."));

            images.Add(FromArt("crystalOff", new Dictionary<char, Color> { ['o'] = G.Hex(0x7ff0ff) * 0.6f },
                "....o....",
                "...o.o...",
                "..o...o..",
                ".o.....o.",
                "o.......o",
                "o.......o",
                ".o.....o.",
                "..o...o..",
                "...o.o...",
                "....o....",
                "........."));

            var flamePal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x5a1a10), ['r'] = G.Hex(0xe8452c), ['y'] = G.Hex(0xffb23e), ['w'] = G.Hex(0xfff3c4),
            };
            images.Add(FromArt("ember0", flamePal,
                "...o...",
                "..oro..",
                "..ory..",
                ".oryro.",
                ".orywro",
                "oryywro",
                "orywwro",
                ".orwyo.",
                "..ooo.."));
            images.Add(FromArt("ember1", flamePal,
                "....o..",
                "...oro.",
                "..oryo.",
                ".oryro.",
                ".orywro",
                "orywyro",
                "orywwro",
                ".orwyo.",
                "..ooo.."));
            images.Add(FromArt("ember2", flamePal,
                "..o....",
                ".oro...",
                ".oyro..",
                ".oryro.",
                "oryywo.",
                "orywyro",
                "orywwro",
                ".orwyo.",
                "..ooo.."));

            images.Add(FromArt("shrine", new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x2a2030), ['s'] = G.Hex(0xb8a88e), ['S'] = G.Hex(0x7d6c5a), ['l'] = G.Hex(0xe0d2b4), ['g'] = G.Hex(0x7fb24a),
            },
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "....oooooooo....",
                "...olllllllso...",
                "...ossssssSSo...",
                "....osssssSo....",
                ".....ossssSo....",
                ".....ossssSo....",
                ".....ossssSo....",
                ".....osgssSo....",
                ".....ogggsSo....",
                "....osssssSSo...",
                "...olllllllSSo..",
                "...osssssssSSo..",
                "..oooooooooooooo"));

            var bellPal = new Dictionary<char, Color>
            {
                ['o'] = G.Hex(0x3a2310), ['g'] = G.Hex(0xf0c04c), ['G'] = G.Hex(0xb8862a), ['l'] = G.Hex(0xfff0b0), ['d'] = G.Hex(0x7a5418),
            };
            images.Add(FromArt("bell", bellPal,
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
                "........o......."));

            // Pack into a single sheet.
            int sheetW = 128, x = 0, y = 0, rowH = 0;
            var placed = new List<(string, Rectangle, int, Color[])>();
            foreach (var img in images)
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

        private static (string, int, int, Color[]) FromArt(string name, Dictionary<char, Color> pal, params string[] rows)
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

        public static void Draw(SpriteBatch sb, string frame, Vector2 pos, Color color, Vector2 origin, Vector2 scale, bool flipX = false, float rot = 0f)
        {
            sb.Draw(Sheet, pos, Frames[frame], color, rot, origin, scale, flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public static void DrawSilhouette(SpriteBatch sb, string frame, Vector2 pos, Color color, Vector2 origin, Vector2 scale, bool flipX = false)
        {
            sb.Draw(SheetWhite, pos, Frames[frame], color, 0f, origin, scale, flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }
}
