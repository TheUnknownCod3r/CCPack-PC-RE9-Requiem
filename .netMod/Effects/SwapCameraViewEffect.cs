using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Temporarily flips <c>app.PlayerContext</c> TPS/FPS (<c>CurrentViewMode</c> / <c>CurrentBodyViewMode</c>).
    /// </summary>
    public class SwapCameraViewEffect : EffectBase
    {
        public override string Code => "swapview";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady
                   && gameState.IsPlayerAlive
                   && !gameState.IsViewModeSwapActive
                   && gameState.TryGetPlayerContextManaged() != null;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            int durationMs = request.Duration > 0 ? request.Duration : 30_000;
            gameState.SetViewModeSwapRequestId(request.Id, request.RequestID);

            if (gameState.StartViewModeSwap(durationMs))
            {
                Logger.LogInfo($"{Code}: Swapped view mode for {durationMs}ms");
                return Task.FromResult((int)CCStatus.Success);
            }

            Logger.LogError($"{Code}: Failed to start view mode swap");
            return Task.FromResult((int)CCStatus.Retry);
        }
    }
}
