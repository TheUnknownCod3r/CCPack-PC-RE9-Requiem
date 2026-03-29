using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect to empty the current weapon's magazine
    /// </summary>
    public class EmptyAmmoEffect : EffectBase
    {
        public override string Code => "emptyweap";

        protected override bool CanExecute(GameState gameState, CCRequest request)
        {
            // Need a weapon equipped
            return RE3CrowdControlPlugin.AllowWeaponManipulation(gameState)
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
                    Logger.LogError("EmptyAmmoEffect: Inventory not found");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                // Get the main weapon slot
                var mainSlot = inventory.Call("get_MainSlot");
                if (mainSlot == null)
                {
                    Logger.LogError("EmptyAmmoEffect: Main slot not found");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                var slotObj = mainSlot as ManagedObject;
                if (slotObj == null)
                {
                    Logger.LogError("EmptyAmmoEffect: Main slot is not a ManagedObject");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                // Get current ammo
                var currentObj = slotObj.Call("get_Number");
                if (currentObj == null)
                {
                    Logger.LogError("EmptyAmmoEffect: Could not get current ammo");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                int current = Convert.ToInt32(currentObj);

                // Only empty if there's ammo
                if (current > 0)
                {
                    slotObj.Call("set_Number", 0);
                    Logger.LogInfo($"EmptyAmmoEffect: Emptied weapon magazine from {current} to 0");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo("EmptyAmmoEffect: Weapon magazine already empty");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"EmptyAmmoEffect: Error emptying weapon magazine - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



