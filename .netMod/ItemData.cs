using System.Collections.Generic;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Item ID mappings for RE3
    /// </summary>
    public static class ItemData
    {
        // Healing items
        public static readonly Dictionary<string, string> HealingItems = new()//re9
        {
            { "herbg", "it00_00_000" },     // Green Herb
            { "herbgg", "it00_00_100" },    // Mixed Herb (G+G)
            { "herbggg", "it00_00_200" },   // Mixed Herb (G+G+G)
            { "med", "it00_01_000" }        // Med Injector
        };
        public static readonly Dictionary<string, string> DamageItems = new()//re9
        {
            { "molotov", "it20_00_002" },
            { "acid", "it20_00_005" }
        };
        // Ammo items (from CCRE3.lua)
        public static readonly Dictionary<string, string> AmmoItems = new()
        {
            { "grenade", "it20_00_000" },
            { "grenade_stack", "it20_00_004" },
            { "molotov", "it20_00_002" },
            { "acid", "it20_00_005" },
            { "hemolytic", "it99_50_001" }
        };

        // Ammo amounts to give (tuned for RE3)
        public static readonly Dictionary<string, int> AmmoAmounts = new()
        {
            { "grenade", 2 },
            { "grenade_stack", 1 },
            { "molotov", 2 },
            { "acid", 2 },
            { "hemolytic", 1 }
        };

        // Weapons (from CCRE3.lua)
        public static readonly Dictionary<string, int> Weapons = new()
        {
            { "g19", 1 },
            { "burst", 2 },
            { "g18", 3 },
            { "edge", 4 },
            { "mup", 7 },
            { "m3", 11 },
            { "cqbr", 22 },
            { "lightning", 31 },
            { "raiden", 32 },
            { "mgl", 42 },
            { "knife", 46 },
            { "survive", 47 },
            { "hot", 48 },
            { "rocket", 49 },
            { "grenade", 65 },
            { "flash", 66 }
        };

        // Weapon ammo amounts (initial ammo when granted)
        public static readonly Dictionary<string, int> WeaponAmmo = new()
        {
            { "knife", 0 },
            { "rocket", 1 }
        };

        // Weapons that require 2 slots (big weapons)
        public static readonly Dictionary<string, bool> WeaponBig = new()
        {
            { "knife", false },
            { "rocket", true }
        };
    }
}



