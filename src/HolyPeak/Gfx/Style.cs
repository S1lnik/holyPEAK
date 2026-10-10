using Microsoft.Xna.Framework;

namespace HolyPeak.Gfx
{
    /// <summary>Colour script for one chapter (and the title screen).</summary>
    public sealed class Style
    {
        public int Id;
        public Color[] Sky;             // top -> horizon gradient stops
        public Color PeakFar, PeakNear, PeakGlow;
        public Color MountFar, MountMid, MountNear;
        public Color Silhouette;        // closest parallax layer
        public Color Haze;

        public Color Outline, Dark, Base, Light, Speck;
        public Color Alt, AltDark, AltLight;     // '=' secondary terrain
        public Color Cap, CapDark, CapLight;     // top surface (grass / moss / snow / gold)
        public Color Back, BackDark, BackLight;  // ':' background walls
        public Color Plat, PlatDark, PlatLight;  // one-way platforms
        public Color Spike, SpikeDark, SpikeLight;
        public Color Accent1, Accent2;           // flowers / decorations
        public Color Ambient;                    // particle tint
        public Color Light2;                     // global tint for hint text etc.
        public bool Stars, Snow, Fireflies, Motes, Dust, Clouds;

        public static readonly Style[] Chapters = { Foothills(), Ruins(), Frost(), Peak() };
        public static readonly Style Title = TitleStyle();

        private static Color H(uint c) => G.Hex(c);

        private static Style Foothills() => new Style
        {
            Id = 0,
            Sky = new[] { H(0x223361), H(0x4a4f8c), H(0x9a6d9e), H(0xf09a7f), H(0xffd29a) },
            PeakFar = H(0x7d78a8), PeakNear = H(0x655f92), PeakGlow = H(0xfff1c4),
            MountFar = H(0x8a7aa6), MountMid = H(0x60608f), MountNear = H(0x3e4670),
            Silhouette = H(0x223050), Haze = H(0xf6b48f),
            Outline = H(0x1e130e), Dark = H(0x3b2519), Base = H(0x5a3a27), Light = H(0x7a5134), Speck = H(0x8f6a49),
            Alt = H(0x726b7a), AltDark = H(0x4b4654), AltLight = H(0x958ea0),
            Cap = H(0x5aa832), CapDark = H(0x3c7a26), CapLight = H(0x9ce05a),
            Back = H(0x3a2c34), BackDark = H(0x2a1f27), BackLight = H(0x4a3a43),
            Plat = H(0xa06e40), PlatDark = H(0x6b4426), PlatLight = H(0xd09a5c),
            Spike = H(0xb9bccb), SpikeDark = H(0x5f6276), SpikeLight = H(0xf2f4ff),
            Accent1 = H(0xffe066), Accent2 = H(0xff7aa8),
            Ambient = H(0xfff2a0), Light2 = H(0xfff4e0),
            Fireflies = true,
        };

        private static Style Ruins() => new Style
        {
            Id = 1,
            Sky = new[] { H(0x1a1735), H(0x3a2a58), H(0x7a3f6e), H(0xc7637a), H(0xf2a25e) },
            PeakFar = H(0x6a4f7e), PeakNear = H(0x553f6c), PeakGlow = H(0xffd9a0),
            MountFar = H(0x6d4a72), MountMid = H(0x4c3560), MountNear = H(0x33264a),
            Silhouette = H(0x211a33), Haze = H(0xe5806e),
            Outline = H(0x1b1418), Dark = H(0x433636), Base = H(0x645450), Light = H(0x83716a), Speck = H(0x9a877c),
            Alt = H(0xb39670), AltDark = H(0x7c6248), AltLight = H(0xd8bb8f),
            Cap = H(0x6f9a3c), CapDark = H(0x4a6e29), CapLight = H(0xa6cf5f),
            Back = H(0x3c2e3c), BackDark = H(0x2a2030), BackLight = H(0x51404e),
            Plat = H(0xb39670), PlatDark = H(0x7c6248), PlatLight = H(0xe0c79a),
            Spike = H(0xc2b3a0), SpikeDark = H(0x6d5d50), SpikeLight = H(0xfff0d8),
            Accent1 = H(0xffb14e), Accent2 = H(0xc77dff),
            Ambient = H(0xffd7a0), Light2 = H(0xffe8d0),
            Dust = true,
        };

