using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HolyPeak.Core;

namespace HolyPeak.World
{
    public enum Ev : byte
    {
        // pilgrim
        Jump, Leap, Land, Cling, GlideOpen, GlideClose, Swing, Pogo, HealStart, HealCancel, Heal, Hurt, Death, Hazard,
        // world
        Toll, GlassBreak, Resonate, WallHit, WallBreak, Clank, ThornHit, Ember, CacheHit, CacheBreak, Lever, GateOpen,
        Item, Ability, Interact, Exit, ShadeReclaim, ProjectilePop,
        // creatures
        EnemyHit, EnemyDie, EnemyTell, EnemyJump, EnemyLand, EnemyLunge, Shoot,
        // guardians
        BossStart, BossHit, BossDie, BossCue,
    }

    public struct GameEvent
    {
        public Ev Type;
        public Vector2 Pos;
        public Vector2 Dir;
        public int Index;
    }

    public enum ThingKind : byte { Shrine, Merchant, Npc, Tablet, Heart, Flask, Cache, Altar, Lever, GreatBell, Shade }

    public struct Thing
    {
        public ThingKind Kind;
        public int X, Y;            // bottom centre of the marker tile
        public Rectangle Box;
        public int Hp, LastSwing, Index;
        public bool Used;

        public bool NeedsUp => Kind == ThingKind.Shrine || Kind == ThingKind.Merchant || Kind == ThingKind.Npc ||
                               Kind == ThingKind.Tablet || Kind == ThingKind.GreatBell || Kind == ThingKind.Altar;
    }

    /// <summary>A connected clump of breakable stone, bell-glass or resonance stone.</summary>
    public struct TileGroup
    {
        public Tile Kind;
        public int X0, Y0, X1, Y1;  // tile bounds (inclusive)
        public int Hp, LastSwing, Index;
        public bool Gone;           // broken / shattered
        public float Timer;         // resonance: seconds left solid
        public bool Pending;        // resonance: woken while the pilgrim stood inside it
        public Rectangle Rect => new Rectangle(X0 * 8, Y0 * 8, (X1 - X0 + 1) * 8, (Y1 - Y0 + 1) * 8);
    }

    /// <summary>Deterministic xorshift RNG whose whole state is one integer (so rooms snapshot cheaply).</summary>
    public struct SimRng
    {
        public uint S;
        public SimRng(int seed) { S = (uint)seed * 2654435761u + 0x9E3779B9u; if (S == 0) S = 1; }
        public uint Next() { S ^= S << 13; S ^= S >> 17; S ^= S << 5; return S; }
        public float NextFloat() => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * NextFloat();
    }

    /// <summary>
    /// A live, simulated room: tile grid with breakable / tollable parts, the things in it, its creatures,
    /// projectiles, dropped embers, its guardian (if any) and the pilgrim.
    /// </summary>
    public sealed class Room
    {
        public const int TileSize = 8;
        public const float ResonanceTime = 2.6f;
        public const float ResonanceRange = 76f;
        public const int WallHp = 3, CacheHp = 4;

        public readonly RoomDef Def;
        public readonly int Cols, Rows, Width, Height;
        public readonly Tile[] Tiles;
        public readonly bool[] Updraft;
        public readonly short[] GroupOf;
        public TileGroup[] Groups;
        public Thing[] Things;
        public readonly List<(Point pos, string text)> Hints = new List<(Point, string)>();
        public readonly Point? StartPoint;
        public readonly Point? BossSpawn;
        private readonly bool[] _openL, _openR, _openT, _openB;

        public PlayerSim P;
        public Enemy[] Enemies;
        public Projectile[] Projectiles = new Projectile[64];
        public EmberDrop[] Drops = new EmberDrop[96];
        public Boss Boss;
        public SimRng Rand;
        public float Time;
        public bool GatesOpen, ArenaClosed;
        public float GateAnim;      // 0 closed .. 1 open (visual)
        public bool Exited;
        public int ExitDir;         // 1 up, 2 left, 3 right, 4 down
        public bool HazardPending;
        public int NearThing = -1;

        public readonly HashSet<string> Flags;
        public bool Invincible;
        public bool NoEnemies;
        public bool EmberCharm;

        public readonly List<GameEvent> Events = new List<GameEvent>(32);
        public bool RecordEvents = true;

