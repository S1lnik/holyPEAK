using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.World;

namespace HolyPeak.Gfx
{
    /// <summary>
    /// Everything about how the pilgrim looks: animation, squash &amp; stretch, the scarf (whose colour
    /// shows whether the dash is ready), dash afterimages and footstep dust.
    /// </summary>
    public sealed class PlayerVisual
    {
        public static readonly Color ScarfNoDash = G.Hex(0xe9dcc0);
        public static readonly Color ScarfReady = G.Hex(0xe2333f);
        public static readonly Color ScarfGold = G.Hex(0xffc94a);
        /// <summary>Set once every ember has been carried to the top: the scarf glows gold.</summary>
        public bool Golden;
        private Color Ready => Golden ? ScarfGold : ScarfReady;
        public static readonly Color ScarfSpent = G.Hex(0x5cb8ff);

        private const int ScarfLen = 6;
        private readonly Vector2[] _scarf = new Vector2[ScarfLen];
        public Vector2 Scale = Vector2.One;
        private string _frame = "idle0";
        private float _anim, _idle, _flash, _ghostTimer, _dustTimer, _slideTimer, _time;
        private int _runFrame, _lastRunFrame;
        private Color _scarfColor = ScarfReady;
        public float GiftBlend = 1f;   // 0 = cream scarf, 1 = full colour (used by the shrine cutscene)

        private struct Ghost
        {
            public Vector2 Pos, Scale;
            public string Frame;
            public bool Flip;
            public float Life;
            public Color Color;
        }

        private readonly List<Ghost> _ghosts = new List<Ghost>();

        public Color ScarfColor => _scarfColor;

        public void Reset(in PlayerSim p)
        {
            var anchor = Anchor(p);
            for (int i = 0; i < ScarfLen; i++) _scarf[i] = anchor + new Vector2(-p.Facing * i * 1.5f, i * 0.5f);
            Scale = Vector2.One;
            _ghosts.Clear();
            _flash = 0;
        }

        private static Vector2 Anchor(in PlayerSim p) => new Vector2(p.X + PlayerSim.W / 2f - p.Facing * 1f, p.Y + 3.5f);

        public void OnEvent(in GameEvent e, in PlayerSim p, Particles parts, Style st, bool shakeOn, Action<float, float> shake)
        {
            var rng = parts.Rng;
            Color dust = G.Mix(st.Light2, st.Base, 0.4f) * 0.9f;
            switch (e.Type)
            {
                case Ev.Jump:
                    Scale = new Vector2(0.72f, 1.32f);
                    for (int i = 0; i < 5; i++)
                        parts.Emit(e.Pos + new Vector2(rng.Range(-3, 3), 0), new Vector2(rng.Range(-25, 25), rng.Range(-14, -2)), dust, 0.35f, 1, drag: 4);
                    AudioManager.Play("jump", 0.55f, pitchVariance: 0.06f);
                    break;
                case Ev.WallJump:
                    Scale = new Vector2(0.75f, 1.3f);
                    for (int i = 0; i < 6; i++)
                        parts.Emit(e.Pos + new Vector2(0, rng.Range(-4, 2)), new Vector2(e.Dir.X * rng.Range(10, 40), rng.Range(-20, 10)), dust, 0.35f, 1, drag: 4);
                    AudioManager.Play("walljump", 0.55f, pitchVariance: 0.05f);
                    break;
                case Ev.SuperJump:
                    Scale = new Vector2(1.35f, 0.75f);
                    for (int i = 0; i < 10; i++)
                        parts.Emit(e.Pos, new Vector2(-e.Dir.X * rng.Range(20, 70), rng.Range(-20, 0)), dust, 0.4f, 1, drag: 4);
                    AudioManager.Play("superjump", 0.65f);
                    break;
                case Ev.Land:
                {
                    float impact = Calc.Clamp01((e.Dir.Y - 40) / 200f);
                    Scale = new Vector2(1 + 0.4f * impact, 1 - 0.32f * impact);
                    int n = (int)(2 + impact * 8);
                    for (int i = 0; i < n; i++)
                    {
                        float side = rng.Sign();
                        parts.Emit(e.Pos + new Vector2(side * rng.Range(0, 3), -1), new Vector2(side * rng.Range(10, 50) * (0.5f + impact), rng.Range(-12, -2)), dust, 0.4f, 1, drag: 5);
                    }
                    if (impact > 0.05f) AudioManager.Play("land", 0.25f + impact * 0.45f, pitchVariance: 0.08f);
                    break;
                }
                case Ev.Dash:
                {
                    Scale = MathF.Abs(e.Dir.X) > MathF.Abs(e.Dir.Y) ? new Vector2(1.4f, 0.65f) : new Vector2(0.65f, 1.4f);
                    _ghostTimer = 0;
                    for (int i = 0; i < 10; i++)
                        parts.Emit(e.Pos, -e.Dir * rng.Range(30, 90) + rng.InCircle(20), _scarfColor * 0.9f, 0.35f, 1, drag: 3);
                    AudioManager.Play("dash", 0.7f, pitchVariance: 0.04f);
                    if (shakeOn) shake(0.12f, 1.5f);
                    break;
                }
                case Ev.DashRefill:
                    _flash = 0.12f;
                    AudioManager.Play("refill", 0.3f);
                    break;
                case Ev.WallSlideStart:
                    AudioManager.Play("slide", 0.25f, pitchVariance: 0.1f);
                    break;
            }
        }

