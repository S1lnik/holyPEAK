using System;
using System.Collections.Generic;

namespace HolyPeak.World
{
    /// <summary>Everything the mountain's people say, and what the peddler sells.</summary>
    public static class Lore
    {
        public sealed class Speech
        {
            public string Speaker;
            public string[] Lines;
        }

        /// <summary>What an NPC says, given what the pilgrim has done so far.</summary>
        public static Speech Npc(string id, ICollection<string> flags)
        {
            bool grips = flags.Contains("ab:grips"), bell = flags.Contains("ab:bell"), kite = flags.Contains("ab:kite");
            switch (id)
            {
                case "elder":
                    if (!grips)
                        return S("Elder Bram",
                            "Another pilgrim. Every hundred years one of you climbs, and none come down.",
                            "The road up the mountain is a cliff now. Only iron could hold to it.",
                            "West, through the Thornwood, the old smiths left their grips. Something guards them. Something with tusks.",
                            "Before you go: our old well leads down to the wayside chapel. Pilgrims left a psalm there, for anyone brave enough to read it.",
                            "And the children climb the watchtower to play on the rooftops. They say something shines up there.",
                            "Rest at the shrine before you go. If you fall, it is the shrine that will remember you.");
                    if (!bell)
                        return S("Elder Bram",
                            "Iron on your hands! So the boar is dead. I did not think I would live to see it.",
                            "The cliff above the village leads to the old abbey. The monks there rang bells to keep the dark away.",
                            "Their abbot never stopped listening. Be careful what you ring near him.");
                    if (!kite)
                        return S("Elder Bram",
                            "You carry one of the abbey's hand-bells. Glass that was cast to its note will break when it sings.",
                            "Above the abbey are the frost cliffs. The wind there is strong enough to lift a grown man.",
                            "With the right sail, perhaps it would carry you.");
                    return S("Elder Bram",
                        "The great bell is close now. I can feel it in my teeth.",
                        "Whatever waits beside it has waited a thousand years. It will not be in a hurry. Do not be in one either.");

                case "ione-village":
                    return S("Sister Ione",
                        "Hush, pilgrim. Listen. Do you hear it? Neither do I.",
                        "The great bell at the summit has been silent for a thousand years. They say the valley has not seen a true dawn since.",
                        "I came to pray beneath it. I got as far as this village.",
                        "Strike with {STRIKE}. Hold {DOWN} and strike in the air to bounce off creatures and thorns.",
                        "Every blow you land gathers grace. Kneel and hold {HEAL} to turn it into a flame of vigil.",
                        "Or spend it on a psalm, if you learn one. The same grace heals you or burns for you. Choose well.");
                case "ione-abbey":
                    return S("Sister Ione",
                        "You made it this far! The monks of this abbey were my order, long ago.",
                        "They cast their windows from bell-glass, which shatters when the right note is rung.",
                        "Their hand-bell is still here somewhere, deeper in. And the abbot... he never left either.",
                        "Below the crypt is the old scriptorium. The brothers wrote one psalm there that falls like a star.");
                case "ione-frost":
                    return S("Sister Ione",
                        "The cold has teeth up here. So does the thing beneath the snow.",
                        "When the snow trembles, move. It always surfaces where it last heard you.",
                        kite ? "And when the wind rises, trust your kite to it." : "There is a kite somewhere on these cliffs, left by a pilgrim braver than I.");
                case "ione-summit":
                    return S("Sister Ione",
                        "This is where the old keeper stands. It silenced the bell so the mountain could sleep.",
                        "It will turn your staff aside. But it was made to obey a bell. Ring yours close to it.",
                        "And when it rings the great bell itself... answer it. Toll as the sound reaches you, and it will pass you by.");
                case "tam":
                    return S("Old Tam",
                        "Ah! A pilgrim with pockets! Embers for wares, wares for embers.",
                        "Charms, too. Wear them at a shrine, as many as your notches allow.",
                        "The mountain takes, and Tam gives. For a price.");
            }
            return S("Pilgrim", "...");
        }

        private static Speech S(string speaker, params string[] lines) => new Speech { Speaker = speaker, Lines = lines };

        public static Speech Tablet(string text) => new Speech { Speaker = null, Lines = text.Split('|', StringSplitOptions.TrimEntries) };

        // ---- abilities -----------------------------------------------------------------------

        public static (string name, string[] lines) AbilityInfo(Abilities a) => a switch
        {
            Abilities.Grips => ("Iron Grips", new[]
            {
                "Press toward a wall while in the air to cling to it.",
                "{JUMP} to leap away. Leap between walls to climb.",
            }),
            Abilities.Bell => ("Bell of Resonance", new[]
            {
                "{TOLL} to ring it. Its note shatters bell-glass,",
                "wakes resonance stones and staggers creatures.",
                "Rung in the air, it lifts you once.",
            }),
            _ => ("Pilgrim's Kite", new[]
            {
                "Press {JUMP} in the air to open the kite and glide.",
                "While it is open, updrafts carry you upward.",
            }),
        };

        // ---- the peddler's wares ---------------------------------------------------------------

        public sealed class Ware
        {
            public string Id, Name, Desc;
            public int Price;
            public string Requires;
        }

        public static readonly Ware[] Wares =
        {
            new Ware { Id = "map", Name = "Surveyor's Map", Desc = "Marks every chamber of the mountain on your map.", Price = 40 },
            new Ware { Id = "c-stride", Name = "Charm: Pilgrim's Stride", Desc = "Worn soles that remember every road. You run faster.", Price = 80 },
            new Ware { Id = "c-votive", Name = "Charm: Votive Cup", Desc = "Your strikes gather more grace, for spells and for healing.", Price = 120 },
            new Ware { Id = "charm", Name = "Charm: Ember Charm", Desc = "Creatures drop more embers, and embers fly to you from further away.", Price = 150 },
            new Ware { Id = "c-ward", Name = "Charm: Thornward", Desc = "Thorns still send you back, but no longer burn a flame.", Price = 160 },
            new Ware { Id = "whet1", Name = "Whetstone", Desc = "Your staff strikes harder. Your spells grow with it.", Price = 180 },
            new Ware { Id = "notch1", Name = "Charm Notch", Desc = "Room for one more charm's worth.", Price = 200 },
            new Ware { Id = "flask", Name = "Grace Vessel", Desc = "Hold more grace: one more heal or spell.", Price = 220 },
            new Ware { Id = "heart", Name = "Vessel of Vigil", Desc = "One more flame of vigil: your health grows.", Price = 300 },
            new Ware { Id = "notch2", Name = "Second Charm Notch", Desc = "Room for yet another charm.", Price = 400, Requires = "notch1" },
            new Ware { Id = "whet2", Name = "Pilgrim's Whetstone", Desc = "Your staff strikes harder still.", Price = 420, Requires = "whet1" },
        };

        public static string BossLine(string id) => id switch
        {
            "thornback" => "Thornback has fallen.",
            "abbot" => "The Hollow Abbot is at peace.",
            "wyrm" => "The Frost Wyrm sleeps forever.",
            _ => "The Bellkeeper kneels.",
        };
    }
}
