using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that reduces player HP to 1
    /// </summary>
    public class OneHPEffect : EffectBase
    {
        public override string Code => "onehp";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Block while invulnerable or OHKO is active
            if (gameState.IsInvulnerable || gameState.IsOneHitKO)
            {
                Logger.LogInfo("OneHPEffect: Blocked because invulnerability/OHKO is active");
                return false;
            }

            // Can execute if player has more than 1 HP
            return gameState.CurrentHealth > 1 && gameState.IsPlayerAlive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("OneHPEffect: Executing one HP effect");

            if (gameState.IsInvulnerable || gameState.IsOneHitKO)
            {
                Logger.LogInfo("OneHPEffect: Invulnerability/OHKO active, skipping");
                return Task.FromResult((int)CCStatus.Unavailable);
            }

            // Set health to 1
            if (gameState.SetHealth(1))
            {
                Logger.LogInfo("OneHPEffect: Reduced player HP to 1");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("OneHPEffect: Failed to set health to 1");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



