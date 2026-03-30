using System;
using System.Threading.Tasks;
using REFrameworkNET;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Downgrade all healing items to their lowest form.
    /// </summary>
    public class DowngradeHealingItemsEffect : EffectBase
    {
        public override string Code => "healdown";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                int changed = gameState.DowngradeHealingItems();
                if (changed <= 0)
                {
                    Logger.LogInfo("DowngradeHealingItemsEffect: No healing items to downgrade");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                Logger.LogInfo($"DowngradeHealingItemsEffect: Downgraded {changed} healing items");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"DowngradeHealingItemsEffect: Error downgrading healing items - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