        public Room(RoomDef def, HashSet<string> flags, bool noEnemies = false)
        {
            Def = def;
            Flags = flags;
            NoEnemies = noEnemies;
            Cols = def.Cols; Rows = def.Rows;
            Width = Cols * TileSize; Height = Rows * TileSize;
            Tiles = new Tile[Cols * Rows];
            Updraft = new bool[Cols * Rows];
            GroupOf = new short[Cols * Rows];
            Rand = new SimRng(def.Id.GetHashCode() & 0x7FFF);

            var things = new List<Thing>();
            var enemies = new List<Enemy>();
            int cacheIndex = 0;

            for (int ty = 0; ty < Rows; ty++)
            {
                string row = def.Map[ty];
                for (int tx = 0; tx < Cols; tx++)
                {
                    char c = row[tx];
                    int i = ty * Cols + tx;
                    GroupOf[i] = -1;
                    Tiles[i] = c switch
                    {
                        '#' => Tile.Solid, '=' => Tile.Solid2, ':' => Tile.Back, '-' => Tile.Platform,
                        '^' => Tile.ThornUp, 'v' => Tile.ThornDown, '<' => Tile.ThornLeft, '>' => Tile.ThornRight,
                        'x' => Tile.Breakable, 'b' => Tile.BellGlass, 'r' => Tile.Resonance, '|' => Tile.Gate, '!' => Tile.ArenaDoor,
                        _ => Tile.Empty,
                    };
                    if (c == 'u') Updraft[i] = true;
                    int bx = tx * TileSize + 4, by = ty * TileSize + 8;
                    switch (c)
                    {
                        case 'G': things.Add(MakeThing(ThingKind.Shrine, bx, by, 24, 26)); break;
                        case 'M': things.Add(MakeThing(ThingKind.Merchant, bx, by, 22, 18)); break;
                        case 'N': things.Add(MakeThing(ThingKind.Npc, bx, by, 18, 16)); break;
                        case 'T': things.Add(MakeThing(ThingKind.Tablet, bx, by, 18, 16)); break;
                        case 'H': things.Add(MakeThing(ThingKind.Heart, bx, by - 4, 10, 10, Flags.Contains("heart:" + def.Id))); break;
                        case 'F': things.Add(MakeThing(ThingKind.Flask, bx, by - 4, 10, 10, Flags.Contains("flask:" + def.Id))); break;
                        case 'A': things.Add(MakeThing(ThingKind.Altar, bx, by, 18, 22, Flags.Contains("ab:" + def.Prop("ability")))); break;
                        case 'L': things.Add(MakeThing(ThingKind.Lever, bx, by, 8, 14, Flags.Contains("lever:" + def.Id))); break;
                        case 'K': things.Add(MakeThing(ThingKind.GreatBell, bx, by, 48, 40)); break;
                        case '$':
                        {
                            var t = MakeThing(ThingKind.Cache, bx, by, 14, 14, Flags.Contains($"cache:{def.Id}:{cacheIndex}"));
                            t.Index = cacheIndex++;
                            t.Hp = CacheHp;
                            things.Add(t);
                            break;
                        }
                        case '1': case '2': case '3':
                        {
                            string text = def.Prop("hint" + c);
                            if (text != null) Hints.Add((new Point(bx, ty * TileSize + 4), text));
                            break;
                        }
                        case '@': StartPoint = new Point(bx - PlayerSim.W / 2, by - PlayerSim.H); break;
                        case 'B': BossSpawn = new Point(bx, by); break;
                    }
                    if (!noEnemies)
                    {
                        EnemyType? et = c switch
                        {
                            'c' => EnemyType.Crawler, 'h' => EnemyType.Hopper, 'w' => EnemyType.Wisp,
                            'k' => EnemyType.Sentinel, 's' => EnemyType.Spitter, 'd' => EnemyType.Dropper, _ => null,
                        };
                        if (et.HasValue) enemies.Add(EnemyAI.Create(et.Value, tx, ty, this));
                    }
                }
            }
            Things = things.ToArray();
            Enemies = enemies.ToArray();
            BuildGroups();

            string gateKey = def.Prop("gatekey", def.Id);
            GatesOpen = Flags.Contains("lever:" + gateKey);
            GateAnim = GatesOpen ? 1 : 0;

            _openL = new bool[Rows]; _openR = new bool[Rows]; _openT = new bool[Cols]; _openB = new bool[Cols];
            for (int ty = 0; ty < Rows; ty++)
            {
                _openL[ty] = Atlas.AtWorld(def.WorldX - 4, def.WorldY + ty * TileSize + 4) != null;
                _openR[ty] = Atlas.AtWorld(def.WorldX + Width + 4, def.WorldY + ty * TileSize + 4) != null;
            }
            for (int tx = 0; tx < Cols; tx++)
            {
                _openT[tx] = Atlas.AtWorld(def.WorldX + tx * TileSize + 4, def.WorldY - 4) != null;
                _openB[tx] = Atlas.AtWorld(def.WorldX + tx * TileSize + 4, def.WorldY + Height + 4) != null;
            }

            string bossId = def.Prop("boss");
            if (bossId != null && BossSpawn.HasValue && !Flags.Contains("boss:" + bossId) && !noEnemies)
                Boss = Bosses.Create(bossId, this, BossSpawn.Value);
        }

        private static Thing MakeThing(ThingKind kind, int x, int y, int w, int h, bool used = false)
            => new Thing { Kind = kind, X = x, Y = y, Box = new Rectangle(x - w / 2, y - h, w, h), Used = used, LastSwing = -1 };

