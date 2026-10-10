using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;
using HolyPeak.World;

namespace HolyPeak.Gfx
{
    /// <summary>
    /// Paints a whole room's static geometry (terrain, background walls, platforms, spikes and
    /// decorations) into two textures, pixel by pixel, in the chapter's style.
    /// </summary>
    public static class TileArt
    {
        public static void Build(Room room, Style st, out Texture2D front, out Texture2D back)
        {
            int cols = room.Cols, rows = room.Rows, W = room.Width, H = room.Height;
            var fp = new Color[W * H];
            var bp = new Color[W * H];
            int seed = room.Def.Id.GetHashCode() & 0xFFFF;

            bool Solid(int tx, int ty) =>
                tx < 0 || ty < 0 || tx >= cols || ty >= rows || Room.IsSolidTile(room.TileAt(tx, ty));
            Tile T(int tx, int ty) => room.TileAt(tx, ty);
            char Ch(int tx, int ty) => tx < 0 || ty < 0 || tx >= cols || ty >= rows ? '#' : room.Def.Map[ty][tx];
            bool Clean(int tx, int ty) { char c = Ch(tx, ty); return c == '.' || c == 'P' || c == 'h' || c == 'k' || c == ':'; }

            // Pixel-level distance from open air (Chebyshev), for smooth interior shading.
            var pdist = new byte[W * H];
            {
                var pq = new Queue<int>();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        if (Solid(x >> 3, y >> 3)) pdist[i] = 255;
                        else { pdist[i] = 0; pq.Enqueue(i); }
                    }
                while (pq.Count > 0)
                {
                    int i = pq.Dequeue();
                    int x = i % W, y = i / W;
                    if (pdist[i] >= 40) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                            int ni = ny * W + nx;
                            if (pdist[ni] > pdist[i] + 1) { pdist[ni] = (byte)(pdist[i] + 1); pq.Enqueue(ni); }
                        }
                }
            }

