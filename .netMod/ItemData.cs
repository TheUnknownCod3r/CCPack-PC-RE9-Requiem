using System.Collections.Generic;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Item / weapon id mappings for RE9 (numeric ids + string item names where used).
    /// </summary>
    public static class ItemData
    {
        // Healing items
        public static readonly Dictionary<string, int> HealingItems = new()
        {
            { "herbg", 0 },      // it00_00_000
            { "herbgg", 100 },   // it00_00_100
            { "herbggg", 200 },  // it00_00_200
            { "med", 1000 }      // it00_01_000
        };

        /// <summary>
        /// RE9ItemID-style catalog numbers are not the same as <c>app.ItemID</c> enum values in memory.
        /// <c>canMergeOrAdd(ItemID, …)</c> / <c>ItemStockData.ctor(ItemID, …)</c> need the static field token (resolved at runtime).
        /// </summary>
        public static readonly Dictionary<int, string> CatalogNumericToItemIdField = new()
        {
            { 0, "it00_00_000" },
            { 100, "it00_00_100" },
            { 200, "it00_00_200" },
            { 1000, "it00_01_000" },
            { 4000000, "it40_00_000" },
            { 4001000, "it40_01_000" },
            { 4002000, "it40_02_000" },
            { 4003000, "it40_03_000" },
            { 4005000, "it40_05_000" },
            { 2000000, "it20_00_000" },
            { 2000001, "it20_00_001" },
            { 2000002, "it20_00_002" },
            { 2000004, "it20_00_004" },
            { 2000005, "it20_00_005" },
            { 5000012, "it50_00_012" }
        };
        public static readonly Dictionary<string, string> DamageItems = new()//re9
        {
            { "molotov", "it20_00_002" },
            { "acid", "it20_00_005" }
        };
        /// <summary>Numeric item IDs for <see cref="GameState.AddAmmoItem"/> (from RE9ItemID's.txt / game data).</summary>
        public static readonly Dictionary<string, int> AmmoItems = new()
        {
            { "handgun", 4000000 },
            { "shotgun", 4001000 },
            { "submachine", 4003000 },
            { "mag", 4002000 },
            { "large", 4005000 },
            { "grenade", 2000000 },
            { "grenade_stack", 2000004 },
            { "molotov", 2000002 },
            { "acid", 2000005 },
            { "ink_tin", 5000012 }
        };

        /// <summary>
        /// Throwable sub-weapons are normal inventory stackables (e.g. it20_00_000), not internal WeaponID 65/66.
        /// Matches chaos mod ITEM_REWARD_DEFS.hand_grenade / re9_item_adder mergeOrAdd flow.
        /// </summary>
        public static readonly Dictionary<string, int> ThrowableInventoryItemIds = new()
        {
            { "grenade", 2000000 }, // it20_00_000 Hand Grenade
            { "flash", 2000001 }    // it20_00_001 (see reference app.ItemID)
        };

        public static readonly Dictionary<string, int> AmmoAmounts = new()
        {
            { "handgun", 15 },
            { "shotgun", 6 },
            { "submachine", 40 },
            { "mag", 10 },
            { "large", 8 },
            { "grenade", 2 },
            { "grenade_stack", 1 },
            { "molotov", 2 },
            { "acid", 2 },
            { "ink_tin", 1 }
        };

        public static readonly Dictionary<string, string> GraceItems = new()
        {
            { "it99_50_001", "Hemolytic Injector" },
            { "it99_06_000", "Blood Collector" },
            { "it99_01_000", "Hip Pouch"},
            { "it99_02_002", "Steroids"},
            { "it10_20_008", "Kotetsu"},
            { "it10_03_003", "Freya's Needle"},
            { "it99_07_002", "Rugged Rookie Charm"}
        };
        // Weapon type ids (InventoryManager / pack)
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
            // Grenade / flash: use ThrowableInventoryItemIds + mergeOrAdd as items, not these internal ids.
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



