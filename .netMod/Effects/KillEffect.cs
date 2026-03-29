using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Kill player effect - sets health to 0
    /// </summary>
    public class KillEffect : EffectBase
    {
        public override string Code => "kill";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Use the standard CanKill check which handles invincibility blocking
            // but allows kill during OHKO
            if (!gameState.CanKill())
            {
                return false;
            }
            
            // For kill effect, be more lenient - try to execute even if state detection is uncertain
            // Only block if we're certain the player is already dead
            gameState.Update();
            
            
            // If player is definitely dead (health is 0 and we detected it), don't kill again
            if (gameState.CurrentHealth == 0 && gameState.MaxHealth > 0)
            {
                Logger.LogInfo("KillEffect: Player is already dead");
                return false;
            }
            
            return true; // Allow kill attempt
        }

        protected override async Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo("KillEffect: Executing kill effect");

            // Use a dedicated kill path that doesn't depend on clamping
            if (gameState.ForceKillPlayer())
            {
                Logger.LogInfo("KillEffect: Player killed");
                await Task.CompletedTask;
                return CCStatus.Success;
            }

            Logger.LogError("KillEffect: ForceKillPlayer failed");
            return CCStatus.Retry;
        }
    }
}



