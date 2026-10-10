using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HolyPeak.World
{
    public enum Ev : byte
    {
        Jump, WallJump, SuperJump, Land, Dash, DashRefill, WallSlideStart, Death,
        Spring, Crystal, CrystalRespawn, EmberTouch, CrumbleShake, CrumbleBreak, CrumbleRestore,
        PulseSwitch, Exit, Gift, Bell,
    }

    public struct GameEvent
    {
        public Ev Type;
        public Vector2 Pos;
        public Vector2 Dir;
        public int Index;
    }

    public struct CrumbleBlock
    {
        public int X, Y, W;        // pixel rect, height is one tile
        public byte State;         // 0 solid, 1 shaking (still solid), 2 gone
        public float Timer;
        public Rectangle Rect => new Rectangle(X, Y, W, 8);
    }

    public struct SpringEnt
    {
        public int X, Y;           // tile origin in pixels
        public int Dir;            // 0 = up, 1 = launches right, -1 = launches left
        public float Anim;         // visual compression (also cheap to snapshot)
        public Rectangle Trigger => Dir switch
        {
            0 => new Rectangle(X + 1, Y + 3, 6, 5),
            1 => new Rectangle(X, Y + 1, 5, 6),
            _ => new Rectangle(X + 3, Y + 1, 5, 6),
        };
    }

    public struct CrystalEnt
    {
        public float X, Y;         // center
        public bool Active;
        public float Timer;
        public Rectangle Trigger => new Rectangle((int)X - 5, (int)Y - 5, 10, 10);
    }

    public struct PulseBlock
    {
        public int X, Y, W, H;
        public int Group;          // 1 or 2
        public bool Solid;
        public Rectangle Rect => new Rectangle(X, Y, W, H);
    }

    public struct EmberEnt
    {
        public float X, Y;         // home center
        public byte State;         // 0 waiting, 1 carried by player
        public Rectangle Trigger => new Rectangle((int)X - 5, (int)Y - 5, 10, 10);
    }

    /// <summary>
    /// A live, simulated room. Holds the tile grid, all interactive entities and the player.
    /// Everything that changes during play is plain data (structs / arrays) so it can be snapshotted.
    /// </summary>
    public sealed class Room
    {
        public const int TileSize = 8;
        public const float CrumbleShakeTime = 0.45f;
        public const float CrumbleRespawnTime = 2.0f;
        public const float CrystalRespawnTime = 2.5f;

        public readonly RoomDef Def;
        public readonly int Cols, Rows, Width, Height;
        public readonly Tile[] Tiles;
        public readonly Point SpawnPoint;
        public readonly Point? HintPos, Hint2Pos;
        public readonly bool HasShrine, HasBell;
        public readonly Rectangle ShrineRect, BellRect;

        public CrumbleBlock[] Crumbles;
        public SpringEnt[] Springs;
        public CrystalEnt[] Crystals;
        public PulseBlock[] Pulses;
        public bool HasEmber;
        public EmberEnt Ember;

        public PlayerSim P;
        public float Time;
        public int PulseActive = 1;
        public bool ShrineUsed, BellRung;
        public bool Exited;
        public int ExitDir;

        // Rules that come from the save / assist settings.
        public bool DashAbility;
        public bool InfiniteDash;
        public bool Invincible;

        public readonly List<GameEvent> Events = new List<GameEvent>(32);
        public bool RecordEvents = true;

        public Room(RoomDef def, bool dashAbility)
        {
            Def = def;
            Cols = def.Cols; Rows = def.Rows;
            Width = Cols * TileSize; Height = Rows * TileSize;
            Tiles = new Tile[Cols * Rows];
            DashAbility = dashAbility;

            var crumbles = new List<CrumbleBlock>();
            var springs = new List<SpringEnt>();
            var crystals = new List<CrystalEnt>();
            var pulses = new List<PulseBlock>();

            for (int ty = 0; ty < Rows; ty++)
            {
                string row = def.Map[ty];
                for (int tx = 0; tx < Cols; tx++)
                {
                    char c = row[tx];
                    int px = tx * TileSize, py = ty * TileSize;
                    Tile t = Tile.Empty;
                    switch (c)
                    {
                        case '#': t = Tile.Solid; break;
                        case '=': t = Tile.Solid2; break;
                        case ':': t = Tile.Back; break;
                        case '-': t = Tile.JumpThru; break;
                        case '^': t = Tile.SpikeUp; break;
                        case 'v': t = Tile.SpikeDown; break;
                        case '<': t = Tile.SpikeLeft; break;
                        case '>': t = Tile.SpikeRight; break;
                        case 'P': SpawnPoint = new Point(px + (TileSize - PlayerSim.W) / 2, py + TileSize - PlayerSim.H); break;
                        case 'C':
                            if (tx > 0 && row[tx - 1] == 'C' && crumbles.Count > 0)
                            {
                                var last = crumbles[^1];
                                last.W += TileSize;
                                crumbles[^1] = last;
                            }
                            else crumbles.Add(new CrumbleBlock { X = px, Y = py, W = TileSize });
                            break;
                        case 'S': springs.Add(new SpringEnt { X = px, Y = py, Dir = 0 }); break;
                        case '{': springs.Add(new SpringEnt { X = px, Y = py, Dir = 1 }); break;
                        case '}': springs.Add(new SpringEnt { X = px, Y = py, Dir = -1 }); break;
                        case 'D': crystals.Add(new CrystalEnt { X = px + 4, Y = py + 4, Active = true }); break;
                        case 'E': HasEmber = true; Ember = new EmberEnt { X = px + 4, Y = py + 4 }; break;
                        case '1':
                        case '2':
                            pulses.Add(new PulseBlock { X = px, Y = py, W = TileSize, H = TileSize, Group = c - '0' });
                            break;
                        case 'G': HasShrine = true; ShrineRect = new Rectangle(px - 4, py - 8, 16, 16); break;
                        case 'B': HasBell = true; BellRect = new Rectangle(px - 8, py - 16, 24, 24); break;
                        case 'h': HintPos = new Point(px + 4, py + 4); break;
                        case 'k': Hint2Pos = new Point(px + 4, py + 4); break;
                    }
                    Tiles[ty * Cols + tx] = t;
                }
            }

            Crumbles = crumbles.ToArray();
            Springs = springs.ToArray();
            Crystals = crystals.ToArray();
            Pulses = MergePulses(pulses);
            Respawn();
        }

        private static PulseBlock[] MergePulses(List<PulseBlock> cells)
        {
            // Merge horizontal runs of the same group into wider blocks for nicer rendering.
            cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var merged = new List<PulseBlock>();
            foreach (var c in cells)
            {
                if (merged.Count > 0)
                {
                    var last = merged[^1];
                    if (last.Y == c.Y && last.Group == c.Group && last.X + last.W == c.X)
                    {
                        last.W += c.W;
                        merged[^1] = last;
                        continue;
                    }
                }
                merged.Add(c);
            }
            return merged.ToArray();
        }

        /// <summary>Restore the room to its initial state and place the player at the spawn.</summary>
        public void Respawn()
        {
            Time = 0;
            Exited = false;
            ExitDir = 0;
            for (int i = 0; i < Crumbles.Length; i++) { Crumbles[i].State = 0; Crumbles[i].Timer = 0; }
            for (int i = 0; i < Crystals.Length; i++) { Crystals[i].Active = true; Crystals[i].Timer = 0; }
            for (int i = 0; i < Springs.Length; i++) Springs[i].Anim = 0;
            if (HasEmber) Ember.State = 0;
            PulseActive = 1;
            for (int i = 0; i < Pulses.Length; i++) Pulses[i].Solid = Pulses[i].Group == 1;
            P.Spawn(SpawnPoint, DashAbility);
        }

        public void Emit(Ev type, Vector2 pos, Vector2 dir = default, int index = 0)
        {
            if (RecordEvents) Events.Add(new GameEvent { Type = type, Pos = pos, Dir = dir, Index = index });
        }

        public Tile TileAt(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) return Tile.Empty;
            return Tiles[ty * Cols + tx];
        }

        public static bool IsSolidTile(Tile t) => t == Tile.Solid || t == Tile.Solid2;

        public float WindNow
        {
            get
            {
                if (Def.Wind == 0) return 0;
                if (Def.WindPeriod <= 0) return Def.Wind;
                // Gusts: calm -> ramp -> full -> ramp -> calm.
                float ph = (Time % Def.WindPeriod) / Def.WindPeriod;
                float s = MathF.Sin(ph * MathF.PI * 2f - MathF.PI / 2f) * 0.5f + 0.5f;
                return Def.Wind * MathF.Min(1f, s * 1.4f);
            }
        }

        // ---- Collision queries -------------------------------------------------------

        public bool SolidAt(int x, int y, int w, int h)
        {
            var exits = Def.Exits;
            if (x < 0 && (exits & ExitSide.Left) == 0) return true;
            if (x + w > Width && (exits & ExitSide.Right) == 0) return true;
            if (y < 0 && (exits & ExitSide.Top) == 0) return true;
            int tx0 = FloorDiv(x, TileSize), tx1 = FloorDiv(x + w - 1, TileSize);
            int ty0 = FloorDiv(y, TileSize), ty1 = FloorDiv(y + h - 1, TileSize);
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                    if (IsSolidTile(TileAt(tx, ty))) return true;

            var r = new Rectangle(x, y, w, h);
            for (int i = 0; i < Crumbles.Length; i++)
                if (Crumbles[i].State != 2 && Crumbles[i].Rect.Intersects(r)) return true;
            for (int i = 0; i < Pulses.Length; i++)
                if (Pulses[i].Solid && Pulses[i].Rect.Intersects(r)) return true;
            return false;
        }

        /// <summary>True if a one-way platform's top surface sits exactly on pixel row <paramref name="row"/>.</summary>
        public bool PlatformTopAt(int x, int row, int w)
        {
            if (row < 0 || (row & 7) != 0) return false;
            int ty = row / TileSize;
            int tx0 = FloorDiv(x, TileSize), tx1 = FloorDiv(x + w - 1, TileSize);
            for (int tx = tx0; tx <= tx1; tx++)
                if (TileAt(tx, ty) == Tile.JumpThru) return true;
            return false;
        }

        public bool SolidOrPlatformBelow(int x, int y)
            => SolidAt(x, y + 1, PlayerSim.W, PlayerSim.H) || PlatformTopAt(x, y + PlayerSim.H, PlayerSim.W);

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        // ---- Simulation ---------------------------------------------------------------

        public void Update(in PlayerInput input, float dt)
        {
            Time += dt;
            UpdatePulses();
            UpdateCrumbles(dt);
            UpdateCrystals(dt);
            for (int i = 0; i < Springs.Length; i++) Springs[i].Anim = Math.Max(0, Springs[i].Anim - dt * 4f);

            if (P.State == PState.Dead || Exited) return;
            P.Update(this, input, dt);
            if (P.State == PState.Dead) return;
            Interact();
        }

        private void UpdatePulses()
        {
            if (Pulses.Length == 0) return;
            int active = ((int)(Time / Def.PulsePhase) & 1) == 0 ? 1 : 2;
            if (active != PulseActive)
            {
                PulseActive = active;
                Emit(Ev.PulseSwitch, Vector2.Zero, index: active);
            }
            var pr = P.Hitbox;
            for (int i = 0; i < Pulses.Length; i++)
            {
                ref var b = ref Pulses[i];
                bool want = b.Group == PulseActive;
                if (!want) b.Solid = false;
                else if (!b.Solid && (P.State == PState.Dead || !b.Rect.Intersects(pr))) b.Solid = true;
            }
        }

        private void UpdateCrumbles(float dt)
        {
            for (int i = 0; i < Crumbles.Length; i++)
            {
                ref var c = ref Crumbles[i];
                if (c.State == 1)
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0)
                    {
                        c.State = 2;
                        c.Timer = CrumbleRespawnTime;
                        Emit(Ev.CrumbleBreak, new Vector2(c.X + c.W / 2f, c.Y + 4), index: i);
                    }
                }
                else if (c.State == 2)
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0 && !c.Rect.Intersects(P.Hitbox))
                    {
                        c.State = 0;
                        Emit(Ev.CrumbleRestore, new Vector2(c.X + c.W / 2f, c.Y + 4), index: i);
                    }
                }
            }
        }

        private void UpdateCrystals(float dt)
        {
            for (int i = 0; i < Crystals.Length; i++)
            {
                ref var c = ref Crystals[i];
                if (c.Active) continue;
                c.Timer -= dt;
                if (c.Timer <= 0)
                {
                    c.Active = true;
                    Emit(Ev.CrystalRespawn, new Vector2(c.X, c.Y), index: i);
                }
            }
        }

        private void Interact()
        {
            var hb = P.Hitbox;

            // Spikes: only lethal when moving into them (or resting on them).
            if (!Invincible)
            {
                int tx0 = FloorDiv(hb.Left, TileSize), tx1 = FloorDiv(hb.Right - 1, TileSize);
                int ty0 = FloorDiv(hb.Top, TileSize), ty1 = FloorDiv(hb.Bottom - 1, TileSize);
                for (int ty = ty0; ty <= ty1; ty++)
                    for (int tx = tx0; tx <= tx1; tx++)
                    {
                        Tile t = TileAt(tx, ty);
                        if (t < Tile.SpikeUp) continue;
                        int px = tx * TileSize, py = ty * TileSize;
                        Rectangle kill; bool lethal; Vector2 dir;
                        switch (t)
                        {
                            case Tile.SpikeUp: kill = new Rectangle(px, py + 5, 8, 3); lethal = P.Speed.Y >= 0; dir = new Vector2(0, -1); break;
                            case Tile.SpikeDown: kill = new Rectangle(px, py, 8, 3); lethal = P.Speed.Y <= 0; dir = new Vector2(0, 1); break;
                            case Tile.SpikeLeft: kill = new Rectangle(px + 5, py, 3, 8); lethal = P.Speed.X >= 0; dir = new Vector2(-1, 0); break;
                            default: kill = new Rectangle(px, py, 3, 8); lethal = P.Speed.X <= 0; dir = new Vector2(1, 0); break;
                        }
                        if (lethal && kill.Intersects(hb)) { P.Kill(this, dir); return; }
                    }
            }

            // Pit
            if (P.Y > Height)
            {
                if (Invincible) { BounceFromPit(); }
                else { P.Kill(this, new Vector2(0, -1)); return; }
            }

            // Crumble triggers: standing on top or wall-sliding against one.
            for (int i = 0; i < Crumbles.Length; i++)
            {
                ref var c = ref Crumbles[i];
                if (c.State != 0) continue;
                var r = c.Rect;
                bool standing = P.OnGround && hb.Bottom == r.Top && hb.Right > r.Left && hb.Left < r.Right;
                bool sliding = P.WallSliding && hb.Bottom > r.Top && hb.Top < r.Bottom &&
                               ((P.WallDir > 0 && hb.Right == r.Left) || (P.WallDir < 0 && hb.Left == r.Right));
                if (standing || sliding)
                {
                    c.State = 1;
                    c.Timer = CrumbleShakeTime;
                    Emit(Ev.CrumbleShake, new Vector2(r.Center.X, r.Center.Y), index: i);
                }
            }

            // Springs
            for (int i = 0; i < Springs.Length; i++)
            {
                ref var s = ref Springs[i];
                if (!s.Trigger.Intersects(hb)) continue;
                if (s.Dir == 0 && P.Speed.Y < 0 && P.State != PState.Dash) continue;
                if (s.Dir != 0 && P.Speed.X * s.Dir >= PlayerSim.SideSpringH * 0.9f) continue;
                if (s.Dir == 0) P.Y = Math.Min(P.Y, s.Y + 4 - PlayerSim.H);
                P.Bounce(this, s.Dir);
                s.Anim = 1f;
                Emit(Ev.Spring, new Vector2(s.X + 4, s.Y + 4), new Vector2(s.Dir, s.Dir == 0 ? -1 : 0), i);
            }

            // Dash crystals
            if (DashAbility)
            {
                for (int i = 0; i < Crystals.Length; i++)
                {
                    ref var c = ref Crystals[i];
                    if (!c.Active || P.Dashes >= 1 || !c.Trigger.Intersects(hb)) continue;
                    P.Dashes = 1;
                    c.Active = false;
                    c.Timer = CrystalRespawnTime;
                    Emit(Ev.Crystal, new Vector2(c.X, c.Y), index: i);
                }
            }

            if (HasEmber && Ember.State == 0 && Ember.Trigger.Intersects(hb))
            {
                Ember.State = 1;
                Emit(Ev.EmberTouch, new Vector2(Ember.X, Ember.Y));
            }

            if (HasShrine && !ShrineUsed && ShrineRect.Intersects(hb))
            {
                ShrineUsed = true;
                DashAbility = true;
                P.DashAbility = true;
                P.Dashes = 1;
                Emit(Ev.Gift, new Vector2(ShrineRect.Center.X, ShrineRect.Center.Y));
            }

            if (HasBell && !BellRung && BellRect.Intersects(hb))
            {
                BellRung = true;
                Emit(Ev.Bell, new Vector2(BellRect.Center.X, BellRect.Center.Y));
            }

            // Exits: any edge but the bottom.
            var c0 = P.Center;
            int exit = 0;
            if (c0.Y < 0) exit = 1;
            else if (c0.X < 0) exit = 2;
            else if (c0.X > Width) exit = 3;
            if (exit != 0)
            {
                Exited = true;
                ExitDir = exit;
                Emit(Ev.Exit, c0, index: exit);
            }
        }

        private void BounceFromPit()
        {
            // Assist invincibility: launch the player back up instead of dying.
            P.Y = Height - PlayerSim.H;
            P.Bounce(this, 0);
            P.Speed.Y = -260f;
            Emit(Ev.Spring, P.Feet, new Vector2(0, -1), -1);
        }

        // ---- Snapshots (used by the solver) ----------------------------------------------

        public sealed class Snapshot
        {
            public PlayerSim P;
            public float Time;
            public int PulseActive;
            public bool ShrineUsed, BellRung, Exited, DashAbility;
            public int ExitDir;
            public EmberEnt Ember;
            public CrumbleBlock[] Crumbles;
            public CrystalEnt[] Crystals;
            public PulseBlock[] Pulses;
            public SpringEnt[] Springs;
        }

        public Snapshot Save()
        {
            return new Snapshot
            {
                P = P, Time = Time, PulseActive = PulseActive, ShrineUsed = ShrineUsed, BellRung = BellRung,
                Exited = Exited, ExitDir = ExitDir, Ember = Ember, DashAbility = DashAbility,
                Crumbles = (CrumbleBlock[])Crumbles.Clone(),
                Crystals = (CrystalEnt[])Crystals.Clone(),
                Pulses = (PulseBlock[])Pulses.Clone(),
                Springs = (SpringEnt[])Springs.Clone(),
            };
        }

        public void Load(Snapshot s)
        {
            P = s.P; Time = s.Time; PulseActive = s.PulseActive; ShrineUsed = s.ShrineUsed; BellRung = s.BellRung;
            Exited = s.Exited; ExitDir = s.ExitDir; Ember = s.Ember; DashAbility = s.DashAbility;
            Array.Copy(s.Crumbles, Crumbles, Crumbles.Length);
            Array.Copy(s.Crystals, Crystals, Crystals.Length);
            Array.Copy(s.Pulses, Pulses, Pulses.Length);
            Array.Copy(s.Springs, Springs, Springs.Length);
        }
    }
}
