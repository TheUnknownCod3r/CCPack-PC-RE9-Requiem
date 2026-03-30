using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that fully heals all enemies
    /// </summary>
    public class EnemyFullHealEffect : EffectBase
    {
        public override string Code => "efull";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyFullHealEffect: Executing enemy full heal effect");
            return Task.FromResult(gameState.FullHealAllEnemies() ? (int)CCStatus.Success : (int)CCStatus.Retry);
        }
    }
}



