using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;
using HolyPeak.World;

namespace HolyPeak.Gfx
{
    /// <summary>Draws a room's static art and all of its interactive objects.</summary>
    public sealed class RoomRenderer : IDisposable
    {
        public static readonly Color PulseA = G.Hex(0xffc94a);
        public static readonly Color PulseB = G.Hex(0xb07cff);

        private readonly Room _room;
        private readonly Style _st;
        private readonly Texture2D _front, _back, _crumble;
        public bool EmberGhost;        // already collected on an earlier visit
        public Vector2 EmberPos;       // where the ember is drawn (follows the player when carried)
        public float BellSwing;        // radians amplitude
        public float BellTime;
        public float ShrineGlow = 1f;

        public RoomRenderer(Room room, Style st)
        {
            _room = room;
            _st = st;
            TileArt.Build(room, st, out _front, out _back);
            _crumble = MakeCrumble(st);
            if (room.HasEmber) EmberPos = new Vector2(room.Ember.X, room.Ember.Y);
        }

        public void Dispose()
        {
            _front.Dispose();
            _back.Dispose();
            _crumble.Dispose();
        }

        private static Texture2D MakeCrumble(Style st)
        {
            // Two 8x8 variants side by side: cracked, weathered stone.
            string[] art =
            {
                "oooooooooooooooo",
                "ollllllooLlllllo",
                "obbbbdbooLbbbbdo",
                "obbbdbbooLbbbdbo",
                "obbdbbbooLbbdbbo",
                "odbbbbbooLbdbbbo",
                "obbbbbdooddbbbbo",
                "oooooooooooooooo",
            };
            // Crumbling stone always uses the terrain palette so it never reads as a golden pulse block.
            bool gold = st.Id == 3;
            Color alt = gold ? st.Base : st.Alt, altLight = gold ? st.Light : st.AltLight, altDark = gold ? st.Dark : st.AltDark;
            Color baseC = G.Mix(alt, altLight, 0.35f);
            var d = new Color[16 * 8];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    char ch = art[y][x];
                    Color c = ch switch
                    {
                        'o' => st.Outline,
                        'l' => altLight,
                        'L' => G.Mix(altLight, Color.White, 0.3f),
                        'd' => altDark,
                        _ => baseC,
                    };
                    if (ch == 'b' && Calc.Hash(x, y, 91) > 0.85f) c = G.Mix(baseC, altDark, 0.4f);
                    d[y * 16 + x] = c;
                }
            // rework the second variant's seam into a proper crack
            for (int y = 1; y < 7; y++) d[y * 16 + 8] = st.Outline;
            d[2 * 16 + 9] = altDark; d[4 * 16 + 10] = altDark; d[5 * 16 + 11] = altDark;
            return G.Make(16, 8, d);
        }

        public void DrawBack(SpriteBatch sb) => sb.Draw(_back, Vector2.Zero, Color.White);
        public void DrawTerrain(SpriteBatch sb) => sb.Draw(_front, Vector2.Zero, Color.White);

