using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace AutoCrafterLimits
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rodik.planetcrafter.autocrafterlimits";
        public const string PluginName = "AutoCrafterLimits";
        public const string PluginVersion = "1.0.0";

        private Harmony _harmony;

        private void Awake()
        {
            ModRuntime.Initialize(Logger);

            GameObject uiObject = new GameObject("AutoCrafterLimitsUi");
            DontDestroyOnLoad(uiObject);
            ModRuntime.Ui = uiObject.AddComponent<AutoCrafterLimitsUi>();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo("AutoCrafterLimits initialized.");
        }

        private void OnDestroy()
        {
            ModRuntime.Store.Save();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }
}
