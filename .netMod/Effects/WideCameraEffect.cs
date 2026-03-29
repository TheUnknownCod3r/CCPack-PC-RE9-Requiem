using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets camera FOV to wide (130 degrees) for a duration
    /// </summary>
    public class WideCameraEffect : EffectBase
    {
        public override string Code => "wide";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and FOV effect isn't already active
            return gameState.IsGameReady && !gameState.IsFOVActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("WideCameraEffect: Executing wide camera effect");

            // Store request ID for sending Stopped response later
            gameState.SetFOVRequestId(request.Id, request.RequestID);
            
            // Start FOV effect with wide FOV (130.0 like LUA version)
            if (gameState.StartFOV(130.0f, request.Duration))
            {
                Logger.LogInfo($"WideCameraEffect: Started wide camera mode - FOV: 130.0, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("WideCameraEffect: Failed to start wide camera mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



