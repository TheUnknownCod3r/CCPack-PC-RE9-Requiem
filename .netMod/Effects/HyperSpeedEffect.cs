using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that greatly increases player movement speed
    /// </summary>
    public class HyperSpeedEffect : EffectBase
    {
        public override string Code => "hyper";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if speed effect is not already active (mutually exclusive)
            return !gameState.IsSpeedActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("HyperSpeedEffect: Executing hyper speed effect");

            // Store request ID for sending Stopped response later
            gameState.SetSpeedRequestId(request.Id, request.RequestID);
            
            if (gameState.StartSpeed(8.0f, request.Duration, true))
            {
                Logger.LogInfo($"HyperSpeedEffect: Started hyper speed mode - Speed: 8.0x, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("HyperSpeedEffect: Failed to start hyper speed mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



