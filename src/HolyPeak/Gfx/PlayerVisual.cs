using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.World;

namespace HolyPeak.Gfx
{
    /// <summary>
    /// How the pilgrim looks: animation, squash and stretch, the flowing cloak, the staff (and the slash
    /// arcs it leaves), the kite, the lantern at the staff's head, dust and footstep sounds.
    /// </summary>
    public sealed class PlayerVisual
    {
        public static readonly Color Outline = G.Hex(0x1b1630);
        public static readonly Color Wood = G.Hex(0x8a5a32), WoodLight = G.Hex(0xc08a50);
        public static readonly Color Brass = G.Hex(0xf0c04c), BrassDark = G.Hex(0xa8782a);
        public static readonly Color Lantern = G.Hex(0xffd88a);
        public static readonly Color CloakColor = G.Hex(0x29355e), CloakLight = G.Hex(0x3c4d7e);
        public static readonly Color SlashColor = G.Hex(0xfff2d0);

        private const int CloakLen = 5;
        private readonly Vector2[] _cloak = new Vector2[CloakLen];
        public Vector2 Scale = Vector2.One;
        private string _frame = "idle0";
        private float _anim, _idle, _dustTimer, _slideTimer, _time, _blinkT = 3f, _healGlow;
        private int _runFrame, _lastRunFrame;
        private float _staffAngle = -MathF.PI / 2;
        private Strike _lastStrike;
        private float _strikeT;
        private int _strikeFacing = 1;
        private float _kiteOpen;
        private float _castT = 9;
        private Spell _castSpell;
        private int _castFacing = 1;
        public bool HasBell;
        public Vector2 LanternPos { get; private set; }
        public float LanternGlow { get; private set; } = 1f;

        public void Reset(in PlayerSim p)
        {
            var a = CloakAnchor(p);
            for (int i = 0; i < CloakLen; i++) _cloak[i] = a + new Vector2(-p.Facing * i * 2f, i * 1.5f);
            Scale = Vector2.One;
            _kiteOpen = 0;
            _strikeT = 9;
        }

        private static Vector2 Feet(in PlayerSim p) => new Vector2(p.X + PlayerSim.W / 2f, p.Y + PlayerSim.H);
        private Vector2 CloakAnchor(in PlayerSim p) => Feet(p) + new Vector2(-p.Facing * 2.5f, -10f + (Scale.Y < 1 ? 2 : 0));

