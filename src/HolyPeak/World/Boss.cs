using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HolyPeak.World
{
    /// <summary>
    /// A guardian. Sleeps until the pilgrim steps into its arena, then the arena doors close until one of them falls.
    /// Subclasses implement the fight; this class handles the trigger, damage bookkeeping and the death throes.
    /// </summary>
    public abstract class Boss
    {
        public string Id, Name, Title;
        public int Hp, MaxHp;
        public bool Active, Dead, Finished;
        public float X, Y;              // feet / anchor in room pixels
        public int Facing = -1;
        public int State, Phase = 1;
        public float T, Time, Flash, IntroT, DeathT;
        public int LastSwing = -1;
        public readonly Point Spawn;
        public readonly int EmberReward;

        /// <summary>Areas where the staff can hurt this guardian.</summary>
        protected readonly List<Rectangle> Hurt = new List<Rectangle>(4);
        /// <summary>Areas that hurt the pilgrim, with their damage.</summary>
        protected readonly List<(Rectangle box, int damage)> Harm = new List<(Rectangle, int)>(8);

        public const float IntroTime = 2.2f, DeathTime = 3.2f;
        public bool InIntro => Active && IntroT < IntroTime;

        protected Boss(string id, string name, string title, int hp, Point spawn, int embers)
        {
            Id = id; Name = name; Title = title;
            Hp = MaxHp = hp;
            Spawn = spawn;
            X = spawn.X; Y = spawn.Y;
            EmberReward = embers;
        }

        public float HpFraction => MaxHp > 0 ? Math.Max(0, Hp) / (float)MaxHp : 0;

        public void Tick(Room r, float dt)
        {
            Time += dt;
            if (Flash > 0) Flash -= dt;
            if (Finished) return;
            if (!Active)
            {
                if (ShouldWake(r))
                {
                    Active = true;
                    IntroT = 0;
                    r.ArenaClosed = true;
                    r.Emit(Ev.BossStart, new Vector2(X, Y - 20));
                    OnWake(r);
                }
                return;
            }
            if (Dead)
            {
                DeathT += dt;
                DeathThroes(r, dt);
                if (DeathT >= DeathTime)
                {
                    Finished = true;
                    r.ArenaClosed = false;
                    r.Flags.Add("boss:" + Id);
                    r.DropEmbers(new Vector2(X, Y - 16), EmberReward, 1.4f);
                    r.Emit(Ev.BossCue, new Vector2(X, Y - 16), index: 999);
                }
                return;
            }
            if (IntroT < IntroTime)
            {
                IntroT += dt;
                Intro(r, dt);
                return;
            }
            Hurt.Clear();
            Harm.Clear();
            Update(r, dt);
            CollectBoxes();
        }

        /// <summary>Wakes when the pilgrim is on the arena floor, clear of the doors.</summary>
        protected virtual bool ShouldWake(Room r)
        {
            var p = r.P.Center;
            if (!r.P.OnGround || r.P.State != PState.Normal) return false;
            if (Math.Abs(p.X - Spawn.X) > 13 * 8) return false;
            var hb = r.P.Hitbox;
            hb.Inflate(10, 0);
            for (int ty = Room.FloorDiv(hb.Top, 8); ty <= Room.FloorDiv(hb.Bottom - 1, 8); ty++)
                for (int tx = Room.FloorDiv(hb.Left, 8); tx <= Room.FloorDiv(hb.Right - 1, 8); tx++)
                    if (r.TileAt(tx, ty) == Tile.ArenaDoor) return false;
            return true;
        }

        protected virtual void OnWake(Room r) { }
        protected virtual void Intro(Room r, float dt) { }
        protected abstract void Update(Room r, float dt);
        protected abstract void CollectBoxes();
        protected virtual void DeathThroes(Room r, float dt) { }
        public virtual void OnToll(Room r, Vector2 c) { }

        /// <summary>Whether a strike may land right now (e.g. not while shielded or vanished).</summary>
        protected virtual bool Vulnerable => true;

        public bool HurtboxHit(Rectangle box)
        {
            if (InIntro) return false;
            foreach (var h in Hurt) if (h.Intersects(box)) return true;
            return false;
        }

        public int ContactDamage(Rectangle hb, out float fromX)
        {
            fromX = X;
            if (InIntro || Dead) return 0;
            int best = 0;
            foreach (var (box, dmg) in Harm)
                if (dmg > best && box.Intersects(hb)) { best = dmg; fromX = box.Center.X; }
            return best;
        }

        public virtual void TakeHit(Room r, int damage, Rectangle box, int dirX)
        {
            if (!Vulnerable)
            {
                Deflect(r, box, dirX);
                return;
            }
            Hp -= damage;
            Flash = 0.12f;
            var at = box.Center.ToVector2();
            if (Hp <= 0)
            {
                Hp = 0;
                Dead = true;
                DeathT = 0;
                r.Emit(Ev.BossDie, new Vector2(X, Y - 16));
                ClearAttacks(r);
                return;
            }
            r.Emit(Ev.BossHit, at, new Vector2(dirX, 0));
            OnDamaged(r);
        }

        protected virtual void Deflect(Room r, Rectangle box, int dirX)
        {
            r.Emit(Ev.Clank, box.Center.ToVector2(), new Vector2(dirX, 0), -1);
            if (r.P.Attack == Strike.Side) r.P.Speed.X = -r.P.Facing * 150f;
        }

        protected virtual void OnDamaged(Room r) { }

        protected static void ClearAttacks(Room r)
        {
            for (int i = 0; i < r.Projectiles.Length; i++) r.Projectiles[i].Active = false;
        }

        /// <summary>Pick one of several moves with the room's deterministic RNG, avoiding an immediate repeat.</summary>
        protected int Pick(Room r, int last, params float[] weights)
        {
            float sum = 0;
            for (int i = 0; i < weights.Length; i++) if (i != last) sum += weights[i];
            float v = r.Rand.NextFloat() * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == last) continue;
                v -= weights[i];
                if (v <= 0) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>Pixel row just below the ceiling above (x, fromY).</summary>
        protected static float CeilingAt(Room r, float x, float fromY)
        {
            int tx = (int)x / 8;
            int ty = (int)fromY / 8;
            while (ty > 0 && !r.StaticSolid(tx, ty - 1)) ty--;
            return ty * 8;
        }

        /// <summary>Left and right wall (or arena door) positions at the given floor row.</summary>
        protected static (float left, float right) ArenaSpan(Room r, float x, float floorY)
        {
            int row = (int)(floorY - 4) / 8;
            int l = (int)x / 8, rr = l;
            bool Wall(int tx) { var t = r.TileAt(tx, row); return Room.IsSolidTile(t) || t == Tile.ArenaDoor || tx < 0 || tx >= r.Cols; }
            while (!Wall(l - 1)) l--;
            while (!Wall(rr + 1)) rr++;
            return (l * 8, (rr + 1) * 8);
        }

        public IReadOnlyList<Rectangle> Hurtboxes => Hurt;
        public IReadOnlyList<(Rectangle box, int damage)> Harmboxes => Harm;
    }

    public static class Bosses
    {
        public static Boss Create(string id, Room room, Point spawn) => id switch
        {
            "thornback" => new Thornback(spawn),
            "abbot" => new HollowAbbot(spawn, room),
            "wyrm" => new FrostWyrm(spawn, room),
            "bellkeeper" => new Bellkeeper(spawn, room),
            _ => throw new InvalidOperationException($"unknown boss '{id}'"),
        };

        public static string NameOf(string id) => id switch
        {
            "thornback" => "Thornback", "abbot" => "The Hollow Abbot", "wyrm" => "The Frost Wyrm", "bellkeeper" => "The Bellkeeper", _ => id,
        };
    }
}
