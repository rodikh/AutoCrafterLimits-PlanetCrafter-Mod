using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using SpaceCraft;
using UnityEngine;

namespace AutoCrafterLimits
{
    /// <summary>JsonUtility-compatible leaf type; JsonUtility fails on any type that contains an array.</summary>
    [Serializable]
    internal sealed class JsonUtilThreshold
    {
        public string ItemId;
        public int Amount;
    }

    internal sealed class AutoCrafterConfigStore
    {
        private string _path;
        private readonly ManualLogSource _logger;
        private readonly Dictionary<int, AutoCrafterLimitConfig> _configs = new Dictionary<int, AutoCrafterLimitConfig>();

        private AutoCrafterConfigStore(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static AutoCrafterConfigStore Create(ManualLogSource logger)
        {
            return new AutoCrafterConfigStore(logger);
        }

        /// <summary>
        /// Switch to a save file's config: clear current state, load from path if it exists.
        /// Path: BepInEx/config/AutoCrafterLimits/{saveFileName}.json (avoids polluting the game's save folder)
        /// </summary>
        public void ReloadForSave(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName))
            {
                _path = null;
                _configs.Clear();
                return;
            }

            string folder = Path.Combine(Paths.ConfigPath, "AutoCrafterLimits");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, saveFileName + ".json");
            _configs.Clear();

            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }

                string json = File.ReadAllText(_path);
                PersistedStore persisted = JsonHelper.Deserialize(json);
                if (persisted == null || persisted.AutoCrafters == null || persisted.AutoCrafters.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < persisted.AutoCrafters.Length; i++)
                {
                    PersistedCrafterConfig entry = persisted.AutoCrafters[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    AutoCrafterLimitConfig runtime = new AutoCrafterLimitConfig(entry.Id)
                    {
                        LastOutputGroupId = entry.LastOutputGroupId,
                        EnableOutputLimit = entry.EnableOutputLimit,
                        OutputLimitCountsPlanetWide = entry.OutputLimitCountsPlanetWide,
                        TargetOutputAmount = Mathf.Max(0, entry.TargetOutputAmount),
                        EnableInputThreshold = entry.EnableInputThreshold,
                        InputThresholdCountsPlanetWide = entry.InputThresholdCountsPlanetWide
                    };

                    if (entry.InputThresholds != null)
                    {
                        for (int t = 0; t < entry.InputThresholds.Length; t++)
                        {
                            PersistedThreshold threshold = entry.InputThresholds[t];
                            if (threshold == null || string.IsNullOrEmpty(threshold.ItemId))
                            {
                                continue;
                            }

                            runtime.SetThreshold(threshold.ItemId, Mathf.Max(0, threshold.Amount));
                        }
                    }

                    _configs[entry.Id] = runtime;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load AutoCrafterLimits config: " + ex);
            }
        }

        public AutoCrafterLimitConfig GetOrCreate(int crafterId)
        {
            if (_configs.TryGetValue(crafterId, out AutoCrafterLimitConfig config))
            {
                return config;
            }

            EnsurePathFromCurrentSave();

            config = new AutoCrafterLimitConfig(crafterId);
            _configs[crafterId] = config;
            return config;
        }

        private void EnsurePathFromCurrentSave()
        {
            if (!string.IsNullOrEmpty(_path))
            {
                return;
            }

            string saveFileName = GetCurrentSaveFileName();
            if (string.IsNullOrEmpty(saveFileName))
            {
                return;
            }

            string folder = Path.Combine(Paths.ConfigPath, "AutoCrafterLimits");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, saveFileName + ".json");
        }

        public bool Remove(int crafterId)
        {
            return _configs.Remove(crafterId);
        }

        public void Save()
        {
            string path = _path;
            if (string.IsNullOrEmpty(path))
            {
                string saveFileName = GetCurrentSaveFileName();
                if (string.IsNullOrEmpty(saveFileName))
                {
                    return;
                }
                string folder = Path.Combine(Paths.ConfigPath, "AutoCrafterLimits");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, saveFileName + ".json");
                _path = path;
            }

            try
            {
                string json = SerializeWithJsonUtilWorkaround();
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save AutoCrafterLimits.json: " + ex);
            }
        }

        private static string GetCurrentSaveFileName()
        {
            SavedDataHandler handler = Managers.GetManager<SavedDataHandler>();
            return handler != null ? handler.GetCurrentSaveFileName() : null;
        }

        /// <summary>JsonUtility fails on array fields. Serialize leaf types with JsonUtility, build structure manually. Only persists crafters that have limits enabled.</summary>
        private string SerializeWithJsonUtilWorkaround()
        {
            var crafterJsons = new List<string>();
            foreach (KeyValuePair<int, AutoCrafterLimitConfig> kvp in _configs)
            {
                if (!HasLimitsEnabled(kvp.Value))
                {
                    continue;
                }
                var thresholdJsons = new List<string>();
                foreach (KeyValuePair<string, int> t in kvp.Value.InputThresholds)
                {
                    var jt = new JsonUtilThreshold { ItemId = t.Key, Amount = Mathf.Max(0, t.Value) };
                    thresholdJsons.Add(JsonUtility.ToJson(jt));
                }
                string thresholdsArray = "[" + string.Join(",", thresholdJsons) + "]";
                string lastOutput = kvp.Value.LastOutputGroupId != null ? "\"" + EscapeJsonString(kvp.Value.LastOutputGroupId) + "\"" : "null";
                string crafterJson = "{\"Id\":" + kvp.Key
                    + ",\"LastOutputGroupId\":" + lastOutput
                    + ",\"EnableOutputLimit\":" + (kvp.Value.EnableOutputLimit ? "true" : "false")
                    + ",\"OutputLimitCountsPlanetWide\":" + (kvp.Value.OutputLimitCountsPlanetWide ? "true" : "false")
                    + ",\"TargetOutputAmount\":" + Mathf.Max(0, kvp.Value.TargetOutputAmount)
                    + ",\"EnableInputThreshold\":" + (kvp.Value.EnableInputThreshold ? "true" : "false")
                    + ",\"InputThresholdCountsPlanetWide\":" + (kvp.Value.InputThresholdCountsPlanetWide ? "true" : "false")
                    + ",\"InputThresholds\":" + thresholdsArray + "}";
                crafterJsons.Add(crafterJson);
            }
            return "{\"AutoCrafters\":[" + string.Join(",", crafterJsons) + "]}";
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool HasLimitsEnabled(AutoCrafterLimitConfig config)
        {
            if (config.EnableOutputLimit && config.TargetOutputAmount > 0)
            {
                return true;
            }
            if (config.EnableInputThreshold && config.InputThresholds.Count > 0)
            {
                return true;
            }
            return false;
        }
    }
}