        private static Style Frost() => new Style
        {
            Id = 2,
            Sky = new[] { H(0x0b1430), H(0x172c55), H(0x2e5486), H(0x5d8cb8), H(0xa9d0e8) },
            PeakFar = H(0x8fb0d4), PeakNear = H(0x6f93bd), PeakGlow = H(0xe8f6ff),
            MountFar = H(0x5d80ad), MountMid = H(0x3f5f8c), MountNear = H(0x2a4269),
            Silhouette = H(0x172540), Haze = H(0xbfe0f5),
            Outline = H(0x101826), Dark = H(0x2d3b52), Base = H(0x46597a), Light = H(0x627aa0), Speck = H(0x7d95b8),
            Alt = H(0x8fd0ee), AltDark = H(0x4f93bb), AltLight = H(0xd6f3ff),
            Cap = H(0xeef6ff), CapDark = H(0xb9cfe6), CapLight = H(0xffffff),
            Back = H(0x24314a), BackDark = H(0x1a2438), BackLight = H(0x32425f),
            Plat = H(0xa9dcf2), PlatDark = H(0x5b9cc4), PlatLight = H(0xeafaff),
            Spike = H(0xbfe9ff), SpikeDark = H(0x5a9fca), SpikeLight = H(0xffffff),
            Accent1 = H(0xbff0ff), Accent2 = H(0x8ad6ff),
            Ambient = H(0xffffff), Light2 = H(0xe8f6ff),
            Snow = true, Stars = true,
        };

        private static Style Peak() => new Style
        {
            Id = 3,
            Sky = new[] { H(0x07081c), H(0x171842), H(0x33296a), H(0x6a4a8e), H(0xd6a0b0) },
            PeakFar = H(0xcfc4e8), PeakNear = H(0xa99cc9), PeakGlow = H(0xffe9a8),
            MountFar = H(0x5a4c8a), MountMid = H(0x433a72), MountNear = H(0x2f2a58),
            Silhouette = H(0xe8e2f4), Haze = H(0xf2d2e2),
            Outline = H(0x2a2238), Dark = H(0x6e6484), Base = H(0xa99fba), Light = H(0xc9c0d6), Speck = H(0x9184a6),
            Alt = H(0xf0c04c), AltDark = H(0xb8862a), AltLight = H(0xffe7a0),
            Cap = H(0xffe08a), CapDark = H(0xd6a646), CapLight = H(0xfff6d0),
            Back = H(0x4a4163), BackDark = H(0x362f4c), BackLight = H(0x5b5178),
            Plat = H(0xf0c04c), PlatDark = H(0xa8781e), PlatLight = H(0xfff0b0),
            Spike = H(0xffe7a8), SpikeDark = H(0xc08a2e), SpikeLight = H(0xffffff),
            Accent1 = H(0xffd166), Accent2 = H(0xb69cff),
            Ambient = H(0xffe9a8), Light2 = H(0xfff6e0),
            Stars = true, Motes = true, Clouds = true,
        };

        private static Style TitleStyle()
        {
            var s = Frost();
            s.Id = 4;
            s.Sky = new[] { G.Hex(0x060716), G.Hex(0x111538), G.Hex(0x232a5c), G.Hex(0x4a4a86), G.Hex(0x9b86b8) };
            s.PeakFar = G.Hex(0xb9b3dc); s.PeakNear = G.Hex(0x8f88bd); s.PeakGlow = G.Hex(0xffe6a0);
            s.MountFar = G.Hex(0x40457a); s.MountMid = G.Hex(0x2d3263); s.MountNear = G.Hex(0x1c2147);
            s.Silhouette = G.Hex(0x10132e); s.Haze = G.Hex(0x8a7bb8);
            s.Snow = true; s.Stars = true;
            return s;
        }
    }
}