        /// <summary>Crumbling blocks, pulse blocks, springs, shrine and bell frame.</summary>
        public void DrawObjects(SpriteBatch sb, float time, Random shakeRng)
        {
            foreach (var c in _room.Crumbles)
            {
                if (c.State == 2)
                {
                    if (c.Timer < 0.4f)
                    {
                        float a = 1 - c.Timer / 0.4f;
                        for (int x = 0; x < c.W; x += 8) G.RectOutline(sb, c.X + x, c.Y, 8, 8, _st.AltLight * (0.4f * a));
                    }
                    continue;
                }
                int ox = 0, oy = 0;
                if (c.State == 1) { ox = shakeRng.Next(-1, 2); oy = shakeRng.Next(0, 2); }
                for (int x = 0; x < c.W; x += 8)
                {
                    int variant = ((c.X + x) / 8 + c.Y / 8) & 1;
                    sb.Draw(_crumble, new Vector2(c.X + x + ox, c.Y + oy), new Rectangle(variant * 8, 0, 8, 8),
                            c.State == 1 ? Color.Lerp(Color.White, new Color(255, 190, 170), 0.45f) : Color.White);
                }
            }

            float phaseT = _room.Def.PulsePhase > 0 ? (_room.Time % _room.Def.PulsePhase) / _room.Def.PulsePhase : 0;
            foreach (var b in _room.Pulses)
            {
                Color col = b.Group == 1 ? PulseA : PulseB;
                bool soon = !b.Solid && phaseT > 0.75f && b.Group != _room.PulseActive;
                if (b.Solid)
                {
                    G.Rect(sb, b.X, b.Y, b.W, b.H, G.Mul(col, 0.55f));
                    G.Rect(sb, b.X + 1, b.Y + 1, b.W - 2, b.H - 2, col);
                    G.Rect(sb, b.X + 1, b.Y + 1, b.W - 2, 1, G.Mix(col, Color.White, 0.6f));
                    G.Rect(sb, b.X + 1, b.Y + b.H - 2, b.W - 2, 1, G.Mul(col, 0.75f));
                    for (int x = b.X + 3; x < b.X + b.W - 2; x += 8)
                    {
                        G.Rect(sb, x, b.Y + 3, 2, 2, G.Mix(col, Color.White, 0.75f));
                    }
                    G.RectOutline(sb, b.X, b.Y, b.W, b.H, G.Mul(col, 0.4f));
                }
                else
                {
                    float a = soon ? (0.5f + 0.5f * MathF.Sin(time * 40f)) : 0.35f;
                    for (int x = b.X; x < b.X + b.W; x += 2)
                    {
                        G.Rect(sb, x, b.Y, 1, 1, col * a);
                        G.Rect(sb, x + 1, b.Y + b.H - 1, 1, 1, col * a);
                    }
                    for (int y = b.Y; y < b.Y + b.H; y += 2)
                    {
                        G.Rect(sb, b.X, y + 1, 1, 1, col * a);
                        G.Rect(sb, b.X + b.W - 1, y, 1, 1, col * a);
                    }
                }
            }

            foreach (var s in _room.Springs) DrawSpring(sb, s);

            if (_room.HasShrine)
            {
                var r = _room.ShrineRect;
                var f = Sprites.Frames["shrine"];
                var pos = new Vector2(r.Center.X - f.Width / 2f, r.Bottom - f.Height);
                sb.Draw(Sprites.Sheet, pos, f, Color.White);
                if (!_room.ShrineUsed)
                {
                    // the floating feather of wind
                    var c = new Vector2(r.Center.X, r.Top - 4 + MathF.Sin(time * 2.2f) * 2);
                    DrawFeather(sb, c, time, ShrineGlow);
                }
            }

            if (_room.HasBell) DrawBellTower(sb, time);
        }

        private void DrawFeather(SpriteBatch sb, Vector2 c, float time, float a)
        {
            Color white = Color.White * a, tint = PlayerVisual.ScarfReady * a;
            float tilt = MathF.Sin(time * 1.3f) * 0.3f;
            for (int i = -4; i <= 4; i++)
            {
                var p = c + new Vector2(MathF.Sin(tilt) * i, i);
                G.Rect(sb, MathF.Round(p.X), MathF.Round(p.Y), 1, 1, white);
                int w = 2 - Math.Abs(i) / 2;
                if (i > -4 && i < 4) G.Rect(sb, MathF.Round(p.X) - w, MathF.Round(p.Y), w, 1, tint);
                if (i > -4 && i < 4) G.Rect(sb, MathF.Round(p.X) + 1, MathF.Round(p.Y), w, 1, G.Mul(tint, 0.8f));
            }
        }

        private void DrawSpring(SpriteBatch sb, in SpringEnt s)
        {
            float comp = s.Anim > 0.5f ? (1 - s.Anim) * 2 : s.Anim * 2; // squash then overshoot
            float ext = s.Anim > 0 ? 1 + (s.Anim > 0.75f ? -0.6f : 0.6f * MathF.Sin(s.Anim * MathF.PI * 3)) : 0;
            _ = comp;
            Color plate = G.Hex(0xd8d8e0), dark = G.Hex(0x4a4a5c), coil = G.Hex(0xe2333f), coilD = G.Hex(0x8a1c28);
            int h = (int)MathF.Round(4 + ext * 2);
            h = Math.Clamp(h, 2, 7);
            if (s.Dir == 0)
            {
                int bx = s.X, by = s.Y + 8;
                G.Rect(sb, bx + 1, by - 2, 6, 2, dark);
                for (int i = 0; i < h - 2; i++)
                    G.Rect(sb, bx + ((i & 1) == 0 ? 2 : 3), by - 3 - i, 3, 1, (i & 1) == 0 ? coil : coilD);
                G.Rect(sb, bx, by - h - 1, 8, 2, plate);
                G.Rect(sb, bx, by - h, 8, 1, G.Mul(plate, 0.7f));
            }
            else
            {
                int dir = s.Dir;
                int wx = dir > 0 ? s.X : s.X + 8, by = s.Y;
                G.Rect(sb, dir > 0 ? wx : wx - 2, by + 1, 2, 6, dark);
                for (int i = 0; i < h - 2; i++)
                    G.Rect(sb, dir > 0 ? wx + 2 + i : wx - 3 - i, by + ((i & 1) == 0 ? 2 : 3), 1, 3, (i & 1) == 0 ? coil : coilD);
                int px = dir > 0 ? wx + h - 1 : wx - h - 1;
                G.Rect(sb, px, by, 2, 8, plate);
            }
        }

