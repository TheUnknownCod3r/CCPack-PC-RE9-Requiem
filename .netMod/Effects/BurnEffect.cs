using System.Threading.Tasks;
using REFrameworkNET;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Applies burn/fire status to the player.
    /// </summary>
    public class BurnEffect : EffectBase
    {
        public override string Code => "burn";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (!gameState.TryApplyBurnStatus())
            {
                Logger.LogInfo("BurnEffect: Failed to apply burn status");
                return Task.FromResult((int)CCStatus.Retry);
            }

            Logger.LogInfo("BurnEffect: Burn status applied");
            return Task.FromResult((int)CCStatus.Success);
        }
    }
}


