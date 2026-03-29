using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets all enemies' HP to 1
    /// </summary>
    public class EnemyOneHPEffect : EffectBase
    {
        public override string Code => "eonehp";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyOneHPEffect: Executing enemy one HP effect");
            return Task.FromResult(gameState.SetAllEnemiesToOneHP() ? (int)CCStatus.Success : (int)CCStatus.Retry);
        }
    }
}



