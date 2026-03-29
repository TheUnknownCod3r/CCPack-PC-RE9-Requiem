using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that makes all enemies giant (1.5x scale) for a duration
    /// </summary>
    public class EnemyGiantEffect : EffectBase
    {
        public override string Code => "egiant";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and enemy size effect isn't already active
            return gameState.IsGameReady && !gameState.IsEnemySizeActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyGiantEffect: Executing giant enemies effect");

            // Store request ID for sending Stopped response later
            gameState.SetEnemySizeRequestId(request.Id, request.RequestID);
            
            // Start enemy size effect with giant scale (1.5x like LUA version)
            if (gameState.StartEnemySize(1.5f, request.Duration, isGiant: true))
            {
                Logger.LogInfo($"EnemyGiantEffect: Started giant enemies mode - Scale: 1.5, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("EnemyGiantEffect: Failed to start giant enemies mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



