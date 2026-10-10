using System;
using System.Collections.Generic;

namespace HolyPeak.World
{
    /// <summary>Charms the pilgrim can wear. Each costs notches; the pilgrim starts with three.</summary>
    [Flags]
    public enum Charm : uint
    {
        None = 0,
        Stride = 1 << 0,      // run faster
        Feather = 1 << 1,     // fall slower while holding jump
        Votive = 1 << 2,      // more grace from each strike
        Ember = 1 << 3,       // more embers, gathered from further away
        LongStaff = 1 << 4,   // the staff reaches further
        QuickStaff = 1 << 5,  // strike more often
        Prayer = 1 << 6,      // heal faster
        Psalter = 1 << 7,     // spells hit harder
        Bramble = 1 << 8,     // thorns burst out when hurt
        Clasp = 1 << 9,       // cling to walls for longer
        Echo = 1 << 10,       // a second air toll
        Vigil = 1 << 11,      // one more flame of vigil
        Ward = 1 << 12,       // thorns cost no flame
    }

    /// <summary>Spells the pilgrim can learn. Each costs grace, the same grace that heals.</summary>
    [Flags]
    public enum Spell : byte
    {
        None = 0,
        Bolt = 1,       // Lantern Bolt: a piercing flame thrown forward
        Star = 2,       // Falling Star: plunge down, breaking cracked floors, and burst on landing
        Psalm = 4,      // Rising Psalm: a pillar of light above
    }

    public static class Charms
    {
        public sealed class Info
        {
            public Charm Charm;
            public string Id, Name, Desc;
            public int Cost;
        }

        public const int StartNotches = 3;

        public static readonly Info[] All =
        {
            new Info { Charm = Charm.Stride, Id = "stride", Name = "Pilgrim's Stride", Cost = 1, Desc = "Worn soles that remember every road. You run faster." },
            new Info { Charm = Charm.Feather, Id = "feather", Name = "Feather Mantle", Cost = 1, Desc = "Hold jump while falling to drift down slowly." },
            new Info { Charm = Charm.Votive, Id = "votive", Name = "Votive Cup", Cost = 1, Desc = "Your strikes gather more grace." },
            new Info { Charm = Charm.Ember, Id = "ember", Name = "Ember Charm", Cost = 1, Desc = "Creatures drop more embers, and embers fly to you from further away." },
            new Info { Charm = Charm.LongStaff, Id = "longstaff", Name = "Long Staff", Cost = 2, Desc = "Your staff reaches further." },
            new Info { Charm = Charm.QuickStaff, Id = "quickstaff", Name = "Quick Staff", Cost = 2, Desc = "You strike more quickly." },
            new Info { Charm = Charm.Prayer, Id = "prayer", Name = "Steady Prayer", Cost = 1, Desc = "You heal more quickly." },
            new Info { Charm = Charm.Psalter, Id = "psalter", Name = "Psalter", Cost = 2, Desc = "Your spells strike harder, and the lantern bolt burns larger." },
            new Info { Charm = Charm.Bramble, Id = "bramble", Name = "Bramble Vow", Cost = 2, Desc = "When you are hurt, thorns burst out and strike everything near you." },
            new Info { Charm = Charm.Clasp, Id = "clasp", Name = "Iron Clasp", Cost = 1, Desc = "You cling to walls far longer before you slip." },
            new Info { Charm = Charm.Echo, Id = "echo", Name = "Echoing Bell", Cost = 2, Desc = "The bell can lift you twice before you land." },
            new Info { Charm = Charm.Vigil, Id = "vigil", Name = "Candle of Vigil", Cost = 2, Desc = "One more flame of vigil while you wear it." },
            new Info { Charm = Charm.Ward, Id = "ward", Name = "Thornward", Cost = 1, Desc = "Thorns still send you back, but no longer burn a flame." },
        };

        public static Info Get(Charm c)
        {
            foreach (var i in All) if (i.Charm == c) return i;
            return null;
        }

        public static Info Get(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }

        public static int Index(Charm c)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Charm == c) return i;
            return -1;
        }

        public static Charm Owned(ICollection<string> flags)
        {
            var c = Charm.None;
            foreach (var i in All) if (flags.Contains("charm:" + i.Id)) c |= i.Charm;
            return c;
        }

        public static int Notches(ICollection<string> flags) => StartNotches + Progress.Count(flags, "notch:");

        public static int Used(Charm equipped)
        {
            int n = 0;
            foreach (var i in All) if ((equipped & i.Charm) != 0) n += i.Cost;
            return n;
        }

        public static Spell Spells(ICollection<string> flags)
        {
            var s = Spell.None;
            if (flags.Contains("spell:bolt")) s |= Spell.Bolt;
            if (flags.Contains("spell:star")) s |= Spell.Star;
            if (flags.Contains("spell:psalm")) s |= Spell.Psalm;
            return s;
        }

        public static Spell SpellFromId(string id) => id switch
        {
            "bolt" => Spell.Bolt, "star" => Spell.Star, "psalm" => Spell.Psalm, _ => Spell.None,
        };

        public static (string name, string[] lines) SpellInfo(Spell s) => s switch
        {
            Spell.Bolt => ("Lantern Bolt", new[]
            {
                "{CAST} to throw a flame from your lantern.",
                "It burns through every creature in its path.",
                "Spells spend grace, the same grace that heals you.",
            }),
            Spell.Star => ("Falling Star", new[]
            {
                "Hold {DOWN} and {CAST} to plunge like a falling star.",
                "You break through cracked floors,",
                "and burst into light where you land.",
            }),
            _ => ("Rising Psalm", new[]
            {
                "Hold {UP} and {CAST} to raise a pillar of light",
                "that strikes everything above you.",
            }),
        };
    }
}
