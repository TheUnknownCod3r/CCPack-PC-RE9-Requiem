using System.Threading.Tasks;
using REFrameworkNET;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Applies poison status to the player.
    /// </summary>
    public class PoisonEffect : EffectBase
    {
        public override string Code => "poison";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (!gameState.TryApplyPoisonStatus())
            {
                Logger.LogInfo("PoisonEffect: Failed to apply poison status");
                return Task.FromResult((int)CCStatus.Retry);
            }

            Logger.LogInfo("PoisonEffect: Poison status applied");
            return Task.FromResult((int)CCStatus.Success);
        }
    }
}


