using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that decreases player movement speed
    /// </summary>
    public class SlowSpeedEffect : EffectBase
    {
        public override string Code => "slow";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if speed effect is not already active (mutually exclusive)
            return !gameState.IsSpeedActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("SlowSpeedEffect: Executing slow speed effect");

            // Store request ID for sending Stopped response later
            gameState.SetSpeedRequestId(request.Id, request.RequestID);
            
            if (gameState.StartSpeed(0.33f, request.Duration, false))
            {
                Logger.LogInfo($"SlowSpeedEffect: Started slow speed mode - Speed: 0.33x, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("SlowSpeedEffect: Failed to start slow speed mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



