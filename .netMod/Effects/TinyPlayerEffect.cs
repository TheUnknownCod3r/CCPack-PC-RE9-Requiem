using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets player scale to tiny (0.33x) for a duration
    /// </summary>
    public class TinyPlayerEffect : EffectBase
    {
        public override string Code => "tiny";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and scale effect isn't already active
            return gameState.IsGameReady && !gameState.IsScaleActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("TinyPlayerEffect: Executing tiny player effect");

            // Store request ID for sending Stopped response later
            gameState.SetScaleRequestId(request.Id, request.RequestID);
            
            // Start scale effect with tiny scale (0.33 like LUA version)
            if (gameState.StartScale(0.33f, request.Duration, isGiant: false))
            {
                Logger.LogInfo($"TinyPlayerEffect: Started tiny player mode - Scale: 0.33, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("TinyPlayerEffect: Failed to start tiny player mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



