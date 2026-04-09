using System;
using REFrameworkNET;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Centralizes <c>app.GuiManager</c> access (RE9 uses GuiManager; some builds expose GUIManager).
    /// </summary>
    internal static class GuiManagerProbe
    {
        public static ManagedObject? TryGetGuiManager()
        {
            object? gui = API.GetManagedSingleton("app.GuiManager")
                          ?? API.GetManagedSingleton("app.GUIManager");
            if (gui == null)
                return null;
            return gui as ManagedObject ?? GameState.ExtractFromInvokeRet(gui) as ManagedObject;
        }

        public static bool? TryGetBoolGetter(ManagedObject? target, string getterName)
        {
            if (target == null)
                return null;
            try
            {
                object? r = target.Call(getterName);
                if (r == null)
                    return null;
                return Convert.ToBoolean(r);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Inventory sub-view is open (GuiInventoryBridge.Active).</summary>
        public static bool? TryGetInventoryActive(ManagedObject gui)
        {
            try
            {
                object? bridge = gui.Call("get_Inventory");
                if (bridge == null)
                    return null;
                var bridgeMo = bridge as ManagedObject ?? GameState.ExtractFromInvokeRet(bridge) as ManagedObject;
                return TryGetBoolGetter(bridgeMo, "get_Active");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>File / notes-style inventory tab (when present).</summary>
        public static bool? TryGetInventoryFileClueActive(ManagedObject gui)
        {
            try
            {
                object? bridge = gui.Call("get_Inventory");
                if (bridge == null)
                    return null;
                var bridgeMo = bridge as ManagedObject ?? GameState.ExtractFromInvokeRet(bridge) as ManagedObject;
                return TryGetBoolGetter(bridgeMo, "get_ActiveFileClue");
            }
            catch
            {
                return null;
            }
        }
    }
}
