using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets player HP to 1 for a duration, then restores original health
    /// </summary>
    public class OHKOEffect : EffectBase
    {
        public override string Code => "ohko";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if player is alive and has more than 1 HP
            // Also check that OHKO and Invincibility aren't already active (mutually exclusive)
            return gameState.CurrentHealth > 1 
                && gameState.IsPlayerAlive 
                && !gameState.IsOneHitKO 
                && !gameState.IsInvulnerable;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("OHKOEffect: Executing one hit KO effect");

            // Store original health and activate OHKO mode
            float originalHealth = gameState.CurrentHealth;
            
            // Store request ID for sending Stopped response later
            gameState.SetOHKORequestId(request.Id, request.RequestID);
            
            if (gameState.StartOHKO(originalHealth, request.Duration))
            {
                Logger.LogInfo($"OHKOEffect: Started OHKO mode - stored health: {originalHealth}, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("OHKOEffect: Failed to start OHKO mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



