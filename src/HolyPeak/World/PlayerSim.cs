using System;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    public enum PState : byte { Normal, Dash, Dead, Frozen }

    /// <summary>Raw input for one simulation frame. Buffering is handled by the simulation itself.</summary>
    public struct PlayerInput
    {
        public sbyte MoveX, MoveY;
        public bool Jump, JumpPressed, DashPressed;
    }

    /// <summary>
    /// All gameplay-relevant player state lives in this struct so the whole room can be
    /// snapshotted cheaply (used by the level solver). Movement is integer-pixel with
    /// sub-pixel remainders, so collision is exact and never tunnels.
    /// </summary>
    public struct PlayerSim
    {
        public const int W = 6, H = 10;

        // Tuning (pixels, seconds)
        public const float MaxRun = 90f;
        public const float RunAccel = 1000f;
        public const float RunReduce = 400f;
        public const float AirMult = 0.65f;
        public const float Gravity = 900f;
        public const float HalfGravThreshold = 40f;
        public const float MaxFall = 160f;
        public const float FastMaxFall = 240f;
        public const float FastMaxAccel = 300f;
        public const float JumpSpeed = -105f;
        public const float JumpHBoost = 40f;
        public const float VarJumpTime = 0.2f;
        public const float CoyoteTime = 0.1f;
        public const float JumpBufferTime = 0.1f;
        public const float DashBufferTime = 0.08f;
        public const float WallSlideSpeed = 38f;
        public const int WallJumpCheckDist = 3;
        public const float WallJumpForceTime = 0.16f;
        public const float WallJumpHSpeed = MaxRun + JumpHBoost;
        public const float DashSpeed = 240f;
        public const float EndDashSpeed = 160f;
        public const float EndDashUpMult = 0.75f;
        public const float DashTime = 0.15f;
        public const float DashCooldown = 0.2f;
        public const float DashRefillCooldown = 0.1f;
        public const float SuperJumpH = 260f;
        public const float SpringSpeed = -185f;
        public const float SideSpringH = 210f;
        public const float SideSpringV = -140f;
        public const int UpCornerCorrection = 4;
        public const int DashCornerCorrection = 4;

        public int X, Y;
        public float RemX, RemY;
        public Vector2 Speed;
        public int Facing;
        public PState State;
        public int Dashes;
        public bool DashAbility;
        public float Coyote, JumpBuffer, DashBuffer;
        public float VarJumpTimer, VarJumpSpeed;
        public float ForceMoveXTimer;
        public int ForceMoveX;
        public float DashTimer, DashCooldownTimer, DashRefillTimer;
        public Vector2 DashDir;
        public bool AutoJump;
        public float MaxFallCur;
        public bool OnGround, WallSliding;
        public int WallDir;

        public Rectangle Hitbox => new Rectangle(X, Y, W, H);
        public Vector2 Center => new Vector2(X + W / 2f, Y + H / 2f);
        public Vector2 Feet => new Vector2(X + W / 2f, Y + H);
        public bool CanDash => DashAbility && Dashes > 0 && DashCooldownTimer <= 0;

        public void Spawn(Point topLeft, bool dashAbility)
        {
            this = default;
            X = topLeft.X; Y = topLeft.Y;
            Facing = 1;
            State = PState.Normal;
            DashAbility = dashAbility;
            Dashes = dashAbility ? 1 : 0;
            MaxFallCur = MaxFall;
            OnGround = true;
        }

        public void Update(Room room, in PlayerInput inp, float dt)
        {
            if (State == PState.Dead || State == PState.Frozen) return;

            JumpBuffer = inp.JumpPressed ? JumpBufferTime : Math.Max(0, JumpBuffer - dt);
            DashBuffer = inp.DashPressed ? DashBufferTime : Math.Max(0, DashBuffer - dt);
            DashCooldownTimer -= dt;
            DashRefillTimer -= dt;

            OnGround = Speed.Y >= 0 && room.SolidOrPlatformBelow(X, Y);
            if (OnGround)
            {
                Coyote = CoyoteTime;
                if (DashAbility && DashRefillTimer <= 0 && Dashes < 1)
                {
                    Dashes = 1;
                    room.Emit(Ev.DashRefill, Center);
                }
            }
            else Coyote -= dt;

            if (room.InfiniteDash && DashAbility && Dashes < 1 && DashRefillTimer <= 0) Dashes = 1;

            if (State == PState.Normal) NormalUpdate(room, inp, dt);
            else if (State == PState.Dash) DashUpdate(room, inp, dt);

            MoveH(room, Speed.X * dt);
            MoveV(room, Speed.Y * dt);

            float wind = room.WindNow;
            if (wind != 0 && State != PState.Dash)
                MoveH(room, wind * (OnGround ? 0.5f : 1f) * dt, applyWindOnly: true);
        }

        private void NormalUpdate(Room room, in PlayerInput inp, float dt)
        {
            int moveX = inp.MoveX;
            if (ForceMoveXTimer > 0) { ForceMoveXTimer -= dt; moveX = ForceMoveX; }
            if (moveX != 0) Facing = moveX;

            if (DashBuffer > 0 && CanDash)
            {
                StartDash(room, inp);
                return;
            }

            // Horizontal
            float mult = OnGround ? 1f : AirMult;
            if (Math.Abs(Speed.X) > MaxRun && Math.Sign(Speed.X) == moveX)
                Speed.X = Calc.Approach(Speed.X, MaxRun * moveX, RunReduce * mult * dt);
            else
                Speed.X = Calc.Approach(Speed.X, MaxRun * moveX, RunAccel * mult * dt);

            // Fast fall
            if (inp.MoveY == 1 && Speed.Y >= MaxFall)
                MaxFallCur = Calc.Approach(MaxFallCur, FastMaxFall, FastMaxAccel * dt);
            else
                MaxFallCur = Calc.Approach(MaxFallCur, MaxFall, FastMaxAccel * dt);

            float maxFall = MaxFallCur;
            bool wasSliding = WallSliding;
            WallSliding = false;
            if (!OnGround && moveX != 0 && Speed.Y >= 0 && room.SolidAt(X + moveX, Y, W, H))
            {
                WallSliding = true;
                WallDir = moveX;
                maxFall = Math.Min(maxFall, WallSlideSpeed);
                if (!wasSliding) room.Emit(Ev.WallSlideStart, Center, new Vector2(moveX, 0));
            }

            if (!OnGround)
            {
                float g = (Math.Abs(Speed.Y) < HalfGravThreshold && (inp.Jump || AutoJump)) ? 0.5f : 1f;
                Speed.Y = Calc.Approach(Speed.Y, maxFall, Gravity * g * dt);
            }

            // Variable jump height
            if (VarJumpTimer > 0)
            {
                if (AutoJump || inp.Jump) Speed.Y = Math.Min(Speed.Y, VarJumpSpeed);
                else VarJumpTimer = 0;
                VarJumpTimer -= dt;
            }
            if (VarJumpTimer <= 0) AutoJump = false;

            if (JumpBuffer > 0)
            {
                if (Coyote > 0) Jump(room, moveX);
                else if (WallJumpCheck(room, 1)) WallJump(room, -1);
                else if (WallJumpCheck(room, -1)) WallJump(room, 1);
            }
        }

        private void DashUpdate(Room room, in PlayerInput inp, float dt)
        {
            // Jumping out of a grounded horizontal / down-diagonal dash keeps the momentum.
            if (JumpBuffer > 0 && DashDir.X != 0 && DashDir.Y >= 0 && Coyote > 0)
            {
                SuperJump(room);
                return;
            }

            DashTimer -= dt;
            if (DashTimer <= 0)
            {
                State = PState.Normal;
                if (DashDir.Y <= 0) Speed = DashDir * EndDashSpeed;
                if (Speed.Y < 0) Speed.Y *= EndDashUpMult;
                MaxFallCur = MaxFall;
            }
        }

        private void Jump(Room room, int moveX)
        {
            JumpBuffer = 0; Coyote = 0;
            VarJumpTimer = VarJumpTime;
            AutoJump = false;
            Speed.X += JumpHBoost * moveX;
            Speed.Y = JumpSpeed;
            VarJumpSpeed = Speed.Y;
            room.Emit(Ev.Jump, Feet);
        }

        private void SuperJump(Room room)
        {
            JumpBuffer = 0; Coyote = 0;
            VarJumpTimer = VarJumpTime;
            AutoJump = false;
            int dir = Math.Sign(DashDir.X);
            Facing = dir;
            Speed.X = SuperJumpH * dir;
            Speed.Y = JumpSpeed;
            if (DashDir.Y > 0) { Speed.X *= 1.25f; Speed.Y *= 0.5f; }
            VarJumpSpeed = Speed.Y;
            State = PState.Normal;
            room.Emit(Ev.SuperJump, Feet, new Vector2(dir, 0));
        }

        private bool WallJumpCheck(Room room, int dir) => room.SolidAt(X + dir * WallJumpCheckDist, Y, W, H);

        private void WallJump(Room room, int dir)
        {
            JumpBuffer = 0; Coyote = 0;
            VarJumpTimer = VarJumpTime;
            AutoJump = false;
            // Always push away from the wall: no single-wall climbing, walls must come in pairs.
            ForceMoveX = dir; ForceMoveXTimer = WallJumpForceTime;
            Facing = dir;
            Speed.X = WallJumpHSpeed * dir;
            Speed.Y = JumpSpeed;
            VarJumpSpeed = Speed.Y;
            WallSliding = false;
            room.Emit(Ev.WallJump, new Vector2(dir > 0 ? X : X + W, Y + H - 2), new Vector2(dir, 0));
        }

        private void StartDash(Room room, in PlayerInput inp)
        {
            DashBuffer = 0;
            Dashes--;
            DashCooldownTimer = DashCooldown;
            DashRefillTimer = DashRefillCooldown;
            Vector2 dir = new Vector2(inp.MoveX, inp.MoveY);
            if (dir == Vector2.Zero) dir = new Vector2(Facing, 0);
            dir.Normalize();
            DashDir = dir;
            Vector2 before = Speed;
            Vector2 sp = dir * DashSpeed;
            if (Math.Sign(before.X) == Math.Sign(sp.X) && Math.Abs(before.X) > Math.Abs(sp.X)) sp.X = before.X;
            Speed = sp;
            if (dir.X != 0) Facing = Math.Sign(dir.X);
            DashTimer = DashTime;
            State = PState.Dash;
            VarJumpTimer = 0; AutoJump = false;
            ForceMoveXTimer = 0;
            WallSliding = false;
            room.Emit(Ev.Dash, Center, dir);
        }

        public void Bounce(Room room, int dir)
        {
            State = PState.Normal;
            DashTimer = 0;
            if (DashAbility) Dashes = 1;
            DashRefillTimer = 0;
            AutoJump = true;
            VarJumpTimer = VarJumpTime;
            WallSliding = false;
            MaxFallCur = MaxFall;
            if (dir == 0)
            {
                Speed.X *= 0.5f;
                Speed.Y = SpringSpeed;
                VarJumpSpeed = Speed.Y;
            }
            else
            {
                Speed.X = SideSpringH * dir;
                Speed.Y = SideSpringV;
                VarJumpSpeed = Speed.Y;
                ForceMoveX = dir; ForceMoveXTimer = 0.25f;
                Facing = dir;
            }
            JumpBuffer = 0; Coyote = 0;
        }

        // ---- Movement -------------------------------------------------------------

        private void MoveH(Room room, float amount, bool applyWindOnly = false)
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
                // Dash corner correction: slide up over small ledges.
                if (!applyWindOnly && State == PState.Dash && DashDir.Y <= 0)
                {
                    bool corrected = false;
                    for (int i = 1; i <= DashCornerCorrection; i++)
                    {
                        if (!room.SolidAt(X + sign, Y - i, W, H))
                        {
                            Y -= i; X += sign; move -= sign; corrected = true;
                            break;
                        }
                    }
                    if (corrected) continue;
                }
                if (!applyWindOnly) Speed.X = 0;
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
                    : room.SolidAt(X, Y + 1, W, H) || room.PlatformTopAt(X, Y + H, W);
                if (!blocked)
                {
                    Y += sign; move -= sign;
                    continue;
                }
                if (sign < 0)
                {
                    // Ceiling corner correction: nudge around corners we barely clip.
                    if (Speed.X <= 0)
                        for (int i = 1; i <= UpCornerCorrection; i++)
                            if (!room.SolidAt(X - i, Y - 1, W, H)) { X -= i; goto corrected; }
                    if (Speed.X >= 0)
                        for (int i = 1; i <= UpCornerCorrection; i++)
                            if (!room.SolidAt(X + i, Y - 1, W, H)) { X += i; goto corrected; }
                    if (Speed.Y < 0) { Speed.Y = 0; VarJumpTimer = 0; AutoJump = false; }
                    RemY = 0;
                    return;
                corrected:
                    Y -= 1; move += 1;
                    continue;
                }
                else
                {
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

        public void Kill(Room room, Vector2 dir)
        {
            if (State == PState.Dead) return;
            State = PState.Dead;
            room.Emit(Ev.Death, Center, dir);
        }
    }
}
