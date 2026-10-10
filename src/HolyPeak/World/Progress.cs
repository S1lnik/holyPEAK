using System;
using System.Collections.Generic;

namespace HolyPeak.World
{
    /// <summary>How much of the mountain the pilgrim has found.</summary>
    public static class Progress
    {
        public static readonly string[] AbilityIds = { "grips", "bell", "kite" };
        public static readonly string[] BossIds = { "thornback", "abbot", "wyrm", "bellkeeper" };

        public static (int got, int total) Completion(ICollection<string> flags)
        {
            int got = 0, total = 0;
            void Count(string flag) { total++; if (flags.Contains(flag)) got++; }
            foreach (var a in AbilityIds) Count("ab:" + a);
            foreach (var b in BossIds) Count("boss:" + b);
            foreach (var r in Atlas.Rooms)
            {
                if (r.Has('H')) Count("heart:" + r.Id);
                if (r.Has('F')) Count("flask:" + r.Id);
                if (r.Has('P')) Count("spell:" + r.Prop("spell"));
                if (r.Has('C') && r.Prop("charm", "notch") == "notch") Count("notch:" + r.Id);
                int caches = r.Count('$');
                for (int i = 0; i < caches; i++) Count($"cache:{r.Id}:{i}");
            }
            foreach (var c in Charms.All) Count("charm:" + c.Id);
            // wares that are charms or notches are already counted above
            foreach (var w in Lore.Wares)
                if (w.Id != "charm" && !w.Id.StartsWith("c-") && !w.Id.StartsWith("notch")) Count("bought:" + w.Id);
            Count("notch:shop1");
            Count("notch:shop2");
            Count("rung");
            return (got, total);
        }

        public static int Percent(ICollection<string> flags)
        {
            var (got, total) = Completion(flags);
            return total == 0 ? 0 : (int)Math.Floor(got * 100.0 / total);
        }

        public static int Count(ICollection<string> flags, string prefix)
        {
            int n = 0;
            foreach (var f in flags) if (f.StartsWith(prefix)) n++;
            return n;
        }

        public static Abilities AbilitiesFrom(ICollection<string> flags)
        {
            var a = Abilities.None;
            if (flags.Contains("ab:grips")) a |= Abilities.Grips;
            if (flags.Contains("ab:bell")) a |= Abilities.Bell;
            if (flags.Contains("ab:kite")) a |= Abilities.Kite;
            return a;
        }
    }
}
