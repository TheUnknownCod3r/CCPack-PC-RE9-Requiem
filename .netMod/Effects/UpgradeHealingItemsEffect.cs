using System;
using System.Threading.Tasks;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Upgrade all healing items to their strongest form.
    /// </summary>
    public class UpgradeHealingItemsEffect : EffectBase
    {
        public override string Code => "healup";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                int changed = gameState.UpgradeHealingItems();
                if (changed <= 0)
                {
                    Logger.LogInfo("UpgradeHealingItemsEffect: No healing items to upgrade");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                Logger.LogInfo($"UpgradeHealingItemsEffect: Upgraded {changed} healing items");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"UpgradeHealingItemsEffect: Error upgrading healing items - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}


