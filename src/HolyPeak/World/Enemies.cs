using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    public enum EnemyType : byte { Crawler, Hopper, Wisp, Sentinel, Spitter, Dropper }

    /// <summary>One creature of the mountain. Plain data so rooms stay cheap to copy.</summary>
    public struct Enemy
    {
        public EnemyType Type;
        public bool Alive;
        public int X, Y, W, H;
        public float RemX, RemY;
        public Vector2 Speed;
        public int Facing;
        public int Hp, MaxHp;
        public byte State;
        public float T, T2;
        public float Stun, Flash, Anim;
        public int LastSwing;
        public Vector2 Home, Aim;
        public int Orient;          // spitter: 0 floor, 1 ceiling, 2 on a wall to its left, 3 on a wall to its right
        public bool OnGround;

        public Rectangle Box => new Rectangle(X, Y, W, H);
        public Vector2 Center => new Vector2(X + W / 2f, Y + H / 2f);

        /// <summary>The sentinel's spear while it lunges.</summary>
        public Rectangle Spear => Facing > 0 ? new Rectangle(X + W, Y + 6, 14, 4) : new Rectangle(X - 14, Y + 6, 14, 4);
        public bool SpearOut => Type == EnemyType.Sentinel && State == 3;
        public bool Shielded => Type == EnemyType.Sentinel && Stun <= 0 && State <= 2;
    }

    /// <summary>Behaviour for every enemy type. Fully deterministic (uses the room's RNG only).</summary>
    public static class EnemyAI
    {
        public static Enemy Create(EnemyType type, int tx, int ty, Room room)
        {
            var e = new Enemy { Type = type, Alive = true, Facing = room.Rand.NextFloat() < 0.5f ? -1 : 1, LastSwing = -1 };
            (e.W, e.H, e.MaxHp) = type switch
            {
                EnemyType.Crawler => (12, 8, 4),
                EnemyType.Hopper => (10, 10, 6),
                EnemyType.Wisp => (9, 9, 4),
                EnemyType.Sentinel => (11, 16, 14),
                EnemyType.Spitter => (10, 10, 6),
                _ => (10, 8, 4),
            };
            e.Hp = e.MaxHp;
            int px = tx * 8, py = ty * 8;
            e.X = px + 4 - e.W / 2;
            e.Y = py + 8 - e.H;
            e.Anim = room.Rand.Range(0, 10);
            e.T = room.Rand.Range(0.3f, 1.5f);
            if (type == EnemyType.Wisp) e.Y = py + 4 - e.H / 2;
            if (type == EnemyType.Dropper) e.Y = py;
            if (type == EnemyType.Spitter)
            {
                bool below = room.StaticSolid(tx, ty + 1), above = room.StaticSolid(tx, ty - 1);
                bool left = room.StaticSolid(tx - 1, ty), right = room.StaticSolid(tx + 1, ty);
                e.Orient = below ? 0 : above ? 1 : left ? 2 : right ? 3 : 0;
                if (e.Orient == 1) e.Y = py;
                if (e.Orient == 2) { e.X = px; e.Y = py + 4 - e.H / 2; }
                if (e.Orient == 3) { e.X = px + 8 - e.W; e.Y = py + 4 - e.H / 2; }
            }
            e.Home = e.Center;
            return e;
        }

        public static int Embers(EnemyType t) => t switch
        {
            EnemyType.Crawler => 3, EnemyType.Hopper => 5, EnemyType.Wisp => 4,
            EnemyType.Sentinel => 11, EnemyType.Spitter => 6, _ => 5,
        };

        public static void Update(ref Enemy e, Room r, float dt)
        {
            e.Anim += dt;
            if (e.Flash > 0) e.Flash -= dt;
            var p = r.P.Center;
            var c = e.Center;
            float dx = p.X - c.X, dy = p.Y - c.Y;
            bool playerOk = r.P.State == PState.Normal;

            if (e.Stun > 0)
            {
                e.Stun -= dt;
                if (e.Type == EnemyType.Wisp) { e.Speed *= MathF.Max(0, 1 - 6 * dt); Fly(ref e, r, dt); }
                else if (e.Type != EnemyType.Spitter && !(e.Type == EnemyType.Dropper && e.State == 0))
                {
                    e.Speed.X = Calc.Approach(e.Speed.X, 0, 400 * dt);
                    Fall(ref e, r, dt);
                }
                return;
            }

            switch (e.Type)
            {
                case EnemyType.Crawler:
                    Walk(ref e, r, dt, 24f, true);
                    break;

                case EnemyType.Hopper:
                    switch (e.State)
                    {
                        case 0: // idle, shuffles now and then
                            e.T -= dt;
                            e.Speed.X = Calc.Approach(e.Speed.X, 0, 600 * dt);
                            if (playerOk && MathF.Abs(dx) < 104 && MathF.Abs(dy) < 70 && e.T <= 0 && e.OnGround)
                            {
                                e.State = 1; e.T = 0.38f; e.Facing = dx >= 0 ? 1 : -1;
                                r.Emit(Ev.EnemyTell, c, index: (int)e.Type);
                            }
                            else if (e.T <= -2f) { e.T = 0; e.Facing = -e.Facing; }
                            break;
                        case 1: // crouch: the tell
                            e.T -= dt;
                            if (e.T <= 0)
                            {
                                e.State = 2;
                                float dist = Math.Clamp(MathF.Abs(dx), 30, 100);
                                e.Speed = new Vector2(e.Facing * dist * 1.05f, -235f);
                                e.OnGround = false;
                                r.Emit(Ev.EnemyJump, c);
                            }
                            break;
                        case 2:
                            if (e.OnGround && e.Speed.Y >= 0) { e.State = 3; e.T = 0.55f; e.Speed.X = 0; r.Emit(Ev.EnemyLand, new Vector2(c.X, e.Y + e.H)); }
                            break;
                        default:
                            e.T -= dt;
                            if (e.T <= 0) { e.State = 0; e.T = 0.25f; }
                            break;
                    }
                    Fall(ref e, r, dt, e.State == 2 ? 0f : 1f);
                    break;

                case EnemyType.Wisp:
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    switch (e.State)
                    {
                        case 0: // drift around home
                        {
                            var target = e.Home + new Vector2(MathF.Sin(e.Anim * 0.8f) * 14f, MathF.Sin(e.Anim * 1.7f) * 6f);
                            e.Speed = Calc.Approach(e.Speed, (target - c) * 2f, 120 * dt);
                            if (playerOk && dist < 112) { e.State = 1; e.T = 1.5f; }
                            break;
                        }
                        case 1: // close in
                        {
                            var dir = dist > 1 ? new Vector2(dx, dy - 10) / dist : Vector2.Zero;
                            e.Speed = Calc.Approach(e.Speed, dir * 42f, 160 * dt);
                            e.Facing = dx >= 0 ? 1 : -1;
                            e.T -= dt;
                            if (!playerOk || dist > 190) e.State = 0;
                            else if (e.T <= 0 && dist < 96)
                            {
                                e.State = 2; e.T = 0.45f;
                                r.Emit(Ev.EnemyTell, c, index: (int)e.Type);
                            }
                            break;
                        }
                        case 2: // tell: shiver and pull back
                            e.Speed = Calc.Approach(e.Speed, new Vector2(-MathF.Sign(dx) * 18f, -10f), 300 * dt);
                            e.T -= dt;
                            if (e.T <= 0)
                            {
                                e.State = 3; e.T = 0.42f;
                                var d = new Vector2(dx, dy);
                                if (d.LengthSquared() < 1) d = new Vector2(e.Facing, 0);
                                d.Normalize();
                                e.Aim = d;
                                e.Speed = d * 155f;
                                r.Emit(Ev.EnemyLunge, c);
                            }
                            break;
                        case 3: // swoop
                            e.T -= dt;
                            if (e.T <= 0) { e.State = 4; e.T = 0.75f; }
                            break;
                        default: // recover
                            e.Speed = Calc.Approach(e.Speed, Vector2.Zero, 220 * dt);
                            e.T -= dt;
                            if (e.T <= 0) { e.State = 1; e.T = 1.6f; }
                            break;
                    }
                    Fly(ref e, r, dt);
                    break;
                }

                case EnemyType.Sentinel:
                {
                    bool sees = playerOk && MathF.Abs(dx) < 112 && MathF.Abs(dy) < 40;
                    switch (e.State)
                    {
                        case 0: // patrol
                            if (sees) { e.State = 1; e.Facing = dx >= 0 ? 1 : -1; r.Emit(Ev.EnemyTell, c, index: 100); break; }
                            Walk(ref e, r, dt, 15f, true);
                            return;
                        case 1: // guard and advance, shield raised
                            e.Facing = dx >= 0 ? 1 : -1;
                            if (!sees) { e.State = 0; break; }
                            if (MathF.Abs(dx) < 42 && e.OnGround) { e.State = 2; e.T = 0.48f; e.Speed.X = 0; r.Emit(Ev.EnemyTell, c, index: (int)e.Type); break; }
                            e.Speed.X = Calc.Approach(e.Speed.X, e.Facing * 24f, 300 * dt);
                            if (!GroundAhead(ref e, r) || WallAhead(ref e, r)) e.Speed.X = 0;
                            break;
                        case 2: // wind up
                            e.T -= dt;
                            e.Speed.X = Calc.Approach(e.Speed.X, -e.Facing * 10f, 200 * dt);
                            if (e.T <= 0) { e.State = 3; e.T = 0.22f; e.Speed.X = e.Facing * 175f; r.Emit(Ev.EnemyLunge, c); }
                            break;
                        case 3: // lunge
                            e.T -= dt;
                            if (!GroundAhead(ref e, r)) e.Speed.X = 0;
                            if (e.T <= 0) { e.State = 4; e.T = 0.8f; }
                            break;
                        default: // recover, shield lowered
                            e.Speed.X = Calc.Approach(e.Speed.X, 0, 700 * dt);
                            e.T -= dt;
                            if (e.T <= 0) e.State = (byte)(sees ? 1 : 0);
                            break;
                    }
                    Fall(ref e, r, dt);
                    break;
                }

                case EnemyType.Spitter:
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    e.Facing = dx >= 0 ? 1 : -1;
                    e.T -= dt;
                    if (e.State == 0)
                    {
                        if (playerOk && dist < 150 && e.T <= 0) { e.State = 1; e.T = 0.55f; r.Emit(Ev.EnemyTell, c, index: (int)e.Type); }
                    }
                    else if (e.T <= 0)
                    {
                        e.State = 0; e.T = 2.1f;
                        var d = new Vector2(dx, dy);
                        if (d.LengthSquared() < 1) d = new Vector2(0, -1);
                        d.Normalize();
                        var mouth = c + MouthDir(e) * 5f;
                        r.Spawn(new Projectile { Type = ProjType.Glob, X = mouth.X, Y = mouth.Y, VX = d.X * 92f, VY = d.Y * 92f, R = 3, Damage = 1, Life = 4f, HitsTerrain = true });
                        r.Emit(Ev.Shoot, mouth);
                    }
                    break;
                }

                case EnemyType.Dropper:
                    switch (e.State)
                    {
                        case 0: // hanging from the ceiling
                            if (playerOk && MathF.Abs(dx) < 30 && dy > 0 && dy < 130) { e.State = 1; e.T = 0.32f; r.Emit(Ev.EnemyTell, c, index: (int)e.Type); }
                            break;
                        case 1: // shake loose
                            e.T -= dt;
                            if (e.T <= 0) { e.State = 2; e.Speed = new Vector2(0, 30); }
                            break;
                        case 2: // falling
                            Fall(ref e, r, dt);
                            if (e.OnGround) { e.State = 3; e.T = 0.3f; r.Emit(Ev.EnemyLand, new Vector2(c.X, e.Y + e.H)); }
                            break;
                        default: // scuttle after the pilgrim
                            e.T -= dt;
                            if (e.T <= 0 && playerOk) { e.Facing = dx >= 0 ? 1 : -1; e.T = 0.9f; }
                            Walk(ref e, r, dt, 44f, true);
                            break;
                    }
                    break;
            }
        }

        public static Vector2 MouthDir(in Enemy e) => e.Orient switch
        {
            1 => new Vector2(0, 1), 2 => new Vector2(1, 0), 3 => new Vector2(-1, 0), _ => new Vector2(0, -1),
        };

        private static bool GroundAhead(ref Enemy e, Room r)
        {
            int fx = e.Facing > 0 ? e.X + e.W : e.X - 1;
            return r.SolidAt(fx, e.Y + e.H, 1, 1, true) || r.PlatformTopAt(fx, e.Y + e.H, 1);
        }

        private static bool WallAhead(ref Enemy e, Room r) => r.SolidAt(e.X + e.Facing, e.Y, e.W, e.H, true);

        private static void Walk(ref Enemy e, Room r, float dt, float speed, bool turnAtLedges)
        {
            if (e.OnGround)
            {
                if (WallAhead(ref e, r) || (turnAtLedges && !GroundAhead(ref e, r))) e.Facing = -e.Facing;
                e.Speed.X = Calc.Approach(e.Speed.X, e.Facing * speed, 600 * dt);
            }
            Fall(ref e, r, dt);
        }

        private static void Fall(ref Enemy e, Room r, float dt, float friction = 0f)
        {
            e.Speed.Y = Calc.Approach(e.Speed.Y, 220f, 900f * dt);
            if (!MoveX(ref e, r, e.Speed.X * dt)) e.Speed.X = 0;
            if (!MoveY(ref e, r, e.Speed.Y * dt))
            {
                if (e.Speed.Y > 0) e.OnGround = true;
                e.Speed.Y = 0;
            }
            else e.OnGround = e.Speed.Y >= 0 && (r.SolidAt(e.X, e.Y + 1, e.W, e.H, true) || r.PlatformTopAt(e.X, e.Y + e.H, e.W));
        }

        private static void Fly(ref Enemy e, Room r, float dt)
        {
            if (!MoveX(ref e, r, e.Speed.X * dt)) e.Speed.X *= -0.5f;
            if (!MoveY(ref e, r, e.Speed.Y * dt)) e.Speed.Y *= -0.5f;
        }

        public static bool MoveX(ref Enemy e, Room r, float amount)
        {
            e.RemX += amount;
            int move = (int)MathF.Round(e.RemX);
            e.RemX -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                if (r.SolidAt(e.X + sign, e.Y, e.W, e.H, true)) { e.RemX = 0; return false; }
                e.X += sign; move -= sign;
            }
            return true;
        }

        public static bool MoveY(ref Enemy e, Room r, float amount)
        {
            e.RemY += amount;
            int move = (int)MathF.Round(e.RemY);
            e.RemY -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                bool blocked = r.SolidAt(e.X, e.Y + sign, e.W, e.H, true) || (sign > 0 && e.Type != EnemyType.Wisp && r.PlatformTopAt(e.X, e.Y + e.H, e.W));
                if (blocked) { e.RemY = 0; return false; }
                e.Y += sign; move -= sign;
            }
            return true;
        }
    }

    public enum ProjType : byte { Glob, Orb, Shard, Flame, Wave, Thorn, Icicle, Note, Rock, Ring }

    public struct Projectile
    {
        public ProjType Type;
        public bool Active;
        public float X, Y, VX, VY, Gravity, Life, R, T;
        public float Delay;         // harmless and motionless until T reaches this (a telegraph)
        public int Damage;
        public bool HitsTerrain;
        public bool Armed => T >= Delay;
        public bool Breakable => Type == ProjType.Glob || Type == ProjType.Orb || Type == ProjType.Note;
    }

    /// <summary>An ember shaken loose from a creature or a cache, bouncing until the pilgrim collects it.</summary>
    public struct EmberDrop
    {
        public bool Active;
        public float X, Y, VX, VY, T;
        public int Value;
    }
}