        public void Update(in PlayerSim p, float dt, float wind, Particles parts, Style st)
        {
            _time += dt;
            Scale = Calc.Approach(Scale, Vector2.One, dt * 3.2f);
            if (_flash > 0) _flash -= dt;

            // Scarf colour follows dash state.
            Color target = !p.DashAbility ? ScarfNoDash : (p.Dashes > 0 ? Ready : ScarfSpent);
            target = Color.Lerp(ScarfNoDash, target, GiftBlend);
            _scarfColor = _flash > 0 ? Color.White : Color.Lerp(_scarfColor, target, Calc.Damp(30f, dt));

            // Animation
            if (p.State == PState.Dash) _frame = "dash";
            else if (p.WallSliding) _frame = "slide";
            else if (!p.OnGround) _frame = p.Speed.Y < 10 ? "jump" : "fall";
            else if (MathF.Abs(p.Speed.X) > 8)
            {
                _anim += dt * (4f + MathF.Abs(p.Speed.X) * 0.1f);
                _runFrame = (int)_anim % 4;
                _frame = "run" + _runFrame;
                if (_runFrame != _lastRunFrame && (_runFrame == 0 || _runFrame == 2))
                    AudioManager.Play("step", 0.18f, -0.2f, pitchVariance: 0.15f);
                _lastRunFrame = _runFrame;
            }
            else
            {
                _idle += dt;
                _frame = ((int)(_idle / 0.55f) & 1) == 0 ? "idle0" : "idle1";
            }

            // Dash afterimages
            if (p.State == PState.Dash)
            {
                _ghostTimer -= dt;
                if (_ghostTimer <= 0)
                {
                    _ghostTimer = 0.035f;
                    _ghosts.Add(new Ghost { Pos = Feet(p), Frame = _frame, Flip = p.Facing < 0, Scale = Scale, Life = 0.28f, Color = Ready });
                }
            }
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var g = _ghosts[i];
                g.Life -= dt;
                if (g.Life <= 0) _ghosts.RemoveAt(i); else _ghosts[i] = g;
            }

            // Dust while running / sliding
            Color dust = G.Mix(st.Light2, st.Base, 0.4f) * 0.8f;
            _dustTimer -= dt;
            if (p.OnGround && MathF.Abs(p.Speed.X) > 70 && _dustTimer <= 0)
            {
                _dustTimer = 0.09f;
                parts.Emit(Feet(p) + new Vector2(-p.Facing * 2, -1), new Vector2(-p.Facing * parts.Rng.Range(5, 20), parts.Rng.Range(-10, -2)), dust, 0.3f, 1, drag: 4);
            }
            if (p.WallSliding)
            {
                _slideTimer -= dt;
                if (_slideTimer <= 0)
                {
                    _slideTimer = 0.06f;
                    parts.Emit(new Vector2(p.WallDir > 0 ? p.X + PlayerSim.W : p.X, p.Y + parts.Rng.Range(2, 8)),
                               new Vector2(-p.WallDir * parts.Rng.Range(4, 14), parts.Rng.Range(-6, 4)), dust, 0.3f, 1, drag: 4);
                }
            }

            // Scarf physics: each knot trails the previous one.
            _scarf[0] = Anchor(p) + new Vector2(0, _frame == "idle1" || _frame == "run1" || _frame == "run3" ? 1 : 0);
            float seg = 1.8f;
            for (int i = 1; i < ScarfLen; i++)
            {
                var force = new Vector2(-p.Facing * 7f + wind * 0.45f, 34f) + new Vector2(0, MathF.Sin(_time * 9f + i * 0.9f) * 7f);
                if (p.State == PState.Dash) force += -p.DashDir * 40f;
                _scarf[i] += force * dt;
                var d = _scarf[i] - _scarf[i - 1];
                float len = d.Length();
                if (len > seg) _scarf[i] = _scarf[i - 1] + d / len * seg;
            }
        }

        public static Vector2 Feet(in PlayerSim p) => new Vector2(p.X + PlayerSim.W / 2f, p.Y + PlayerSim.H);

        public void Draw(SpriteBatch sb, in PlayerSim p, float alpha = 1f)
        {
            var origin = new Vector2(5, 13);
            foreach (var g in _ghosts)
            {
                float a = g.Life / 0.28f;
                Sprites.DrawSilhouette(sb, g.Frame, Round(g.Pos), g.Color * (0.6f * a * alpha), origin, g.Scale, g.Flip);
            }

            // scarf: stamp small squares along each segment; outline pass first, then colour
            Color outline = G.Hex(0x1b1630) * alpha;
            for (int pass = 0; pass < 2; pass++)
                for (int i = ScarfLen - 1; i >= 1; i--)
                {
                    float size = i < 3 ? 2 : 1;
                    Color c = pass == 0 ? outline : (i >= ScarfLen - 2 ? G.Mul(_scarfColor, 0.78f) : _scarfColor) * alpha;
                    float s = size + (pass == 0 ? 2 : 0);
                    for (int st = 0; st < 3; st++)
                    {
                        var pos = Round(Vector2.Lerp(_scarf[i - 1], _scarf[i], st / 3f));
                        G.Rect(sb, pos.X - MathF.Floor(s / 2f), pos.Y - MathF.Floor(s / 2f), s, s, c);
                    }
                }

            Sprites.Draw(sb, _frame, Round(Feet(p)), Color.White * alpha, origin, Scale, p.Facing < 0);

            // the knot of the scarf sits over the neck
            var k = Round(_scarf[0]);
            G.Rect(sb, k.X - 1, k.Y - 1, 3, 2, _scarfColor * alpha);
        }

        private static Vector2 Round(Vector2 v) => new Vector2(MathF.Round(v.X), MathF.Round(v.Y));
    }
}
