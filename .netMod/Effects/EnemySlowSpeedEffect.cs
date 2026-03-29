using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that makes all enemies move slow (0.5x speed) for a duration
    /// </summary>
    public class EnemySlowSpeedEffect : EffectBase
    {
        public override string Code => "eslow";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and enemy speed effect isn't already active
            return gameState.IsGameReady && !gameState.IsEnemySpeedActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemySlowSpeedEffect: Executing slow enemies effect");

            // Store request ID for sending Stopped response later
            gameState.SetEnemySpeedRequestId(request.Id, request.RequestID);
            
            // Start enemy speed effect with slow speed (0.5x like LUA version)
            if (gameState.StartEnemySpeed(0.5f, request.Duration, isFast: false))
            {
                Logger.LogInfo($"EnemySlowSpeedEffect: Started slow enemies mode - Speed: 0.5x, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("EnemySlowSpeedEffect: Failed to start slow enemies mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