        public void OnEvent(in GameEvent e, in PlayerSim p, Particles parts, Style st)
        {
            var rng = parts.Rng;
            Color dust = G.Mix(st.Light2, st.Base, 0.4f) * 0.9f;
            switch (e.Type)
            {
                case Ev.Jump:
                    Scale = new Vector2(0.75f, 1.28f);
                    for (int i = 0; i < 5; i++)
                        parts.Emit(e.Pos + new Vector2(rng.Range(-3, 3), 0), new Vector2(rng.Range(-25, 25), rng.Range(-14, -2)), dust, 0.35f, 1, drag: 4);
                    AudioManager.Play("jump", 0.5f, pitchVariance: 0.06f);
                    break;
                case Ev.Leap:
                    Scale = new Vector2(0.78f, 1.25f);
                    for (int i = 0; i < 7; i++)
                        parts.Emit(e.Pos + new Vector2(0, rng.Range(-6, 2)), new Vector2(e.Dir.X * rng.Range(10, 50), rng.Range(-25, 10)), dust, 0.35f, 1, drag: 4);
                    AudioManager.Play("leap", 0.6f, pitchVariance: 0.05f);
                    break;
                case Ev.Cling:
                    Scale = new Vector2(1.15f, 0.9f);
                    for (int i = 0; i < 4; i++)
                        parts.Emit(e.Pos + new Vector2(0, rng.Range(-3, 5)), new Vector2(-e.Dir.X * rng.Range(5, 20), rng.Range(-10, 10)), dust, 0.3f, 1, drag: 4);
                    AudioManager.Play("cling", 0.5f, pitchVariance: 0.08f);
                    break;
                case Ev.Land:
                {
                    float impact = Calc.Clamp01((e.Dir.Y - 60) / 180f);
                    Scale = new Vector2(1 + 0.35f * impact, 1 - 0.28f * impact);
                    int n = (int)(2 + impact * 8);
                    for (int i = 0; i < n; i++)
                    {
                        float side = rng.Sign();
                        parts.Emit(e.Pos + new Vector2(side * rng.Range(0, 3), -1), new Vector2(side * rng.Range(10, 50) * (0.5f + impact), rng.Range(-12, -2)), dust, 0.4f, 1, drag: 5);
                    }
                    if (impact > 0.05f) AudioManager.Play("land", 0.25f + impact * 0.45f, pitchVariance: 0.08f);
                    break;
                }
                case Ev.Swing:
                    _lastStrike = p.Attack;
                    _strikeT = 0;
                    _strikeFacing = p.Facing;
                    AudioManager.Play("swing", 0.55f, pitchVariance: 0.1f);
                    break;
                case Ev.Pogo:
                    Scale = new Vector2(0.8f, 1.25f);
                    AudioManager.Play("pogo", 0.6f, pitchVariance: 0.05f);
                    break;
                case Ev.GlideOpen:
                    AudioManager.Play("glide", 0.55f, pitchVariance: 0.05f);
                    break;
                case Ev.HealStart:
                    AudioManager.Play("healstart", 0.5f);
                    break;
                case Ev.Heal:
                    _healGlow = 1f;
                    parts.Burst(e.Pos, 14, Lantern, 20, 60, 0.6f, 1, -30, 3, additive: true);
                    parts.Emit(e.Pos, Vector2.Zero, Lantern * 0.6f, 0.5f, 6, PShape.Glow, sizeEnd: 40, additive: true);
                    AudioManager.Play("heal", 0.7f);
                    break;
                case Ev.Mantle:
                    Scale = new Vector2(1.2f, 0.82f);
                    for (int i = 0; i < 4; i++)
                        parts.Emit(e.Pos + new Vector2(0, rng.Range(-2, 1)), new Vector2(rng.Range(-15, 15), rng.Range(-20, -5)), dust, 0.3f, 1, drag: 4);
                    AudioManager.Play("mantle", 0.45f, pitchVariance: 0.08f);
                    break;
                case Ev.Vault:
                    Scale = new Vector2(0.7f, 1.32f);
                    for (int i = 0; i < 9; i++)
                        parts.Emit(e.Pos + new Vector2(rng.Range(-3, 3), -1), new Vector2(-e.Dir.X * rng.Range(20, 70), rng.Range(-30, -4)), dust, 0.4f, 1, drag: 4);
                    AudioManager.Play("vault", 0.65f, pitchVariance: 0.05f);
                    break;
                case Ev.RopeGrab:
                    Scale = new Vector2(0.9f, 1.12f);
                    AudioManager.Play("ropegrab", 0.5f, pitchVariance: 0.08f);
                    break;
                case Ev.RopeJump:
                    Scale = new Vector2(0.75f, 1.28f);
                    AudioManager.Play("ropejump", 0.55f, pitchVariance: 0.06f);
                    break;
                case Ev.Lantern:
                    Scale = e.Dir.Y < 0 ? new Vector2(0.75f, 1.3f) : new Vector2(1.25f, 0.85f);
                    break;
                case Ev.Cast:
                    _castT = 0;
                    _castSpell = (Spell)e.Index;
                    _castFacing = p.Facing;
                    Scale = new Vector2(1.15f, 0.9f);
                    AudioManager.Play("cast", 0.6f, pitchVariance: 0.05f);
                    break;
            }
        }

