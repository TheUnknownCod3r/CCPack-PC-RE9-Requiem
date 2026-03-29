using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Base class for give weapon effects
    /// </summary>
    public class GiveWeaponEffect : GiveItemEffectBase
    {
        private readonly string _weaponKey;
        private readonly string _effectCode;

        public GiveWeaponEffect(string weaponKey, string effectCode)
        {
            _weaponKey = weaponKey;
            _effectCode = effectCode;
        }

        public override string Code => _effectCode;

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                // Check if weapon is available for current character
                if (!gameState.IsWeaponAvailableForCharacter(_weaponKey))
                {
                    Logger.LogInfo($"{Code}: Weapon '{_weaponKey}' is not available for current character");
                    return Task.FromResult((int)CCStatus.Unavailable);
                }

                if (!ItemData.Weapons.TryGetValue(_weaponKey, out int weaponId))
                {
                    Logger.LogError($"{Code}: Invalid weapon key '{_weaponKey}'");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                bool isBig = ItemData.WeaponBig.TryGetValue(_weaponKey, out bool big) && big;
                int ammoAmount = ItemData.WeaponAmmo.TryGetValue(_weaponKey, out int ammo) ? ammo : 0;

                if (gameState.AddWeapon(_weaponKey, weaponId, isBig, ammoAmount))
                {
                    Logger.LogInfo($"{Code}: Added weapon {_weaponKey} (ID: {weaponId}, Big: {isBig}, Ammo: {ammoAmount})");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo($"{Code}: Failed to add weapon - no space or already exists");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{Code}: Error adding weapon - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