        private void BuildGroups()
        {
            var groups = new List<TileGroup>();
            var stack = new Stack<int>();
            for (int i = 0; i < Tiles.Length; i++)
            {
                var kind = Tiles[i];
                if (GroupOf[i] >= 0 || (kind != Tile.Breakable && kind != Tile.BellGlass && kind != Tile.Resonance)) continue;
                int gi = groups.Count;
                var g = new TileGroup { Kind = kind, X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1, Hp = WallHp, LastSwing = -1, Index = gi };
                stack.Push(i);
                GroupOf[i] = (short)gi;
                while (stack.Count > 0)
                {
                    int j = stack.Pop();
                    int x = j % Cols, y = j / Cols;
                    g.X0 = Math.Min(g.X0, x); g.Y0 = Math.Min(g.Y0, y); g.X1 = Math.Max(g.X1, x); g.Y1 = Math.Max(g.Y1, y);
                    void Visit(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows) return;
                        int k = ny * Cols + nx;
                        if (GroupOf[k] >= 0 || Tiles[k] != kind) return;
                        GroupOf[k] = (short)gi;
                        stack.Push(k);
                    }
                    Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
                }
                if (kind == Tile.Breakable) g.Gone = Flags.Contains(GroupFlag(g));
                if (kind == Tile.BellGlass) g.Gone = Flags.Contains(GroupFlag(g));
                groups.Add(g);
            }
            Groups = groups.ToArray();
        }

        private string GroupFlag(in TileGroup g) => (g.Kind == Tile.Breakable ? "wall:" : "glass:") + Def.Id + ":" + g.X0 + "," + g.Y0;

        public void Emit(Ev type, Vector2 pos, Vector2 dir = default, int index = 0)
        {
            if (RecordEvents) Events.Add(new GameEvent { Type = type, Pos = pos, Dir = dir, Index = index });
        }

        // ---- tiles and collision ----------------------------------------------------------