        public void Update(in PlayerSim p, float dt, float wind, Particles parts, Style st)
        {
            _time += dt;
            Scale = Calc.Approach(Scale, Vector2.One, dt * 3.0f);
            _strikeT += dt;
            _castT += dt;
            _healGlow = Math.Max(0, _healGlow - dt * 1.5f);
            _kiteOpen = Calc.Approach(_kiteOpen, p.Gliding ? 1 : 0, dt * (p.Gliding ? 9 : 6));
            _blinkT -= dt;
            if (_blinkT < -0.12f) _blinkT = 2.5f + parts.Rng.Range(0, 3f);

            // Animation
            if (p.State == PState.Dead || p.StunT > 0) _frame = "hurt";
            else if (p.Healing) _frame = "kneel";
            else if (p.OnRope) _frame = "hang";
            else if (p.Diving) _frame = "dive";
            else if (_castT < 0.2f) _frame = "cast";
            else if (p.VaultT > 0.1f) _frame = "vault";
            else if (p.MantleT > 0.12f && !p.OnGround) _frame = "tuck";
            else if (p.Clinging) _frame = "cling";
            else if (p.Gliding) _frame = "glide";
            else if (_strikeT < 0.2f && _lastStrike == Strike.Up) _frame = "lookup";
            else if (!p.OnGround) _frame = p.Speed.Y < 20 ? "jump" : "fall";
            else if (MathF.Abs(p.Speed.X) > 8)
            {
                _anim += dt * (5f + MathF.Abs(p.Speed.X) * 0.09f);
                _runFrame = (int)_anim % 4;
                _frame = "run" + _runFrame;
                if (_runFrame != _lastRunFrame && (_runFrame == 0 || _runFrame == 2))
                    AudioManager.Play("step", 0.16f, -0.2f, pitchVariance: 0.15f);
                _lastRunFrame = _runFrame;
            }
            else
            {
                _idle += dt;
                _frame = _blinkT < 0 ? "blink" : (((int)(_idle / 0.6f) & 1) == 0 ? "idle0" : "idle1");
            }

            // Staff angle
            float target = StaffRest(p);
            float swing = SwingAngle(p, out bool swinging);
            if (swinging) _staffAngle = swing;
            else if (_castT < 0.06f || (p.VaultT > 0.2f)) _staffAngle = target;
            else _staffAngle = LerpAngle(_staffAngle, target, Calc.Damp(16f, dt));

            var hand = Hand(p);
            var dir = new Vector2(MathF.Cos(_staffAngle), MathF.Sin(_staffAngle));
            LanternPos = hand + dir * 11f;
            LanternGlow = 0.9f + 0.1f * MathF.Sin(_time * 7f) * MathF.Sin(_time * 3.1f) + _healGlow * 0.8f;

            // Dust
            Color dust = G.Mix(st.Light2, st.Base, 0.4f) * 0.8f;
            _dustTimer -= dt;
            if (p.OnGround && MathF.Abs(p.Speed.X) > 70 && _dustTimer <= 0)
            {
                _dustTimer = 0.1f;
                parts.Emit(Feet(p) + new Vector2(-p.Facing * 2, -1), new Vector2(-p.Facing * parts.Rng.Range(5, 20), parts.Rng.Range(-10, -2)), dust, 0.3f, 1, drag: 4);
            }
            if (p.Clinging && p.Speed.Y > 10)
            {
                _slideTimer -= dt;
                if (_slideTimer <= 0)
                {
                    _slideTimer = 0.07f;
                    parts.Emit(new Vector2(p.WallDir > 0 ? p.X + PlayerSim.W : p.X, p.Y + parts.Rng.Range(2, 10)),
                               new Vector2(-p.WallDir * parts.Rng.Range(4, 14), parts.Rng.Range(-6, 4)), dust, 0.3f, 1, drag: 4);
                }
            }
            if (p.Diving && parts.Rng.Chance(dt * 90))
                parts.Emit(p.Center + new Vector2(parts.Rng.Range(-3, 3), -4), new Vector2(parts.Rng.Range(-8, 8), -40), WorldRenderer.SpellColor, 0.3f, 1, PShape.Line, drag: 3, additive: true);
            if (p.VaultT > 0 && parts.Rng.Chance(dt * 40))
                parts.Emit(p.Center, new Vector2(-p.Speed.X * 0.2f, 0), dust, 0.25f, 1, drag: 4);
            if (p.Healing && parts.Rng.Chance(dt * 30))
            {
                var c = p.Center + parts.Rng.InCircle(14);
                parts.Emit(c, (p.Center - c) * 2.5f, Lantern, 0.4f, 1, additive: true);
            }

            // Cloak: each knot trails the previous one.
            _cloak[0] = CloakAnchor(p);
            float seg = 2.4f;
            for (int i = 1; i < CloakLen; i++)
            {
                var force = new Vector2(-p.Facing * 10f + wind * 0.5f - p.Speed.X * 0.15f, 30f - p.Speed.Y * 0.12f) + new Vector2(0, MathF.Sin(_time * 7f + i * 0.9f) * 6f);
                if (p.Gliding) force.Y -= 20f;
                if (p.Diving) force.Y -= 90f;
                _cloak[i] += force * dt;
                var d = _cloak[i] - _cloak[i - 1];
                float len = d.Length();
                if (len > seg) _cloak[i] = _cloak[i - 1] + d / len * seg;
            }
        }

