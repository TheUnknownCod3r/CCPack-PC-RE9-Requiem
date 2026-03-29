using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets player scale to giant (1.5x) for a duration
    /// </summary>
    public class GiantPlayerEffect : EffectBase
    {
        public override string Code => "giant";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and scale effect isn't already active
            return gameState.IsGameReady && !gameState.IsScaleActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("GiantPlayerEffect: Executing giant player effect");

            // Store request ID for sending Stopped response later
            gameState.SetScaleRequestId(request.Id, request.RequestID);
            
            // Start scale effect with giant scale (1.5 like LUA version)
            if (gameState.StartScale(1.5f, request.Duration, isGiant: true))
            {
                Logger.LogInfo($"GiantPlayerEffect: Started giant player mode - Scale: 1.5, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("GiantPlayerEffect: Failed to start giant player mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



