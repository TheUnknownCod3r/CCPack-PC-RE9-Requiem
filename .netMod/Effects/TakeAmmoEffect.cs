using System;
using System.Threading.Tasks;
using REFrameworkNET;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Take a random ammo stack from the player inventory.
    /// </summary>
    public class TakeAmmoEffect : EffectBase
    {
        public override string Code => "takeammo";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return RE9CrowdControlPlugin.AllowWeaponManipulation(gameState)
                && gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                if (!gameState.TryTakeRandomAmmo(out var ammoKey, out var removedAmount))
                {
                    Logger.LogInfo("TakeAmmoEffect: No ammo found to remove");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                Logger.LogInfo($"TakeAmmoEffect: Removed {removedAmount} {ammoKey ?? "ammo"}");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"TakeAmmoEffect: Error removing ammo - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



