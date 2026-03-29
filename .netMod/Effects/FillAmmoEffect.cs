using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC.Effects;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Effect to fill the current weapon's magazine
    /// </summary>
    public class FillAmmoEffect : EffectBase
    {
        public override string Code => "fillweap";

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
                    Logger.LogError("FillAmmoEffect: Inventory not found");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                // Get the main weapon slot
                var mainSlot = inventory.Call("get_MainSlot");
                if (mainSlot == null)
                {
                    Logger.LogError("FillAmmoEffect: Main slot not found");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                var slotObj = mainSlot as ManagedObject;
                if (slotObj == null)
                {
                    Logger.LogError("FillAmmoEffect: Main slot is not a ManagedObject");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                // Get current and max ammo
                var currentObj = slotObj.Call("get_Number");
                var maxObj = slotObj.Call("get_MaxNumber");
                
                if (currentObj == null || maxObj == null)
                {
                    Logger.LogError("FillAmmoEffect: Could not get ammo numbers");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                int current = Convert.ToInt32(currentObj);
                int max = Convert.ToInt32(maxObj);

                // Only fill if not already full
                if (current < max)
                {
                    slotObj.Call("set_Number", max);
                    Logger.LogInfo($"FillAmmoEffect: Filled weapon magazine from {current} to {max}");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo("FillAmmoEffect: Weapon magazine already full");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"FillAmmoEffect: Error filling weapon magazine - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



