using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Dynamic effect class that handles all ammo give effects
    /// Extracts the ammo key from the effect code (e.g., "giveammo_handgun" -> "handgun")
    /// </summary>
    public class GiveAmmoEffect : GiveItemEffectBase
    {
        private readonly string _effectCode;
        private string? _ammoKey = null;

        public GiveAmmoEffect(string effectCode)
        {
            _effectCode = effectCode;
        }

        public override string Code => _effectCode;

        private string GetAmmoKey()
        {
            if (_ammoKey != null)
                return _ammoKey;

            // Extract ammo key from code (e.g., "giveammo_handgun" -> "handgun")
            const string prefix = "giveammo_";
            if (_effectCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _ammoKey = _effectCode.Substring(prefix.Length);
            }
            else
            {
                _ammoKey = _effectCode; // Fallback to full code
            }

            return _ammoKey;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                string ammoKey = GetAmmoKey();
                
                // Check if ammo is available for current character
                if (!gameState.IsAmmoAvailableForCharacter(ammoKey))
                {
                    Logger.LogInfo($"{Code}: Ammo '{ammoKey}' is not available for current character");
                    return Task.FromResult((int)CCStatus.Unavailable);
                }
                
                if (!ItemData.AmmoItems.TryGetValue(ammoKey, out int itemId))
                {
                    Logger.LogError($"{Code}: Invalid ammo key '{ammoKey}'");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                int amount = ItemData.AmmoAmounts.TryGetValue(ammoKey, out int ammoAmount) ? ammoAmount : 5;

                if (gameState.AddAmmoItem(itemId, amount))
                {
                    Logger.LogInfo($"{Code}: Added ammo {ammoKey} (ID: {itemId}, Amount: {amount})");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo($"{Code}: Failed to add ammo - no space");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{Code}: Error adding ammo - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



