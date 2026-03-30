using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that fully heals the player to maximum health
    /// </summary>
    public class FullHealEffect : EffectBase
    {
        public override string Code => "full";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if player is alive and not at max health
            // Also check that OHKO and Invincibility aren't active
            return gameState.IsPlayerAlive 
                && gameState.CurrentHealth < gameState.MaxHealth 
                && !gameState.IsOneHitKO 
                && !gameState.IsInvulnerable;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("FullHealEffect: Executing full heal effect");

            // Set health to max
            if (gameState.SetHealth(gameState.MaxHealth,1))
            {
                Logger.LogInfo($"FullHealEffect: Fully healed player to {gameState.MaxHealth} HP");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("FullHealEffect: Failed to set health to max");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



