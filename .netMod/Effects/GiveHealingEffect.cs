using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Base class for give healing item effects
    /// </summary>
    public class GiveHealingEffect : GiveItemEffectBase
    {
        private readonly string _healingKey;
        private readonly string _effectCode;

        public GiveHealingEffect(string healingKey, string effectCode)
        {
            _healingKey = healingKey;
            _effectCode = effectCode;
        }

        public override string Code => _effectCode;

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                if (!ItemData.HealingItems.TryGetValue(_healingKey, out int itemId))
                {
                    Logger.LogError($"{Code}: Invalid healing item key '{_healingKey}'");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                if (gameState.AddHealingItem(itemId))
                {
                    Logger.LogInfo($"{Code}: Added healing item {_healingKey} (ID: {itemId})");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo($"{Code}: Failed to add healing item - no space");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{Code}: Error adding healing item - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



