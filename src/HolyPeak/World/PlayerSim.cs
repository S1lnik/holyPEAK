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
        public bool Jump, JumpPressed, StrikePressed, TollPressed, Heal, InteractPressed, CastPressed;
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
        public const float MaxRun = 84f, StrideRun = 97f;
        public const float GroundAccel = 1300f, GroundDecel = 1700f;
        public const float AirAccel = 950f, AirDecel = 650f;
        public const float OverspeedDecay = 420f, MomentumDecay = 110f;
        // Jumping and falling
        public const float JumpSpeed = 238f;
        public const float JumpCut = 0.42f;
        public const float GravityUp = 860f, GravityDown = 1060f;
        public const float ApexMult = 0.55f, ApexWindow = 40f;
        public const float MaxFall = 215f, FastFall = 260f, FeatherFall = 120f;
        public const float CoyoteTime = 0.08f, JumpBufferTime = 0.11f;
        public const int UpCorner = 3;
        // Ledge vault: pull up onto a ledge the hands can reach
        public const int MantleReach = 9;
        // Staff vault: plant the staff while running and spring forward
        public const float VaultMinSpeed = 62f, VaultH = 178f, VaultV = 212f, VaultMomentum = 0.6f;
        // Ropes and vines
        public const float RopeGravity = 900f, RopePush = 420f, RopeDamping = 0.22f, RopeClimb = 55f, RopeJump = 140f, RopeMaxAngle = 1.45f;
        public const float RopeRegrab = 0.3f;
        // Strike lanterns
        public const float LanternBounce = 292f, LanternKickX = 215f, LanternKickY = 150f, LanternRelight = 0.9f;
        // Iron Grips
        public const float ClingHold = 0.32f, ClaspHold = 1.1f, ClingSlideMax = 48f, ClingSlideAccel = 170f;
        public const float LeapH = 132f, LeapV = 222f, LeapForce = 0.14f;
        // Bell of Resonance
        public const float TollCooldown = 0.5f, TollHop = 158f, TollRadius = 46f;
        // Pilgrim's Kite
        public const float GlideFall = 34f, GlideMaxRun = 100f, UpdraftLift = 125f, UpdraftAccel = 950f;
        // Staff
        public const float StrikeTime = 0.24f, StrikeActiveFrom = 0.02f, StrikeActiveTo = 0.13f, StrikeCooldown = 0.33f, QuickCooldown = 0.22f, StrikeBuffer = 0.12f;
        public const float PogoSpeed = 228f, RecoilSpeed = 92f;
        // Grace, spells and healing
        public const int GracePerHit = 11, VotiveGrace = 6, SpellCost = 33, HealCost = 33, BaseGrace = 99;
        public const float CastLock = 0.22f, CastCooldown = 0.34f, BoltSpeed = 320f, DiveSpeed = 430f, DiveLag = 0.16f;
        public const float HealTime = 0.75f, PrayerHealTime = 0.45f;
        // Hurt
        public const float Invulnerability = 1.15f, HurtStun = 0.22f, HurtKnockX = 125f, HurtKnockY = 150f;

        public int X, Y;
        public float RemX, RemY;
        public Vector2 Speed;
        public int Facing;
        public PState State;
        public Abilities Ab;
        public Charm Ch;
        public Spell Sp;

        public int Hp, MaxHp, Grace, MaxGrace, Damage;

        public float Coyote, JumpBuffer, StrikeBufferT;
        public bool Rising;                 // rising from a jump whose height can still be cut short
        public bool OnGround, Clinging, Gliding, InUpdraft;
        public int WallDir;
        public float ClingTimer;
        public float ForceMoveTimer;
        public int ForceMoveX;
        public int AirTolls;                // airborne toll-lifts left before landing
        public float TollCooldownT;
        public Strike Attack;
        public float AttackT, AttackCooldownT;
        public int SwingId;
        public bool SwingConnected;
        public float Invuln, StunT;
        public bool Healing;
        public float HealT;
        public int SafeX, SafeY;
        public bool SafeLocked;             // in a trial, the first safe ground is the only one
        public float AirTime;
        public float DropT;                 // falling through a one-way platform
        public float MomentumT;             // launched: speed above a run fades slowly
        public float VaultT, MantleT;       // staff vault / ledge vault (pose and re-trigger guard)
        public int Rope;                    // index of the rope held, or -1
        public int LastRope;
        public float RopeAngle, RopeVel, RopeLen, RopeCd;
        public Spell Casting;
        public float CastT, CastCooldownT, LandLagT;
        public bool Diving;
        public int SpellId;

        public Rectangle Hitbox => new Rectangle(X, Y, W, H);
        public Vector2 Center => new Vector2(X + W / 2f, Y + H / 2f);
        public Vector2 Feet => new Vector2(X + W / 2f, Y + H);
        public Vector2 Hands => new Vector2(X + W / 2f, Y + 3);
        public bool Has(Abilities a) => (Ab & a) == a;
        public bool Wears(Charm c) => (Ch & c) == c;
        public bool Knows(Spell s) => (Sp & s) == s;
        public bool OnRope => Rope >= 0;
        public bool StrikeActive => Attack != Strike.None && AttackT >= StrikeActiveFrom && AttackT <= StrikeActiveTo;
        public int MaxAirTolls => Wears(Charm.Echo) ? 2 : 1;
        public int SpellDamage => Wears(Charm.Psalter) ? Math.Max(1, (Damage * 3 + 3) / 4) : Math.Max(1, Damage / 2);
        public float HealDuration => Wears(Charm.Prayer) ? PrayerHealTime : HealTime;

        /// <summary>World rectangle covered by the staff while a strike is active.</summary>
        public Rectangle StrikeBox
        {
            get
            {
                int e = Wears(Charm.LongStaff) ? 7 : 0;
                switch (Attack)
                {
                    case Strike.Up: return new Rectangle(X + W / 2 - 9, Y - 20 - e, 18, 22 + e);
                    case Strike.Down: return new Rectangle(X + W / 2 - 8, Y + H - 2, 16, 20 + e);
                    default: return Facing > 0 ? new Rectangle(X + W - 1, Y - 3, 22 + e, 16) : new Rectangle(X - 21 - e, Y - 3, 22 + e, 16);
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
            Rising = Clinging = Gliding = InUpdraft = Healing = Diving = false;
            ClingTimer = ForceMoveTimer = TollCooldownT = AttackT = AttackCooldownT = Invuln = StunT = HealT = AirTime = DropT = 0;
            MomentumT = VaultT = MantleT = RopeCd = CastT = CastCooldownT = LandLagT = 0;
            Rope = -1; LastRope = -1;
            SafeLocked = false;
            Attack = Strike.None;
            Casting = Spell.None;
            AirTolls = MaxAirTolls;
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
            CastCooldownT -= dt;
            if (CastT > 0) CastT -= dt;
            if (Invuln > 0) Invuln -= dt;
            if (MomentumT > 0) MomentumT -= dt;
            if (VaultT > 0) VaultT -= dt;
            if (MantleT > 0) MantleT -= dt;
            if (RopeCd > 0) RopeCd -= dt;

            if (Rope >= 0)
            {
                UpdateRope(room, inp, dt);
                return;
            }

            if (DropT > 0) DropT -= dt;
            OnGround = Speed.Y >= 0 && (room.SolidAt(X, Y + 1, W, H) || (DropT <= 0 && room.PlatformTopAt(X, Y + H, W)));
            if (OnGround)
            {
                Coyote = CoyoteTime;
                AirTolls = MaxAirTolls;
                AirTime = 0;
                MomentumT = 0;
                if (Gliding) { Gliding = false; room.Emit(Ev.GlideClose, Center); }
                Clinging = false;
                Rising = false;
                if (!SafeLocked && room.IsSafeGround(X, Y))
                {
                    SafeX = X; SafeY = Y;
                    SafeLocked = room.Trial;
                }
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

            if (Diving)
            {
                UpdateDive(room, dt);
                return;
            }

            if (LandLagT > 0)
            {
                LandLagT -= dt;
                Speed.X = Calc.Approach(Speed.X, 0, GroundDecel * dt);
                MoveH(room, Speed.X * dt);
                MoveV(room, Speed.Y * dt);
                return;
            }

            UpdateHeal(room, inp, dt);
            UpdateCast(room, inp);
            if (Diving) return;
            if (!TryMantle(room, inp)) UpdateCling(room, inp, dt);
            UpdateRun(inp, dt);
            UpdateGravity(inp, dt);
            UpdateJump(room, inp);
            UpdateToll(room, inp);
            UpdateStrike(room, inp, dt);

            MoveH(room, Speed.X * dt);
            MoveV(room, Speed.Y * dt);
            TryGrabRope(room);
        }

        // ---- healing, grace and spells --------------------------------------------------------

        private void UpdateHeal(Room room, in PlayerInput inp, float dt)
        {
            if (Healing)
            {
                if (!inp.Heal || !OnGround || inp.JumpPressed || inp.StrikePressed || inp.CastPressed)
                {
                    Healing = false;
                    room.Emit(Ev.HealCancel, Center);
                    return;
                }
                HealT += dt;
                if (HealT >= HealDuration)
                {
                    Grace -= HealCost;
                    Hp = Math.Min(MaxHp, Hp + 1);
                    room.Emit(Ev.Heal, Center);
                    // keep praying while the button is held and there is grace and a flame left to light
                    HealT = 0;
                    if (Grace < HealCost || Hp >= MaxHp) Healing = false;
                }
            }
            else if (inp.Heal && OnGround && Grace >= HealCost && Hp < MaxHp && Attack == Strike.None && CastT <= 0 && Math.Abs(Speed.X) < 1f && inp.MoveX == 0)
            {
                Healing = true;
                HealT = 0;
                room.Emit(Ev.HealStart, Center);
            }
        }

        public void GainGrace(int amount) => Grace = Math.Min(MaxGrace, Grace + amount);

        private void UpdateCast(Room room, in PlayerInput inp)
        {
            if (!inp.CastPressed || CastCooldownT > 0 || Healing || Sp == Spell.None) return;
            var s = inp.MoveY < 0 ? Spell.Psalm : (inp.MoveY > 0 ? Spell.Star : Spell.Bolt);
            if (!Knows(s)) s = Knows(Spell.Bolt) ? Spell.Bolt : (Knows(Spell.Star) ? Spell.Star : Spell.Psalm);
            if (Grace < SpellCost)
            {
                CastCooldownT = 0.25f;
                room.Emit(Ev.NoGrace, Center);
                return;
            }
            Grace -= SpellCost;
            SpellId++;
            Casting = s;
            CastT = CastLock;
            CastCooldownT = CastCooldown;
            Attack = Strike.None;
            StrikeBufferT = 0;
            if (Clinging) Facing = -WallDir;
            room.Emit(Ev.Cast, Center, new Vector2(Facing, 0), (int)s);
            switch (s)
            {
                case Spell.Bolt:
                    room.CastBolt(this);
                    if (OnGround) Speed.X -= Facing * 50f;
                    else if (!Clinging) Speed.X = -Facing * 70f;
                    break;
                case Spell.Psalm:
                    room.CastPsalm(this);
                    break;
                case Spell.Star:
                    if (OnGround)
                    {
                        room.StarBurst(this);
                        LandLagT = DiveLag;
                    }
                    else
                    {
                        Diving = true;
                        Clinging = Gliding = Rising = false;
                        Speed = new Vector2(0, DiveSpeed);
                    }
                    break;
            }
        }

        private void UpdateDive(Room room, float dt)
        {
            Speed = new Vector2(0, DiveSpeed);
            if (Invuln < 0.08f) Invuln = 0.08f;
            room.DiveBreak(this);
            MoveV(room, Speed.Y * dt);
            room.DiveHits(this);
            if (Speed.Y == 0 || room.SolidAt(X, Y + 1, W, H) || (DropT <= 0 && room.PlatformTopAt(X, Y + H, W)))
            {
                // MoveV zeroes the speed on landing
                if (room.DiveBreak(this)) { Speed.Y = DiveSpeed; return; }
                Diving = false;
                Speed = Vector2.Zero;
                LandLagT = DiveLag;
                OnGround = true;
                room.StarBurst(this);
            }
        }

        // ---- parkour --------------------------------------------------------------------------

        /// <summary>Pulls up onto a ledge that the pilgrim's hands can reach while pressing toward it.</summary>
        private bool TryMantle(Room room, in PlayerInput inp)
        {
            int dir = inp.MoveX;
            if (dir == 0 || OnGround || MantleT > 0 || ForceMoveTimer > 0 || Speed.Y < -90f || Healing) return false;
            if (!room.SolidAt(X + dir, Y, W, H)) return false;
            for (int d = 1; d <= MantleReach; d++)
            {
                if (room.SolidAt(X, Y - d, W, H)) return false;          // a ceiling in the way
                if (room.SolidAt(X + dir, Y - d, W, H)) continue;
                // the ledge top is d pixels above the feet: hop just high enough to clear it
                float v = MathF.Sqrt(2 * GravityUp * (d + 2.5f));
                if (Speed.Y > -v) Speed.Y = -v;
                Speed.X = dir * Math.Max(Math.Abs(Speed.X), 55f);
                ForceMoveX = dir; ForceMoveTimer = 0.1f;
                Facing = dir;
                Clinging = false;
                Rising = false;
                MantleT = 0.25f;
                RemY = 0;
                room.Emit(Ev.Mantle, new Vector2(dir > 0 ? X + W : X, Y + H - d), new Vector2(dir, 0));
                return true;
            }
            return false;
        }

        private void StaffVault(Room room)
        {
            int dir = Math.Sign(Speed.X);
            StrikeBufferT = 0;
            AttackCooldownT = 0.2f;
            Speed = new Vector2(dir * VaultH, -VaultV);
            MomentumT = VaultMomentum;
            VaultT = 0.32f;
            Facing = dir;
            OnGround = false;
            Coyote = 0;
            JumpBuffer = 0;
            Rising = false;
            room.Emit(Ev.Vault, Feet, new Vector2(dir, 0));
        }

        private void TryGrabRope(Room room)
        {
            if (OnGround || Diving || StunT > 0 || room.Ropes.Length == 0) return;
            var hands = Hands;
            for (int i = 0; i < room.Ropes.Length; i++)
            {
                if (i == LastRope && RopeCd > 0) continue;
                var r = room.Ropes[i];
                if (Math.Abs(hands.X - r.Anchor.X) > 5 || hands.Y < r.Anchor.Y + 8 || hands.Y > r.Anchor.Y + r.Length) continue;
                Rope = i;
                RopeLen = Math.Clamp(hands.Y - r.Anchor.Y, 10, r.Length);
                RopeAngle = MathF.Asin(Math.Clamp((hands.X - r.Anchor.X) / RopeLen, -0.9f, 0.9f));
                // carry the pilgrim's momentum into the swing
                RopeVel = (Speed.X * MathF.Cos(RopeAngle) - Speed.Y * MathF.Sin(RopeAngle)) / RopeLen;
                Clinging = Gliding = Rising = false;
                AirTolls = MaxAirTolls;
                Attack = Strike.None;
                room.Emit(Ev.RopeGrab, hands, index: i);
                return;
            }
        }

        private void UpdateRope(Room room, in PlayerInput inp, float dt)
        {
            var r = room.Ropes[Rope];
            OnGround = false;
            Coyote = 0;
            if (inp.JumpPressed)
            {
                LastRope = Rope;
                Rope = -1;
                RopeCd = RopeRegrab;
                JumpBuffer = 0;
                if (inp.MoveY > 0) { Speed *= 0.8f; return; }       // let go
                Speed = new Vector2(Speed.X * 1.1f, Math.Min(Speed.Y * 1.1f - RopeJump, -RopeJump * 0.7f));
                if (Math.Abs(Speed.X) > 1) Facing = Math.Sign(Speed.X);
                MomentumT = 0.5f;
                Rising = false;
                room.Emit(Ev.RopeJump, Hands, new Vector2(Facing, 0));
                return;
            }
            float oldAngle = RopeAngle, oldLen = RopeLen;
            float acc = -RopeGravity / RopeLen * MathF.Sin(RopeAngle) + inp.MoveX * RopePush * MathF.Cos(RopeAngle) / RopeLen;
            RopeVel += acc * dt;
            RopeVel *= 1 - RopeDamping * dt;
            RopeAngle += RopeVel * dt;
            if (Math.Abs(RopeAngle) > RopeMaxAngle) { RopeAngle = Math.Sign(RopeAngle) * RopeMaxAngle; RopeVel = 0; }
            if (inp.MoveY != 0 && inp.MoveX == 0) RopeLen = Math.Clamp(RopeLen + inp.MoveY * RopeClimb * dt, 10, r.Length);

            var hand = r.Anchor + RopeLen * new Vector2(MathF.Sin(RopeAngle), MathF.Cos(RopeAngle));
            int nx = (int)MathF.Round(hand.X - W / 2f), ny = (int)MathF.Round(hand.Y - 3);
            if (room.SolidAt(nx, ny, W, H))
            {
                RopeAngle = oldAngle;
                RopeLen = oldLen;
                RopeVel = -RopeVel * 0.3f;
            }
            else { X = nx; Y = ny; }
            RemX = RemY = 0;
            Speed = RopeLen * RopeVel * new Vector2(MathF.Cos(RopeAngle), -MathF.Sin(RopeAngle));
            if (inp.MoveX != 0) Facing = inp.MoveX;
            else if (Math.Abs(Speed.X) > 20) Facing = Math.Sign(Speed.X);
            UpdateToll(room, inp);
        }

        // ---- moving ---------------------------------------------------------------------------

        private bool WallAt(Room room, int dir, int dist = 1) => room.SolidAt(X + dir * dist, Y + 2, W, H - 5);

        /// <summary>A wall the Iron Grips can hold: bell-glass is too slick to cling to.</summary>
        private bool GripAt(Room room, int dir) => WallAt(room, dir) && !room.GlassAt(X + dir, Y + 2, W, H - 5);

        private void UpdateCling(Room room, in PlayerInput inp, float dt)
        {
            if (!Has(Abilities.Grips) || OnGround) { Clinging = false; return; }
            if (Clinging)
            {
                // Stay latched until the pilgrim lets go, drops, or slides off the end of the wall.
                if (!GripAt(room, WallDir) || inp.MoveX == -WallDir || inp.MoveY > 0)
                {
                    Clinging = false;
                    if (inp.MoveY > 0) Speed.Y = Math.Max(Speed.Y, 40f);
                    return;
                }
                ClingTimer += dt;
                Speed.X = 0;
                Facing = WallDir;
                float hold = Wears(Charm.Clasp) ? ClaspHold : ClingHold;
                Speed.Y = ClingTimer < hold ? 0 : Calc.Approach(Speed.Y, Wears(Charm.Clasp) ? ClingSlideMax * 0.6f : ClingSlideMax, ClingSlideAccel * dt);
                return;
            }
            int dir = inp.MoveX;
            if (dir != 0 && ForceMoveTimer <= 0 && Speed.Y > -60f && !Healing && GripAt(room, dir))
            {
                Clinging = true;
                WallDir = dir;
                Facing = dir;
                ClingTimer = 0;
                Speed = Vector2.Zero;
                RemY = 0;
                Rising = false;
                AirTolls = MaxAirTolls;
                MomentumT = 0;
                if (Gliding) Gliding = false;
                room.Emit(Ev.Cling, new Vector2(dir > 0 ? X + W : X, Y + 4), new Vector2(dir, 0));
            }
        }

        private void UpdateRun(in PlayerInput inp, float dt)
        {
            if (Clinging) return;
            int moveX = Healing ? 0 : inp.MoveX;
            if (ForceMoveTimer > 0) { ForceMoveTimer -= dt; moveX = ForceMoveX; }
            if (moveX != 0 && Attack == Strike.None && CastT <= 0) Facing = moveX;

            float run = Wears(Charm.Stride) ? StrideRun : MaxRun;
            float max = Gliding ? Math.Max(GlideMaxRun, run) : run;
            float target = moveX * max;
            if (Math.Abs(Speed.X) > max && (Math.Sign(Speed.X) == moveX || (MomentumT > 0 && moveX == 0)))
            {
                // launched: hold onto the extra speed for a while in the air
                Speed.X = Calc.Approach(Speed.X, moveX == 0 ? 0 : target, (MomentumT > 0 && !OnGround ? MomentumDecay : OverspeedDecay) * dt);
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
            else if (inp.Jump && Wears(Charm.Feather)) max = FeatherFall;
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
                int wall = Clinging ? WallDir : (GripAt(room, 1) || WallAt(room, 1, 2) && !room.GlassAt(X + 2, Y + 2, W, H - 5) ? 1
                         : (GripAt(room, -1) || WallAt(room, -1, 2) && !room.GlassAt(X - 2, Y + 2, W, H - 5) ? -1 : 0));
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
            MomentumT = 0;
            Speed = new Vector2(LeapH * dir, -LeapV);
            room.Emit(Ev.Leap, new Vector2(dir > 0 ? X : X + W, Y + H - 3), new Vector2(dir, 0));
        }

        private void UpdateToll(Room room, in PlayerInput inp)
        {
            if (!inp.TollPressed || TollCooldownT > 0 || !Has(Abilities.Bell) || Healing) return;
            TollCooldownT = TollCooldown;
            bool hop = !OnGround && !Clinging && Rope < 0 && AirTolls > 0;
            if (hop)
            {
                AirTolls--;
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
            if (StrikeBufferT <= 0 || AttackCooldownT > 0 || Healing || CastT > 0) return;
            if (OnGround && inp.MoveY > 0 && Math.Abs(Speed.X) >= VaultMinSpeed)
            {
                StaffVault(room);
                return;
            }
            StrikeBufferT = 0;
            if (Clinging) Facing = -WallDir;
            if (inp.MoveY < 0) Attack = Strike.Up;
            else if (inp.MoveY > 0 && !OnGround && !Clinging) Attack = Strike.Down;
            else Attack = Strike.Side;
            AttackT = 0;
            AttackCooldownT = Wears(Charm.QuickStaff) ? QuickCooldown : StrikeCooldown;
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
                AirTolls = MaxAirTolls;
                Gliding = false;
                RemY = 0;
                room.Emit(Ev.Pogo, Feet);
            }
            else if (Attack == Strike.Side)
            {
                Speed.X = -Facing * RecoilSpeed;
                ForceMoveTimer = 0;
                MomentumT = 0;
            }
            else if (Attack == Strike.Up && Speed.Y < 0)
            {
                Speed.Y *= 0.5f;
            }
        }

        /// <summary>Struck a lantern: it throws the pilgrim away from it.</summary>
        public void LanternLaunch(Room room)
        {
            SwingConnected = true;
            AirTolls = MaxAirTolls;
            Gliding = false;
            Rising = false;
            RemY = 0;
            switch (Attack)
            {
                case Strike.Down:
                    Speed.Y = -LanternBounce;
                    break;
                case Strike.Side:
                    Speed = new Vector2(-Facing * LanternKickX, -LanternKickY);
                    ForceMoveX = -Facing; ForceMoveTimer = 0.12f;
                    MomentumT = 0.45f;
                    break;
                default:
                    Speed.Y = Math.Max(Speed.Y, 90f);
                    break;
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
            Clinging = Gliding = Healing = Rising = Diving = false;
            Rope = -1;
            Attack = Strike.None;
            ForceMoveTimer = MomentumT = LandLagT = 0;
            room.Emit(Ev.Hurt, Center, new Vector2(dir, 0), damage);
            if (Wears(Charm.Bramble) && damage > 0) room.BrambleBurst(this);
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
                MomentumT = 0;
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
