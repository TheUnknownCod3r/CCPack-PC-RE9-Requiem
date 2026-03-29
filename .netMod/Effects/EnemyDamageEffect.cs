using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that damages all enemies by 25% of their max HP
    /// </summary>
    public class EnemyDamageEffect : EffectBase
    {
        public override string Code => "edamage";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyDamageEffect: Executing enemy damage effect");
            return Task.FromResult(gameState.DamageAllEnemies() ? (int)CCStatus.Success : (int)CCStatus.Retry);
        }
    }
}



