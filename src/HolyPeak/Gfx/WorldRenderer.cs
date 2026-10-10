using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;
using HolyPeak.World;

namespace HolyPeak.Gfx
{
    /// <summary>Draws a room: its painted terrain, everything that moves or changes in it, and the lights it casts.</summary>
    public sealed class WorldRenderer : IDisposable
    {
        public static readonly Color GlassColor = G.Hex(0xffd36a);
        public static readonly Color ResonanceColor = G.Hex(0x9a8cff);
        public static readonly Color EmberColor = G.Hex(0xff9a3a);
        public static readonly Color NoteColor = G.Hex(0xffe08a);

        private readonly Room _room;
        private readonly Style _st;
        private Texture2D _front, _back;
        private readonly Dictionary<int, Texture2D> _walls = new Dictionary<int, Texture2D>();
        private List<Vector2> _lights;
        private readonly List<(int x, int y0, int y1)> _gateCols = new List<(int, int, int)>();
        private readonly List<(int x, int y0, int y1)> _doorCols = new List<(int, int, int)>();
        public float DoorAnim;
        public bool ShrineActive;   // the shrine here is the pilgrim's respawn point
        public int RegionStyle => _st.Id;

        public WorldRenderer(Room room, Style st)
        {
            _room = room;
            _st = st;
            TileArt.Build(room, st, out _front, out _back, out _lights);
            for (int i = 0; i < room.Groups.Length; i++)
                if (room.Groups[i].Kind == Tile.Breakable && !room.Groups[i].Gone)
                    _walls[i] = TileArt.BuildBreakable(room, st, room.Groups[i]);
            FindColumns(Tile.Gate, _gateCols);
            FindColumns(Tile.ArenaDoor, _doorCols);
            DoorAnim = room.ArenaClosed ? 1 : 0;
        }

        private void FindColumns(Tile kind, List<(int, int, int)> into)
        {
            for (int tx = 0; tx < _room.Cols; tx++)
            {
                int ty = 0;
                while (ty < _room.Rows)
                {
                    if (_room.TileAt(tx, ty) != kind) { ty++; continue; }
                    int y0 = ty;
                    while (ty < _room.Rows && _room.TileAt(tx, ty) == kind) ty++;
                    into.Add((tx, y0, ty - 1));
                }
            }
        }

        /// <summary>A wall broke: repaint the terrain so the hole gets proper edges.</summary>
        public void OnWallBroken(int group)
        {
            if (_walls.TryGetValue(group, out var t)) { t.Dispose(); _walls.Remove(group); }
            _front.Dispose(); _back.Dispose();
            TileArt.Build(_room, _st, out _front, out _back, out _lights);
        }

        public void Dispose()
        {
            _front.Dispose();
            _back.Dispose();
            foreach (var t in _walls.Values) t.Dispose();
        }

        public void Update(float dt)
        {
            DoorAnim = Calc.Approach(DoorAnim, _room.ArenaClosed ? 1 : 0, dt * 3f);
        }

        public void DrawBack(SpriteBatch sb) => sb.Draw(_back, Vector2.Zero, Color.White);
        public void DrawTerrain(SpriteBatch sb) => sb.Draw(_front, Vector2.Zero, Color.White);

        // ---- dynamic tiles -----------------------------------------------------------------

        public void DrawDynamic(SpriteBatch sb, float time)
        {
            var room = _room;
            foreach (var kv in _walls)
            {
                var g = room.Groups[kv.Key];
                if (g.Gone) continue;
                int shake = (int)(time * 60) % 2;
                var off = g.Hp < Room.WallHp && time - _wallHitT < 0.15f ? new Vector2(shake * 2 - 1, 0) : Vector2.Zero;
                sb.Draw(kv.Value, g.Rect.Location.ToVector2() + off, Color.White);
            }

            for (int i = 0; i < room.Groups.Length; i++)
            {
                var g = room.Groups[i];
                if (g.Kind == Tile.BellGlass && !g.Gone) DrawGlass(sb, g, time);
                else if (g.Kind == Tile.Resonance) DrawResonance(sb, g, time);
            }

            // portcullis gates: bars slide up into the ceiling as they open
            Color iron = G.Hex(0x3a3848), ironL = G.Hex(0x7a788c), ironD = G.Hex(0x1a1822);
            foreach (var (x, y0, y1) in _gateCols)
            {
                float full = (y1 - y0 + 1) * 8;
                float h = MathF.Round(full * (1 - room.GateAnim));
                int px = x * 8, py = y0 * 8;
                if (h <= 0) { G.Rect(sb, px, py - 2, 8, 2, ironD); continue; }
                for (int b = 1; b < 8; b += 3)
                {
                    G.Rect(sb, px + b, py, 2, h, iron);
                    G.Rect(sb, px + b, py, 1, h, ironL);
                    G.Rect(sb, px + b, py + h - 2, 2, 3, ironD);
                    G.Rect(sb, px + b, py + h, 2, 1, ironL);
                }
                for (float yy = py + 6; yy < py + h - 2; yy += 10) G.Rect(sb, px, yy, 8, 2, iron);
                G.Rect(sb, px, py - 2, 8, 2, ironD);
            }

            // arena doors: thorned stone that grinds up out of the floor
            if (DoorAnim > 0.01f)
            {
                Color stone = G.Mix(_st.Dark, _st.Base, 0.5f), stoneL = _st.Light, rune = G.Hex(0xff5a4a);
                foreach (var (x, y0, y1) in _doorCols)
                {
                    float full = (y1 - y0 + 1) * 8;
                    float h = MathF.Round(full * Calc.EaseOutCubic(DoorAnim));
                    int px = x * 8;
                    float top = (y1 + 1) * 8 - h;
                    G.Rect(sb, px, top, 8, h, _st.Outline);
                    G.Rect(sb, px + 1, top + 1, 6, h - 1, stone);
                    G.Rect(sb, px + 1, top + 1, 2, h - 1, stoneL);
                    for (float yy = top + 4; yy < top + h - 3; yy += 9)
                        G.Rect(sb, px + 3, yy, 2, 3, rune * (0.6f + 0.4f * MathF.Sin(time * 5 + yy)));
                    for (int k = 0; k < 3; k++) G.Rect(sb, px + 2 + k * 2, top - 1 - (k % 2), 1, 2, _st.Spike);
                }
            }

            // updrafts: rising streaks of wind
            for (int ty = 0; ty < room.Rows; ty++)
                for (int tx = 0; tx < room.Cols; tx++)
                {
                    if (!room.Updraft[ty * room.Cols + tx]) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        float h = Calc.Hash(tx, ty * 3 + k, 77);
                        float y = ty * 8 + 8 - ((time * (40 + h * 30) + h * 16) % 16);
                        if (y < ty * 8) continue;
                        float x = tx * 8 + 1 + h * 6 + MathF.Sin(time * 3 + h * 9) * 1;
                        G.Rect(sb, MathF.Round(x), MathF.Round(y), 1, 3, Color.White * (0.18f + h * 0.15f));
                    }
                }
        }

