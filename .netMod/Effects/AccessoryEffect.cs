using System;
using System.Threading;
using System.Threading.Tasks;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    public class AccessoryEffect : EffectBase
    {
        private const int AccessoryCooldownMs = 1000;
        private readonly string _code;
        private readonly string _accessoryId;

        public AccessoryEffect(string code, string accessoryId)
        {
            _code = code;
            _accessoryId = accessoryId;
        }

        public override string Code => _code;

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady && gameState.IsPlayerAlive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            Logger.LogInfo($"{Code}: Accessory effects are disabled for RE3DotNet-CC");
            return Task.FromResult(CCStatus.Unavailable);
        }
    }
}


