using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Core;

namespace HolyPeak.Gfx
{
    public enum PShape : byte { Square, Disc, Glow, Line }

    public struct Particle
    {
        public Vector2 Pos, Vel;
        public Color Color, ColorEnd;
        public float Life, MaxLife;
        public float Size, SizeEnd;
        public float Gravity, Drag;
        public PShape Shape;
        public bool Additive;
    }

    /// <summary>A simple pooled particle system in world space.</summary>
    public sealed class Particles
    {
        private readonly Particle[] _p;
        private int _count;
        public readonly Rng Rng = new Rng(1234);

        public Particles(int capacity = 2048) { _p = new Particle[capacity]; }

        public void Clear() => _count = 0;

        public ref Particle Add()
        {
            if (_count >= _p.Length)
            {
                // recycle the oldest-looking slot
                int idx = Rng.Range(0, _p.Length);
                return ref _p[idx];
            }
            return ref _p[_count++];
        }

        public void Emit(Vector2 pos, Vector2 vel, Color c, float life, float size = 1, PShape shape = PShape.Square,
                         float gravity = 0, float drag = 0, Color? end = null, float sizeEnd = -1, bool additive = false)
        {
            ref var p = ref Add();
            p.Pos = pos; p.Vel = vel; p.Color = c; p.ColorEnd = end ?? c * 0f;
            p.Life = life; p.MaxLife = life; p.Size = size; p.SizeEnd = sizeEnd < 0 ? size : sizeEnd;
            p.Gravity = gravity; p.Drag = drag; p.Shape = shape; p.Additive = additive;
        }

        public void Burst(Vector2 pos, int n, Color c, float speedMin, float speedMax, float life, float size = 1,
                          float gravity = 0, float drag = 2f, PShape shape = PShape.Square, float spread = MathF.PI * 2, float dir = 0, bool additive = false)
        {
            for (int i = 0; i < n; i++)
            {
                float a = dir + (Rng.NextFloat() - 0.5f) * spread;
                float s = Rng.Range(speedMin, speedMax);
                Emit(pos, new Vector2(MathF.Cos(a), MathF.Sin(a)) * s, c, life * Rng.Range(0.7f, 1.2f), size, shape, gravity, drag,
                     additive: additive);
            }
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _count; i++)
            {
                ref var p = ref _p[i];
                p.Life -= dt;
                if (p.Life <= 0)
                {
                    _p[i] = _p[--_count];
                    i--;
                    continue;
                }
                p.Vel.Y += p.Gravity * dt;
                if (p.Drag > 0) p.Vel *= MathF.Max(0, 1 - p.Drag * dt);
                p.Pos += p.Vel * dt;
            }
        }

        public void Draw(SpriteBatch sb, bool additivePass)
        {
            for (int i = 0; i < _count; i++)
            {
                ref var p = ref _p[i];
                if (p.Additive != additivePass) continue;
                float t = 1 - p.Life / p.MaxLife;
                Color c = Color.Lerp(p.Color, p.ColorEnd, t);
                float s = Calc.Lerp(p.Size, p.SizeEnd, t);
                switch (p.Shape)
                {
                    case PShape.Square:
                    {
                        float sz = MathF.Max(1, MathF.Round(s));
                        G.Rect(sb, MathF.Round(p.Pos.X - sz / 2f), MathF.Round(p.Pos.Y - sz / 2f), sz, sz, c);
                        break;
                    }
                    case PShape.Disc: G.DiscAt(sb, p.Pos, s, c); break;
                    case PShape.Glow: G.GlowAt(sb, p.Pos, s, c); break;
                    case PShape.Line:
                    {
                        var d = p.Vel * 0.03f;
                        G.Line(sb, p.Pos - d, p.Pos, c, MathF.Max(1, s));
                        break;
                    }
                }
            }
        }
    }
}
