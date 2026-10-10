using System;
using Microsoft.Xna.Framework;

namespace HolyPeak.World
{
    /// <summary>
    /// Moving the pilgrim from one room to the next. Shared by the game and the verifier so a
    /// verified route replays frame for frame in the game.
    /// </summary>
    public static class Travel
    {
        /// <summary>The boost given when climbing up into the room above, so ledges beside the opening can be reached.</summary>
        public const float UpBoost = 265f;

        /// <summary>The room the pilgrim just walked into, where they arrive in it, and with what speed.</summary>
        public static RoomDef Next(Room room, out Vector2 local, out Vector2 speed)
        {
            var p = room.P;
            var def = room.Def;
            var worldCenter = new Vector2(def.WorldX + p.X + PlayerSim.W / 2f, def.WorldY + p.Y + PlayerSim.H / 2f);
            var next = Atlas.AtWorld((int)MathF.Floor(worldCenter.X), (int)MathF.Floor(worldCenter.Y));
            speed = p.Speed;
            local = Vector2.Zero;
            if (next == null) return null;
            int dir = room.ExitDir;
            if (dir == 1) speed.Y = Math.Min(speed.Y, -UpBoost);
            local = new Vector2(def.WorldX + p.X - next.WorldX, def.WorldY + p.Y - next.WorldY);
            if (dir == 2) local.X = Math.Min(local.X, next.Cols * 8 - PlayerSim.W - 1);
            if (dir == 3) local.X = Math.Max(local.X, 1);
            if (dir == 1) local.Y = Math.Min(local.Y, next.Rows * 8 - PlayerSim.H - 1);
            if (dir == 4) local.Y = Math.Max(local.Y, 1);
            return next;
        }

        /// <summary>Puts the pilgrim (with everything they carry) into a freshly built room.</summary>
        public static void Arrive(Room room, in PlayerSim carry, Vector2? local, Vector2 speed, Point? spawn)
        {
            var p = carry;
            p.Spawn(Point.Zero, carry.Facing);
            p.Hp = carry.Hp; p.MaxHp = carry.MaxHp; p.Flasks = carry.Flasks; p.MaxFlasks = carry.MaxFlasks;
            p.Damage = carry.Damage; p.Ab = carry.Ab; p.Invuln = carry.Invuln; p.State = PState.Normal;
            if (local.HasValue)
            {
                p.X = (int)MathF.Round(local.Value.X); p.Y = (int)MathF.Round(local.Value.Y);
                p.Speed = speed;
                p.OnGround = false;
                p.AirToll = carry.AirToll;
            }
            else if (spawn.HasValue)
            {
                p.X = spawn.Value.X; p.Y = spawn.Value.Y;
            }
            p.SafeX = p.X; p.SafeY = p.Y;
            room.P = p;
            Unstick(room);
            room.P.SafeX = room.P.X; room.P.SafeY = room.P.Y;
        }

        /// <summary>If the pilgrim arrives inside something solid, move to the nearest free spot.</summary>
        public static void Unstick(Room room)
        {
            ref var p = ref room.P;
            if (!room.SolidAt(p.X, p.Y, PlayerSim.W, PlayerSim.H)) return;
            for (int r = 1; r <= 24; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        if (!room.SolidAt(p.X + dx, p.Y + dy, PlayerSim.W, PlayerSim.H)) { p.X += dx; p.Y += dy; return; }
                    }
        }
    }
}
