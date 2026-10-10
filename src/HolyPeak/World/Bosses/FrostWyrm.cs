using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    /// <summary>
    /// The Frost Wyrm, a serpent of old ice that swims through the snow of Frostwind. Bursts out of the
    /// floor in great arcs, rears up to breathe frost, and shakes icicles loose from the cliffs above.
    /// </summary>
    public sealed class FrostWyrm : Boss
    {
        public const int Segments = 13;
        public const float SegLen = 6.5f;
        private enum S { Hidden, Plume, Arc, Rise, Inhale, Breath, Sink, Quake, Rage }

        private readonly float _floor;
        private float _left, _right;
        private int _last = -1, _arcs;
        private float _x0, _x1, _dur, _s, _breathAngle;
        private readonly Vector2[] _trail = new Vector2[240];
        private int _trailHead;
        public Vector2 Head;
        public Vector2 PlumeAt;
        public bool Plume => State == (int)S.Plume;
        public bool Inhaling => State == (int)S.Inhale;
        public bool Breathing => State == (int)S.Breath;
        public bool Quaking => State == (int)S.Quake;
        public float Floor => _floor;

        public FrostWyrm(Point spawn, Room room) : base("wyrm", "The Frost Wyrm", "Serpent of the Old Ice", 124, spawn, 240)
        {
            _floor = spawn.Y;
            Head = new Vector2(spawn.X, _floor + 40);
            for (int i = 0; i < _trail.Length; i++) _trail[i] = Head;
        }

        /// <summary>Body segment i, spaced evenly along the path the head has travelled.</summary>
        public Vector2 Segment(int i)
        {
            float want = (i + 1) * SegLen, walked = 0;
            var prev = _trail[_trailHead];
            for (int k = 1; k < _trail.Length; k++)
            {
                var p = _trail[((_trailHead - k) % _trail.Length + _trail.Length) % _trail.Length];
                float d = Vector2.Distance(prev, p);
                if (walked + d >= want && d > 0) return Vector2.Lerp(prev, p, (want - walked) / d);
                walked += d;
                prev = p;
            }
            return prev;
        }

        private float Tell => Phase == 2 ? 0.8f : 1f;

        protected override void OnWake(Room r)
        {
            (_left, _right) = ArenaSpan(r, X, _floor);
            Facing = r.P.Center.X < X ? -1 : 1;
            PlumeAt = new Vector2(X, _floor);
        }

        protected override void Intro(Room r, float dt)
        {
            // Rears up out of the snow where it slept, roars, and sinks back.
            float k = IntroT / IntroTime;
            float h = k < 0.5f ? Calc.EaseOutCubic(k * 2) : 1 - Calc.EaseInCubic((k - 0.5f) * 2);
            Head = new Vector2(X + MathF.Sin(IntroT * 3) * 4, _floor + 30 - h * 90);
            Push();
            if (IntroT > 0.9f && IntroT - dt <= 0.9f) r.Emit(Ev.BossCue, Head, index: 40);
        }

        private void Go(S s, float t) { State = (int)s; T = t; }

        private void Push()
        {
            _trailHead = (_trailHead + 1) % _trail.Length;
            _trail[_trailHead] = Head;
        }

        protected override void Update(Room r, float dt)
        {
            var p = r.P.Center;
            T -= dt;

            if (Phase == 1 && Hp <= MaxHp / 2 && State == (int)S.Hidden)
            {
                Phase = 2;
                Go(S.Rage, 1.0f);
                r.Emit(Ev.BossCue, new Vector2(X, _floor), index: 41);
            }

            switch ((S)State)
            {
                case S.Hidden:
                    Head = new Vector2(Head.X, _floor + 40);
                    if (T <= 0)
                    {
                        int m = Pick(r, _last, 1.2f, 1f, 0.7f);
                        _last = m;
                        if (m == 0)
                        {
                            _arcs = Phase == 2 ? 2 : 1;
                            StartArc(r, p.X);
                        }
                        else if (m == 1)
                        {
                            bool leftSide = p.X > (_left + _right) / 2;
                            float x = leftSide ? _left + 26 : _right - 26;
                            Facing = leftSide ? 1 : -1;
                            PlumeAt = new Vector2(x, _floor);
                            Head = new Vector2(x, _floor + 30);
                            _next = S.Rise;
                            Go(S.Plume, 0.55f * Tell);
                            r.Emit(Ev.BossCue, PlumeAt, index: 42);
                        }
                        else
                        {
                            Go(S.Quake, 0.6f);
                            r.Emit(Ev.BossCue, new Vector2(X, _floor), index: 41);
                        }
                    }
                    break;

                case S.Plume:
                    if (T <= 0)
                    {
                        if (_next == S.Arc) { _s = 0; Go(S.Arc, 99f); r.Emit(Ev.BossCue, PlumeAt, index: 43); }
                        else { Go(S.Rise, 0.45f); r.Emit(Ev.BossCue, PlumeAt, index: 43); }
                    }
                    break;

                case S.Arc:
                {
                    _s += dt / _dur;
                    float s = Math.Min(_s, 1.35f);
                    float x = Calc.Lerp(_x0, _x1, s);
                    float y = _floor + 26 - 4 * 112f * s * (1 - s);
                    Head = new Vector2(x, y);
                    Facing = _x1 > _x0 ? 1 : -1;
                    if (_s >= 1.35f)
                    {
                        _arcs--;
                        if (_arcs > 0) StartArc(r, p.X);
                        else Go(S.Hidden, (Phase == 2 ? 0.55f : 0.8f));
                    }
                    break;
                }

                case S.Rise:
                    Head = new Vector2(PlumeAt.X, Calc.Lerp(_floor + 30, _floor - 58, Calc.EaseOutCubic(1 - T / 0.45f)));
                    if (T <= 0) { Go(S.Inhale, 0.6f * Tell); r.Emit(Ev.BossCue, Head, index: 44); }
                    break;

                case S.Inhale:
                    Head = new Vector2(PlumeAt.X - Facing * 4 * (1 - T / 0.6f), _floor - 58 - 3 * MathF.Sin(Time * 30));
                    _breathAngle = MathF.Atan2(p.Y - Head.Y, p.X - Head.X);
                    if (T <= 0) { Go(S.Breath, Phase == 2 ? 1.1f : 0.85f); r.Emit(Ev.BossCue, Head, index: 45); _shardT = 0; }
                    break;

                case S.Breath:
                {
                    Head = new Vector2(PlumeAt.X + Facing * 3, _floor - 58);
                    float want = MathF.Atan2(p.Y - Head.Y, p.X - Head.X);
                    _breathAngle = ApproachAngle(_breathAngle, want, (Phase == 2 ? 0.9f : 0.6f) * dt);
                    _shardT -= dt;
                    while (_shardT <= 0)
                    {
                        _shardT += 0.055f;
                        float a = _breathAngle + r.Rand.Range(-0.2f, 0.2f);
                        float sp = r.Rand.Range(150, 185);
                        r.Spawn(new Projectile { Type = ProjType.Shard, X = Head.X + MathF.Cos(a) * 8, Y = Head.Y + MathF.Sin(a) * 8, VX = MathF.Cos(a) * sp, VY = MathF.Sin(a) * sp, R = 3, Life = 2f, Damage = 1, HitsTerrain = true });
                    }
                    if (T <= 0) Go(S.Sink, 0.6f);
                    break;
                }

                case S.Sink:
                    Head = new Vector2(PlumeAt.X, Calc.Lerp(_floor - 58, _floor + 40, Calc.EaseInCubic(1 - T / 0.6f)));
                    if (T <= 0) Go(S.Hidden, 0.7f);
                    break;

                case S.Quake:
                    if (T <= 0)
                    {
                        int n = Phase == 2 ? 10 : 7;
                        float span = _right - _left - 16;
                        for (int i = 0; i < n; i++)
                        {
                            float x = _left + 8 + (i + r.Rand.Range(0.15f, 0.85f)) * span / n;
                            float top = CeilingAt(r, x, _floor - 8) + 4;
                            r.Spawn(new Projectile { Type = ProjType.Icicle, X = x, Y = top, Gravity = 600, R = 4, Delay = 0.55f + r.Rand.Range(0, 1.1f), Life = 4f, Damage = 1, HitsTerrain = true });
                        }
                        Go(S.Hidden, 1.6f);
                    }
                    break;

                case S.Rage:
                    if (T <= 0) Go(S.Hidden, 0.2f);
                    break;
            }
            Push();
        }

        private S _next;
        private float _shardT;

        private void StartArc(Room r, float px)
        {
            int dir = r.Rand.NextFloat() < 0.5f ? -1 : 1;
            float len = 150f;
            float x0 = px - dir * 72f;
            if (x0 < _left + 16 || x0 > _right - 16) { dir = -dir; x0 = px - dir * 72f; }
            x0 = Math.Clamp(x0, _left + 16, _right - 16);
            float x1 = Math.Clamp(x0 + dir * len, _left + 10, _right - 10);
            _x0 = x0; _x1 = x1;
            _dur = (Phase == 2 ? 1.05f : 1.25f) * Math.Max(0.6f, Math.Abs(x1 - x0) / len);
            PlumeAt = new Vector2(x0, _floor);
            Head = new Vector2(x0, _floor + 30);
            _next = S.Arc;
            Go(S.Plume, (_arcs == 2 || Phase == 1 ? 0.62f : 0.42f) * Tell);
            r.Emit(Ev.BossCue, PlumeAt, index: 42);
        }

        private static float ApproachAngle(float a, float b, float max)
        {
            float d = MathF.IEEERemainder(b - a, MathF.PI * 2);
            return a + Math.Clamp(d, -max, max);
        }

        public bool Above(Vector2 v) => v.Y < _floor - 2;

        protected override void CollectBoxes()
        {
            if (Above(Head))
            {
                var head = new Rectangle((int)Head.X - 8, (int)Head.Y - 7, 16, 14);
                Hurt.Add(head);
                Harm.Add((head, State == (int)S.Arc ? 2 : 1));
            }
            for (int i = 0; i < Segments; i++)
            {
                var s = Segment(i);
                if (!Above(s)) continue;
                int rad = i < 3 ? 7 : (i < 7 ? 6 : (i < 10 ? 5 : 4));
                var box = new Rectangle((int)s.X - rad, (int)s.Y - rad, rad * 2, rad * 2);
                if (i < 3) Hurt.Add(box);
                box.Inflate(-1, -1);
                Harm.Add((box, 1));
            }
        }

        protected override void DeathThroes(Room r, float dt)
        {
            // Rises one last time, then collapses into the snow.
            float k = DeathT / DeathTime;
            Head = new Vector2(Head.X, Calc.Lerp(Math.Min(Head.Y, _floor - 40), _floor + 40, Calc.EaseInCubic(Calc.Clamp01((k - 0.35f) / 0.65f))));
            Push();
        }
    }
}
