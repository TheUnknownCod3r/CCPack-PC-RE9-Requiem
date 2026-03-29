using System.Threading.Tasks;
using REFrameworkNET;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Clears burn/fire status from the player.
    /// </summary>
    public class CureBurnEffect : EffectBase
    {
        public override string Code => "cureburn";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (!gameState.TryClearBurnStatus())
            {
                Logger.LogInfo("CureBurnEffect: Failed to clear burn status");
                return Task.FromResult((int)CCStatus.Retry);
            }

            Logger.LogInfo("CureBurnEffect: Burn status cleared");
            return Task.FromResult((int)CCStatus.Success);
        }
    }
}


