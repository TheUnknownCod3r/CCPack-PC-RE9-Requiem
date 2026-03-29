using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that makes the player invincible for a duration by continuously healing to max HP
    /// </summary>
    public class InvincibilityEffect : EffectBase
    {
        public override string Code => "invul";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if player is alive
            // Also check that OHKO and Invincibility aren't already active
            return gameState.IsPlayerAlive && !gameState.IsOneHitKO && !gameState.IsInvulnerable;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("InvincibilityEffect: Executing invincibility effect");

            // Store original health and activate invincibility mode
            float originalHealth = gameState.CurrentHealth;
            
            // Store request ID for sending Stopped response later
            gameState.SetInvincibilityRequestId(request.Id, request.RequestID);
            
            if (gameState.StartInvincibility(originalHealth, request.Duration))
            {
                Logger.LogInfo($"InvincibilityEffect: Started invincibility mode - stored health: {originalHealth}, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("InvincibilityEffect: Failed to start invincibility mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