        private float _wallHitT = -9;
        public void OnWallHit(float time) => _wallHitT = time;

        private void DrawGlass(SpriteBatch sb, in TileGroup g, float time)
        {
            for (int ty = g.Y0; ty <= g.Y1; ty++)
                for (int tx = g.X0; tx <= g.X1; tx++)
                {
                    if (_room.GroupOf[ty * _room.Cols + tx] != g.Index) continue;
                    int px = tx * 8, py = ty * 8;
                    G.Rect(sb, px, py, 8, 8, GlassColor * 0.35f);
                    bool up = _room.TileAt(tx, ty - 1) != Tile.BellGlass, dn = _room.TileAt(tx, ty + 1) != Tile.BellGlass;
                    bool lf = _room.TileAt(tx - 1, ty) != Tile.BellGlass, rt = _room.TileAt(tx + 1, ty) != Tile.BellGlass;
                    Color rim = G.Hex(0xa8742a);
                    if (up) G.Rect(sb, px, py, 8, 1, rim);
                    if (dn) G.Rect(sb, px, py + 7, 8, 1, rim);
                    if (lf) G.Rect(sb, px, py, 1, 8, rim);
                    if (rt) G.Rect(sb, px + 7, py, 1, 8, rim);
                    // leaded panes and a glint that sweeps across
                    if (((tx + ty) & 1) == 0) G.Rect(sb, px + 3, py + 1, 1, 6, G.Hex(0x8a5a1a) * 0.7f);
                    float sweep = (time * 0.6f + (tx + ty) * 0.07f) % 2f;
                    if (sweep < 0.25f) G.Rect(sb, px + 1 + (int)(sweep * 24) % 6, py + 1, 1, 6, Color.White * 0.6f);
                    G.Rect(sb, px + 1, py + 1, 1, 2, Color.White * 0.5f);
                }
            // a small bell glyph marks it as answerable
            var c = g.Rect.Center;
            DrawBellGlyph(sb, new Vector2(c.X, c.Y), G.Hex(0x6a4210) * 0.8f);
        }

        private static void DrawBellGlyph(SpriteBatch sb, Vector2 c, Color col)
        {
            int x = (int)c.X, y = (int)c.Y;
            G.Rect(sb, x - 1, y - 3, 2, 1, col);
            G.Rect(sb, x - 2, y - 2, 4, 3, col);
            G.Rect(sb, x - 3, y + 1, 6, 1, col);
            G.Rect(sb, x, y + 2, 1, 1, col);
        }

        private void DrawResonance(SpriteBatch sb, in TileGroup g, float time)
        {
            bool solid = g.Timer > 0 && !g.Pending;
            float fade = solid && g.Timer < 0.7f ? (((int)(g.Timer * 16) & 1) == 0 ? 0.45f : 1f) : 1f;
            for (int ty = g.Y0; ty <= g.Y1; ty++)
                for (int tx = g.X0; tx <= g.X1; tx++)
                {
                    if (_room.GroupOf[ty * _room.Cols + tx] != g.Index) continue;
                    int px = tx * 8, py = ty * 8;
                    if (solid)
                    {
                        G.Rect(sb, px, py, 8, 8, G.Hex(0x221a4a) * fade);
                        G.Rect(sb, px + 1, py + 1, 6, 6, ResonanceColor * fade);
                        G.Rect(sb, px + 1, py + 1, 6, 1, G.Mix(ResonanceColor, Color.White, 0.6f) * fade);
                        G.Rect(sb, px + 2, py + 3, 2, 2, Color.White * (0.6f * fade));
                    }
                    else
                    {
                        float a = 0.25f + 0.12f * MathF.Sin(time * 2 + tx * 0.7f + ty);
                        if (g.Pending) a = 0.6f;
                        for (int k = 0; k < 8; k += 2)
                        {
                            G.Rect(sb, px + k, py, 1, 1, ResonanceColor * a);
                            G.Rect(sb, px + k + 1, py + 7, 1, 1, ResonanceColor * a);
                            G.Rect(sb, px, py + k + 1, 1, 1, ResonanceColor * a);
                            G.Rect(sb, px + 7, py + k, 1, 1, ResonanceColor * a);
                        }
                    }
                }
        }

        // ---- things ------------------------------------------------------------------------

