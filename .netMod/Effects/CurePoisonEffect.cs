using System.Threading.Tasks;
using REFrameworkNET;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Clears poison status from the player.
    /// </summary>
    public class CurePoisonEffect : EffectBase
    {
        public override string Code => "curepoison";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (!gameState.TryClearPoisonStatus())
            {
                Logger.LogInfo("CurePoisonEffect: Failed to clear poison status");
                return Task.FromResult((int)CCStatus.Retry);
            }

            Logger.LogInfo("CurePoisonEffect: Poison status cleared");
            return Task.FromResult((int)CCStatus.Success);
        }
    }
}


