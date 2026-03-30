using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that makes all enemies move fast (2.0x speed) for a duration
    /// </summary>
    public class EnemyFastSpeedEffect : EffectBase
    {
        public override string Code => "efast";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and enemy speed effect isn't already active
            return gameState.IsGameReady && !gameState.IsEnemySpeedActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("EnemyFastSpeedEffect: Executing fast enemies effect");

            // Store request ID for sending Stopped response later
            gameState.SetEnemySpeedRequestId(request.Id, request.RequestID);
            
            // Start enemy speed effect with fast speed (2.0x like LUA version)
            if (gameState.StartEnemySpeed(2.0f, request.Duration, isFast: true))
            {
                Logger.LogInfo($"EnemyFastSpeedEffect: Started fast enemies mode - Speed: 2.0x, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("EnemyFastSpeedEffect: Failed to start fast enemies mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



