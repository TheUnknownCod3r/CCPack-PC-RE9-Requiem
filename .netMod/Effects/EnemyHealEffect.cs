using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that heals all enemies by 25% of their max HP
    /// </summary>
    public class EnemyHealEffect : EffectBase
    {
        public override string Code => "eheal";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyHealEffect: Executing enemy heal effect");
            return Task.FromResult(gameState.HealAllEnemies() ? (int)CCStatus.Success : (int)CCStatus.Retry);
        }
    }
}