        public void DrawThings(SpriteBatch sb, float time, Abilities owned)
        {
            for (int i = 0; i < _room.Things.Length; i++)
            {
                var t = _room.Things[i];
                var feet = new Vector2(t.X, t.Y);
                switch (t.Kind)
                {
                    case ThingKind.Shrine:
                        Sprites.DrawFeet(sb, "shrine", feet, Color.White);
                        break;
                    case ThingKind.Merchant:
                        Sprites.DrawFeet(sb, ((int)(time * 1.5f) & 1) == 0 ? "merchant0" : "merchant1", feet + new Vector2(-2, 0), Color.White);
                        // hanging lantern on the pack
                        G.Rect(sb, t.X - 9, t.Y - 15, 3, 4, G.Hex(0xffd070));
                        break;
                    case ThingKind.Npc:
                        Sprites.DrawFeet(sb, ((int)(time * 1.1f) & 1) == 0 ? "npc0" : "npc1", feet, Color.White, flipX: _room.P.Center.X < t.X);
                        break;
                    case ThingKind.Tablet:
                        Sprites.DrawFeet(sb, "tablet", feet, Color.White);
                        break;
                    case ThingKind.Heart:
                        if (!t.Used) Sprites.DrawCentered(sb, "vessel", feet + new Vector2(0, MathF.Sin(time * 2.5f) * 1.5f), Color.White);
                        break;
                    case ThingKind.Flask:
                        if (!t.Used) Sprites.DrawCentered(sb, "balm", feet + new Vector2(0, MathF.Sin(time * 2.5f + 1) * 1.5f), Color.White);
                        break;
                    case ThingKind.Cache:
                        if (!t.Used) Sprites.DrawFeet(sb, t.Hp <= Room.CacheHp / 2 ? "cache1" : "cache0", feet, Color.White);
                        else
                        {
                            G.Rect(sb, t.X - 6, t.Y - 2, 12, 2, G.Hex(0x3a2e2c));
                            G.Rect(sb, t.X - 4, t.Y - 3, 3, 1, G.Hex(0x5a4a46));
                        }
                        break;
                    case ThingKind.Altar:
                    {
                        Sprites.DrawFeet(sb, "altar", feet, Color.White);
                        if (!t.Used)
                        {
                            string icon = _room.Def.Prop("ability") switch { "grips" => "grips", "bell" => "handbell", _ => "kite" };
                            Sprites.DrawCentered(sb, icon, feet + new Vector2(0, -20 + MathF.Sin(time * 2f) * 2f), Color.White);
                        }
                        break;
                    }
                    case ThingKind.Lever:
                        DrawLever(sb, t);
                        break;
                    case ThingKind.GreatBell:
                        DrawGreatBell(sb, t, time);
                        break;
                    case ThingKind.Shade:
                        if (!t.Used)
                        {
                            float a = 0.55f + 0.2f * MathF.Sin(time * 4);
                            Sprites.DrawFeet(sb, "idle0", feet + new Vector2(0, MathF.Sin(time * 2) * 1.5f - 2), G.Hex(0x2a1a4a) * a, white: true);
                        }
                        break;
                }
            }
        }

        public float BellSwing, BellTime;

        private void DrawGreatBell(SpriteBatch sb, in Thing t, float time)
        {
            float cx = t.X, baseY = t.Y;
            Color wood = G.Hex(0x6b4426), woodD = G.Hex(0x3e2614), woodL = G.Hex(0x9c6b3f);
            G.Rect(sb, cx - 22, baseY - 46, 4, 46, wood); G.Rect(sb, cx - 22, baseY - 46, 1, 46, woodL);
            G.Rect(sb, cx + 18, baseY - 46, 4, 46, wood); G.Rect(sb, cx + 18, baseY - 46, 1, 46, woodL);
            G.Rect(sb, cx - 27, baseY - 50, 54, 4, woodD); G.Rect(sb, cx - 27, baseY - 50, 54, 1, woodL);
            for (int i = 0; i < 7; i++) G.Rect(sb, cx - 27 + i * 3, baseY - 53 - i, 54 - i * 6, 1, i == 6 ? woodL : wood);
            G.Rect(sb, cx - 1, baseY - 46, 2, 3, woodD);
            var f = Sprites.Frames["bell"];
            float ang = MathF.Sin(BellTime * 3.2f) * BellSwing;
            sb.Draw(Sprites.Sheet, new Vector2(cx, baseY - 44), f, Color.White, ang, new Vector2(f.Width / 2f, 0), 2f, SpriteEffects.None, 0f);
        }

        private void DrawLever(SpriteBatch sb, in Thing t)
        {
            Color o = _st.Outline, metal = G.Hex(0x8a8aa0), metalL = G.Hex(0xc8c8d8), wood = G.Hex(0x8a5a32);
            float bx = t.X, by = t.Y;
            G.Rect(sb, bx - 4, by - 3, 8, 3, o);
            G.Rect(sb, bx - 3, by - 3, 6, 2, metal);
            G.Rect(sb, bx - 3, by - 3, 6, 1, metalL);
            float a = t.Used ? 0.6f : -0.6f;
            var top = new Vector2(bx + MathF.Sin(a) * 9, by - 3 - MathF.Cos(a) * 9);
            PlayerVisual.PixelLine(sb, new Vector2(bx, by - 3), top, o, 3);
            PlayerVisual.PixelLine(sb, new Vector2(bx, by - 3), top, wood);
            G.Rect(sb, MathF.Round(top.X) - 1, MathF.Round(top.Y) - 1, 3, 3, G.Hex(0xd8443c));
        }

        // ---- creatures ---------------------------------------------------------------------