        private static Vector2 Hand(in PlayerSim p)
        {
            var f = Feet(p);
            if (p.Healing) return f + new Vector2(p.Facing * 4, -5);
            if (p.OnRope) return f + new Vector2(-p.Facing * 3, -8);
            if (p.Clinging) return f + new Vector2(-p.WallDir * 3, -8);
            return f + new Vector2(p.Facing * 4, -7);
        }

        private float StaffRest(in PlayerSim p)
        {
            int f = p.Clinging ? -p.WallDir : p.Facing;
            float a;
            if (p.Diving) a = MathF.PI / 2 - f * 0.15f;
            else if (_castT < 0.22f) a = _castSpell == Spell.Psalm ? -MathF.PI / 2 + 0.1f : (_castSpell == Spell.Star ? 1.2f : -0.15f);
            else if (p.VaultT > 0.12f) a = 1.95f;
            else if (p.OnRope) a = -2.4f;
            else if (p.Gliding) a = -MathF.PI / 2 + f * 0.12f;
            else if (p.Healing) a = -MathF.PI / 2;
            else if (p.Clinging) a = -1.0f;
            else if (!p.OnGround) a = p.Speed.Y < 0 ? -1.25f : -1.9f;
            else if (MathF.Abs(p.Speed.X) > 8) a = -1.15f + MathF.Sin(_anim * MathF.PI / 2) * 0.08f;
            else a = -MathF.PI / 2 + 0.08f;
            return f > 0 ? a : MathF.PI - a;
        }

        private float SwingAngle(in PlayerSim p, out bool swinging)
        {
            swinging = _strikeT < PlayerSim.StrikeTime;
            if (!swinging) return 0;
            float k = Calc.EaseOutCubic(Math.Min(1, _strikeT / 0.11f));
            float a0, a1;
            switch (_lastStrike)
            {
                case Strike.Up: a0 = 0.2f; a1 = -1.95f; break;
                case Strike.Down: a0 = -1.2f; a1 = 1.65f; break;
                default: a0 = -2.3f; a1 = 0.35f; break;
            }
            float a = Calc.Lerp(a0, a1, k);
            return _strikeFacing > 0 ? a : MathF.PI - a;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = MathF.IEEERemainder(b - a, MathF.PI * 2);
            return a + d * t;
        }

        private static Vector2 Round(Vector2 v) => new Vector2(MathF.Round(v.X), MathF.Round(v.Y));

