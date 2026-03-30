using System.Threading.Tasks;
using REFrameworkNET;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Applies the healing-over-time (doping) status to the player.
    /// </summary>
    public class HealStatusEffect : EffectBase
    {
        public override string Code => "healstatus";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (!gameState.TryApplyDopingStatus())
            {
                Logger.LogInfo("HealStatusEffect: Failed to apply healing status");
                return Task.FromResult((int)CCStatus.Retry);
            }

            Logger.LogInfo("HealStatusEffect: Healing status applied");
            return Task.FromResult((int)CCStatus.Success);
        }
    }
}


