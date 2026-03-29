using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Hurt/Damage player effect - reduces health by 25% of max health
    /// </summary>
    public class HurtEffect : EffectBase
    {
        public override string Code => "damage";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.CanHurt();
        }

        protected override async Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("HurtEffect: Executing damage effect");

            // Get current health before modification
            var currentHP = gameState.CurrentHealth;
            var maxHP = gameState.MaxHealth;

            // Calculate damage amount (25% of max health, like LUA version)
            var damageAmount = maxHP / 4.0f;

            // Check if health is too low (like LUA version checks BEFORE modifying)
            // LUA version: if hp < (damageAmount / 2) then return Retry
            if (currentHP < (damageAmount / 2.0f))
            {
                Logger.LogInfo("HurtEffect: Health too low, skipping");
                return CCStatus.Retry;
            }

            // Calculate new health - ensure it never goes below 1
            // LUA version: hp = hp - del, but we need to ensure minimum of 1
            float newHealth = currentHP - damageAmount;
            
            // Ensure health never goes below 1 (player should always be alive after damage)
            if (newHealth < 1.0f)
            {
                Logger.LogInfo($"HurtEffect: Calculated health ({newHealth:F1}) would be below 1, setting to 1 instead");
                newHealth = 1.0f;
            }
            
            // Double-check: never set health to 0
            if (newHealth <= 0)
            {
                Logger.LogInfo("HurtEffect: Health would be 0 or below, setting to 1 instead");
                newHealth = 1.0f;
            }

            // Set new health
            if (gameState.SetHealth(damageAmount,2))
            {
                Logger.LogInfo($"HurtEffect: Damaged player from {currentHP:F1} to {newHealth:F1} HP");
                await Task.CompletedTask;
                return CCStatus.Success;
            }

            Logger.LogError("HurtEffect: Failed to set health");
            return CCStatus.Retry;
        }
    }
}



