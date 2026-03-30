using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect that sets camera FOV to narrow (50 degrees) for a duration
    /// </summary>
    public class NarrowCameraEffect : EffectBase
    {
        public override string Code => "narrow";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Can execute if game is ready and FOV effect isn't already active
            return gameState.IsGameReady && !gameState.IsFOVActive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("NarrowCameraEffect: Executing narrow camera effect");

            // Store request ID for sending Stopped response later
            gameState.SetFOVRequestId(request.Id, request.RequestID);
            
            // Start FOV effect with narrow FOV (50.0 like LUA version)
            if (gameState.StartFOV(50.0f, request.Duration))
            {
                Logger.LogInfo($"NarrowCameraEffect: Started narrow camera mode - FOV: 50.0, duration: {request.Duration}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError("NarrowCameraEffect: Failed to start narrow camera mode");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}