            void SetF(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) fp[y * W + x] = c; }
            Color GetF(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? fp[y * W + x] : Color.Transparent;

            // ---- background walls ----
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    if (T(tx, ty) != Tile.Back && !BackFill(room, tx, ty)) continue;
                    for (int ly = 0; ly < 8; ly++)
                        for (int lx = 0; lx < 8; lx++)
                        {
                            int x = tx * 8 + lx, y = ty * 8 + ly;
                            Color c = BackPixel(st, x, y, seed);
                            // broken, soft edges where the wall meets open air
                            bool edgeL = !IsBackish(room, tx - 1, ty) && lx < 2;
                            bool edgeR = !IsBackish(room, tx + 1, ty) && lx > 5;
                            bool edgeU = !IsBackish(room, tx, ty - 1) && ly < 2;
                            bool edgeD = !IsBackish(room, tx, ty + 1) && ly > 5;
                            if (edgeL || edgeR || edgeU || edgeD)
                            {
                                float h = Calc.Hash(x, y, seed + 7);
                                if (h < 0.45f) continue;
                                c = st.BackDark;
                            }
                            // ambient occlusion near terrain
                            float ao = 0;
                            if (Solid(tx, ty - 1)) ao = Math.Max(ao, 1f - ly / 6f);
                            if (Solid(tx - 1, ty)) ao = Math.Max(ao, 1f - lx / 5f);
                            if (Solid(tx + 1, ty)) ao = Math.Max(ao, 1f - (7 - lx) / 5f);
                            if (ao > 0) c = G.Mix(c, st.BackDark, ao * 0.8f);
                            bp[y * W + x] = c;
                        }
                }

            // ---- terrain ----
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    Tile t = T(tx, ty);
                    if (!Room.IsSolidTile(t)) continue;
                    bool alt = t == Tile.Solid2;
                    bool up = !Solid(tx, ty - 1), dn = !Solid(tx, ty + 1), lf = !Solid(tx - 1, ty), rt = !Solid(tx + 1, ty);
                    bool ul = !Solid(tx - 1, ty - 1), ur = !Solid(tx + 1, ty - 1), dl = !Solid(tx - 1, ty + 1), dr = !Solid(tx + 1, ty + 1);
                    for (int ly = 0; ly < 8; ly++)
                        for (int lx = 0; lx < 8; lx++)
                        {
                            int x = tx * 8 + lx, y = ty * 8 + ly;
                            // chamfer outer corners
                            if (up && lf && lx + ly < 2) continue;
                            if (up && rt && (7 - lx) + ly < 2) continue;
                            if (dn && lf && lx + (7 - ly) < 2) continue;
                            if (dn && rt && (7 - lx) + (7 - ly) < 2) continue;

                            int d = 99;
                            if (up) d = Math.Min(d, ly);
                            if (dn) d = Math.Min(d, 7 - ly);
                            if (lf) d = Math.Min(d, lx);
                            if (rt) d = Math.Min(d, 7 - lx);
                            if (ul && !up && !lf) d = Math.Min(d, Math.Max(lx, ly));
                            if (ur && !up && !rt) d = Math.Min(d, Math.Max(7 - lx, ly));
                            if (dl && !dn && !lf) d = Math.Min(d, Math.Max(lx, 7 - ly));
                            if (dr && !dn && !rt) d = Math.Min(d, Math.Max(7 - lx, 7 - ly));

                            Color c = alt ? AltPixel(st, x, y, seed) : TerrainPixel(st, x, y, seed);
                            float pd = pdist[y * W + x] == 255 ? 40 : pdist[y * W + x];
                            float shade = 1f - (st.Id == 3 ? 0.16f : 0.3f) * Calc.SmoothStep((pd - 3f) / 26f);
                            if (d == 1 && (up || ul || ur)) c = G.Mix(c, alt ? st.AltLight : st.Light, 0.55f);
                            if (dn && 7 - ly <= 1) c = G.Mul(c, 0.8f);
                            c = G.Mul(c, shade);
                            if (d == 0) c = st.Outline;
                            SetF(x, y, c);
                        }

                    // top cap (grass / moss / snow / gold)
                    if (up && (!alt || st.Id == 2 || st.Id == 3))
                        for (int lx = 0; lx < 8; lx++)
                        {
                            int x = tx * 8 + lx;
                            if (up && lf && lx < 1) continue;
                            if (up && rt && lx > 6) continue;
                            int thick = CapThickness(st, x, seed);
                            for (int ly = 0; ly < thick; ly++)
                            {
                                int y = ty * 8 + ly;
                                Color c = ly == 0 ? st.CapLight : (ly == thick - 1 ? st.CapDark : st.Cap);
                                if (st.Id == 0 && ly == 0 && Calc.Hash(x, y, seed + 3) < 0.3f) c = st.Cap;
                                SetF(x, y, c);
                            }
                            // dark line under the cap
                            int yy = ty * 8 + thick;
                            if (thick < 7 && (st.Id == 0 || st.Id == 1)) SetF(x, yy, G.Mix(GetF(x, yy), st.Outline, 0.5f));
                            // cap spilling over edges
                            if (lf && lx == 0) { for (int k = 1; k < thick + 1; k++) SetF(x, ty * 8 + k, st.CapDark); }
                            if (rt && lx == 7) { for (int k = 1; k < thick + 1; k++) SetF(x, ty * 8 + k, st.CapDark); }
                        }
                }

            // ---- one-way platforms ----
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    if (T(tx, ty) != Tile.JumpThru) continue;
                    bool lEnd = T(tx - 1, ty) != Tile.JumpThru && !Solid(tx - 1, ty);
                    bool rEnd = T(tx + 1, ty) != Tile.JumpThru && !Solid(tx + 1, ty);
                    for (int lx = 0; lx < 8; lx++)
                    {
                        int x = tx * 8 + lx, y = ty * 8;
                        if ((lEnd && lx == 0) || (rEnd && lx == 7))
                        {
                            for (int k = 0; k < 4; k++) SetF(x, y + k, st.Outline);
                            continue;
                        }
                        SetF(x, y, st.Outline);
                        SetF(x, y + 1, st.PlatLight);
                        SetF(x, y + 2, Calc.Hash(x, ty, seed) < 0.15f ? st.PlatDark : st.Plat);
                        SetF(x, y + 3, st.PlatDark);
                        SetF(x, y + 4, st.Outline);
                    }
                    // little brackets underneath
                    if ((tx & 1) == 0)
                    {
                        int x = tx * 8 + 3, y = ty * 8 + 5;
                        SetF(x, y, st.Outline); SetF(x + 1, y, st.Outline);
                        SetF(x, y + 1, st.PlatDark); SetF(x + 1, y + 1, st.Outline);
                        SetF(x + 1, y + 2, st.Outline);
                    }
                }

            // ---- spikes ----
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    Tile t = T(tx, ty);
                    if (t < Tile.SpikeUp) continue;
                    // pass 0: outline, pass 1: fill
                    for (int pass = 0; pass < 2; pass++)
                        for (int sp = 0; sp < 2; sp++)
                            for (int r = 0; r < 6; r++)          // r = 0 tip ... 5 base
                            {
                                float half = 0.5f + r * 0.36f;
                                for (int a2 = -1; a2 <= 4; a2++)
                                {
                                    float off = a2 - 1.5f;
                                    bool inside = MathF.Abs(off) <= half;
                                    bool ring = !inside && MathF.Abs(off) <= half + 1f;
                                    if (pass == 0 && !inside && !ring) continue;
                                    if (pass == 1 && !inside) continue;
                                    Color c = pass == 0 ? st.Outline : (off < -0.2f ? st.SpikeLight : (r == 0 ? st.SpikeLight : st.Spike));
                                    if (pass == 1 && off > half - 0.9f && r > 1) c = st.SpikeDark;
                                    int along = sp * 4 + a2;
                                    if (along < 0 || along > 7) continue;
                                    int lx, ly;
                                    switch (t)
                                    {
                                        case Tile.SpikeUp: lx = along; ly = 2 + r; break;
                                        case Tile.SpikeDown: lx = along; ly = 5 - r; break;
                                        case Tile.SpikeLeft: lx = 2 + r; ly = along; break;
                                        default: lx = 5 - r; ly = along; break;
                                    }
                                    SetF(tx * 8 + lx, ty * 8 + ly, c);
                                    if (pass == 0 && r == 0)
                                    {
                                        // cap the tip
                                        switch (t)
                                        {
                                            case Tile.SpikeUp: SetF(tx * 8 + lx, ty * 8 + ly - 1, st.Outline); break;
                                            case Tile.SpikeDown: SetF(tx * 8 + lx, ty * 8 + ly + 1, st.Outline); break;
                                            case Tile.SpikeLeft: SetF(tx * 8 + lx - 1, ty * 8 + ly, st.Outline); break;
                                            default: SetF(tx * 8 + lx + 1, ty * 8 + ly, st.Outline); break;
                                        }
                                    }
                                }
                            }
                }

            // ---- decorations on top of and under terrain ----
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    if (!Room.IsSolidTile(T(tx, ty))) continue;
                    bool alt = T(tx, ty) == Tile.Solid2;
                    if (ty > 0 && !Solid(tx, ty - 1) && Clean(tx, ty - 1))
                        DecorateTop(st, tx, ty, alt, seed, SetF, Solid(tx - 1, ty) && Solid(tx + 1, ty));
                    if (ty < rows - 1 && !Solid(tx, ty + 1) && Clean(tx, ty + 1))
                        DecorateBottom(st, tx, ty, alt, seed, SetF);
                }

            front = G.Make(W, H, fp);
            back = G.Make(W, H, bp);
        }

        private static bool IsBackish(Room room, int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= room.Cols || ty >= room.Rows) return true;
            var t = room.TileAt(tx, ty);
            return t == Tile.Back || Room.IsSolidTile(t) || BackFill(room, tx, ty);
        }

        /// <summary>Entity cells (springs, crystals...) inside a background wall keep the wall behind them.</summary>
        private static bool BackFill(Room room, int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= room.Cols || ty >= room.Rows) return false;
            char c = room.Def.Map[ty][tx];
            if (c == '.' || c == ':' || Room.IsSolidTile(room.TileAt(tx, ty))) return false;
            int n = 0;
            if (room.TileAt(tx - 1, ty) == Tile.Back || Room.IsSolidTile(room.TileAt(tx - 1, ty))) n++;
            if (room.TileAt(tx + 1, ty) == Tile.Back || Room.IsSolidTile(room.TileAt(tx + 1, ty))) n++;
            if (room.TileAt(tx, ty - 1) == Tile.Back || Room.IsSolidTile(room.TileAt(tx, ty - 1))) n++;
            if (room.TileAt(tx, ty + 1) == Tile.Back || Room.IsSolidTile(room.TileAt(tx, ty + 1))) n++;
            return n >= 3 && (room.TileAt(tx - 1, ty) == Tile.Back || room.TileAt(tx + 1, ty) == Tile.Back ||
                              room.TileAt(tx, ty - 1) == Tile.Back || room.TileAt(tx, ty + 1) == Tile.Back);
        }

        private static int CapThickness(Style st, int x, int seed)
        {
            float h = Calc.Hash(x, 77, seed);
            return st.Id switch
            {
                0 => h < 0.25f ? 2 : (h < 0.8f ? 3 : 4),
                1 => h < 0.5f ? 1 : (h < 0.85f ? 2 : 3),
                2 => 3 + (int)(Calc.ValueNoise(x / 3f, 1, seed) * 2.5f),
                _ => 2,
            };
        }

        // ---- per-chapter pixel patterns ------------------------------------------------

        private static Color TerrainPixel(Style st, int x, int y, int seed)
        {
            switch (st.Id)
            {
                case 0:
                {
                    float n = Calc.Fbm(x / 7f, y / 7f, 3, seed);
                    Color c = n < 0.38f ? st.Dark : (n > 0.62f ? st.Light : st.Base);
                    float h = Calc.Hash(x, y, seed);
                    if (h > 0.975f) c = st.Speck;
                    // embedded pebbles
                    float p = Calc.Hash(x / 4, y / 3, seed + 5);
                    if (p > 0.955f)
                    {
                        int lx = x % 4, ly = y % 3;
                        if (lx > 0 && ly > 0) c = ly == 1 ? st.AltLight : st.Alt;
                    }
                    return c;
                }
                case 1:
                {
                    // Voronoi rubble: irregular stones separated by dark cracks
                    Voronoi(x / 6.5f, y / 5f, seed, out float f1, out float f2, out int cell);
                    if (f2 - f1 < 0.12f) return st.Dark;
                    float ch = Calc.Hash(cell, seed);
                    Color c = ch < 0.33f ? st.Base : (ch < 0.66f ? G.Mix(st.Base, st.Light, 0.5f) : st.Light);
                    if (f2 - f1 < 0.22f) c = G.Mix(c, st.Dark, 0.5f);
                    if (Calc.Hash(x, y, seed) > 0.97f) c = st.Speck;
                    return c;
                }
                case 2:
                {
                    float n = Calc.Fbm(x / 16f, y / 16f, 3, seed);
                    float band = MathF.Sin(y * 0.42f + n * 6f);
                    Color c = st.Base;
                    if (band > 0.55f) c = G.Mix(st.Base, st.Light, 0.7f);
                    else if (band < -0.8f) c = G.Mix(st.Base, st.Dark, 0.75f);
                    if (Calc.Hash(x, y, seed) > 0.975f) c = st.Speck;
                    return c;
                }
                default:
                {
                    // large carved marble blocks with faint veins
                    int row = Floor(y, 8);
                    int ox = (row & 1) * 8;
                    int lx = Mod(x + ox, 16), ly = Mod(y, 8);
                    Color c = Calc.Hash(Floor(x + ox, 16), row, seed + 13) > 0.5f ? st.Light : G.Mix(st.Light, st.Base, 0.45f);
                    if (ly == 7 || lx == 15) c = G.Mix(st.Base, st.Dark, 0.35f);
                    else if (ly == 0 || lx == 0) c = G.Mix(c, Color.White, 0.35f);
                    float n = Calc.Fbm(x / 9f, y / 9f, 3, seed);
                    float v = MathF.Abs(MathF.Sin(x * 0.09f + y * 0.05f + n * 8f));
                    if (v < 0.04f && ly != 7 && lx != 15) c = st.Speck;
                    return c;
                }
            }
        }

        private static Color AltPixel(Style st, int x, int y, int seed)
        {
            switch (st.Id)
            {
                case 0:
                case 1:
                {
                    int bh = st.Id == 0 ? 6 : 4, bw = 8;
                    int row = Floor(y, bh);
                    int ox = (row & 1) * (bw / 2);
                    int col = Floor(x + ox, bw);
                    int lx = Mod(x + ox, bw), ly = Mod(y, bh);
                    if (ly == bh - 1 || lx == bw - 1) return st.AltDark;
                    float h = Calc.Hash(col, row, seed + 11);
                    Color c = h < 0.3f ? G.Mix(st.Alt, st.AltDark, 0.35f) : (h > 0.8f ? G.Mix(st.Alt, st.AltLight, 0.4f) : st.Alt);
                    if (ly == 0) c = G.Mix(c, st.AltLight, 0.45f);
                    if (Calc.Hash(x, y, seed + 2) > 0.96f) c = st.AltDark;
                    return c;
                }
                case 2:
                {
                    float n = Calc.Fbm(x / 9f, y / 9f, 2, seed);
                    Color c = G.Mix(st.Alt, st.AltLight, n * 0.7f);
                    if (Mod(x + y, 13) == 0 || Mod(x + y, 13) == 1 && Calc.Hash(x, y, seed) > 0.5f) c = st.AltLight;
                    if (Mod(x - y * 2, 17) == 0) c = G.Mix(c, st.AltDark, 0.4f);
                    return c;
                }
                default:
                {
                    int ly = Mod(y, 4);
                    Color c = ly == 0 ? st.AltLight : (ly == 3 ? st.AltDark : st.Alt);
                    if (Mod(x, 8) == 3 && Mod(y, 8) == 2) c = st.CapLight;
                    if (Mod(x + y, 9) == 0 && ly != 3) c = G.Mix(c, st.AltLight, 0.6f);
                    return c;
                }
            }
        }

        private static Color BackPixel(Style st, int x, int y, int seed)
        {
            switch (st.Id)
            {
                case 1:
                {
                    int row = Floor(y, 6);
                    int ox = (row & 1) * 6;
                    int lx = Mod(x + ox, 12), ly = Mod(y, 6);
                    if (ly == 5 || lx == 11) return st.BackDark;
                    float h = Calc.Hash(Floor(x + ox, 12), row, seed + 21);
                    Color c = h < 0.3f ? G.Mix(st.Back, st.BackDark, 0.4f) : (h > 0.85f ? st.BackLight : st.Back);
                    if (Calc.Hash(x, y, seed + 9) > 0.95f) c = st.BackDark;
                    return c;
                }
                case 3:
                {
                    int lx = Mod(x, 24);
                    Color c = st.Back;
                    if (lx < 2) c = st.BackLight;
                    else if (lx < 3) c = st.BackDark;
                    if (Calc.Fbm(x / 9f, y / 9f, 2, seed) > 0.62f) c = G.Mix(c, st.BackLight, 0.4f);
                    return c;
                }
                default:
                {
                    float n = Calc.Fbm(x / 8f, y / 8f, 3, seed + 4);
                    return n < 0.4f ? st.BackDark : (n > 0.65f ? st.BackLight : st.Back);
                }
            }
        }

        private static void DecorateTop(Style st, int tx, int ty, bool alt, int seed, Action<int, int, Color> set, bool flat)
        {
            int bx = tx * 8, by = ty * 8;
            float h = Calc.Hash(tx, ty, seed + 31);
            switch (st.Id)
            {
                case 0:
                    // grass blades
                    for (int lx = 0; lx < 8; lx++)
                    {
                        float g = Calc.Hash(bx + lx, by, seed + 40);
                        if (g > 0.55f) set(bx + lx, by - 1, g > 0.8f ? st.CapLight : st.Cap);
                        if (g > 0.86f) set(bx + lx, by - 2, st.CapLight);
                    }
                    if (flat && h > 0.82f)
                    {
                        int fx = bx + 2 + (int)(Calc.Hash(tx, ty, seed + 32) * 4);
                        Color petal = Calc.Hash(tx, ty, seed + 33) > 0.5f ? st.Accent1 : st.Accent2;
                        set(fx, by - 1, st.CapDark); set(fx, by - 2, st.CapDark);
                        set(fx, by - 3, petal); set(fx - 1, by - 3, G.Mul(petal, 0.8f)); set(fx + 1, by - 3, G.Mul(petal, 0.8f));
                        set(fx, by - 4, G.Mul(petal, 0.85f)); set(fx, by - 3, G.Hex(0xfff6c8));
                    }
                    else if (flat && h > 0.74f)
                    {
                        int rx = bx + 1 + (int)(Calc.Hash(tx, ty, seed + 34) * 4);
                        set(rx, by - 1, st.AltDark); set(rx + 1, by - 1, st.Alt); set(rx + 2, by - 1, st.AltDark);
                        set(rx + 1, by - 2, st.AltLight);
                    }
                    break;
                case 1:
                    if (flat && h > 0.8f)
                    {
                        // rubble pebbles
                        int rx = bx + (int)(Calc.Hash(tx, ty, seed + 35) * 5);
                        set(rx, by - 1, st.Dark); set(rx + 1, by - 1, st.Light); set(rx + 2, by - 1, st.Base);
                        set(rx + 1, by - 2, st.Light);
                    }
                    else if (!alt && h > 0.55f)
                    {
                        for (int lx = 0; lx < 8; lx++)
                            if (Calc.Hash(bx + lx, by, seed + 41) > 0.6f) set(bx + lx, by - 1, st.CapDark);
                    }
                    else if (flat && h > 0.5f && h < 0.53f)
                    {
                        // a small candle left by earlier pilgrims
                        int cx = bx + 4;
                        set(cx, by - 1, st.AltLight); set(cx, by - 2, st.AltLight); set(cx, by - 3, st.Accent1);
                    }
                    break;
                case 2:
                    // snow mounds and the odd ice shard
                    for (int lx = 0; lx < 8; lx++)
                    {
                        float n = Calc.ValueNoise((bx + lx) / 3f, 5, seed);
                        if (n > 0.62f) set(bx + lx, by - 1, st.CapLight);
                    }
                    if (flat && h > 0.86f)
                    {
                        int sx = bx + 2 + (int)(Calc.Hash(tx, ty, seed + 36) * 4);
                        set(sx, by - 1, st.Alt); set(sx, by - 2, st.AltLight); set(sx, by - 3, st.AltLight);
                        set(sx + 1, by - 1, st.AltDark); set(sx + 1, by - 2, st.Alt);
                    }
                    break;
                default:
                    if (flat && h > 0.83f)
                    {
                        int fx = bx + 2 + (int)(Calc.Hash(tx, ty, seed + 37) * 4);
                        set(fx, by - 1, st.CapDark); set(fx, by - 2, st.Accent2); set(fx - 1, by - 2, G.Mul(st.Accent2, 0.7f));
                        set(fx + 1, by - 2, G.Mul(st.Accent2, 0.7f)); set(fx, by - 3, st.CapLight);
                    }
                    break;
            }
        }

        private static void DecorateBottom(Style st, int tx, int ty, bool alt, int seed, Action<int, int, Color> set)
        {
            int bx = tx * 8, by = ty * 8 + 8;
            for (int lx = 1; lx < 7; lx++)
            {
                float h = Calc.Hash(bx + lx, by, seed + 50);
                switch (st.Id)
                {
                    case 0:
                        if (h > 0.86f)
                        {
                            int len = 1 + (int)(Calc.Hash(bx + lx, by, seed + 51) * 4);
                            for (int k = 0; k < len; k++) set(bx + lx + (k == len - 1 && h > 0.93f ? 1 : 0), by + k, st.Dark);
                        }
                        break;
                    case 1:
                        if (h > 0.9f && !alt)
                        {
                            int len = 2 + (int)(Calc.Hash(bx + lx, by, seed + 52) * 7);
                            for (int k = 0; k < len; k++) set(bx + lx, by + k, k % 3 == 2 ? st.CapLight : st.CapDark);
                        }
                        break;
                    case 2:
                        if (h > 0.8f)
                        {
                            int len = 2 + (int)(Calc.Hash(bx + lx, by, seed + 53) * 5);
                            for (int k = 0; k < len; k++)
                            {
                                set(bx + lx, by + k, k == 0 ? st.AltLight : st.Alt);
                                if (k < len / 2) set(bx + lx + 1, by + k, st.AltDark);
                            }
                            lx += 2;
                        }
                        break;
                    default:
                        if (h > 0.93f)
                        {
                            set(bx + lx, by, st.AltDark);
                            set(bx + lx, by + 1, st.Alt);
                            set(bx + lx, by + 2, st.CapLight);
                        }
                        break;
                }
            }
        }

        private static void Voronoi(float x, float y, int seed, out float f1, out float f2, out int cell)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            f1 = 9; f2 = 9; cell = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j;
                    float px = cx + 0.15f + Calc.Hash(cx, cy, seed) * 0.7f;
                    float py = cy + 0.15f + Calc.Hash(cx, cy, seed + 1) * 0.7f;
                    float d = MathF.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; cell = cx * 7919 + cy * 104729; }
                    else if (d < f2) f2 = d;
                }
        }

        private static int Floor(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;
        private static int Mod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }
    }
}
