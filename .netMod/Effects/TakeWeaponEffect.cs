using System;
using System.Threading.Tasks;
using REFrameworkNET;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Take the currently equipped weapon from the player.
    /// </summary>
    public class TakeWeaponEffect : EffectBase
    {
        public override string Code => "takeweap";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return RE3CrowdControlPlugin.AllowWeaponManipulation(gameState)
                && gameState.IsGameReady
                && gameState.HasWeaponEquipped();
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                if (!gameState.TryRemoveEquippedWeapon())
                {
                    Logger.LogInfo("TakeWeaponEffect: Failed to remove equipped weapon");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                Logger.LogInfo("TakeWeaponEffect: Removed equipped weapon");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"TakeWeaponEffect: Error removing weapon - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



