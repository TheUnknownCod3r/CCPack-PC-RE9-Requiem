using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Heal player effect - restores 25% of max health, capped at full
    /// </summary>
    public class HealEffect : EffectBase
    {
        public override string Code => "heal";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.CanHeal();
        }

        protected override async Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("HealEffect: Executing heal effect");

            var currentHP = gameState.CurrentHealth;
            var maxHP = gameState.MaxHealth;
            var healAmount = maxHP / 4.0f;
            float newHealth = currentHP + healAmount;
            if (newHealth > maxHP)
            {
                newHealth = maxHP;
            }

            // Set new health
            if (gameState.SetHealth(newHealth))
            {
                Logger.LogInfo($"HealEffect: Healed player from {currentHP:F1} to {newHealth:F1} HP");
                await Task.CompletedTask;
                return CCStatus.Success;
            }

            Logger.LogError("HealEffect: Failed to set health");
            return CCStatus.Retry;
        }
    }
}



