using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that makes all enemies tiny (0.33x scale) for a duration
    /// </summary>
    public class EnemyTinyEffect : EffectBase
    {
        public override string Code => "etiny";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and enemy size effect isn't already active
            return gameState.IsGameReady && !gameState.IsEnemySizeActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyTinyEffect: Executing tiny enemies effect");

            // Store request ID for sending Stopped response later
            gameState.SetEnemySizeRequestId(request.Id, request.RequestID);
            
            // Start enemy size effect with tiny scale (0.33x like LUA version)
            if (gameState.StartEnemySize(0.33f, request.Duration, isGiant: false))
            {
                Logger.LogInfo($"EnemyTinyEffect: Started tiny enemies mode - Scale: 0.33, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("EnemyTinyEffect: Failed to start tiny enemies mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



