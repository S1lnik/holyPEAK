using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    /// <summary>
    /// The Hollow Abbot, last keeper of the Sunken Abbey. Vanishes and reappears, hurls candle flames,
    /// swings his censer at anyone who stands too close; wounded, he rains wax from the vaults and
    /// sings rings of notes that only a bell can answer.
    /// </summary>
    public sealed class HollowAbbot : Boss
    {
        private enum S { Idle, Out, In, Summon, Fire, Raise, Swing, Recover, Rain, Sing, Scream }
        private enum Move { Volley, Censer, Rain, Notes }

        private readonly float _floor;
        private float _left, _right;
        private Move _next;
        private int _last = -1, _flames, _waves;
        public Vector2[] Flames = new Vector2[6];
        public int FlameCount;
        public float Alpha = 1f;
        public bool Swinging => State == (int)S.Swing;
        public bool Raising => State == (int)S.Raise;
        public bool Summoning => State == (int)S.Summon || State == (int)S.Fire;
        public bool Singing => State == (int)S.Sing;

        public HollowAbbot(Point spawn, Room room) : base("abbot", "The Hollow Abbot", "Keeper of the Sunken Abbey", 92, spawn, 180)
        {
            _floor = spawn.Y;
            Y = _floor - 40;
        }

        private float Tell => Phase == 2 ? 0.8f : 1f;

        protected override void OnWake(Room r)
        {
            (_left, _right) = ArenaSpan(r, X, _floor);
            Facing = r.P.Center.X < X ? -1 : 1;
        }

        protected override void Intro(Room r, float dt)
        {
            Y = Calc.Lerp(_floor - 40, _floor - 70, Calc.SmoothStep(IntroT / IntroTime));
            if (IntroT > 0.9f && IntroT - dt <= 0.9f) r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 20);
        }

        private void Go(S s, float t) { State = (int)s; T = t; }

        protected override void Update(Room r, float dt)
        {
            var p = r.P.Center;
            T -= dt;
            if (State != (int)S.Out && State != (int)S.In) Facing = p.X < X ? -1 : 1;

            if (Phase == 1 && Hp <= MaxHp / 2 && (State == (int)S.Idle || State == (int)S.Recover))
            {
                Phase = 2;
                Go(S.Scream, 1.0f);
                r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 20);
            }

            switch ((S)State)
            {
                case S.Idle:
                    Alpha = 1;
                    Y += MathF.Sin(Time * 2.4f) * 6f * dt;
                    if (T <= 0)
                    {
                        int m = Pick(r, _last, 1f, 1f, Phase == 2 ? 0.9f : 0f, Phase == 2 ? 0.9f : 0f);
                        _last = m;
                        _next = (Move)m;
                        Go(S.Out, 0.3f);
                        r.Emit(Ev.BossCue, new Vector2(X, Y - 15), index: 21);
                    }
                    break;

                case S.Out:
                    Alpha = Math.Max(0, T / 0.3f);
                    if (T <= 0)
                    {
                        PlaceFor(r, _next);
                        Go(S.In, 0.3f);
                        r.Emit(Ev.BossCue, new Vector2(X, Y - 15), index: 22);
                    }
                    break;

                case S.In:
                    Alpha = 1 - Math.Max(0, T / 0.3f);
                    if (T <= 0)
                    {
                        Alpha = 1;
                        switch (_next)
                        {
                            case Move.Volley:
                                _flames = Phase == 2 ? 5 : 3;
                                FlameCount = 0;
                                Go(S.Summon, 0.18f);
                                break;
                            case Move.Censer:
                                Go(S.Raise, 0.52f * Tell);
                                r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 23);
                                break;
                            case Move.Rain:
                                _waves = 2;
                                Go(S.Rain, 0.2f);
                                break;
                            default:
                                _waves = 2;
                                Go(S.Sing, 0.5f);
                                r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 24);
                                break;
                        }
                    }
                    break;

                case S.Summon:
                    UpdateFlames();
                    if (T <= 0)
                    {
                        if (FlameCount < _flames)
                        {
                            FlameCount++;
                            r.Emit(Ev.BossCue, Flames[FlameCount - 1], index: 25);
                            T = 0.16f;
                        }
                        else Go(S.Fire, 0.45f * Tell);
                    }
                    break;

                case S.Fire:
                    UpdateFlames();
                    if (T <= 0)
                    {
                        if (FlameCount == 0) { Go(S.Recover, 0.55f); break; }
                        var f = Flames[FlameCount - 1];
                        var d = p - f;
                        if (d.LengthSquared() < 1) d = new Vector2(0, 1);
                        d.Normalize();
                        float sp = Phase == 2 ? 158f : 136f;
                        r.Spawn(new Projectile { Type = ProjType.Orb, X = f.X, Y = f.Y, VX = d.X * sp, VY = d.Y * sp, R = 4, Life = 4f, Damage = 1, HitsTerrain = true });
                        r.Emit(Ev.Shoot, f, index: 1);
                        FlameCount--;
                        T = Phase == 2 ? 0.19f : 0.24f;
                    }
                    break;

                case S.Raise:
                    if (T <= 0) { Go(S.Swing, 0.26f); r.Emit(Ev.BossCue, new Vector2(X + Facing * 20, Y - 12), index: 26); }
                    break;

                case S.Swing:
                    if (T <= 0) Go(S.Recover, Phase == 2 ? 0.5f : 0.7f);
                    break;

                case S.Recover:
                    if (T <= 0) Go(S.Idle, (Phase == 2 ? 0.35f : 0.55f));
                    break;

                case S.Rain:
                    if (T <= 0)
                    {
                        if (_waves-- <= 0) { Go(S.Recover, 0.4f); break; }
                        int n = 6;
                        float span = _right - _left - 24;
                        float off = r.Rand.Range(0, span / n);
                        for (int i = 0; i < n; i++)
                        {
                            float x = _left + 12 + off + i * span / n;
                            float top = CeilingAt(r, x, _floor - 8) + 4;
                            r.Spawn(new Projectile { Type = ProjType.Flame, X = x, Y = top, Gravity = 420, R = 4, Delay = 0.75f * Tell, Life = 4f, Damage = 1, HitsTerrain = true });
                        }
                        r.Emit(Ev.BossCue, new Vector2(X, Y - 20), index: 27);
                        T = 1.05f;
                    }
                    break;

                case S.Sing:
                    if (T <= 0)
                    {
                        if (_waves-- <= 0) { Go(S.Recover, 0.5f); break; }
                        int n = 11;
                        float rot = r.Rand.Range(0, MathF.PI * 2 / n);
                        var c = new Vector2(X, Y - 16);
                        for (int i = 0; i < n; i++)
                        {
                            float a = rot + i * MathF.PI * 2 / n;
                            r.Spawn(new Projectile { Type = ProjType.Note, X = c.X, Y = c.Y, VX = MathF.Cos(a) * 54f, VY = MathF.Sin(a) * 54f, R = 4, Life = 6f, Damage = 1 });
                        }
                        r.Emit(Ev.BossCue, c, index: 28);
                        T = 0.95f;
                    }
                    break;

                case S.Scream:
                    if (T <= 0) Go(S.Idle, 0.2f);
                    break;
            }
        }

        private void UpdateFlames()
        {
            for (int i = 0; i < Flames.Length; i++)
            {
                float a = -MathF.PI / 2 + (i - (_flames - 1) / 2f) * 0.55f + MathF.Sin(Time * 2f) * 0.05f;
                Flames[i] = new Vector2(X, Y - 16) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 30f;
            }
        }

        private void PlaceFor(Room r, Move m)
        {
            float px = r.P.Center.X;
            float mid = (_left + _right) / 2f;
            switch (m)
            {
                case Move.Censer:
                {
                    int side = px > mid ? -1 : 1;           // appear on the roomier side of the pilgrim
                    if (r.Rand.NextFloat() < 0.35f) side = -side;
                    float x = px + side * 34f;
                    if (x < _left + 14 || x > _right - 14) x = px - side * 34f;
                    X = Math.Clamp(x, _left + 14, _right - 14);
                    Y = _floor - 2;
                    break;
                }
                case Move.Volley:
                {
                    float x = px < mid ? _right - 46 : _left + 46;
                    X = x + r.Rand.Range(-10, 10);
                    Y = _floor - 70 - r.Rand.Range(0, 16);
                    break;
                }
                default:
                    X = mid + r.Rand.Range(-30, 30);
                    Y = _floor - 92;
                    break;
            }
        }

        protected override void CollectBoxes()
        {
            if (State == (int)S.Out || State == (int)S.In) return;
            var body = new Rectangle((int)X - 8, (int)Y - 30, 16, 30);
            Hurt.Add(body);
            var harm = body;
            harm.Inflate(-2, -3);
            Harm.Add((harm, 1));
            if (Swinging) Harm.Add((Facing > 0 ? new Rectangle((int)X + 2, (int)Y - 34, 42, 36) : new Rectangle((int)X - 44, (int)Y - 34, 42, 36), 1));
        }

        protected override void DeathThroes(Room r, float dt)
        {
            Alpha = 1 - Calc.Clamp01((DeathT - 2.2f) / 1f);
            Y = Calc.Approach(Y, _floor - 2, 30 * dt);
        }
    }
}
