using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE9DotNet_CC.Effects;

namespace RE9DotNet_CC.Effects
{
    /// <summary>
    /// Effect to unequip the current weapon
    /// </summary>
    public class UnequipWeaponEffect : EffectBase
    {
        public override string Code => "unequipweap";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Need a weapon equipped
            return RE9CrowdControlPlugin.AllowWeaponManipulation(gameState)
                && gameState.IsGameReady
                && gameState.HasWeaponEquipped();
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                var inventory = gameState.GetInventory();
                if (inventory == null)
                {
                    Logger.LogError("UnequipWeaponEffect: Inventory not found");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                // Get the main weapon slot index
                int slotIndex = gameState.GetEquippedWeaponSlotIndex();
                if (slotIndex < 0)
                {
                    Logger.LogError("UnequipWeaponEffect: No weapon equipped");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                // Unequip the weapon
                inventory.Call("unequipSlot", slotIndex);
                
                Logger.LogInfo($"UnequipWeaponEffect: Unequipped weapon from slot {slotIndex}");
                return Task.FromResult((int)CCStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"UnequipWeaponEffect: Error unequipping weapon - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



