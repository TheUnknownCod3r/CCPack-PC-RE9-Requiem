using System.Threading.Tasks;

namespace RE9DotNet_CC.Effects
{
    public class PostEffectFilterEffect : EffectBase
    {
        private readonly string _code;
        private readonly string _filterKey;

        public PostEffectFilterEffect(string code, string filterKey)
        {
            _code = code;
            _filterKey = filterKey;
        }

        public override string Code => _code;

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            return gameState.IsGameReady && gameState.IsPlayerAlive;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            if (gameState.TryApplyPostEffectFilter(_filterKey))
                return Task.FromResult(CCStatus.Success);

            return Task.FromResult(CCStatus.Retry);
        }
    }
}