        public Tile TileAt(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) return Tile.Empty;
            return Tiles[ty * Cols + tx];
        }

        public static bool IsSolidTile(Tile t) => t == Tile.Solid || t == Tile.Solid2;
        public static bool IsThorn(Tile t) => t >= Tile.ThornUp && t <= Tile.ThornRight;

        /// <summary>Terrain that never changes (used for decoration and spawning).</summary>
        public bool StaticSolid(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) return true;
            return IsSolidTile(Tiles[ty * Cols + tx]);
        }

        public bool TileSolid(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) return false;
            int i = ty * Cols + tx;
            switch (Tiles[i])
            {
                case Tile.Solid:
                case Tile.Solid2: return true;
                case Tile.Breakable:
                case Tile.BellGlass: return !Groups[GroupOf[i]].Gone;
                case Tile.Resonance: return Groups[GroupOf[i]].Timer > 0 && !Groups[GroupOf[i]].Pending;
                case Tile.Gate: return !GatesOpen;
                case Tile.ArenaDoor: return ArenaClosed;
                default: return false;
            }
        }

        /// <param name="edgesSolid">creatures never leave their room, so every edge is a wall to them</param>
        public bool SolidAt(int x, int y, int w, int h, bool edgesSolid = false)
        {
            int ty0 = FloorDiv(y, TileSize), ty1 = FloorDiv(y + h - 1, TileSize);
            int tx0 = FloorDiv(x, TileSize), tx1 = FloorDiv(x + w - 1, TileSize);
            if (x < 0 && EdgeBlocked(_openL, ty0, ty1, edgesSolid)) return true;
            if (x + w > Width && EdgeBlocked(_openR, ty0, ty1, edgesSolid)) return true;
            if (y < 0 && EdgeBlocked(_openT, tx0, tx1, edgesSolid)) return true;
            if (y + h > Height && EdgeBlocked(_openB, tx0, tx1, edgesSolid)) return true;
            for (int ty = Math.Max(0, ty0); ty <= Math.Min(Rows - 1, ty1); ty++)
                for (int tx = Math.Max(0, tx0); tx <= Math.Min(Cols - 1, tx1); tx++)
                    if (TileSolid(tx, ty)) return true;
            return false;
        }

        private static bool EdgeBlocked(bool[] open, int a, int b, bool edgesSolid)
        {
            if (edgesSolid) return true;
            for (int i = a; i <= b; i++)
            {
                int k = Math.Clamp(i, 0, open.Length - 1);
                if (!open[k]) return true;
            }
            return false;
        }

        /// <summary>True if a one-way platform's top surface sits exactly on pixel row <paramref name="row"/>.</summary>
        public bool PlatformTopAt(int x, int row, int w)
        {
            if (row < 0 || (row & 7) != 0) return false;
            int ty = row / TileSize;
            int tx0 = FloorDiv(x, TileSize), tx1 = FloorDiv(x + w - 1, TileSize);
            for (int tx = tx0; tx <= tx1; tx++)
                if (TileAt(tx, ty) == Tile.Platform) return true;
            return false;
        }

        public bool SolidOrPlatformBelow(int x, int y, int w, int h)
            => SolidAt(x, y + 1, w, h) || PlatformTopAt(x, y + h, w);

        public bool UpdraftAt(int x, int y, int w, int h)
        {
            int tx0 = Math.Max(0, FloorDiv(x, TileSize)), tx1 = Math.Min(Cols - 1, FloorDiv(x + w - 1, TileSize));
            int ty0 = Math.Max(0, FloorDiv(y, TileSize)), ty1 = Math.Min(Rows - 1, FloorDiv(y + h - 1, TileSize));
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                    if (Updraft[ty * Cols + tx]) return true;
            return false;
        }

        /// <summary>Solid, unchanging ground under both feet and no thorns close by: a place to return to after a hazard.</summary>
        public bool IsSafeGround(int x, int y)
        {
            int fy = FloorDiv(y + PlayerSim.H, TileSize);
            int lx = FloorDiv(x, TileSize), rx = FloorDiv(x + PlayerSim.W - 1, TileSize);
            bool Firm(int tx) { var t = TileAt(tx, fy); return IsSolidTile(t) || t == Tile.Platform; }
            if (!Firm(lx) || !Firm(rx)) return false;
            if (ArenaClosed) return false;
            for (int ty = FloorDiv(y, TileSize) - 1; ty <= fy + 1; ty++)
                for (int tx = lx - 2; tx <= rx + 2; tx++)
                    if (IsThorn(TileAt(tx, ty))) return false;
            return true;
        }

        public static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        // ---- spawning -------------------------------------------------------------------

        public void Spawn(in Projectile p)
        {
            for (int i = 0; i < Projectiles.Length; i++)
                if (!Projectiles[i].Active)
                {
                    Projectiles[i] = p;
                    Projectiles[i].Active = true;
                    return;
                }
        }

        public void DropEmbers(Vector2 at, int total, float spread = 1f)
        {
            if (EmberCharm) total = (int)MathF.Ceiling(total * 1.25f);
            while (total > 0)
            {
                int v = total >= 10 ? 5 : 1;
                if (total >= 25) v = 10;
                total -= v;
                for (int i = 0; i < Drops.Length; i++)
                {
                    if (Drops[i].Active) continue;
                    float a = Rand.Range(-MathF.PI * 0.85f, -MathF.PI * 0.15f);
                    float s = Rand.Range(60, 140) * spread;
                    Drops[i] = new EmberDrop { Active = true, X = at.X, Y = at.Y, VX = MathF.Cos(a) * s, VY = MathF.Sin(a) * s, Value = v };
                    break;
                }
            }
        }

        public void AddShade(int x, int y)
        {
            var t = MakeThing(ThingKind.Shade, x, y, 14, 16);
            var list = new List<Thing>(Things) { t };
            Things = list.ToArray();
        }

        /// <summary>Place the pilgrim at the game's very first spawn point (or a shrine) of this room.</summary>
        public Point SpawnAtThing(ThingKind kind)
        {
            foreach (var t in Things)
                if (t.Kind == kind) return new Point(t.X - PlayerSim.W / 2, t.Y - PlayerSim.H);
            return StartPoint ?? new Point(Width / 2, Height / 2);
        }

        // ---- the bell ------------------------------------------------------------------

        public float LastToll = -10f;

        public void Toll(Vector2 c, bool hop)
        {
            LastToll = Time;
            Emit(Ev.Toll, c, index: hop ? 1 : 0);
            float r = PlayerSim.TollRadius;
            for (int gi = 0; gi < Groups.Length; gi++)
            {
                ref var g = ref Groups[gi];
                float range = g.Kind == Tile.Resonance ? ResonanceRange : r;
                if (!GroupInRange(g, c, range)) continue;
                if (g.Kind == Tile.BellGlass && !g.Gone)
                {
                    g.Gone = true;
                    Flags.Add(GroupFlag(g));
                    Emit(Ev.GlassBreak, g.Rect.Center.ToVector2(), index: gi);
                }
                else if (g.Kind == Tile.Resonance)
                {
                    bool wasSolid = g.Timer > 0 && !g.Pending;
                    g.Timer = ResonanceTime;
                    g.Pending = g.Pending || (!wasSolid && GroupOverlaps(g, P.Hitbox));
                    Emit(Ev.Resonate, g.Rect.Center.ToVector2(), index: gi);
                }
            }
            for (int i = 0; i < Enemies.Length; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Alive) continue;
                if (Vector2.Distance(e.Center, c) < r * 1.25f + e.W / 2f)
                {
                    e.Stun = 1.4f;
                    if (e.Type == EnemyType.Wisp) e.Speed = Vector2.Normalize(e.Center - c + new Vector2(0.01f, 0)) * 60f;
                    if (e.Type == EnemyType.Sentinel && e.State <= 3) { e.State = 4; e.T = 1.4f; }
                    if (e.Type == EnemyType.Dropper && e.State <= 1) { e.State = 2; }
                    Emit(Ev.EnemyHit, e.Center, index: -1);
                }
            }
            for (int i = 0; i < Projectiles.Length; i++)
            {
                ref var p = ref Projectiles[i];
                if (p.Active && p.Type == ProjType.Note && Vector2.Distance(new Vector2(p.X, p.Y), c) < r + p.R)
                {
                    p.Active = false;
                    Emit(Ev.ProjectilePop, new Vector2(p.X, p.Y), index: (int)p.Type);
                }
            }
            Boss?.OnToll(this, c);
        }

        private bool GroupInRange(in TileGroup g, Vector2 c, float r)
        {
            for (int ty = g.Y0; ty <= g.Y1; ty++)
                for (int tx = g.X0; tx <= g.X1; tx++)
                {
                    if (GroupOf[ty * Cols + tx] != g.Index) continue;
                    float cx = Math.Clamp(c.X, tx * 8, tx * 8 + 8), cy = Math.Clamp(c.Y, ty * 8, ty * 8 + 8);
                    float dx = cx - c.X, dy = cy - c.Y;
                    if (dx * dx + dy * dy <= r * r) return true;
                }
            return false;
        }

        public bool GroupOverlaps(in TileGroup g, Rectangle box)
        {
            for (int ty = g.Y0; ty <= g.Y1; ty++)
                for (int tx = g.X0; tx <= g.X1; tx++)
                    if (GroupOf[ty * Cols + tx] == g.Index && box.Intersects(new Rectangle(tx * 8, ty * 8, 8, 8))) return true;
            return false;
        }

        // ---- simulation ------------------------------------------------------------------

        public void Update(in PlayerInput input, float dt)
        {
            Time += dt;
            GateAnim = Calc.Approach(GateAnim, GatesOpen ? 1 : 0, dt * 2.5f);
            UpdateGroups(dt);

            if (P.State == PState.Normal && !Exited && !HazardPending)
            {
                P.Update(this, input, dt);
                if (P.State == PState.Normal) ResolveStrike();
            }

            if (!NoEnemies)
            {
                for (int i = 0; i < Enemies.Length; i++)
                    if (Enemies[i].Alive) EnemyAI.Update(ref Enemies[i], this, dt);
                Boss?.Tick(this, dt);
            }
            UpdateProjectiles(dt);
            UpdateDrops(dt);

            if (P.State != PState.Normal || Exited || HazardPending) return;
            Contact();
            if (P.State != PState.Normal || HazardPending) return;
            Hazards();
            if (P.State != PState.Normal || HazardPending) return;
            Interact(input);
            CheckExit();
        }

        private void UpdateGroups(float dt)
        {
            var hb = P.Hitbox;
            for (int i = 0; i < Groups.Length; i++)
            {
                ref var g = ref Groups[i];
                if (g.Kind != Tile.Resonance || g.Timer <= 0) continue;
                g.Timer -= dt;
                if (g.Pending && !GroupOverlaps(g, hb)) g.Pending = false;
                if (g.Timer <= 0) { g.Timer = 0; g.Pending = false; }
            }
        }

        private void ResolveStrike()
        {
            if (!P.StrikeActive) return;
            var box = P.StrikeBox;
            int swing = P.SwingId;
            bool down = P.Attack == Strike.Down;
            bool connected = false, pogo = false;
            int dirX = P.Attack == Strike.Side ? P.Facing : 0;

            for (int i = 0; i < Enemies.Length; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Alive || e.LastSwing == swing || !e.Box.Intersects(box)) continue;
                e.LastSwing = swing;
                connected = true; pogo = true;
                HitEnemy(ref e, i);
            }

            if (Boss != null && Boss.Active && !Boss.Dead && Boss.LastSwing != swing && Boss.HurtboxHit(box))
            {
                Boss.LastSwing = swing;
                connected = true; pogo = true;
                Boss.TakeHit(this, P.Damage, box, dirX);
            }

            for (int gi = 0; gi < Groups.Length; gi++)
            {
                ref var g = ref Groups[gi];
                if (g.Kind != Tile.Breakable || g.Gone || g.LastSwing == swing || !GroupOverlaps(g, box)) continue;
                g.LastSwing = swing;
                connected = true; pogo = true;
                g.Hp--;
                var at = ClosestPointIn(g.Rect, P.Center);
                if (g.Hp <= 0)
                {
                    g.Gone = true;
                    Flags.Add(GroupFlag(g));
                    Emit(Ev.WallBreak, g.Rect.Center.ToVector2(), index: gi);
                }
                else Emit(Ev.WallHit, at, new Vector2(dirX, down ? 1 : (P.Attack == Strike.Up ? -1 : 0)), gi);
            }

            if (down && !P.SwingConnected)
            {
                int tx0 = FloorDiv(box.Left, 8), tx1 = FloorDiv(box.Right - 1, 8);
                int ty0 = FloorDiv(box.Top, 8), ty1 = FloorDiv(box.Bottom - 1, 8);
                for (int ty = ty0; ty <= ty1 && !pogo; ty++)
                    for (int tx = tx0; tx <= tx1; tx++)
                        if (IsThorn(TileAt(tx, ty)))
                        {
                            pogo = true; connected = true;
                            Emit(Ev.ThornHit, new Vector2(Math.Clamp(P.Center.X, tx * 8, tx * 8 + 8), ty * 8 + 2));
                            break;
                        }
            }

            for (int i = 0; i < Things.Length; i++)
            {
                ref var t = ref Things[i];
                if (t.Used || t.LastSwing == swing || !t.Box.Intersects(box)) continue;
                if (t.Kind == ThingKind.Cache)
                {
                    t.LastSwing = swing;
                    connected = true; pogo = true;
                    t.Hp--;
                    var at = new Vector2(t.X, t.Y - 7);
                    if (t.Hp <= 0)
                    {
                        t.Used = true;
                        Flags.Add($"cache:{Def.Id}:{t.Index}");
                        DropEmbers(at, 22);
                        Emit(Ev.CacheBreak, at, index: i);
                    }
                    else
                    {
                        DropEmbers(at, 6);
                        Emit(Ev.CacheHit, at, index: i);
                    }
                }
                else if (t.Kind == ThingKind.Lever)
                {
                    t.LastSwing = swing;
                    t.Used = true;
                    connected = true;
                    Flags.Add("lever:" + Def.Id);
                    Emit(Ev.Lever, new Vector2(t.X, t.Y - 6), index: i);
                    if (Def.Prop("gatekey", Def.Id) == Def.Id && !GatesOpen)
                    {
                        GatesOpen = true;
                        Emit(Ev.GateOpen, new Vector2(t.X, t.Y));
                    }
                }
            }

            for (int i = 0; i < Projectiles.Length; i++)
            {
                ref var p = ref Projectiles[i];
                if (!p.Active || !p.Breakable || !p.Armed) continue;
                if (!CircleRect(p.X, p.Y, p.R, box)) continue;
                p.Active = false;
                connected = true; pogo = true;
                Emit(Ev.ProjectilePop, new Vector2(p.X, p.Y), index: (int)p.Type);
            }

            if (connected && !P.SwingConnected)
            {
                P.SwingConnected = true;
                P.Recoil(this, pogo);
            }
        }

        public void HitEnemy(ref Enemy e, int index)
        {
            int dir = e.Center.X >= P.Center.X ? 1 : -1;
            if (P.Attack == Strike.Side && e.Shielded && e.Facing == -dir)
            {
                Emit(Ev.Clank, new Vector2(dir > 0 ? e.X : e.X + e.W, e.Y + 7), new Vector2(dir, 0), index);
                P.Speed.X = -dir * 150f;
                e.Speed.X = dir * 40f;
                return;
            }
            e.Hp -= P.Damage;
            e.Flash = 0.14f;
            if (e.Type == EnemyType.Wisp)
            {
                var away = e.Center - P.Center;
                if (away.LengthSquared() < 1) away = new Vector2(dir, 0);
                away.Normalize();
                e.Speed = away * 120f;
                if (e.State == 2) { e.State = 4; e.T = 0.5f; }
            }
            else if (e.Type != EnemyType.Spitter && !(e.Type == EnemyType.Dropper && e.State == 0))
            {
                float k = e.Type == EnemyType.Sentinel ? 50f : 95f;
                if (P.Attack == Strike.Side) e.Speed.X = dir * k;
                if (P.Attack == Strike.Up && e.OnGround) e.Speed.Y = -80f;
            }
            if (e.Type == EnemyType.Dropper && e.State <= 1) e.State = 2;
            if (e.Hp <= 0)
            {
                e.Alive = false;
                DropEmbers(e.Center, EnemyAI.Embers(e.Type));
                Emit(Ev.EnemyDie, e.Center, new Vector2(dir, 0), (int)e.Type);
            }
            else Emit(Ev.EnemyHit, e.Center, new Vector2(dir, 0), index);
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = 0; i < Projectiles.Length; i++)
            {
                ref var p = ref Projectiles[i];
                if (!p.Active) continue;
                p.T += dt;
                p.Life -= dt;
                if (p.Life <= 0) { p.Active = false; continue; }
                if (!p.Armed) continue;
                if (p.Type == ProjType.Ring) { p.R += p.VX * dt; continue; }
                p.VY += p.Gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                if (p.HitsTerrain && p.T > p.Delay + 0.05f && SolidAt((int)(p.X - 1), (int)(p.Y - 1), 2, 2, true))
                {
                    p.Active = false;
                    Emit(Ev.ProjectilePop, new Vector2(p.X - p.VX * dt, p.Y - p.VY * dt), index: (int)p.Type);
                    continue;
                }
                if (p.X < -40 || p.Y < -60 || p.X > Width + 40 || p.Y > Height + 40) p.Active = false;
            }
        }

        /// <summary>Height of an erupting thorn at its age (rises, holds, sinks).</summary>
        public static float ThornHeight(in Projectile p)
        {
            float t = p.T - p.Delay, life = p.Life + p.T - p.Delay;
            if (t < 0) return 0;
            float up = Math.Min(1, t / 0.08f), down = Math.Min(1, Math.Max(0, (life - t) / 0.15f));
            return p.R * Math.Min(up, down);
        }

        public bool ProjectileHits(in Projectile p, Rectangle hb)
        {
            if (!p.Armed) return false;
            switch (p.Type)
            {
                case ProjType.Wave: return new Rectangle((int)p.X - 5, (int)p.Y - 10, 10, 10).Intersects(hb);
                case ProjType.Thorn:
                {
                    float h = ThornHeight(p);
                    return h > 3 && new Rectangle((int)p.X - 4, (int)(p.Y - h), 8, (int)h).Intersects(hb);
                }
                case ProjType.Ring:
                {
                    if (Time - LastToll < 0.32f) return false;
                    var c = hb.Center.ToVector2();
                    float d = Vector2.Distance(c, new Vector2(p.X, p.Y));
                    return Math.Abs(d - p.R) < 7f;
                }
                default: return CircleRect(p.X, p.Y, p.R * 0.8f, hb);
            }
        }

        private void UpdateDrops(float dt)
        {
            var pc = P.Center;
            float magnet = EmberCharm ? 64f : 30f;
            for (int i = 0; i < Drops.Length; i++)
            {
                ref var d = ref Drops[i];
                if (!d.Active) continue;
                d.T += dt;
                var to = pc - new Vector2(d.X, d.Y);
                float dist = to.Length();
                if (d.T > 0.35f && dist < magnet && P.State == PState.Normal)
                {
                    var v = to / Math.Max(1, dist) * 220f;
                    d.VX = Calc.Lerp(d.VX, v.X, Calc.Damp(14, dt));
                    d.VY = Calc.Lerp(d.VY, v.Y, Calc.Damp(14, dt));
                    d.X += d.VX * dt; d.Y += d.VY * dt;
                }
                else
                {
                    d.VY = Math.Min(d.VY + 520f * dt, 240f);
                    d.VX *= MathF.Max(0, 1 - 1.5f * dt);
                    float nx = d.X + d.VX * dt;
                    if (SolidAt((int)nx - 1, (int)d.Y - 1, 2, 2, true)) d.VX = -d.VX * 0.5f; else d.X = nx;
                    float ny = d.Y + d.VY * dt;
                    bool floor = d.VY > 0 && (SolidAt((int)d.X - 1, (int)ny, 2, 1, true) || CrossesPlatform((int)d.X - 1, d.Y, ny));
                    if (floor || (d.VY < 0 && SolidAt((int)d.X - 1, (int)ny - 2, 2, 2, true)))
                    {
                        d.VY = d.VY > 0 ? (d.VY > 60 ? -d.VY * 0.42f : 0) : 0;
                        d.VX *= 0.7f;
                    }
                    else d.Y = ny;
                }
                if (dist < 7f && d.T > 0.25f && P.State == PState.Normal)
                {
                    d.Active = false;
                    Emit(Ev.Ember, new Vector2(d.X, d.Y), index: d.Value);
                }
                if (d.T > 45f) d.Active = false;
            }
        }

        private void Contact()
        {
            var hb = P.Hitbox;
            for (int i = 0; i < Enemies.Length; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Alive) continue;
                var body = e.Box;
                body.Inflate(-1, -1);
                if (body.Intersects(hb) || (e.SpearOut && e.Spear.Intersects(hb)))
                {
                    if (P.Hurt(this, 1, e.Center.X)) return;
                }
            }
            for (int i = 0; i < Projectiles.Length; i++)
            {
                ref var p = ref Projectiles[i];
                if (!p.Active || P.Invuln > 0 || !ProjectileHits(p, hb)) continue;
                if (P.Hurt(this, p.Damage, p.X))
                {
                    if (p.Type != ProjType.Wave && p.Type != ProjType.Thorn && p.Type != ProjType.Ring) p.Active = false;
                    return;
                }
            }
            if (Boss != null && Boss.Active && !Boss.Dead && P.Invuln <= 0)
            {
                int dmg = Boss.ContactDamage(hb, out float fromX);
                if (dmg > 0) P.Hurt(this, dmg, fromX);
            }
        }

        private void Hazards()
        {
            var hb = P.Hitbox;
            int tx0 = FloorDiv(hb.Left, TileSize), tx1 = FloorDiv(hb.Right - 1, TileSize);
            int ty0 = FloorDiv(hb.Top, TileSize), ty1 = FloorDiv(hb.Bottom - 1, TileSize);
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    Tile t = TileAt(tx, ty);
                    if (!IsThorn(t)) continue;
                    int px = tx * TileSize, py = ty * TileSize;
                    Rectangle danger = t switch
                    {
                        Tile.ThornUp => new Rectangle(px + 1, py + 4, 6, 4),
                        Tile.ThornDown => new Rectangle(px + 1, py, 6, 4),
                        Tile.ThornLeft => new Rectangle(px + 4, py + 1, 4, 6),
                        _ => new Rectangle(px, py + 1, 4, 6),
                    };
                    if (!danger.Intersects(hb)) continue;
                    P.Hurt(this, 1, px + 4, ignoreInvuln: true);
                    if (P.State == PState.Normal)
                    {
                        HazardPending = true;
                        Emit(Ev.Hazard, P.Center);
                    }
                    return;
                }
        }

        /// <summary>Puts the pilgrim back on the last safe ground after a thorn.</summary>
        public void HazardRespawn()
        {
            HazardPending = false;
            P.X = P.SafeX; P.Y = P.SafeY;
            P.RemX = P.RemY = 0;
            P.Speed = Vector2.Zero;
            P.StunT = 0;
            P.Clinging = P.Gliding = P.Healing = false;
            P.Attack = Strike.None;
            P.State = PState.Normal;
            P.OnGround = true;
            if (P.Invuln < 0.6f) P.Invuln = 0.6f;
        }

        private void Interact(in PlayerInput input)
        {
            var hb = P.Hitbox;
            NearThing = -1;
            for (int i = 0; i < Things.Length; i++)
            {
                ref var t = ref Things[i];
                if (t.Used) continue;
                if (!t.Box.Intersects(hb)) continue;
                switch (t.Kind)
                {
                    case ThingKind.Heart:
                        t.Used = true;
                        Flags.Add("heart:" + Def.Id);
                        P.MaxHp++; P.Hp = P.MaxHp;
                        Emit(Ev.Item, new Vector2(t.X, t.Y), index: 0);
                        break;
                    case ThingKind.Flask:
                        t.Used = true;
                        Flags.Add("flask:" + Def.Id);
                        P.MaxFlasks++; P.Flasks = P.MaxFlasks;
                        Emit(Ev.Item, new Vector2(t.X, t.Y), index: 1);
                        break;
                    case ThingKind.Shade:
                        t.Used = true;
                        Emit(Ev.ShadeReclaim, new Vector2(t.X, t.Y - 7));
                        break;
                    case ThingKind.Cache:
                    case ThingKind.Lever:
                        break;
                    default:
                        if (P.OnGround || t.Kind == ThingKind.Altar) NearThing = i;
                        break;
                }
            }
            if (NearThing >= 0 && input.InteractPressed && P.OnGround && !P.Healing)
            {
                ref var t = ref Things[NearThing];
                if (t.Kind == ThingKind.Altar)
                {
                    t.Used = true;
                    var ab = AbilityFromId(Def.Prop("ability"));
                    Flags.Add("ab:" + Def.Prop("ability"));
                    P.Ab |= ab;
                    Emit(Ev.Ability, new Vector2(t.X, t.Y - 12), index: (int)ab);
                }
                else Emit(Ev.Interact, new Vector2(t.X, t.Y), index: NearThing);
            }
        }

        public static Abilities AbilityFromId(string id) => id switch
        {
            "grips" => Abilities.Grips, "bell" => Abilities.Bell, "kite" => Abilities.Kite, _ => Abilities.None,
        };

        private void CheckExit()
        {
            var c = P.Center;
            int exit = 0;
            if (c.Y < 0) exit = 1;
            else if (c.X < 0) exit = 2;
            else if (c.X > Width) exit = 3;
            else if (c.Y > Height) exit = 4;
            if (exit == 0) return;
            if (Atlas.AtWorld(Def.WorldX + (int)c.X, Def.WorldY + (int)c.Y) == null) return;
            Exited = true;
            ExitDir = exit;
            Emit(Ev.Exit, c, index: exit);
        }

        private bool CrossesPlatform(int x, float y0, float y1)
        {
            for (int row = ((int)MathF.Floor(y0) / 8 + 1) * 8; row <= (int)MathF.Floor(y1); row += 8)
                if (PlatformTopAt(x, row, 2)) return true;
            return false;
        }

        public static bool CircleRect(float cx, float cy, float r, Rectangle b)
        {
            float x = Math.Clamp(cx, b.Left, b.Right), y = Math.Clamp(cy, b.Top, b.Bottom);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static Vector2 ClosestPointIn(Rectangle r, Vector2 p)
            => new Vector2(Math.Clamp(p.X, r.Left, r.Right), Math.Clamp(p.Y, r.Top, r.Bottom));

        // ---- snapshots (used by the verifier; creatures are disabled there) ---------------

        public sealed class Snapshot
        {
            public PlayerSim P;
            public float Time;
            public bool GatesOpen, ArenaClosed, Exited, HazardPending;
            public int ExitDir;
            public TileGroup[] Groups;
            public Thing[] Things;
        }

        public Snapshot Save() => new Snapshot
        {
            P = P, Time = Time, GatesOpen = GatesOpen, ArenaClosed = ArenaClosed, Exited = Exited, ExitDir = ExitDir,
            HazardPending = HazardPending, Groups = (TileGroup[])Groups.Clone(), Things = (Thing[])Things.Clone(),
        };

        public void Load(Snapshot s)
        {
            P = s.P; Time = s.Time; GatesOpen = s.GatesOpen; ArenaClosed = s.ArenaClosed; Exited = s.Exited; ExitDir = s.ExitDir;
            HazardPending = s.HazardPending;
            Array.Copy(s.Groups, Groups, Groups.Length);
            Array.Copy(s.Things, Things, Things.Length);
        }
    }
}
