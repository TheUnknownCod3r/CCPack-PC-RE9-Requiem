using System.Collections.Generic;

namespace RE3DotNet_CC
{
    /// <summary>
    /// Item ID mappings for RE3
    /// </summary>
    public static class ItemData
    {
        // Healing items
        public static readonly Dictionary<string, int> HealingItems = new()
        {
            { "spray", 1 },      // First Aid Spray
            { "herbg", 2 },      // Green Herb
            { "herbr", 3 },      // Red Herb
            { "herbb", 4 },      // Blue Herb
            { "herbgg", 5 },     // Green+Green Herb
            { "herbgr", 6 },     // Green+Red Herb
            { "herbgb", 7 },     // Green+Blue Herb
            { "herbggb", 8 },    // Green+Green+Blue Herb
            { "herbggg", 9 },    // Green+Green+Green Herb
            { "herbgrb", 10 },   // Green+Red+Blue Herb
            { "herbrb", 11 }     // Red+Blue Herb
        };

        // Ammo items (from CCRE3.lua)
        public static readonly Dictionary<string, int> AmmoItems = new()
        {
            { "handgun", 31 },
            { "shotgun", 32 },
            { "submachine", 33 },
            { "mag", 34 },
            { "mine", 36 },      // Mine Rounds
            { "explode", 37 },   // Explosive Rounds
            { "acid", 38 },      // Acid Rounds
            { "flame", 37 },     // Flame Rounds (shares ID with explosive in LUA)
            { "needle", 24 },    // Needle Cartridges
            { "fuel", 25 },
            { "large", 26 },     // Large Caliber Bullets
            { "slshigh", 27 },   // High Powered Bullets
            { "detonator", 31 },
            { "ink", 32 },       // Ink Ribbon
            { "board", 33 }      // Wooden Boards
        };

        // Ammo amounts to give (tuned for RE3)
        public static readonly Dictionary<string, int> AmmoAmounts = new()
        {
            { "handgun", 15 },
            { "shotgun", 8 },
            { "submachine", 60 },
            { "mag", 10 },
            { "mine", 3 },
            { "explode", 3 },
            { "acid", 6 },
            { "flame", 6 },
            { "needle", 50 },
            { "fuel", 100 },
            { "large", 12 },
            { "slshigh", 8 },
            { "detonator", 3 },
            { "ink", 1 },
            { "board", 3 }
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
            { "g19", 12 },
            { "burst", 18 },
            { "g18", 20 },
            { "edge", 12 },
            { "mup", 0 },
            { "m3", 6 },
            { "cqbr", 30 },
            { "lightning", 7 },
            { "raiden", 1 },
            { "mgl", 1 },
            { "knife", 0 },
            { "survive", 0 },
            { "hot", 0 },
            { "rocket", 1 },
            { "grenade", 0 },
            { "flash", 0 }
        };

        // Weapons that require 2 slots (big weapons)
        public static readonly Dictionary<string, bool> WeaponBig = new()
        {
            { "g19", false },
            { "burst", false },
            { "g18", false },
            { "edge", false },
            { "mup", false },
            { "m3", false },
            { "cqbr", true },
            { "lightning", false },
            { "raiden", true },
            { "mgl", false },
            { "knife", false },
            { "survive", false },
            { "hot", false },
            { "rocket", true },
            { "grenade", false },
            { "flash", false }
        };
    }
}



