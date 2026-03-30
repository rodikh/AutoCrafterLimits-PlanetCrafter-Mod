using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AutoCrafterLimits
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rodikh.planetcrafter.autocrafterlimits";
        public const string PluginName = "AutoCrafterLimits";
        public const string PluginVersion = "1.0.3";

        private Harmony _harmony;
        private ConfigEntry<int> _defaultOutputLimitWhenEnabled;
        private ConfigEntry<int> _defaultInputThresholdWhenEnabled;

        private void Awake()
        {
            _defaultOutputLimitWhenEnabled = Config.Bind(
                "Defaults",
                "DefaultOutputLimitWhenEnabled",
                30,
                "Default output limit (0 = unlimited).");

            _defaultInputThresholdWhenEnabled = Config.Bind(
                "Defaults",
                "DefaultInputThresholdWhenEnabled",
                30,
                "Default per-ingredient limit (0 = no threshold).");

            ModRuntime.Initialize(Logger, _defaultOutputLimitWhenEnabled, _defaultInputThresholdWhenEnabled);

            GameObject uiObject = new GameObject("AutoCrafterLimitsUi");
            DontDestroyOnLoad(uiObject);
            ModRuntime.Ui = uiObject.AddComponent<AutoCrafterLimitsUi>();
            uiObject.AddComponent<GameSaveListener>();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo("AutoCrafterLimits initialized.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }
}
