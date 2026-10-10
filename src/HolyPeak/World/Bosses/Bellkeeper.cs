using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    /// <summary>
    /// The Bellkeeper, who silenced the great bell a thousand years ago and has guarded it since.
    /// A golden ward turns every strike aside until the pilgrim's own bell rings close to it. In the
    /// last stand the ward is gone and the keeper sounds the great bell itself: rings that can only
    /// be survived by answering them with a toll.
    /// </summary>
    public sealed class Bellkeeper : Boss
    {
        public const int BodyW = 28, BodyH = 42;
        private enum S { Idle, Stride, Raise, Slam, Recover, DashTell, Dash, DashStop, Barrage, Crouch, Leap, Land, TollRaise, TollRing, Stagger, Roar }
        private enum Move { Slam, Dash, Barrage, Leap, GreatToll }

        private readonly float _floor;
        private float _left, _right;
        public float VX, VY;
        public bool ShieldUp = true;
        public float ShieldT, ShieldFlash;
        private int _last = -1, _combo, _rings, _sinceToll;
        private Move _move;

        public bool Raising => State == (int)S.Raise;
        public bool Slamming => State == (int)S.Slam;
        public bool DashTelling => State == (int)S.DashTell;
        public bool Dashing => State == (int)S.Dash;
        public bool Crouching => State == (int)S.Crouch;
        public bool Airborne => State == (int)S.Leap;
        public bool TollRaising => State == (int)S.TollRaise;
        public bool Staggered => State == (int)S.Stagger || State == (int)S.Roar;
        public bool Barraging => State == (int)S.Barrage;
        public float StateT => T;

        public Bellkeeper(Point spawn, Room room) : base("bellkeeper", "The Bellkeeper", "Warden of the Silent Bell", 190, spawn, 300)
        {
            _floor = spawn.Y;
        }

        protected override bool Vulnerable => !ShieldUp;
        private float Tell => Phase == 3 ? 0.72f : (Phase == 2 ? 0.86f : 1f);
        private float ShieldWindow => Phase == 1 ? 3.6f : 2.7f;

        protected override void OnWake(Room r)
        {
            (_left, _right) = ArenaSpan(r, X, _floor);
            _left += BodyW / 2f; _right -= BodyW / 2f;
            Facing = r.P.Center.X < X ? -1 : 1;
        }

        protected override void Intro(Room r, float dt)
        {
            if (IntroT > 1.0f && IntroT - dt <= 1.0f) r.Emit(Ev.BossCue, new Vector2(X, Y - 30), index: 61);
        }

        private void Go(S s, float t) { State = (int)s; T = t; }

        public override void OnToll(Room r, Vector2 c)
        {
            if (!Active || Dead || InIntro || !ShieldUp || Phase == 3) return;
            if (Vector2.Distance(c, new Vector2(X, Y - BodyH / 2f)) > 78f) return;
            ShieldUp = false;
            ShieldT = ShieldWindow;
            ShieldFlash = 1f;
            r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH / 2f), index: 60);
            if (State != (int)S.Dash && State != (int)S.Leap) { VX = 0; Go(S.Stagger, 0.7f); }
        }

        protected override void Update(Room r, float dt)
        {
            var p = r.P.Center;
            float dx = p.X - X;
            T -= dt;
            if (ShieldFlash > 0) ShieldFlash -= dt * 2;
            if (!ShieldUp && Phase < 3)
            {
                ShieldT -= dt;
                if (ShieldT <= 0) { ShieldUp = true; r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH / 2f), index: 62); }
            }

            bool calm = State == (int)S.Idle || State == (int)S.Recover || State == (int)S.Stride || State == (int)S.Stagger;
            if (calm && Phase == 1 && Hp <= MaxHp * 2 / 3) { Phase = 2; Roar(r); }
            else if (calm && Phase == 2 && Hp <= MaxHp / 3)
            {
                Phase = 3;
                ShieldUp = false;
                ShieldFlash = 1f;
                r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH / 2f), index: 60);
                Roar(r);
            }

            switch ((S)State)
            {
                case S.Idle:
                    Facing = dx < 0 ? -1 : 1;
                    VX = 0;
                    if (T <= 0) Choose(r, dx);
                    break;

                case S.Stride:
                    Facing = dx < 0 ? -1 : 1;
                    VX = Facing * (Phase == 3 ? 62f : 46f);
                    if (Math.Abs(dx) < 46 || T <= 0)
                    {
                        VX = 0;
                        _combo = Phase == 3 ? 3 : 1;
                        Go(S.Raise, 0.56f * Tell);
                        r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH), index: 63);
                    }
                    break;

                case S.Raise:
                    if (T <= 0)
                    {
                        Go(S.Slam, 0.16f);
                        var at = new Vector2(X + Facing * 26, _floor);
                        r.Emit(Ev.BossCue, at, index: 64);
                        for (int d = -1; d <= 1; d += 2)
                            r.Spawn(new Projectile { Type = ProjType.Wave, X = at.X + d * 6, Y = _floor, VX = d * (Phase == 1 ? 150f : 175f), R = 5, Life = 2.4f, Damage = 1, HitsTerrain = true });
                    }
                    break;

                case S.Slam:
                    if (T <= 0)
                    {
                        _combo--;
                        if (_combo > 0)
                        {
                            Facing = dx < 0 ? -1 : 1;
                            X = Math.Clamp(X + Facing * 14, _left, _right);
                            Go(S.Raise, 0.36f);
                            r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH), index: 63);
                        }
                        else Go(S.Recover, Phase == 3 ? 0.55f : 0.75f);
                    }
                    break;

                case S.Recover:
                    VX = 0;
                    if (T <= 0) Go(S.Idle, Phase == 3 ? 0.2f : 0.4f);
                    break;

                case S.DashTell:
                    Facing = dx < 0 ? -1 : 1;
                    if (T <= 0) { Go(S.Dash, 3f); r.Emit(Ev.BossCue, new Vector2(X, Y - 10), index: 65); }
                    break;

                case S.Dash:
                    VX = Calc.Approach(VX, Facing * (Phase == 3 ? 300f : 265f), 1300f * dt);
                    if ((Facing < 0 && X <= _left + 1) || (Facing > 0 && X >= _right - 1))
                    {
                        VX = 0;
                        r.Emit(Ev.BossCue, new Vector2(X + Facing * BodyW / 2f, Y - 20), new Vector2(Facing, 0), 4);
                        Go(S.DashStop, 0.7f);
                    }
                    break;

                case S.DashStop:
                    if (T <= 0) Go(S.Idle, 0.3f);
                    break;

                case S.Barrage:
                    Facing = dx < 0 ? -1 : 1;
                    if (T <= 0)
                    {
                        if (_rings-- <= 0) { Go(S.Recover, 0.5f); break; }
                        int n = 12;
                        float rot = r.Rand.Range(0, MathF.PI * 2 / n);
                        var c = new Vector2(X, Y - 26);
                        float sp = Phase == 1 ? 56f : 66f;
                        for (int i = 0; i < n; i++)
                        {
                            float a = rot + i * MathF.PI * 2 / n;
                            r.Spawn(new Projectile { Type = ProjType.Note, X = c.X, Y = c.Y, VX = MathF.Cos(a) * sp, VY = MathF.Sin(a) * sp, R = 4, Life = 6f, Damage = 1 });
                        }
                        r.Emit(Ev.BossCue, c, index: 28);
                        T = 0.75f;
                    }
                    break;

                case S.Crouch:
                    Facing = dx < 0 ? -1 : 1;
                    if (T <= 0)
                    {
                        float tx = _move == Move.GreatToll ? (_left + _right) / 2f : Math.Clamp(p.X, _left, _right);
                        float flight = 0.85f;
                        VX = (tx - X) / flight;
                        VY = -900f * flight / 2f;
                        Go(S.Leap, 3f);
                        r.Emit(Ev.BossCue, new Vector2(X, Y), index: 5);
                    }
                    break;

                case S.Leap:
                    VY += 900f * dt;
                    if (VY > 0 && Y + VY * dt >= _floor)
                    {
                        Y = _floor; VY = 0; VX = 0;
                        r.Emit(Ev.BossCue, new Vector2(X, Y), index: 6);
                        for (int d = -1; d <= 1; d += 2)
                            r.Spawn(new Projectile { Type = ProjType.Wave, X = X + d * 16, Y = _floor, VX = d * 170f, R = 5, Life = 2.4f, Damage = 1, HitsTerrain = true });
                        if (_move == Move.GreatToll)
                        {
                            Go(S.TollRaise, 1.15f * (Phase == 3 ? 0.9f : 1f));
                            r.Emit(Ev.BossCue, new Vector2(X, Y - BodyH), index: 66);
                        }
                        else
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float x = Calc.Lerp(_left - 8, _right + 8, (i + r.Rand.Range(0.2f, 0.8f)) / 5f);
                                float top = CeilingAt(r, x, _floor - 8) + 4;
                                r.Spawn(new Projectile { Type = ProjType.Icicle, X = x, Y = top, Gravity = 600, R = 4, Delay = 0.5f + r.Rand.Range(0, 0.7f), Life = 4f, Damage = 1, HitsTerrain = true });
                            }
                            Go(S.Land, 0.6f);
                        }
                    }
                    break;

                case S.Land:
                    if (T <= 0) Go(S.Idle, 0.35f);
                    break;

                case S.TollRaise:
                    if (T <= 0)
                    {
                        _rings = Phase == 3 ? 2 : 1;
                        Go(S.TollRing, 0f);
                    }
                    break;

                case S.TollRing:
                    if (T <= 0)
                    {
                        if (_rings-- <= 0) { Go(S.Recover, 0.7f); break; }
                        var c = new Vector2(X, Y - BodyH + 4);
                        r.Spawn(new Projectile { Type = ProjType.Ring, X = c.X, Y = c.Y, VX = 118f, R = 12, Life = 3.4f, Damage = 2 });
                        r.Emit(Ev.BossCue, c, index: 67);
                        T = 1.25f;
                    }
                    break;

                case S.Stagger:
                    VX = 0;
                    if (T <= 0) Go(S.Idle, 0.15f);
                    break;

                case S.Roar:
                    VX = 0;
                    if (T <= 0) Go(S.Idle, 0.2f);
                    break;
            }

            X += VX * dt;
            Y += VY * dt;
            X = Math.Clamp(X, _left, _right);
            if (State != (int)S.Leap) Y = _floor;
        }

        private void Roar(Room r)
        {
            VX = 0;
            Go(S.Roar, 1.2f);
            ClearAttacks(r);
            r.Emit(Ev.BossCue, new Vector2(X, Y - 30), index: 61);
        }

        private void Choose(Room r, float dx)
        {
            _sinceToll++;
            if (Phase == 3 && _sinceToll >= 3)
            {
                _sinceToll = 0;
                _move = Move.GreatToll;
                Go(S.Crouch, 0.4f);
                return;
            }
            float dist = Math.Abs(dx);
            int m = Pick(r, _last, dist < 80 ? 1.4f : 0.8f, dist > 70 ? 1.1f : 0.5f, 0.8f, Phase >= 2 ? 1f : 0f);
            _last = m;
            _move = (Move)m;
            switch (_move)
            {
                case Move.Slam: Go(S.Stride, 1.3f); break;
                case Move.Dash: Go(S.DashTell, 0.55f * Tell); r.Emit(Ev.BossCue, new Vector2(X, Y), index: 2); break;
                case Move.Barrage: _rings = 3; Go(S.Barrage, 0.45f); r.Emit(Ev.BossCue, new Vector2(X, Y - 26), index: 24); break;
                default: Go(S.Crouch, 0.48f * Tell); break;
            }
        }

        protected override void CollectBoxes()
        {
            var body = new Rectangle((int)(X - BodyW / 2f), (int)(Y - BodyH), BodyW, BodyH);
            Hurt.Add(body);
            var harm = body;
            harm.Inflate(-4, -4);
            Harm.Add((harm, Dashing ? 2 : 1));
            if (Slamming)
                Harm.Add((Facing > 0 ? new Rectangle((int)X, (int)_floor - 34, 54, 34) : new Rectangle((int)X - 54, (int)_floor - 34, 54, 34), 2));
        }

        protected override void DeathThroes(Room r, float dt)
        {
            VX = 0;
            if (Y < _floor) { VY += 900 * dt; Y = Math.Min(_floor, Y + VY * dt); }
        }
    }
}