        private static readonly Color[] FlagColors =
        {
            G.Hex(0x4a7bd1), G.Hex(0xf2f2f2), G.Hex(0xd8443c), G.Hex(0x4fae5a), G.Hex(0xf0c04c),
        };

        /// <summary>A string of prayer flags sagging between two points, fluttering in the wind.</summary>
        private static void DrawFlags(SpriteBatch sb, Vector2 a, Vector2 b, float sag, float time, int seed)
        {
            int n = (int)(Vector2.Distance(a, b) / 6);
            Vector2 prev = a;
            for (int i = 1; i <= n * 3; i++)
            {
                float t = i / (float)(n * 3);
                var p = Vector2.Lerp(a, b, t) + new Vector2(0, MathF.Sin(t * MathF.PI) * sag);
                G.Line(sb, prev, p, G.Hex(0x3a2a20) * 0.9f);
                prev = p;
            }
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                var p = Vector2.Lerp(a, b, t) + new Vector2(0, MathF.Sin(t * MathF.PI) * sag);
                Color c = FlagColors[(i + seed) % FlagColors.Length];
                float flutter = MathF.Sin(time * 5f + i * 1.3f) * 1.2f;
                for (int y = 0; y < 5; y++)
                {
                    int w = 4 - y / 2;
                    G.Rect(sb, MathF.Round(p.X - 2 + flutter * y / 4f), MathF.Round(p.Y + 1 + y), w, 1, y == 0 ? G.Mix(c, Color.White, 0.25f) : c);
                }
            }
        }

        private void DrawBellTower(SpriteBatch sb, float time)
        {
            var r = _room.BellRect;
            float cx = r.Center.X, baseY = r.Bottom;
            DrawFlags(sb, new Vector2(cx - 18, baseY - 38), new Vector2(cx - 110, baseY + 6), 14, time, 0);
            DrawFlags(sb, new Vector2(cx + 18, baseY - 38), new Vector2(_room.Width - 16, baseY - 30), 8, time, 2);
            DrawFlags(sb, new Vector2(cx - 112, baseY + 8), new Vector2(cx - 220, baseY + 40), 12, time, 3);
            Color wood = G.Hex(0x6b4426), woodD = G.Hex(0x3e2614), woodL = G.Hex(0x9c6b3f);
            // posts and roof
            G.Rect(sb, cx - 15, baseY - 34, 3, 34, wood); G.Rect(sb, cx - 15, baseY - 34, 1, 34, woodL);
            G.Rect(sb, cx + 12, baseY - 34, 3, 34, wood); G.Rect(sb, cx + 12, baseY - 34, 1, 34, woodL);
            G.Rect(sb, cx - 19, baseY - 37, 38, 3, woodD); G.Rect(sb, cx - 19, baseY - 37, 38, 1, woodL);
            for (int i = 0; i < 6; i++) G.Rect(sb, cx - 19 + i * 2, baseY - 40 - i, 38 - i * 4, 1, i == 5 ? woodL : wood);
            G.Rect(sb, cx - 1, baseY - 34, 2, 3, woodD);
            // bell (swinging)
            var f = Sprites.Frames["bell"];
            float ang = MathF.Sin(BellTime * 3.2f) * BellSwing;
            sb.Draw(Sprites.Sheet, new Vector2(cx, baseY - 32), f, Color.White, ang, new Vector2(f.Width / 2f, 0), 1f, SpriteEffects.None, 0f);
        }

        /// <summary>Crystals and the ember (drawn in front of the player).</summary>
        public void DrawPickups(SpriteBatch sb, float time)
        {
            for (int i = 0; i < _room.Crystals.Length; i++)
            {
                var c = _room.Crystals[i];
                float bob = MathF.Sin(time * 2.5f + i) * 1.5f;
                var f = Sprites.Frames[c.Active ? "crystal" : "crystalOff"];
                var pos = new Vector2(MathF.Round(c.X - f.Width / 2f), MathF.Round(c.Y - f.Height / 2f + bob));
                float a = c.Active ? 1f : 0.3f + 0.2f * MathF.Sin(time * 4f);
                sb.Draw(Sprites.Sheet, pos, f, Color.White * a);
            }
            if (_room.HasEmber && (_room.Ember.State == 0 || _room.Ember.State == 1))
            {
                int frame = (int)(time * 10) % 3;
                var f = Sprites.Frames["ember" + frame];
                float bob = _room.Ember.State == 0 ? MathF.Sin(time * 3f) * 1.5f : 0;
                var pos = new Vector2(MathF.Round(EmberPos.X - f.Width / 2f), MathF.Round(EmberPos.Y - f.Height / 2f + bob));
                Color tint = EmberGhost ? new Color(150, 170, 255) * 0.6f : Color.White;
                sb.Draw(Sprites.Sheet, pos, f, tint);
            }
        }

        /// <summary>Additive glows for anything that emits light.</summary>
        public void DrawGlows(SpriteBatch sb, float time)
        {
            foreach (var c in _room.Crystals)
                if (c.Active) G.GlowAt(sb, new Vector2(c.X, c.Y), 14 + MathF.Sin(time * 3) * 2, G.Hex(0x4fd8ff) * 0.45f);
            if (_room.HasEmber && _room.Ember.State <= 1)
            {
                float fl = 0.8f + 0.2f * MathF.Sin(time * 13f) * MathF.Sin(time * 7f);
                G.GlowAt(sb, EmberPos, 18 * fl, (EmberGhost ? G.Hex(0x7d8cff) : G.Hex(0xff8a3a)) * 0.55f);
            }
            if (_room.HasShrine && !_room.ShrineUsed)
            {
                var r = _room.ShrineRect;
                G.GlowAt(sb, new Vector2(r.Center.X, r.Top - 4), 22 + MathF.Sin(time * 2f) * 3, G.Hex(0xfff0c0) * (0.4f * ShrineGlow));
            }
            if (_room.HasBell)
            {
                var r = _room.BellRect;
                G.GlowAt(sb, new Vector2(r.Center.X, r.Bottom - 22), 34 + MathF.Sin(time * 1.5f) * 4, G.Hex(0xffd88a) * 0.3f);
            }
            foreach (var b in _room.Pulses)
                if (b.Solid) G.GlowAt(sb, new Vector2(b.X + b.W / 2f, b.Y + b.H / 2f), b.W * 0.9f + 6, (b.Group == 1 ? PulseA : PulseB) * 0.22f);
        }

        public void DrawHints(SpriteBatch sb, Vector2 playerCenter, float time)
        {
            if (_room.HasShrine && !_room.ShrineUsed) return;
            DrawHint(sb, _room.Def.Hint, _room.HintPos, playerCenter, time);
            DrawHint(sb, _room.Def.Hint2, _room.Hint2Pos, playerCenter, time);
        }

        private void DrawHint(SpriteBatch sb, string text, Point? at, Vector2 player, float time)
        {
            if (string.IsNullOrEmpty(text) || !at.HasValue) return;
            var font = G.Font;
            string s = Input.Format(text);
            var lines = font.Wrap(s, 150);
            float dist = Vector2.Distance(player, at.Value.ToVector2());
            float a = Calc.Clamp01(1.25f - dist / 160f) * 0.85f + 0.15f;
            float y = at.Value.Y - lines.Count * PixelFont.LineHeight / 2f + MathF.Sin(time * 1.5f) * 0.6f;
            float widest = 0;
            foreach (var line in lines) widest = MathF.Max(widest, font.Measure(line));
            float cx = Math.Clamp(at.Value.X, widest / 2f + 12, _room.Width - widest / 2f - 12);
            foreach (var line in lines)
            {
                font.DrawCentered(sb, line, new Vector2(cx, MathF.Round(y)), _st.Light2 * a, Color.Black * (a * 0.8f));
                y += PixelFont.LineHeight;
            }
        }
    }
}
