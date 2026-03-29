using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Base class for give item effects
    /// </summary>
    public abstract class GiveItemEffectBase : EffectBase
    {
        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady;
        }
    }
}



