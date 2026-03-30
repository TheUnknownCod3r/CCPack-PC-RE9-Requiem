using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that increases player movement speed
    /// </summary>
    public class FastSpeedEffect : EffectBase
    {
        public override string Code => "fast";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if speed effect is not already active (mutually exclusive)
            return !gameState.IsSpeedActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("FastSpeedEffect: Executing fast speed effect");

            // Store request ID for sending Stopped response later
            gameState.SetSpeedRequestId(request.Id, request.RequestID);
            
            if (gameState.StartSpeed(3.0f, request.Duration, true))
            {
                Logger.LogInfo($"FastSpeedEffect: Started fast speed mode - Speed: 3.0x, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("FastSpeedEffect: Failed to start fast speed mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



