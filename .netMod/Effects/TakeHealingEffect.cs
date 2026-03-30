using System;
using System.Threading.Tasks;
using REFrameworkNET;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Take a random healing item from the player inventory.
    /// </summary>
    public class TakeHealingEffect : EffectBase
    {
        public override string Code => "takeheal";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                if (!gameState.TryTakeRandomHealingItem(out var itemId))
                {
                    Logger.LogInfo("TakeHealingEffect: No healing items found to remove");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                Logger.LogInfo($"TakeHealingEffect: Removed healing item ID {itemId}");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"TakeHealingEffect: Error removing healing item - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



