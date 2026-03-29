using System.Diagnostics.CodeAnalysis;
using ConnectorLib.SimpleTCP;
using CrowdControl.Common;
using ConnectorType = CrowdControl.Common.ConnectorType;

namespace CrowdControl.Games.Packs.ResidentEvil3;

public class ResidentEvil3 : SimpleTCPPack<SimpleTCPServerConnector>
{
    public override string Host => "127.0.0.1";

    public override ushort Port => 64772;

    [SuppressMessage("CrowdControl.PackMetadata", "CC1009:Message Format Property")]
    public override ISimpleTCPPack.MessageFormatType MessageFormat => ISimpleTCPPack.MessageFormatType.CrowdControlLegacy;

    public ResidentEvil3(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler) { }

    public override Game Game { get; } = new("Resident Evil 3", "ResidentEvil3", "PC", ConnectorType.SimpleTCPServerConnector);

    public override EffectList Effects { get; } = new List<Effect>
    {
        //General Effects

        new("Reduce HP to 1", "onehp") { Category = "Health" },
        new("Heal Player", "heal"){ Category = "Health" },
        new("Damage Player", "damage"){ Category = "Health" },
        new("Full Heal Player", "full"){ Category = "Health" },

        new("Reduce Enemy HP to 1", "eonehp") { Category = new string[]{"Health", "Enemies" } },
        new("Heal Enemies", "eheal"){ Category = new string[]{"Health", "Enemies" } },
        new("Damage Enemies", "edamage"){ Category = new string[]{"Health", "Enemies" } },
        new("Full Heal Enemies", "efull"){ Category = new string[]{"Health", "Enemies" } },


        new("Give Green Herb", "giveheal_herbg"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Give Blue Herb", "giveheal_herbb"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Give Red Herb", "giveheal_herbr"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Give Green+Green Herb", "giveheal_herbgg"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Give Green+Blue Herb", "giveheal_herbgb"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Give Green+Red Herb", "giveheal_herbgr"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Give First Aid Spray", "giveheal_spray"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Give Green+Red+Blue Herb", "giveheal_herbgrb"){ Category = new string[]{"Give Items","Healing Items"} },

        new("Upgrade Healing Items", "healup"){ Category = new string[]{"Give Items","Healing Items"} },
        new("Downgrade Healing Items", "healdown"){ Category = new string[]{"Take Items","Healing Items"} },

        new("Take Healing Item", "takeheal"){ Category = new string[]{ "Take Items", "Healing Items"} },
        new("Take Current Weapon", "takeweap"){ Category = new string[]{ "Take Items", "Weapons"} },
        new("Take Ammo", "takeammo"){ Category = new string[]{ "Take Items", "Ammo"} },

        new("Unequip Weapon", "unequipweap"){ Category = new string[]{"Ammo","Weapons"} },
        new("Fill Weapon Mag", "fillweap"){ Category = new string[]{"Ammo","Weapons"} },
        new("Empty Weapon Mag", "emptyweap"){ Category = new string[]{"Ammo","Weapons"} },

        new("Give G19 Handgun", "giveweap_g19"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give G18 Burst Handgun", "giveweap_burst"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give G18 Handgun", "giveweap_g18"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Samurai Edge", "giveweap_edge"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Infinite MUP", "giveweap_mup"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give M3 Shotgun", "giveweap_m3"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give CQBR Assault Rifle", "giveweap_cqbr"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Lightning Hawk", "giveweap_lightning"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give RAIDEN", "giveweap_raiden"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Survival Knife", "giveweap_survive"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Combat Knife", "giveweap_knife"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give HOT DOGGER", "giveweap_hot"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Hand Grenade", "giveweap_grenade"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Flash Grenade", "giveweap_flash"){ Category = new string[]{"Give Items","Weapons"} },
        new("Give Infinite Rocket Launcher", "giveweap_rocket"){ Category = new string[]{"Give Items","Weapons"} },

        new("Give Handgun Bullets", "giveammo_handgun"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Shotgun Shells", "giveammo_shotgun"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Assault Rifle Bullets", "giveammo_submachine"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give MAG Ammo", "giveammo_mag"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Mine Rounds", "giveammo_mine"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Explosive Rounds", "giveammo_explode"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Acid Rounds", "giveammo_acid"){ Category = new string[]{"Give Items","Ammo"} },
        new("Give Flame Rounds", "giveammo_flame"){ Category = new string[]{"Give Items","Ammo"} },


        new("One Hit KO", "ohko") { Category = "Health", Duration = 30 },
        new("Invincible", "invul") { Category = "Health", Duration = 30 },

        new("Wide Camera", "wide") { Category = "Camera", Duration = 30 },
        new("Narrow Camera", "narrow") { Category = "Camera", Duration = 30 },


        new("Giant Player", "giant") { Category = "Player Size", Duration = 30 },
        new("Tiny Player", "tiny") { Category = "Player Size", Duration = 30 },

        new("Giant Enemies", "egiant") { Category = new string[]{ "Enemy Size", "Enemies" }, Duration = 30 },
        new("Tiny Enemies", "etiny") { Category = new string[]{ "Enemy Size", "Enemies" }, Duration = 30 },

        //new("Fast Player", "fast") { Category = "Player Speed", Duration = 30 },
        //new("Slow Player", "slow") { Category = "Player Speed", Duration = 30 },
        //new("Hyper Speed Player", "hyper") { Category = "Player Speed", Duration = 15 },

        new("Fast Enemies", "efast") { Category = new string[]{"Enemy Speed", "Enemies" }, Duration = 15 },
        new("Slow Enemies", "eslow") { Category = new string[]{"Enemy Speed", "Enemies" }, Duration = 15 },


        new("Spawn Male Zombie", "spawn_em0000") { Category = "Spawn Enemies" },
        new("Spawn Female Zombie", "spawn_em0100") { Category = "Spawn Enemies" },
        new("Spawn Big Zombie", "spawn_em0200") { Category = "Spawn Enemies" },
        new("Spawn Licker", "spawn_em3000") { Category = "Spawn Enemies" },
        new("Spawn Zombie Dog", "spawn_em4000") { Category = "Spawn Enemies" },

        new("Spawn Hunter", "spawn_em3300") { Category = "Spawn Enemies" },
        new("Spawn Hunter Gamma", "spawn_em3400") { Category = "Spawn Enemies" },

        new("Spawn Nemesis", "spawn_em9000") { Category = "Spawn Enemies" },

        new("Fix Spawns", "fixspawn") { Category = "Admin" },


    };
}