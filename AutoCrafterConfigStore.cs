using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace AutoCrafterLimits
{
    internal sealed class AutoCrafterConfigStore
    {
        private readonly string _path;
        private readonly ManualLogSource _logger;
        private readonly Dictionary<int, AutoCrafterLimitConfig> _configs = new Dictionary<int, AutoCrafterLimitConfig>();

        private AutoCrafterConfigStore(string path, ManualLogSource logger)
        {
            _path = path;
            _logger = logger;
        }

        public static AutoCrafterConfigStore Load(string path, ManualLogSource logger)
        {
            AutoCrafterConfigStore store = new AutoCrafterConfigStore(path, logger);

            try
            {
                if (!File.Exists(path))
                {
                    return store;
                }

                string json = File.ReadAllText(path);
                PersistedStore persisted = JsonUtility.FromJson<PersistedStore>(json);
                if (persisted == null || persisted.AutoCrafters == null)
                {
                    return store;
                }

                for (int i = 0; i < persisted.AutoCrafters.Count; i++)
                {
                    PersistedCrafterConfig entry = persisted.AutoCrafters[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    AutoCrafterLimitConfig runtime = new AutoCrafterLimitConfig(entry.Id)
                    {
                        EnableOutputLimit = entry.EnableOutputLimit,
                        TargetOutputAmount = Mathf.Max(0, entry.TargetOutputAmount),
                        EnableInputThreshold = entry.EnableInputThreshold
                    };

                    if (entry.InputThresholds != null)
                    {
                        for (int t = 0; t < entry.InputThresholds.Count; t++)
                        {
                            PersistedThreshold threshold = entry.InputThresholds[t];
                            if (threshold == null || string.IsNullOrEmpty(threshold.ItemId))
                            {
                                continue;
                            }

                            runtime.SetThreshold(threshold.ItemId, Mathf.Max(0, threshold.Amount));
                        }
                    }

                    store._configs[entry.Id] = runtime;
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to load AutoCrafterLimits.json: " + ex);
            }

            return store;
        }

        public AutoCrafterLimitConfig GetOrCreate(int crafterId)
        {
            if (_configs.TryGetValue(crafterId, out AutoCrafterLimitConfig config))
            {
                return config;
            }

            config = new AutoCrafterLimitConfig(crafterId);
            _configs[crafterId] = config;
            return config;
        }

        public bool Remove(int crafterId)
        {
            return _configs.Remove(crafterId);
        }

        public void Save()
        {
            try
            {
                PersistedStore persisted = new PersistedStore();
                foreach (KeyValuePair<int, AutoCrafterLimitConfig> kvp in _configs)
                {
                    PersistedCrafterConfig entry = new PersistedCrafterConfig
                    {
                        Id = kvp.Key,
                        EnableOutputLimit = kvp.Value.EnableOutputLimit,
                        TargetOutputAmount = Mathf.Max(0, kvp.Value.TargetOutputAmount),
                        EnableInputThreshold = kvp.Value.EnableInputThreshold
                    };

                    foreach (KeyValuePair<string, int> threshold in kvp.Value.InputThresholds)
                    {
                        entry.InputThresholds.Add(new PersistedThreshold
                        {
                            ItemId = threshold.Key,
                            Amount = Mathf.Max(0, threshold.Value)
                        });
                    }

                    persisted.AutoCrafters.Add(entry);
                }

                string json = JsonUtility.ToJson(persisted, true);
                File.WriteAllText(_path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save AutoCrafterLimits.json: " + ex);
            }
        }
    }
}