        public void DrawEnemies(SpriteBatch sb, float time)
        {
            foreach (var e in _room.Enemies)
            {
                if (!e.Alive) continue;
                string frame;
                var feet = new Vector2(e.X + e.W / 2f, e.Y + e.H);
                bool flip = e.Facing < 0;
                float rot = 0;
                bool flipY = false;
                var center = e.Center;
                bool useCenter = false;
                switch (e.Type)
                {
                    case EnemyType.Crawler:
                        frame = ((int)(e.Anim * 8) & 1) == 0 ? "crawler0" : "crawler1";
                        break;
                    case EnemyType.Hopper:
                        frame = e.State == 1 ? "hopper1" : (e.State == 2 ? "hopper2" : "hopper0");
                        if (e.State == 1) feet.X += ((int)(time * 40) & 1) == 0 ? -1 : 1;
                        break;
                    case EnemyType.Wisp:
                        frame = e.State == 2 ? "wisp2" : (((int)(e.Anim * 6) & 1) == 0 ? "wisp0" : "wisp1");
                        useCenter = true;
                        if (e.State == 2) center.X += ((int)(time * 50) & 1) == 0 ? -1 : 1;
                        break;
                    case EnemyType.Sentinel:
                        frame = e.State >= 2 || e.Stun > 0 ? "sentinelLow" : ((int)(e.Anim * 4) & 1) == 0 || e.State == 1 && MathF.Abs(e.Speed.X) < 1 ? "sentinel0" : "sentinel1";
                        if (e.State == 2) feet.X -= e.Facing * (1 + ((int)(time * 30) & 1));
                        break;
                    case EnemyType.Spitter:
                        frame = e.State == 1 ? "spitter1" : "spitter0";
                        useCenter = true;
                        flip = false;
                        switch (e.Orient)
                        {
                            case 1: flipY = true; break;
                            case 2: rot = MathF.PI / 2; break;
                            case 3: rot = -MathF.PI / 2; break;
                        }
                        break;
                    default:
                        frame = e.State <= 1 ? "dropper0" : (((int)(e.Anim * 10) & 1) == 0 ? "dropper1" : "dropper2");
                        if (e.State == 1) feet.X += ((int)(time * 40) & 1) == 0 ? -1 : 1;
                        if (e.State == 0) { useCenter = false; feet.Y = e.Y + 9; }
                        break;
                }
                Color c = Color.White;
                if (useCenter) Sprites.DrawCentered(sb, frame, center, c, flip, rot, flipY: flipY);
                else Sprites.DrawFeet(sb, frame, feet, c, flip);
                if (e.Flash > 0)
                {
                    if (useCenter) Sprites.DrawCentered(sb, frame, center, Color.White * (e.Flash / 0.14f), flip, rot, white: true, flipY: flipY);
                    else Sprites.DrawFeet(sb, frame, feet, Color.White * (e.Flash / 0.14f), flip, white: true);
                }
                if (e.Type == EnemyType.Sentinel)
                {
                    // the spear
                    int f = e.Facing;
                    float y = e.Y + 8;
                    float reach = e.State == 3 ? 16 : (e.State == 2 ? -3 : 4);
                    var a = new Vector2(e.X + e.W / 2f + f * 2, y);
                    var b = new Vector2(e.X + e.W / 2f + f * (8 + reach), y);
                    PlayerVisual.PixelLine(sb, a, b, G.Hex(0x14101a), 3);
                    PlayerVisual.PixelLine(sb, a, b, G.Hex(0x6a4420));
                    G.Rect(sb, MathF.Round(b.X) + (f > 0 ? 0 : -2), MathF.Round(b.Y) - 1, 3, 3, G.Hex(0xcfd2e0));
                }
                if (e.Stun > 0)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float a = time * 6 + k * 2.1f;
                        var p = new Vector2(e.X + e.W / 2f + MathF.Cos(a) * 6, e.Y - 3 + MathF.Sin(a) * 2);
                        G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), 1, 1, NoteColor);
                    }
                }
            }
        }

        // ---- guardians ---------------------------------------------------------------------

        public void DrawBoss(SpriteBatch sb, float time)
        {
            var b = _room.Boss;
            if (b == null || (b.Finished && !(b is FrostWyrm))) return;
            if (b.Finished) return;
            float flash = b.Flash > 0 ? b.Flash / 0.12f : 0;
            float death = b.Dead ? b.DeathT / Boss.DeathTime : 0;
            switch (b)
            {
                case Thornback t: DrawThornback(sb, t, time, flash, death); break;
                case HollowAbbot a: DrawAbbot(sb, a, time, flash, death); break;
                case FrostWyrm w: DrawWyrm(sb, w, time, flash, death); break;
                case Bellkeeper k: DrawKeeper(sb, k, time, flash, death); break;
            }
        }

        private static void Flashy(SpriteBatch sb, string frame, Vector2 feet, bool flip, float flash, float alpha = 1f, float shake = 0)
        {
            if (shake > 0) feet.X += (((int)(feet.Y * 7 + shake * 1000) & 1) == 0 ? -1 : 1) * MathF.Ceiling(shake);
            Sprites.DrawFeet(sb, frame, feet, Color.White * alpha, flip);
            if (flash > 0) Sprites.DrawFeet(sb, frame, feet, Color.White * (flash * alpha), flip, white: true);
        }

        private void DrawThornback(SpriteBatch sb, Thornback t, float time, float flash, float death)
        {
            string frame = "thornback0";
            float shake = 0;
            if (t.Telegraphing) { frame = "thornbackLow"; shake = 1; }
            else if (t.Charging) frame = ((int)(time * 16) & 1) == 0 ? "thornback0" : "thornback1";
            else if (Math.Abs(t.VX) > 1) frame = ((int)(time * 6) & 1) == 0 ? "thornback0" : "thornback1";
            else if (t.Mode == Thornback.S.Stunned) { shake = 0.5f; }
            if (death > 0) { frame = "thornbackLow"; shake = death < 0.7f ? 1 : 0; }
            Flashy(sb, frame, new Vector2(t.X, t.Y), t.Facing < 0, flash, 1 - Calc.Clamp01((death - 0.75f) * 4), shake);
        }

        private void DrawAbbot(SpriteBatch sb, HollowAbbot a, float time, float flash, float death)
        {
            float alpha = a.Alpha * (1 - Calc.Clamp01((death - 0.8f) * 5));
            if (alpha <= 0.01f) return;
            var feet = new Vector2(a.X, a.Y + MathF.Sin(time * 2.4f) * 1.5f);
            // censer on its chain
            int f = a.Facing;
            var hand = feet + new Vector2(f * 7, -16);
            float ang;
            if (a.Raising) ang = -2.3f;
            else if (a.Swinging) ang = Calc.Lerp(-2.3f, 0.9f, Calc.EaseOutCubic(1 - a.T / 0.26f));
            else ang = 1.4f + MathF.Sin(time * 2.2f) * 0.35f;
            var dir = new Vector2(MathF.Cos(ang) * f, MathF.Sin(ang));
            if (f < 0) dir.X = -MathF.Cos(ang);
            var ball = hand + dir * (a.Swinging || a.Raising ? 22 : 12);
            PlayerVisual.PixelLine(sb, hand, ball, G.Hex(0x9a7430) * alpha);
            G.DiscAt(sb, ball, 4, G.Hex(0x0e0a14) * alpha);
            G.DiscAt(sb, ball, 3, G.Hex(0xd8b05a) * alpha);
            Flashy(sb, "abbot", feet, f < 0, flash, alpha);
            if (a.Summoning)
                for (int i = 0; i < a.FlameCount; i++) DrawFlame(sb, a.Flames[i], time + i, alpha);
        }

        private static void DrawFlame(SpriteBatch sb, Vector2 p, float time, float alpha)
        {
            int fr = (int)(time * 10) % 3;
            Sprites.DrawCentered(sb, "ember" + fr, p, Color.White * alpha);
        }

        private void DrawWyrm(SpriteBatch sb, FrostWyrm w, float time, float flash, float death)
        {
            Color o = G.Hex(0x0a1020), ice = G.Hex(0x9fd4f0), iceL = G.Hex(0xe2f6ff), iceD = G.Hex(0x4a7ea8);
            float floor = w.Floor;
            // body, tail first
            for (int i = FrostWyrm.Segments - 1; i >= 0; i--)
            {
                var s = w.Segment(i);
                if (s.Y > floor + 3) continue;
                float rad = i < 3 ? 7 : (i < 7 ? 6 : (i < 10 ? 5 : 4));
                G.DiscAt(sb, s, rad + 1, o);
                G.DiscAt(sb, s, rad, i % 2 == 0 ? ice : iceD);
                G.Rect(sb, MathF.Round(s.X) - 1, MathF.Round(s.Y - rad + 1), 2, 2, iceL);
                // dorsal spike
                G.Rect(sb, MathF.Round(s.X), MathF.Round(s.Y - rad - 2), 1, 2, iceL);
                if (flash > 0 && i < 3) G.DiscAt(sb, s, rad, Color.White * flash);
            }
            if (w.Head.Y < floor + 4)
            {
                var prev = w.Segment(0);
                var d = w.Head - prev;
                float ang = d.LengthSquared() > 0.5f ? MathF.Atan2(d.Y, d.X) : (w.Facing > 0 ? 0 : MathF.PI);
                if (w.Inhaling || w.Breathing) ang = w.Facing > 0 ? 0.25f : MathF.PI - 0.25f;
                bool flip = MathF.Cos(ang) < 0;
                float rot = flip ? ang - MathF.PI : ang;
                string frame = w.Breathing || w.Inhaling ? "wyrmHeadOpen" : "wyrmHead";
                var r = Sprites.Frames[frame];
                var fx = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                var pos = new Vector2(MathF.Round(w.Head.X), MathF.Round(w.Head.Y));
                sb.Draw(Sprites.Sheet, pos, r, Color.White, rot, new Vector2(r.Width / 2f, r.Height / 2f), 1f, fx, 0f);
                if (flash > 0) sb.Draw(Sprites.SheetWhite, pos, r, Color.White * flash, rot, new Vector2(r.Width / 2f, r.Height / 2f), 1f, fx, 0f);
            }
            // snow mound where it will burst out
            if (w.Plume)
            {
                var p = w.PlumeAt;
                float k = 0.5f + 0.5f * MathF.Sin(time * 30);
                for (int i = -2; i <= 2; i++) G.Rect(sb, p.X + i * 4 - 2, p.Y - 2 - (2 - Math.Abs(i)) - k, 4, 2 + (2 - Math.Abs(i)), iceL);
            }
        }

        private void DrawKeeper(SpriteBatch sb, Bellkeeper k, float time, float flash, float death)
        {
            int f = k.Facing;
            var feet = new Vector2(k.X, k.Y);
            float alpha = 1 - Calc.Clamp01((death - 0.8f) * 5);
            float shake = (k.DashTelling || k.Crouching || k.TollRaising) ? 1 : 0;
            if (k.Crouching) feet.Y += 2;
            // hammer: handle from the hand, head at the end
            var hand = feet + new Vector2(f * 10, -22);
            float ang;
            if (k.Raising || k.TollRaising) ang = -1.9f;
            else if (k.Slamming) ang = 0.9f;
            else if (k.Dashing || k.DashTelling) ang = -0.3f;
            else ang = 0.5f + MathF.Sin(time * 1.5f) * 0.05f;
            var dir = new Vector2(MathF.Cos(ang) * f, MathF.Sin(ang));
            var head = hand + dir * 20;
            PlayerVisual.PixelLine(sb, hand, head, G.Hex(0x100c18) * alpha, 4);
            PlayerVisual.PixelLine(sb, hand, head, G.Hex(0x6a4428) * alpha, 2);
            var perp = new Vector2(-dir.Y, dir.X);
            for (int i = -5; i <= 5; i++)
            {
                var q = head + perp * i;
                G.Rect(sb, MathF.Round(q.X) - 3, MathF.Round(q.Y) - 3, 6, 6, G.Hex(0x100c18) * alpha);
            }
            for (int i = -4; i <= 4; i++)
            {
                var q = head + perp * i;
                G.Rect(sb, MathF.Round(q.X) - 2, MathF.Round(q.Y) - 2, 4, 4, (Math.Abs(i) > 2 ? G.Hex(0xb8862a) : G.Hex(0xf0c04c)) * alpha);
            }
            Flashy(sb, "bellkeeper", feet, f < 0, flash, alpha, shake);
        }

        // ---- projectiles and embers ------------------------------------------------------

        public void DrawProjectiles(SpriteBatch sb, float time)
        {
            foreach (var p in _room.Projectiles)
            {
                if (!p.Active) continue;
                var pos = new Vector2(p.X, p.Y);
                bool armed = p.Armed;
                switch (p.Type)
                {
                    case ProjType.Glob:
                        G.DiscAt(sb, pos, 3.5f, G.Hex(0x101410));
                        G.DiscAt(sb, pos, 2.5f, G.Hex(0x8cc05a));
                        G.Rect(sb, MathF.Round(p.X) - 1, MathF.Round(p.Y) - 1, 1, 1, G.Hex(0xe8ff70));
                        break;
                    case ProjType.Orb:
                        DrawFlame(sb, pos, time + p.T, 1f);
                        break;
                    case ProjType.Shard:
                    {
                        var d = new Vector2(p.VX, p.VY);
                        if (d.LengthSquared() > 0) d.Normalize();
                        PlayerVisual.PixelLine(sb, pos - d * 4, pos + d * 2, G.Hex(0x4a7ea8));
                        PlayerVisual.PixelLine(sb, pos - d * 2, pos + d * 2, G.Hex(0xe2f6ff));
                        break;
                    }
                    case ProjType.Flame:
                        if (!armed)
                        {
                            // a candle stub on the ceiling, dripping
                            G.Rect(sb, p.X - 2, p.Y - 4, 4, 5, G.Hex(0xf0e2c0));
                            G.Rect(sb, p.X - 2, p.Y - 4, 1, 5, G.Hex(0xc0a888));
                            if (((int)(p.T * 10) & 1) == 0) G.Rect(sb, p.X, p.Y + 2 + (p.T * 20) % 6, 1, 2, G.Hex(0xffd070));
                        }
                        else DrawFlame(sb, pos, time + p.T, 1f);
                        break;
                    case ProjType.Wave:
                    {
                        float k = (p.T * 14) % 2;
                        for (int i = 0; i < 4; i++)
                        {
                            float h = 9 - i * 2 + (k > 1 ? 1 : 0);
                            G.Rect(sb, MathF.Round(p.X) - 4 + i * 2 * Math.Sign(-p.VX) + 3, p.Y - h, 2, h, (i == 0 ? Color.White : _st.Light2) * (0.9f - i * 0.18f));
                        }
                        break;
                    }
                    case ProjType.Thorn:
                    {
                        if (!armed)
                        {
                            // cracks spreading in the ground
                            float k = p.T / p.Delay;
                            for (int i = -2; i <= 2; i++)
                                if (Calc.Hash((int)p.X + i, (int)(p.T * 20)) < k) G.Rect(sb, MathF.Round(p.X) + i * 2, p.Y - 1, 1, 1, _st.Spike);
                            break;
                        }
                        float h = Room.ThornHeight(p);
                        for (int y = 0; y < h; y++)
                        {
                            float w = Math.Max(1, (1 - y / Math.Max(1, h)) * 4);
                            G.Rect(sb, MathF.Round(p.X - w / 2 - 0.5f), p.Y - y - 1, MathF.Ceiling(w) + 1, 1, _st.Outline);
                            G.Rect(sb, MathF.Round(p.X - w / 2), p.Y - y - 1, MathF.Ceiling(w), 1, y % 4 < 2 ? _st.Spike : _st.SpikeDark);
                        }
                        break;
                    }
                    case ProjType.Icicle:
                    case ProjType.Rock:
                    {
                        bool ice = p.Type == ProjType.Icicle;
                        float sx = armed ? 0 : (((int)(p.T * 30) & 1) == 0 ? -0.5f : 0.5f);
                        if (ice)
                        {
                            for (int y = 0; y < 8; y++)
                            {
                                float w = Math.Max(1, 4 - y / 2);
                                G.Rect(sb, MathF.Round(p.X - w / 2 + sx), MathF.Round(p.Y - 4 + y), w, 1, y < 2 ? G.Hex(0xe2f6ff) : G.Hex(0x9fd4f0));
                            }
                        }
                        else
                        {
                            G.DiscAt(sb, pos + new Vector2(sx, 0), 4.5f, _st.Outline);
                            G.DiscAt(sb, pos + new Vector2(sx, 0), 3.5f, _st.Base);
                            G.Rect(sb, MathF.Round(p.X) - 2, MathF.Round(p.Y) - 2, 2, 1, _st.Light);
                        }
                        if (!armed && ((int)(p.T * 12) & 1) == 0) G.Rect(sb, MathF.Round(p.X + Calc.Hash((int)(p.T * 12), 3) * 6 - 3), p.Y + 5 + (p.T * 30) % 10, 1, 1, _st.Light2 * 0.7f);
                        break;
                    }
                    case ProjType.Note:
                    {
                        float pulse = 0.8f + 0.2f * MathF.Sin(time * 10 + p.X);
                        G.DiscAt(sb, pos, 4f, G.Hex(0x3a2310) * pulse);
                        G.DiscAt(sb, pos, 3f, NoteColor * pulse);
                        G.Rect(sb, MathF.Round(p.X) - 1, MathF.Round(p.Y) - 1, 1, 1, Color.White);
                        break;
                    }
                    case ProjType.Ring:
                    {
                        int n = (int)(p.R * 1.4f) + 16;
                        for (int i = 0; i < n; i++)
                        {
                            float a = i / (float)n * MathF.PI * 2;
                            var q = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * p.R;
                            G.Rect(sb, MathF.Round(q.X) - 1, MathF.Round(q.Y) - 1, 3, 3, G.Hex(0x6a4210));
                            G.Rect(sb, MathF.Round(q.X), MathF.Round(q.Y), 1, 1, NoteColor);
                        }
                        break;
                    }
                }
            }
        }

        public void DrawDrops(SpriteBatch sb, float time)
        {
            foreach (var d in _room.Drops)
            {
                if (!d.Active) continue;
                int fr = (int)(time * 10 + d.X) % 3;
                if (d.Value >= 5) Sprites.DrawCentered(sb, "ember" + fr, new Vector2(d.X, d.Y), Color.White);
                else
                {
                    G.Rect(sb, MathF.Round(d.X) - 1, MathF.Round(d.Y) - 1, 3, 3, G.Hex(0x5a1a10));
                    G.Rect(sb, MathF.Round(d.X), MathF.Round(d.Y) - 1, 1, 2, G.Hex(0xffb23e));
                    G.Rect(sb, MathF.Round(d.X) - 1, MathF.Round(d.Y), 1, 1, G.Hex(0xe8452c));
                }
            }
        }

        // ---- text hints --------------------------------------------------------------------

        public void DrawHints(SpriteBatch sb, Vector2 player, float time)
        {
            foreach (var (pos, text) in _room.Hints) DrawHint(sb, text, pos, player, time);
        }

        private void DrawHint(SpriteBatch sb, string text, Point at, Vector2 player, float time)
        {
            var font = G.Font;
            string s = Input.Format(text);
            var lines = font.Wrap(s, 150);
            float dist = Vector2.Distance(player, at.ToVector2());
            float a = Calc.Clamp01(1.3f - dist / 140f);
            if (a <= 0.01f) return;
            float y = at.Y - lines.Count * PixelFont.LineHeight / 2f + MathF.Sin(time * 1.5f) * 0.6f;
            float widest = 0;
            foreach (var line in lines) widest = MathF.Max(widest, font.Measure(line));
            float cx = Math.Clamp(at.X, widest / 2f + 4, _room.Width - widest / 2f - 4);
            foreach (var line in lines)
            {
                font.DrawCentered(sb, line, new Vector2(cx, MathF.Round(y)), _st.Light2 * a, Color.Black * (a * 0.8f));
                y += PixelFont.LineHeight;
            }
        }

        // ---- light -------------------------------------------------------------------------

        /// <summary>Additive glows over the scene (the bright cores of lights).</summary>
        public void DrawGlows(SpriteBatch sb, float time)
        {
            foreach (var t in _room.Things)
            {
                var c = new Vector2(t.X, t.Y);
                switch (t.Kind)
                {
                    case ThingKind.Shrine:
                        G.GlowAt(sb, c + new Vector2(0, -10), 10 + MathF.Sin(time * 3) * 1.5f, G.Hex(0xffd070) * (ShrineActive ? 0.55f : 0.25f));
                        break;
                    case ThingKind.Heart:
                    case ThingKind.Flask:
                        if (!t.Used) G.GlowAt(sb, c, 12 + MathF.Sin(time * 3) * 2, EmberColor * 0.45f);
                        break;
                    case ThingKind.Altar:
                        if (!t.Used) G.GlowAt(sb, c + new Vector2(0, -20), 18 + MathF.Sin(time * 2) * 3, G.Hex(0xfff0c0) * 0.45f);
                        break;
                    case ThingKind.Cache:
                        if (!t.Used) G.GlowAt(sb, c + new Vector2(0, -6), 10, EmberColor * 0.35f);
                        break;
                    case ThingKind.Shade:
                        if (!t.Used) G.GlowAt(sb, c + new Vector2(0, -8), 14 + MathF.Sin(time * 4) * 2, G.Hex(0x8a5aff) * 0.45f);
                        break;
                    case ThingKind.GreatBell:
                        G.GlowAt(sb, c + new Vector2(0, -28), 40 + MathF.Sin(time * 1.5f) * 4, G.Hex(0xffd88a) * 0.25f);
                        break;
                }
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                float fl = 0.85f + 0.15f * MathF.Sin(time * 7f + i * 2.1f) * MathF.Sin(time * 3.3f + i);
                G.GlowAt(sb, _lights[i], 7 * fl, G.Hex(0xffc860) * 0.5f);
            }
            foreach (var e in _room.Enemies)
                if (e.Alive && e.Type == EnemyType.Wisp) G.GlowAt(sb, e.Center, 12, G.Hex(0x7fd8ff) * (e.State == 2 ? 0.7f : 0.4f));
            foreach (var p in _room.Projectiles)
            {
                if (!p.Active) continue;
                switch (p.Type)
                {
                    case ProjType.Orb: G.GlowAt(sb, new Vector2(p.X, p.Y), 10, EmberColor * 0.6f); break;
                    case ProjType.Flame: if (p.Armed) G.GlowAt(sb, new Vector2(p.X, p.Y), 9, EmberColor * 0.5f); break;
                    case ProjType.Note: G.GlowAt(sb, new Vector2(p.X, p.Y), 8, NoteColor * 0.45f); break;
                    case ProjType.Glob: G.GlowAt(sb, new Vector2(p.X, p.Y), 6, G.Hex(0xb0ff60) * 0.3f); break;
                    case ProjType.Ring:
                    {
                        int n = (int)(p.R * 0.25f) + 8;
                        for (int i = 0; i < n; i++)
                        {
                            float a = i / (float)n * MathF.PI * 2;
                            G.GlowAt(sb, new Vector2(p.X, p.Y) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * p.R, 10, NoteColor * 0.25f);
                        }
                        break;
                    }
                }
            }
            foreach (var d in _room.Drops)
                if (d.Active) G.GlowAt(sb, new Vector2(d.X, d.Y), d.Value >= 5 ? 7 : 4, EmberColor * 0.35f);
            for (int i = 0; i < _room.Groups.Length; i++)
            {
                var g = _room.Groups[i];
                if (g.Kind == Tile.Resonance && g.Timer > 0 && !g.Pending)
                {
                    var r = g.Rect;
                    G.GlowAt(sb, r.Center.ToVector2(), Math.Max(r.Width, r.Height) * 0.7f + 6, ResonanceColor * 0.3f);
                }
            }
            var b = _room.Boss;
            if (b != null && b.Active && !b.Finished)
            {
                switch (b)
                {
                    case HollowAbbot a:
                        if (a.Summoning) for (int i = 0; i < a.FlameCount; i++) G.GlowAt(sb, a.Flames[i], 10, EmberColor * 0.6f);
                        G.GlowAt(sb, new Vector2(a.X + a.Facing * 1, a.Y - 22), 6, G.Hex(0xb8f0ff) * (0.5f * a.Alpha));
                        break;
                    case Bellkeeper k:
                        if (k.ShieldUp)
                        {
                            var c = new Vector2(k.X, k.Y - 22);
                            float pulse = 0.8f + 0.2f * MathF.Sin(time * 4);
                            int n = 36;
                            for (int i = 0; i < n; i++)
                            {
                                float a2 = i / (float)n * MathF.PI * 2 + time * 0.5f;
                                var q = c + new Vector2(MathF.Cos(a2) * 24, MathF.Sin(a2) * 28);
                                G.Rect(sb, MathF.Round(q.X), MathF.Round(q.Y), 1, 1, G.Hex(0xffd36a) * (0.55f * pulse));
                            }
                            G.GlowAt(sb, c, 34, G.Hex(0xffd36a) * (0.18f * pulse));
                        }
                        if (k.ShieldFlash > 0) G.GlowAt(sb, new Vector2(k.X, k.Y - 22), 50, Color.White * (0.5f * k.ShieldFlash));
                        if (k.TollRaising) G.GlowAt(sb, new Vector2(k.X, k.Y - 50), 20 + 30 * (1 - k.StateT), NoteColor * 0.5f);
                        break;
                    case Thornback t:
                        if (t.Phase == 2) G.GlowAt(sb, new Vector2(t.X, t.Y - 22), 26, G.Hex(0xe86a8a) * 0.2f);
                        break;
                }
            }
        }

        /// <summary>Light sources painted into the light map (additive onto the region's darkness).</summary>
        public void DrawLights(SpriteBatch sb, float time, Vector2 cam)
        {
            Color warm = G.Hex(0xffe0b0), cool = G.Hex(0xb8d8ff);
            foreach (var t in _room.Things)
            {
                var c = new Vector2(t.X, t.Y - 8);
                switch (t.Kind)
                {
                    case ThingKind.Shrine: Light(sb, c, ShrineActive ? 70 : 50, warm); break;
                    case ThingKind.Merchant: Light(sb, c + new Vector2(-8, -6), 56, warm); break;
                    case ThingKind.Npc: Light(sb, c, 40, warm); break;
                    case ThingKind.Tablet: Light(sb, c, 26, cool * 0.6f); break;
                    case ThingKind.Altar: if (!t.Used) Light(sb, c + new Vector2(0, -14), 60, Color.White); break;
                    case ThingKind.Heart:
                    case ThingKind.Flask: if (!t.Used) Light(sb, c, 40, warm); break;
                    case ThingKind.Cache: if (!t.Used) Light(sb, c, 36, G.Hex(0xffa060)); break;
                    case ThingKind.Shade: if (!t.Used) Light(sb, c, 40, G.Hex(0xa080ff)); break;
                    case ThingKind.GreatBell: Light(sb, c + new Vector2(0, -20), 110, warm); break;
                    case ThingKind.Lever: Light(sb, c, 18, warm * 0.5f); break;
                }
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                float fl = 0.9f + 0.1f * MathF.Sin(time * 7f + i * 2.1f);
                Light(sb, _lights[i], 48 * fl, G.Hex(0xffd8a0));
            }
            foreach (var e in _room.Enemies)
            {
                if (!e.Alive) continue;
                if (e.Type == EnemyType.Wisp) Light(sb, e.Center, 40, G.Hex(0x9fe0ff));
                else Light(sb, e.Center, 14, Color.White * 0.35f);
            }
            foreach (var p in _room.Projectiles)
            {
                if (!p.Active) continue;
                var c = new Vector2(p.X, p.Y);
                switch (p.Type)
                {
                    case ProjType.Orb: Light(sb, c, 34, G.Hex(0xffb070)); break;
                    case ProjType.Flame: if (p.Armed) Light(sb, c, 30, G.Hex(0xffb070)); break;
                    case ProjType.Note: Light(sb, c, 22, G.Hex(0xffe8a0)); break;
                    case ProjType.Ring: Light(sb, c, p.R + 30, G.Hex(0xffe8a0) * 0.5f); break;
                    case ProjType.Glob: Light(sb, c, 14, G.Hex(0xc0ff80) * 0.6f); break;
                    case ProjType.Wave: Light(sb, c + new Vector2(0, -4), 18, Color.White * 0.5f); break;
                }
            }
            foreach (var d in _room.Drops) if (d.Active) Light(sb, new Vector2(d.X, d.Y), 14, G.Hex(0xffb070) * 0.7f);
            for (int i = 0; i < _room.Groups.Length; i++)
            {
                var g = _room.Groups[i];
                if (g.Kind == Tile.Resonance && g.Timer > 0) Light(sb, g.Rect.Center.ToVector2(), 40, G.Hex(0xb0a0ff));
                if (g.Kind == Tile.BellGlass && !g.Gone) Light(sb, g.Rect.Center.ToVector2(), 30, G.Hex(0xffd890) * 0.5f);
            }
            var b = _room.Boss;
            if (b != null && b.Active && !b.Finished)
            {
                switch (b)
                {
                    case HollowAbbot a:
                        Light(sb, new Vector2(a.X, a.Y - 20), 60 * a.Alpha, G.Hex(0xc0e8ff));
                        if (a.Summoning) for (int i = 0; i < a.FlameCount; i++) Light(sb, a.Flames[i], 30, G.Hex(0xffb070));
                        break;
                    case Bellkeeper k: Light(sb, new Vector2(k.X, k.Y - 22), k.ShieldUp ? 90 : 60, G.Hex(0xffe0a0)); break;
                    case FrostWyrm w: Light(sb, w.Head, 50, G.Hex(0xc0e8ff)); break;
                    case Thornback t: Light(sb, new Vector2(t.X, t.Y - 14), 50, G.Hex(0xffc0c0) * 0.7f); break;
                }
            }
        }

        public static void Light(SpriteBatch sb, Vector2 c, float radius, Color col) => G.SoftAt(sb, c, radius, col);
    }
}
