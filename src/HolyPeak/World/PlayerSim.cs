using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    public enum PState : byte { Normal, Dead, Frozen }

    public enum Strike : byte { None, Side, Up, Down }

    /// <summary>Raw input for one simulation frame. Buffering is handled by the simulation itself.</summary>
    public struct PlayerInput
    {
        public sbyte MoveX, MoveY;
        public bool Jump, JumpPressed, StrikePressed, TollPressed, Heal, InteractPressed;
    }

    /// <summary>
    /// The pilgrim. All gameplay state is in this struct so a room can be snapshotted cheaply (the
    /// level verifier depends on it). Movement is integer-pixel with sub-pixel remainders, so collision
    /// is exact and never tunnels.
    /// </summary>
    public struct PlayerSim
    {
        public const int W = 6, H = 12;

        // Running
        public const float MaxRun = 84f;
        public const float GroundAccel = 1300f, GroundDecel = 1700f;
        public const float AirAccel = 950f, AirDecel = 650f;
        public const float OverspeedDecay = 420f;
        // Jumping and falling
        public const float JumpSpeed = 238f;
        public const float JumpCut = 0.42f;
        public const float GravityUp = 860f, GravityDown = 1060f;
        public const float ApexMult = 0.55f, ApexWindow = 40f;
        public const float MaxFall = 215f, FastFall = 260f;
        public const float CoyoteTime = 0.08f, JumpBufferTime = 0.11f;
        public const int UpCorner = 3;
        // Iron Grips
        public const float ClingHold = 0.32f, ClingSlideMax = 48f, ClingSlideAccel = 170f;
        public const float LeapH = 132f, LeapV = 222f, LeapForce = 0.14f;
        // Bell of Resonance
        public const float TollCooldown = 0.5f, TollHop = 158f, TollRadius = 46f;
        // Pilgrim's Kite
        public const float GlideFall = 34f, GlideMaxRun = 100f, UpdraftLift = 125f, UpdraftAccel = 950f;
        // Staff
        public const float StrikeTime = 0.24f, StrikeActiveFrom = 0.02f, StrikeActiveTo = 0.13f, StrikeCooldown = 0.33f, StrikeBuffer = 0.12f;
        public const float PogoSpeed = 228f, RecoilSpeed = 92f;
        // Hurt / heal
        public const float Invulnerability = 1.15f, HurtStun = 0.22f, HurtKnockX = 125f, HurtKnockY = 150f;
        public const float HealTime = 0.85f;
        public const int HealAmount = 2;

        public int X, Y;
        public float RemX, RemY;
        public Vector2 Speed;
        public int Facing;
        public PState State;
        public Abilities Ab;

        public int Hp, MaxHp, Flasks, MaxFlasks, Damage;

        public float Coyote, JumpBuffer, StrikeBufferT;
        public bool Rising;                 // rising from a jump whose height can still be cut short
        public bool OnGround, Clinging, Gliding, InUpdraft;
        public int WallDir;
        public float ClingTimer;
        public float ForceMoveTimer;
        public int ForceMoveX;
        public bool AirToll;                // the airborne toll-hop is available
        public float TollCooldownT;
        public Strike Attack;
        public float AttackT, AttackCooldownT;
        public int SwingId;
        public bool SwingConnected;
        public float Invuln, StunT;
        public bool Healing;
        public float HealT;
        public int SafeX, SafeY;
        public float AirTime;
        public float DropT;                 // falling through a one-way platform

        public Rectangle Hitbox => new Rectangle(X, Y, W, H);
        public Vector2 Center => new Vector2(X + W / 2f, Y + H / 2f);
        public Vector2 Feet => new Vector2(X + W / 2f, Y + H);
        public bool Has(Abilities a) => (Ab & a) == a;
        public bool StrikeActive => Attack != Strike.None && AttackT >= StrikeActiveFrom && AttackT <= StrikeActiveTo;

        /// <summary>World rectangle covered by the staff while a strike is active.</summary>
        public Rectangle StrikeBox
        {
            get
            {
                switch (Attack)
                {
                    case Strike.Up: return new Rectangle(X + W / 2 - 9, Y - 20, 18, 22);
                    case Strike.Down: return new Rectangle(X + W / 2 - 8, Y + H - 2, 16, 20);
                    default: return Facing > 0 ? new Rectangle(X + W - 1, Y - 3, 22, 16) : new Rectangle(X - 21, Y - 3, 22, 16);
                }
            }
        }

        public void Spawn(Point topLeft, int facing = 1)
        {
            X = topLeft.X; Y = topLeft.Y;
            RemX = RemY = 0;
            Speed = Vector2.Zero;
            Facing = facing == 0 ? 1 : facing;
            State = PState.Normal;
            Coyote = JumpBuffer = StrikeBufferT = 0;
            Rising = Clinging = Gliding = InUpdraft = Healing = false;
            ClingTimer = ForceMoveTimer = TollCooldownT = AttackT = AttackCooldownT = Invuln = StunT = HealT = AirTime = DropT = 0;
            Attack = Strike.None;
            AirToll = true;
            OnGround = true;
            SafeX = X; SafeY = Y;
        }

        public void Update(Room room, in PlayerInput inp, float dt)
        {
            if (State != PState.Normal) return;

            JumpBuffer = inp.JumpPressed ? JumpBufferTime : Math.Max(0, JumpBuffer - dt);
            StrikeBufferT = inp.StrikePressed ? StrikeBuffer : Math.Max(0, StrikeBufferT - dt);
            TollCooldownT -= dt;
            AttackCooldownT -= dt;
            if (Invuln > 0) Invuln -= dt;

            if (DropT > 0) DropT -= dt;
            OnGround = Speed.Y >= 0 && (room.SolidAt(X, Y + 1, W, H) || (DropT <= 0 && room.PlatformTopAt(X, Y + H, W)));
            if (OnGround)
            {
                Coyote = CoyoteTime;
                AirToll = true;
                AirTime = 0;
                if (Gliding) { Gliding = false; room.Emit(Ev.GlideClose, Center); }
                Clinging = false;
                Rising = false;
                if (room.IsSafeGround(X, Y)) { SafeX = X; SafeY = Y; }
            }
            else
            {
                Coyote -= dt;
                AirTime += dt;
            }
            InUpdraft = room.UpdraftAt(X, Y, W, H);

            if (StunT > 0)
            {
                StunT -= dt;
                Speed.Y = Calc.Approach(Speed.Y, MaxFall, GravityDown * dt);
                Speed.X = Calc.Approach(Speed.X, 0, 300 * dt);
                MoveH(room, Speed.X * dt);
                MoveV(room, Speed.Y * dt);
                return;
            }

            UpdateHeal(room, inp, dt);
            UpdateCling(room, inp, dt);
            UpdateRun(inp, dt);
            UpdateGravity(inp, dt);
            UpdateJump(room, inp);
            UpdateToll(room, inp);
            UpdateStrike(room, inp, dt);

            MoveH(room, Speed.X * dt);
            MoveV(room, Speed.Y * dt);
        }

        private void UpdateHeal(Room room, in PlayerInput inp, float dt)
        {
            if (Healing)
            {
                if (!inp.Heal || !OnGround || inp.JumpPressed || inp.StrikePressed)
                {
                    Healing = false;
                    room.Emit(Ev.HealCancel, Center);
                    return;
                }
                HealT += dt;
                if (HealT >= HealTime)
                {
                    Healing = false;
                    Flasks--;
                    Hp = Math.Min(MaxHp, Hp + HealAmount);
                    room.Emit(Ev.Heal, Center);
                }
            }
            else if (inp.Heal && OnGround && Flasks > 0 && Hp < MaxHp && Attack == Strike.None && Math.Abs(Speed.X) < 1f && inp.MoveX == 0)
            {
                Healing = true;
                HealT = 0;
                room.Emit(Ev.HealStart, Center);
            }
        }

        private bool WallAt(Room room, int dir, int dist = 1) => room.SolidAt(X + dir * dist, Y + 2, W, H - 5);

        private void UpdateCling(Room room, in PlayerInput inp, float dt)
        {
            if (!Has(Abilities.Grips) || OnGround) { Clinging = false; return; }
            if (Clinging)
            {
                // Stay latched until the pilgrim lets go, drops, or slides off the end of the wall.
                if (!WallAt(room, WallDir) || inp.MoveX == -WallDir || inp.MoveY > 0)
                {
                    Clinging = false;
                    if (inp.MoveY > 0) Speed.Y = Math.Max(Speed.Y, 40f);
                    return;
                }
                ClingTimer += dt;
                Speed.X = 0;
                Facing = WallDir;
                Speed.Y = ClingTimer < ClingHold ? 0 : Calc.Approach(Speed.Y, ClingSlideMax, ClingSlideAccel * dt);
                return;
            }
            int dir = inp.MoveX;
            if (dir != 0 && ForceMoveTimer <= 0 && Speed.Y > -60f && !Healing && WallAt(room, dir))
            {
                Clinging = true;
                WallDir = dir;
                Facing = dir;
                ClingTimer = 0;
                Speed = Vector2.Zero;
                RemY = 0;
                Rising = false;
                AirToll = true;
                if (Gliding) Gliding = false;
                room.Emit(Ev.Cling, new Vector2(dir > 0 ? X + W : X, Y + 4), new Vector2(dir, 0));
            }
        }

        private void UpdateRun(in PlayerInput inp, float dt)
        {
            if (Clinging) return;
            int moveX = Healing ? 0 : inp.MoveX;
            if (ForceMoveTimer > 0) { ForceMoveTimer -= dt; moveX = ForceMoveX; }
            if (moveX != 0 && Attack == Strike.None) Facing = moveX;

            float max = Gliding ? GlideMaxRun : MaxRun;
            float target = moveX * max;
            if (Math.Abs(Speed.X) > max && Math.Sign(Speed.X) == moveX)
            {
                Speed.X = Calc.Approach(Speed.X, target, OverspeedDecay * dt);
                return;
            }
            bool accelerating = moveX != 0 && (Speed.X == 0 || Math.Sign(Speed.X) == moveX);
            float a = OnGround ? (accelerating ? GroundAccel : GroundDecel) : (accelerating ? AirAccel : AirDecel);
            Speed.X = Calc.Approach(Speed.X, target, a * dt);
        }

        private void UpdateGravity(in PlayerInput inp, float dt)
        {
            if (OnGround || Clinging) return;

            if (Rising && !inp.Jump && Speed.Y < 0)
            {
                Speed.Y *= JumpCut;
                Rising = false;
            }
            if (Speed.Y >= 0) Rising = false;

            if (Gliding && (!inp.Jump || !Has(Abilities.Kite)))
            {
                Gliding = false;
            }

            if (Gliding && InUpdraft)
            {
                Speed.Y = Calc.Approach(Speed.Y, -UpdraftLift, UpdraftAccel * dt);
                return;
            }

            float g = Speed.Y < 0 ? GravityUp : GravityDown;
            if (Math.Abs(Speed.Y) < ApexWindow && inp.Jump) g *= ApexMult;
            float max = MaxFall;
            if (Gliding) max = GlideFall;
            else if (InUpdraft) max = 70f;
            else if (inp.MoveY > 0) max = FastFall;
            if (Speed.Y > max) Speed.Y = Calc.Approach(Speed.Y, max, 900 * dt);
            else Speed.Y = Calc.Approach(Speed.Y, max, g * dt);
        }

        private void UpdateJump(Room room, in PlayerInput inp)
        {
            if (JumpBuffer <= 0 || Healing) return;
            // Down + jump on a one-way platform drops through it.
            if (OnGround && inp.MoveY > 0 && !room.SolidAt(X, Y + 1, W, H) && room.PlatformTopAt(X, Y + H, W))
            {
                JumpBuffer = 0; Coyote = 0;
                DropT = 0.2f;
                OnGround = false;
                Y += 1;
                Speed.Y = Math.Max(Speed.Y, 40f);
                return;
            }
            if (Coyote > 0 && !Clinging)
            {
                JumpBuffer = 0; Coyote = 0;
                Speed.Y = -JumpSpeed;
                Rising = true;
                room.Emit(Ev.Jump, Feet);
                return;
            }
            if (Has(Abilities.Grips))
            {
                int wall = Clinging ? WallDir : (WallAt(room, 1, 2) ? 1 : (WallAt(room, -1, 2) ? -1 : 0));
                if (wall != 0)
                {
                    Leap(room, -wall);
                    return;
                }
            }
            if (Has(Abilities.Kite) && !Gliding && Speed.Y > -60f)
            {
                JumpBuffer = 0;
                Gliding = true;
                Rising = false;
                if (Speed.Y > GlideFall) Speed.Y = Math.Max(GlideFall, Speed.Y * 0.35f);
                room.Emit(Ev.GlideOpen, Center);
            }
        }

        private void Leap(Room room, int dir)
        {
            JumpBuffer = 0; Coyote = 0;
            Clinging = false;
            Gliding = false;
            Rising = true;
            ForceMoveX = dir; ForceMoveTimer = LeapForce;
            Facing = dir;
            Speed = new Vector2(LeapH * dir, -LeapV);
            room.Emit(Ev.Leap, new Vector2(dir > 0 ? X : X + W, Y + H - 3), new Vector2(dir, 0));
        }

        private void UpdateToll(Room room, in PlayerInput inp)
        {
            if (!inp.TollPressed || TollCooldownT > 0 || !Has(Abilities.Bell) || Healing) return;
            TollCooldownT = TollCooldown;
            bool hop = !OnGround && !Clinging && AirToll;
            if (hop)
            {
                AirToll = false;
                Rising = false;
                Gliding = false;
                if (Speed.Y > -TollHop) Speed.Y = -TollHop;
            }
            room.Toll(Center, hop);
        }

        private void UpdateStrike(Room room, in PlayerInput inp, float dt)
        {
            if (Attack != Strike.None)
            {
                AttackT += dt;
                if (AttackT >= StrikeTime) Attack = Strike.None;
            }
            if (StrikeBufferT <= 0 || AttackCooldownT > 0 || Healing) return;
            StrikeBufferT = 0;
            if (Clinging) Facing = -WallDir;
            if (inp.MoveY < 0) Attack = Strike.Up;
            else if (inp.MoveY > 0 && !OnGround && !Clinging) Attack = Strike.Down;
            else Attack = Strike.Side;
            AttackT = 0;
            AttackCooldownT = StrikeCooldown;
            SwingId++;
            SwingConnected = false;
            room.Emit(Ev.Swing, Center, new Vector2(Attack == Strike.Side ? Facing : 0, Attack == Strike.Up ? -1 : (Attack == Strike.Down ? 1 : 0)));
        }

        /// <summary>The staff connected with something solid enough to push off from.</summary>
        public void Recoil(Room room, bool pogo)
        {
            if (Attack == Strike.Down && pogo)
            {
                Speed.Y = -PogoSpeed;
                Rising = false;
                AirToll = true;
                Gliding = false;
                RemY = 0;
                room.Emit(Ev.Pogo, Feet);
            }
            else if (Attack == Strike.Side)
            {
                Speed.X = -Facing * RecoilSpeed;
                ForceMoveTimer = 0;
            }
            else if (Attack == Strike.Up && Speed.Y < 0)
            {
                Speed.Y *= 0.5f;
            }
        }

        /// <summary>Takes damage from something at <paramref name="fromX"/>. Returns false while invulnerable.</summary>
        public bool Hurt(Room room, int damage, float fromX, bool ignoreInvuln = false)
        {
            if (State != PState.Normal) return false;
            if (Invuln > 0 && !ignoreInvuln) return false;
            if (room.Invincible) damage = 0;
            Hp -= damage;
            Invuln = Invulnerability;
            StunT = HurtStun;
            int dir = Center.X >= fromX ? 1 : -1;
            Speed = new Vector2(dir * HurtKnockX, -HurtKnockY);
            Clinging = Gliding = Healing = Rising = false;
            Attack = Strike.None;
            ForceMoveTimer = 0;
            room.Emit(Ev.Hurt, Center, new Vector2(dir, 0), damage);
            if (Hp <= 0)
            {
                Hp = 0;
                State = PState.Dead;
                room.Emit(Ev.Death, Center, new Vector2(dir, 0));
            }
            return true;
        }

        // ---- Movement -------------------------------------------------------------

        private void MoveH(Room room, float amount)
        {
            RemX += amount;
            int move = (int)MathF.Round(RemX);
            if (move == 0) return;
            RemX -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                if (!room.SolidAt(X + sign, Y, W, H))
                {
                    X += sign; move -= sign;
                    continue;
                }
                // Step up tiny ledges while running on the ground.
                if (OnGround && !room.SolidAt(X + sign, Y - 1, W, H) && room.SolidAt(X + sign, Y + H - 1, W, 1))
                {
                    X += sign; Y -= 1; move -= sign;
                    continue;
                }
                Speed.X = 0;
                RemX = 0;
                return;
            }
        }

        private void MoveV(Room room, float amount)
        {
            RemY += amount;
            int move = (int)MathF.Round(RemY);
            if (move == 0) return;
            RemY -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                bool blocked = sign < 0
                    ? room.SolidAt(X, Y - 1, W, H)
                    : room.SolidAt(X, Y + 1, W, H) || (DropT <= 0 && room.PlatformTopAt(X, Y + H, W));
                if (!blocked)
                {
                    Y += sign; move -= sign;
                    continue;
                }
                if (sign < 0)
                {
                    // Ceiling corner correction: nudge around corners the head barely clips.
                    for (int i = 1; i <= UpCorner; i++)
                    {
                        if (Speed.X <= 0 && !room.SolidAt(X - i, Y - 1, W, H)) { X -= i; goto corrected; }
                        if (Speed.X >= 0 && !room.SolidAt(X + i, Y - 1, W, H)) { X += i; goto corrected; }
                    }
                    if (Speed.Y < 0) { Speed.Y = 0; Rising = false; }
                    RemY = 0;
                    return;
                corrected:
                    Y -= 1; move += 1;
                    continue;
                }
                if (Speed.Y > 0)
                {
                    room.Emit(Ev.Land, Feet, new Vector2(0, Speed.Y));
                    Speed.Y = 0;
                }
                RemY = 0;
                return;
            }
        }
    }
}
