using System;
using HarmonyLib;
using SpaceCraft;
using Unity.Netcode;

namespace AutoCrafterLimits
{
    [HarmonyPatch(typeof(MachineAutoCrafter), "CraftIfPossible")]
    internal static class MachineAutoCrafter_CraftIfPossible_Patch
    {
        private static bool Prefix(MachineAutoCrafter __instance, Group linkedGroup)
        {
            if (__instance == null || linkedGroup == null)
            {
                return true;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return true;
            }

            return ModRuntime.ShouldCraft(__instance, linkedGroup, out string _);
        }
    }

    [HarmonyPatch(typeof(UiWindowGroupSelector), "OnOpenAutoCrafter")]
    internal static class UiWindowGroupSelector_OnOpenAutoCrafter_Patch
    {
        private static void Postfix(UiWindowGroupSelector __instance)
        {
            if (ModRuntime.Ui != null)
            {
                ModRuntime.Ui.AttachWindow(__instance);
                ModRuntime.Ui.UpdateWindowStatus(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(UiWindowGroupSelector), "Update")]
    internal static class UiWindowGroupSelector_Update_Patch
    {
        private static void Postfix(UiWindowGroupSelector __instance)
        {
            if (ModRuntime.Ui != null)
            {
                ModRuntime.Ui.UpdateWindowStatus(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(UiWindowGroupSelector), "OnClose")]
    internal static class UiWindowGroupSelector_OnClose_Patch
    {
        private static void Prefix(UiWindowGroupSelector __instance)
        {
            if (ModRuntime.Ui != null)
            {
                ModRuntime.Ui.OnWindowClose(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(JSONExport), "LoadFromJson")]
    internal static class JSONExport_LoadFromJson_Patch
    {
        private static void Postfix(bool __result, string _saveFileName)
        {
            if (!__result || string.IsNullOrEmpty(_saveFileName))
            {
                return;
            }

            SavedDataHandler handler = Managers.GetManager<SavedDataHandler>();
            if (handler == null)
            {
                return;
            }

            string current = handler.GetCurrentSaveFileName();
            if (string.IsNullOrEmpty(current) || current != _saveFileName)
            {
                return;
            }

            ModRuntime.ReloadForSave(_saveFileName);
        }
    }

    [HarmonyPatch(typeof(JSONExport), "CreateNewSaveFile")]
    internal static class JSONExport_CreateNewSaveFile_Patch
    {
        private static void Postfix(string saveFileName)
        {
            if (!string.IsNullOrEmpty(saveFileName))
            {
                ModRuntime.ReloadForSave(saveFileName);
            }
        }
    }

    [HarmonyPatch(typeof(WorldObjectsHandler), "DestroyWorldObject", new Type[] { typeof(WorldObject), typeof(bool) })]
    internal static class WorldObjectsHandler_DestroyWorldObject_Patch
    {
        private static void Prefix(WorldObject worldObject)
        {
            if (worldObject == null)
            {
                return;
            }

            ModRuntime.RemoveCrafterState(worldObject.GetId());
        }
    }
}
