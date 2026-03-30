using System.Threading.Tasks;
using RE9DotNet_CC;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Interface for all Crowd Control effects
    /// </summary>
    public interface IEffect
    {
        /// <summary>
        /// Effect code name (e.g., "heal", "hurt", "kill")
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Execute the effect
        /// </summary>
        /// <param name="gameState">Current game state</param>
        /// <param name="request">Crowd Control request</param>
        /// <returns>Response status code</returns>
        Task<int> ExecuteAsync(GameState gameState, CCRequest request);
    }
}