        /// <summary>Plots a 1-pixel line, which stays crisp where a rotated sprite would smear.</summary>
        public static void PixelLine(SpriteBatch sb, Vector2 a, Vector2 b, Color c, int thickness = 1)
        {
            int x0 = (int)MathF.Round(a.X), y0 = (int)MathF.Round(a.Y), x1 = (int)MathF.Round(b.X), y1 = (int)MathF.Round(b.Y);
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            int o = thickness / 2;
            while (true)
            {
                G.Rect(sb, x0 - o, y0 - o, thickness, thickness, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Draw(SpriteBatch sb, in PlayerSim p, float alpha = 1f)
        {
            // Flicker while invulnerable.
            if (p.Invuln > 0 && p.State == PState.Normal && ((int)(p.Invuln * 20) & 1) == 0) alpha *= 0.35f;
            var feet = Round(Feet(p));
            int f = p.Facing;

            // Kite above the staff
            var hand = Round(Hand(p));
            var dir = new Vector2(MathF.Cos(_staffAngle), MathF.Sin(_staffAngle));
            var tip = hand + dir * 10f;
            var butt = hand - dir * 5f;
            if (_kiteOpen > 0.05f) DrawKite(sb, tip, alpha);

            // Cloak, outline pass then colour
            for (int pass = 0; pass < 2; pass++)
                for (int i = CloakLen - 1; i >= 1; i--)
                {
                    float w = 3.2f - i * 0.45f + (pass == 0 ? 2 : 0);
                    Color c = pass == 0 ? Outline : (i <= 1 ? CloakLight : CloakColor);
                    for (int s = 0; s < 3; s++)
                    {
                        var pos = Round(Vector2.Lerp(_cloak[i - 1], _cloak[i], s / 3f));
                        G.Rect(sb, pos.X - MathF.Floor(w / 2f), pos.Y - MathF.Floor(w / 2f), MathF.Ceiling(w), MathF.Ceiling(w), c * alpha);
                    }
                }

            var r = Sprites.Frames[_frame];
            var fx = f < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (p.OnRope)
            {
                // hang from the hands and lean with the swing
                sb.Draw(Sprites.Sheet, Round(p.Hands), r, Color.White * alpha, -p.RopeAngle * 0.8f, new Vector2(r.Width / 2f, 3), Scale, fx, 0f);
            }
            else
            {
                float tilt = p.VaultT > 0.1f ? f * 0.25f : 0f;
                sb.Draw(Sprites.Sheet, feet, r, Color.White * alpha, tilt, new Vector2(r.Width / 2f, r.Height), Scale, fx, 0f);
            }

            DrawStaff(sb, butt, tip, alpha);
            // hand gripping the staff
            G.Rect(sb, hand.X - 1, hand.Y - 1, 2, 2, Sprites.PilgrimPal['b'] * alpha);
        }

        private void DrawStaff(SpriteBatch sb, Vector2 butt, Vector2 tip, float alpha)
        {
            PixelLine(sb, butt, tip, Outline * alpha, 3);
            PixelLine(sb, butt, tip, Wood * alpha);
            // head: a small brass lantern, or the bell once found
            var t = Round(tip);
            G.Rect(sb, t.X - 2, t.Y - 2, 5, 5, Outline * alpha);
            if (HasBell)
            {
                G.Rect(sb, t.X - 1, t.Y - 2, 3, 3, Brass * alpha);
                G.Rect(sb, t.X - 2, t.Y + 1, 5, 1, BrassDark * alpha);
                G.Rect(sb, t.X, t.Y + 2, 1, 1, Outline * alpha);
            }
            else
            {
                G.Rect(sb, t.X - 1, t.Y - 1, 3, 3, BrassDark * alpha);
                G.Rect(sb, t.X, t.Y, 1, 1, Lantern * alpha);
            }
        }

        private void DrawKite(SpriteBatch sb, Vector2 tip, float alpha)
        {
            var f = Sprites.Frames["kite"];
            float open = Calc.EaseOutBack(_kiteOpen);
            var pos = Round(tip + new Vector2(0, -3 - 3 * open));
            var sway = MathF.Sin(_time * 5f) * 0.12f;
            sb.Draw(Sprites.Sheet, pos, f, Color.White * alpha, sway, new Vector2(f.Width / 2f, f.Height), new Vector2(open, Math.Max(0.3f, open)), SpriteEffects.None, 0f);
            // strings
            G.Rect(sb, pos.X, pos.Y, 1, 3, Outline * (0.6f * alpha));
        }

        /// <summary>Slash arcs and the lantern glow (drawn in the additive pass).</summary>
        public void DrawGlow(SpriteBatch sb, in PlayerSim p)
        {
            if (p.State == PState.Dead) return;
            G.GlowAt(sb, LanternPos, 9 * LanternGlow, Lantern * 0.35f);
            if (_strikeT < 0.16f && _lastStrike != Strike.None)
            {
                var hand = Hand(p);
                float k = Calc.EaseOutCubic(Math.Min(1, _strikeT / 0.11f));
                float fade = 1 - Calc.Clamp01((_strikeT - 0.08f) / 0.08f);
                float a0, a1;
                switch (_lastStrike)
                {
                    case Strike.Up: a0 = 0.2f; a1 = -1.95f; break;
                    case Strike.Down: a0 = -1.2f; a1 = 1.65f; break;
                    default: a0 = -2.3f; a1 = 0.35f; break;
                }
                float aEnd = Calc.Lerp(a0, a1, k);
                float aStart = Calc.Lerp(a0, a1, Math.Max(0, k - 0.8f));
                // a filled crescent: thickest in the middle of the sweep, bright on the outer edge
                int steps = 48;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float a = Calc.Lerp(aStart, aEnd, t);
                    if (_strikeFacing < 0) a = MathF.PI - a;
                    var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    float thick = MathF.Sin(t * MathF.PI) * 6f + 0.5f;
                    float outer = 17f;
                    for (float rr = outer - thick; rr <= outer; rr += 0.5f)
                    {
                        var q = Round(hand + d * rr);
                        float edge = (rr - (outer - thick)) / Math.Max(0.5f, thick);
                        Color c = Color.Lerp(G.Hex(0xffd88a), SlashColor, edge);
                        G.Rect(sb, q.X, q.Y, 1, 1, c * (fade * (0.25f + 0.75f * t) * (0.45f + 0.55f * edge)));
                    }
                }
            }
        }
    }
}
