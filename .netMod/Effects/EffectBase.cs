using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Base class for all effects with common functionality
    /// </summary>
    public abstract class EffectBase : IEffect
    {
        public abstract string Code { get; }

        /// <summary>
        /// Execute the effect with state checking
        /// </summary>
        public async Task<int> ExecuteAsync(GameState gameState, CCRequest request)
        {
            // Update game state before checking
            gameState.Update();

            // Check if game is paused - return RETRY instead of Unavailable
            if (!gameState.IsGameReady)
            {
                Logger.LogInfo($"{Code}: Cannot execute - game is paused (menu/cutscene/loading)");
                return CCStatus.Retry;
            }

            // Check if effect can be executed
            if (!CanExecute(gameState, request))
            {
                Logger.LogInfo($"{Code}: Cannot execute - preconditions not met");
                return CCStatus.Unavailable; // Use Unavailable for precondition failures
            }

            try
            {
                return await OnExecute(gameState, request);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"{Code}: Error executing effect - {ex.Message}");
                return CCStatus.Retry;
            }
        }

        /// <summary>
        /// Check if effect can be executed (override for custom checks)
        /// </summary>
        protected virtual bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.CanExecuteEffect(Code);
        }

        /// <summary>
        /// Implement the actual effect logic
        /// </summary>
        protected abstract Task<int> OnExecute(GameState gameState, CCRequest request);
    }
}



