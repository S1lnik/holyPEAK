using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    /// <summary>
    /// Thornback, the bramble boar of Thornwood. Charges wall to wall, leaps onto the pilgrim and sends
    /// shockwaves along the ground; wounded, it charges twice and wakes thorns under the pilgrim's feet.
    /// </summary>
    public sealed class Thornback : Boss
    {
        public const int BodyW = 38, BodyH = 24;
        public enum S { Idle, Paw, Charge, Stunned, Crouch, Leap, Land, Roar, Erupt, Rage }

        public float VX, VY;
        private readonly float _floor;
        private float _left, _right;
        private int _last = -1, _thornsLeft, _leapsLeft;
        private bool _boundsSet, _secondCharge;
        public S Mode => (S)State;
        public bool Charging => State == (int)S.Charge;
        public bool Airborne => State == (int)S.Leap;
        public bool Telegraphing => State == (int)S.Paw || State == (int)S.Crouch || State == (int)S.Roar;

        public Thornback(Point spawn) : base("thornback", "Thornback", "The Bramble Boar", 52, spawn, 120)
        {
            _floor = spawn.Y;
        }

        private float Speed => Phase == 2 ? 1.15f : 1f;
        private float Tell => Phase == 2 ? 0.82f : 1f;

        private void FindBounds(Room r)
        {
            var (l, rr) = ArenaSpan(r, X, _floor);
            _left = l + BodyW / 2f;
            _right = rr - BodyW / 2f;
            _boundsSet = true;
        }

        protected override void OnWake(Room r)
        {
            FindBounds(r);
            Facing = r.P.Center.X < X ? -1 : 1;
        }

        protected override void Intro(Room r, float dt)
        {
            if (IntroT > 0.6f && IntroT - dt <= 0.6f) r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 1);
        }

        private void Go(S s, float t) { State = (int)s; T = t; }

        protected override void Update(Room r, float dt)
        {
            if (!_boundsSet) FindBounds(r);
            var p = r.P.Center;
            float dx = p.X - X;
            T -= dt;

            if (Phase == 1 && Hp <= MaxHp / 2 && State != (int)S.Leap && State != (int)S.Charge)
            {
                Phase = 2;
                Go(S.Rage, 1.1f);
                VX = 0;
                r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 1);
            }

            switch ((S)State)
            {
                case S.Idle:
                    Facing = dx < 0 ? -1 : 1;
                    VX = Math.Abs(dx) > 70 ? Facing * 34f * Speed : 0;
                    if (T <= 0)
                    {
                        float dist = Math.Abs(dx);
                        int move = Pick(r, _last, dist > 90 ? 0.55f : 0.3f, dist > 90 ? 0.45f : 0.7f, Phase == 2 ? 0.45f : 0f);
                        _last = move;
                        VX = 0;
                        if (move == 0) { Go(S.Paw, 0.62f * Tell); _secondCharge = false; r.Emit(Ev.BossCue, new Vector2(X, Y), index: 2); }
                        else if (move == 1) { Go(S.Crouch, 0.45f * Tell); _leapsLeft = Phase == 2 ? 2 : 1; }
                        else { Go(S.Roar, 0.7f * Tell); _thornsLeft = 4; r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 1); }
                    }
                    break;

                case S.Paw:
                    Facing = dx < 0 ? -1 : 1;
                    if (T <= 0) { Go(S.Charge, 3f); VX = 0; r.Emit(Ev.BossCue, new Vector2(X, Y), index: 3); }
                    break;

                case S.Charge:
                    VX = Calc.Approach(VX, Facing * 215f * Speed, 900f * dt);
                    if ((Facing < 0 && X <= _left + 1) || (Facing > 0 && X >= _right - 1))
                    {
                        VX = 0;
                        r.Emit(Ev.BossCue, new Vector2(Facing < 0 ? X - BodyW / 2f : X + BodyW / 2f, Y - 12), new Vector2(Facing, 0), 4);
                        if (Phase == 2) DropRocks(r);
                        if (Phase == 2 && !_secondCharge && r.Rand.NextFloat() < 0.55f)
                        {
                            _secondCharge = true;
                            Facing = -Facing;
                            Go(S.Paw, 0.38f);
                        }
                        else Go(S.Stunned, Phase == 2 ? 0.8f : 1.05f);
                    }
                    break;

                case S.Stunned:
                    if (T <= 0) Go(S.Idle, 0.35f * Tell);
                    break;

                case S.Crouch:
                    Facing = dx < 0 ? -1 : 1;
                    if (T <= 0)
                    {
                        float tx = Math.Clamp(p.X, _left, _right);
                        float flight = 0.78f / Speed;
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
                        Shockwaves(r);
                        _leapsLeft--;
                        if (_leapsLeft > 0) Go(S.Crouch, 0.3f);
                        else Go(S.Land, 0.55f);
                    }
                    break;

                case S.Land:
                    if (T <= 0) Go(S.Idle, 0.4f * Tell);
                    break;

                case S.Roar:
                    if (T <= 0) Go(S.Erupt, 0f);
                    break;

                case S.Erupt:
                    if (T <= 0)
                    {
                        if (_thornsLeft-- <= 0) { Go(S.Idle, 0.5f); break; }
                        float x = Math.Clamp(p.X + r.Rand.Range(-6, 6), _left - BodyW / 2f + 6, _right + BodyW / 2f - 6);
                        r.Spawn(new Projectile { Type = ProjType.Thorn, X = x, Y = _floor, R = 30, Delay = 0.55f, Life = 0.55f + 0.45f, Damage = 1 });
                        r.Emit(Ev.BossCue, new Vector2(x, _floor), index: 7);
                        T = 0.42f;
                    }
                    break;

                case S.Rage:
                    if (T <= 0) Go(S.Idle, 0.2f);
                    break;
            }

            X += VX * dt;
            Y += VY * dt;
            X = Math.Clamp(X, _left, _right);
            if (State != (int)S.Leap) Y = _floor;
        }

        private void Shockwaves(Room r)
        {
            float speed = Phase == 2 ? 165f : 145f;
            for (int d = -1; d <= 1; d += 2)
                r.Spawn(new Projectile { Type = ProjType.Wave, X = X + d * (BodyW / 2f), Y = _floor, VX = d * speed, R = 5, Life = 2.4f, Damage = 1, HitsTerrain = true });
        }

        private void DropRocks(Room r)
        {
            float px = r.P.Center.X;
            for (int i = 0; i < 3; i++)
            {
                float x = Math.Clamp(px + (i - 1) * 46 + r.Rand.Range(-10, 10), _left - 10, _right + 10);
                float top = CeilingAt(r, x, _floor - 8) + 5;
                r.Spawn(new Projectile { Type = ProjType.Rock, X = x, Y = top, Gravity = 520, R = 5, Delay = 0.45f + i * 0.18f, Life = 3f, Damage = 1, HitsTerrain = true });
            }
        }

        protected override void CollectBoxes()
        {
            var body = new Rectangle((int)(X - BodyW / 2f), (int)(Y - BodyH), BodyW, BodyH);
            Hurt.Add(body);
            var harm = body;
            harm.Inflate(-3, -3);
            Harm.Add((harm, Charging ? 2 : 1));
        }

        protected override void DeathThroes(Room r, float dt)
        {
            VX = 0;
            if (Y < _floor) { VY += 900 * dt; Y = Math.Min(_floor, Y + VY * dt); }
        }
    }
}
