using System.Diagnostics.CodeAnalysis;
using ConnectorLib.SimpleTCP;
using CrowdControl.Common;
using ConnectorType = CrowdControl.Common.ConnectorType;

namespace CrowdControl.Games.Packs.ResidentEvilRequiemNET;

/// <summary>Crowd Control effect list for RE9 — keep codes in sync with <c>RE9CrowdControlPlugin</c>, <c>ItemData</c>, and <c>RE9ItemID's.txt</c>.</summary>
public class ResidentEvilRequiemNET : SimpleTCPPack<SimpleTCPServerConnector>
{
    public override string Host => "127.0.0.1";

    public override ushort Port => 58431;

    [SuppressMessage("CrowdControl.PackMetadata", "CC1009:Message Format Property")]
    public override ISimpleTCPPack.MessageFormatType MessageFormat => ISimpleTCPPack.MessageFormatType.CrowdControlLegacy;

    public ResidentEvilRequiemNET(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler) { }

    public override Game Game { get; } = new("RESIDENT EVIL requiem", "ResidentEvilRequiemNET", "PC", ConnectorType.SimpleTCPServerConnector);

    public override EffectList Effects { get; } = new List<Effect>
    {
        //General Effects
        //new("Kill Player", "kill") { Category = "Health" },

        new("Reduce HP to 1", "onehp") { Category = "Health" },
        new("Heal Player", "heal"){ Category = "Health" },
        new("Damage Player", "damage"){ Category = "Health" },
        new("Full Heal Player", "full"){ Category = "Health" },
        new("Reduce Enemy HP to 1", "eonehp") { Category = new string[]{"Health", "Enemies" } },
        new("Heal Enemies", "eheal"){ Category = new string[]{"Health", "Enemies" } },
        new("Damage Enemies", "edamage"){ Category = new string[]{"Health", "Enemies" } },
        new("Kill Enemies", "ekill"){ Category = new string[]{"Health", "Enemies" } },
        new("Full Heal Enemies", "efull"){ Category = new string[]{"Health", "Enemies" } },


        new("Give Green Herb", "giveheal_herbg"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Give Green+Green Herb", "giveheal_herbgg"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Give Green+Green+Green Herb", "giveheal_herbggg"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Give Med Injector", "giveheal_med"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Upgrade Healing Items", "healup"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Downgrade Healing Items", "healdown"){ Category = new string[]{"Take Items","Healing Items"} },

        new("Take Healing Item", "takeheal"){ Category = new string[]{ "Take Items", "Healing Items"} },
        new("Take Current Weapon", "takeweap"){ Category = new string[]{ "Take Items", "Weapons"} },
        new("Take Ammo", "takeammo"){ Category = new string[]{ "Take Items", "Ammo"} },

        new("Unequip Weapon", "unequipweap"){ Category = new string[]{"Ammo","Weapons"} },
        new("Fill Weapon Mag", "fillweap"){ Category = new string[]{"Ammo","Weapons"} },
        new("Empty Weapon Mag", "emptyweap"){ Category = new string[]{"Ammo","Weapons"} },

        // giveweap_* keys match ItemData.Weapons (in-game names differ from RE2/RE3 labels below).
        new("Give G19", "giveweap_g19"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Burst Handgun", "giveweap_burst"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give G18", "giveweap_g18"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Samurai Edge (EDGE)", "giveweap_edge"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give MUP", "giveweap_mup"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give M3", "giveweap_m3"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give CQBR", "giveweap_cqbr"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Lightning Hawk", "giveweap_lightning"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Raiden", "giveweap_raiden"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give MGL", "giveweap_mgl"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Combat Knife", "giveweap_knife"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Survival Knife", "giveweap_survive"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Hot Dogger", "giveweap_hot"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Rocket Launcher", "giveweap_rocket"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Hand Grenade", "giveweap_grenade"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Flash Grenade", "giveweap_flash"){ Category = new string[]{"Give Items","Weapons"} },

        // Ammo / throwables — IDs from RE9ItemID's.txt (see .netMod/ItemData.cs); not RE3 mine/RPG rounds.
        new("Give Handgun Ammo", "giveammo_handgun"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Shotgun Shells", "giveammo_shotgun"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Machine Gun Ammo", "giveammo_submachine"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give 12.7x55mm Ammo", "giveammo_mag"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Rifle Ammo", "giveammo_large"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Hand Grenade", "giveammo_grenade"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Stacked Hand Grenades", "giveammo_grenade_stack"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Molotov Cocktail", "giveammo_molotov"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Bottle of Acid", "giveammo_acid"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Ink Ribbon Tin", "giveammo_ink_tin"){ Category = new string[]{"Give Items","Misc Items"} },

        new("One Hit KO", "ohko") { Category = "Health", Duration = 30 },
        new("Invincible", "invul") { Category = "Health", Duration = 30 },

        new("Wide Camera", "wide") { Category = "Camera", Duration = 30 },
        new("Narrow Camera", "narrow") { Category = "Camera", Duration = 30 },
        new("Swap 1st/3rd Person Camera", "swapview") { Category = "Camera", Duration = 30 },


        new("Giant Player", "giant") { Category = "Player Size", Duration = 30 },
        new("Tiny Player", "tiny") { Category = "Player Size", Duration = 30 },

        new("Giant Enemies", "egiant") { Category = new string[]{ "Enemy Size", "Enemies" }, Duration = 30 },
        new("Tiny Enemies", "etiny") { Category = new string[]{ "Enemy Size", "Enemies" }, Duration = 30 },

        new("Fast Player", "fast") { Category = "Player Speed", Duration = 30 },
        new("Slow Player", "slow") { Category = "Player Speed", Duration = 30 },
        new("Hyper Speed Player", "hyper") { Category = "Player Speed", Duration = 15 },
        new("Invert Controls", "invert_controls") { Category = "Player", Duration = 15 },

        new("Fast Enemies", "efast") { Category = new string[]{"Enemy Speed", "Enemies" }, Duration = 15 },
        new("Slow Enemies", "eslow") { Category = new string[]{"Enemy Speed", "Enemies" }, Duration = 15 },


        // Spawn codes match RE9DotNet plugin / EnemySpawner (runtime prefab catalog); labels are IDs only — not RE3 enemy names.
        new("Spawn Enemy em0000", "spawn_em0000") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0020", "spawn_em0020") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0100", "spawn_em0100") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0200", "spawn_em0200") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0300", "spawn_em0300") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0400", "spawn_em0400") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0500", "spawn_em0500") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0600", "spawn_em0600") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0700", "spawn_em0700") { Category = "Spawn Enemies" },
        new("Spawn Enemy em0800", "spawn_em0800") { Category = "Spawn Enemies" },
        new("Spawn Enemy em1000", "spawn_em1000") { Category = "Spawn Enemies" },
        new("Spawn Enemy em2500", "spawn_em2500") { Category = "Spawn Enemies" },
        new("Spawn Enemy em2600", "spawn_em2600") { Category = "Spawn Enemies" },
        new("Spawn Enemy em2700", "spawn_em2700") { Category = "Spawn Enemies" },
        new("Spawn Enemy em3000", "spawn_em3000") { Category = "Spawn Enemies" },
        new("Spawn Enemy em4000", "spawn_em4000") { Category = "Spawn Enemies" },
        new("Spawn Enemy em3300", "spawn_em3300") { Category = "Spawn Enemies" },
        new("Cycle Costume", "cycle_costume") { Category = "Player" },


        //new("Fix Spawns", "fixspawn") { Category = "Admin" },

    };
}


