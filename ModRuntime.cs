using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using SpaceCraft;
using UnityEngine;

namespace AutoCrafterLimits
{
    internal static class ModRuntime
    {
        internal const float ScanRefreshSeconds = 2f;

        internal static ManualLogSource Logger;
        internal static AutoCrafterConfigStore Store;
        internal static AutoCrafterLimitsUi Ui;

        internal static readonly Dictionary<int, CachedScan> CachedScans = new Dictionary<int, CachedScan>();
        internal static readonly Dictionary<int, string> BlockReasons = new Dictionary<int, string>();
        internal static readonly HashSet<int> KnownInventoryIdsBuffer = new HashSet<int>();
        internal static readonly List<InventoryAssociated> InventoryAssociatedBuffer = new List<InventoryAssociated>();
        internal static readonly List<InventoryAssociatedProxy> InventoryProxyBuffer = new List<InventoryAssociatedProxy>();

        private static string _configPath;

        internal static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
            _configPath = Path.Combine(Paths.ConfigPath, "AutoCrafterLimits.json");
            Store = AutoCrafterConfigStore.Load(_configPath, logger);
        }

        internal static bool TryGetAutoCrafterData(MachineAutoCrafter crafter, out int worldObjectId, out Group selectedOutputGroup)
        {
            worldObjectId = -1;
            selectedOutputGroup = null;
            if (crafter == null)
            {
                return false;
            }

            WorldObjectAssociated associated = crafter.GetComponent<WorldObjectAssociated>();
            if (associated == null)
            {
                return false;
            }

            WorldObject wo = associated.GetWorldObject();
            if (wo == null)
            {
                return false;
            }

            worldObjectId = wo.GetId();
            List<Group> linkedGroups = wo.GetLinkedGroups();
            if (linkedGroups != null && linkedGroups.Count > 0)
            {
                selectedOutputGroup = linkedGroups[0];
            }

            return true;
        }

        internal static bool ShouldCraft(MachineAutoCrafter crafter, Group outputGroup, out string reason)
        {
            reason = string.Empty;
            if (crafter == null || outputGroup == null)
            {
                return true;
            }

            if (!TryGetAutoCrafterData(crafter, out int worldObjectId, out Group _))
            {
                return true;
            }

            AutoCrafterLimitConfig config = Store.GetOrCreate(worldObjectId);
            Dictionary<string, int> counts = GetOrRefreshCounts(crafter, worldObjectId);
            List<Group> recipeIngredients = outputGroup.GetRecipe().GetIngredientsGroupInRecipe();
            bool changed = config.AdaptToRecipe(recipeIngredients);
            if (changed)
            {
                Store.Save();
            }

            if (config.EnableOutputLimit && config.TargetOutputAmount > 0)
            {
                int outputCount = GetCount(counts, outputGroup.GetId());
                if (outputCount >= config.TargetOutputAmount)
                {
                    reason = "Blocked: output limit reached (" + outputCount + "/" + config.TargetOutputAmount + ")";
                    BlockReasons[worldObjectId] = reason;
                    return false;
                }
            }

            if (config.EnableInputThreshold)
            {
                for (int i = 0; i < recipeIngredients.Count; i++)
                {
                    Group ingredient = recipeIngredients[i];
                    int threshold = config.GetThreshold(ingredient.GetId());
                    if (threshold <= 0)
                    {
                        continue;
                    }

                    int available = GetCount(counts, ingredient.GetId());
                    if (available < threshold)
                    {
                        reason = "Blocked: input threshold " + ingredient.GetId() + " (" + available + "/" + threshold + ")";
                        BlockReasons[worldObjectId] = reason;
                        return false;
                    }
                }
            }

            BlockReasons.Remove(worldObjectId);
            return true;
        }

        internal static string GetBlockReason(MachineAutoCrafter crafter)
        {
            if (!TryGetAutoCrafterData(crafter, out int worldObjectId, out Group outputGroup))
            {
                return string.Empty;
            }

            if (outputGroup != null)
            {
                ShouldCraft(crafter, outputGroup, out string _);
            }

            if (BlockReasons.TryGetValue(worldObjectId, out string reason))
            {
                return reason;
            }

            return string.Empty;
        }

        internal static Dictionary<string, int> GetCurrentCounts(MachineAutoCrafter crafter)
        {
            if (!TryGetAutoCrafterData(crafter, out int worldObjectId, out Group _))
            {
                return null;
            }

            return GetOrRefreshCounts(crafter, worldObjectId);
        }

        internal static int GetCountFromSnapshot(Dictionary<string, int> counts, string itemId)
        {
            if (counts == null)
            {
                return 0;
            }

            return GetCount(counts, itemId);
        }

        private static Dictionary<string, int> GetOrRefreshCounts(MachineAutoCrafter crafter, int worldObjectId)
        {
            if (CachedScans.TryGetValue(worldObjectId, out CachedScan cached))
            {
                if (Time.time - cached.LastScanTime <= ScanRefreshSeconds)
                {
                    return cached.Counts;
                }
            }

            Dictionary<string, int> fresh = ScanAndCount(crafter);
            CachedScans[worldObjectId] = new CachedScan
            {
                LastScanTime = Time.time,
                Counts = fresh
            };
            return fresh;
        }

        private static Dictionary<string, int> ScanAndCount(MachineAutoCrafter crafter)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            KnownInventoryIdsBuffer.Clear();

            WorldObjectAssociated associated = crafter.GetComponent<WorldObjectAssociated>();
            if (associated != null)
            {
                WorldObject wo = associated.GetWorldObject();
                if (wo != null && wo.HasLinkedInventory())
                {
                    KnownInventoryIdsBuffer.Add(wo.GetLinkedInventoryId());
                }
            }

            List<ValueTuple<GameObject, Group>> inRange = crafter.GetGroupsInRangeForListing();
            for (int i = 0; i < inRange.Count; i++)
            {
                GameObject go = inRange[i].Item1;
                Group group = inRange[i].Item2;
                if (go == null || group == null)
                {
                    continue;
                }

                if (group is GroupItem)
                {
                    continue;
                }

                CollectInventoryIdsFrom(go);
            }

            foreach (int inventoryId in KnownInventoryIdsBuffer)
            {
                Inventory inventory = InventoriesHandler.Instance.GetInventoryById(inventoryId);
                if (inventory == null)
                {
                    continue;
                }

                foreach (WorldObject item in inventory.GetInsideWorldObjects())
                {
                    if (item == null || item.GetGroup() == null)
                    {
                        continue;
                    }

                    string itemId = item.GetGroup().GetId();
                    if (string.IsNullOrEmpty(itemId))
                    {
                        continue;
                    }

                    int current;
                    counts.TryGetValue(itemId, out current);
                    counts[itemId] = current + 1;
                }
            }

            return counts;
        }

        private static void CollectInventoryIdsFrom(GameObject go)
        {
            InventoryAssociatedBuffer.Clear();
            InventoryProxyBuffer.Clear();

            go.GetComponentsInChildren(true, InventoryAssociatedBuffer);
            for (int i = 0; i < InventoryAssociatedBuffer.Count; i++)
            {
                int inventoryId = InventoryAssociatedBuffer[i].GetInventoryId();
                if (inventoryId > 0)
                {
                    KnownInventoryIdsBuffer.Add(inventoryId);
                }
            }

            go.GetComponentsInChildren(true, InventoryProxyBuffer);
            for (int i = 0; i < InventoryProxyBuffer.Count; i++)
            {
                ValueTuple<WorldObject, int> data = InventoryProxyBuffer[i].GetRequestedInventoryData();
                if (data.Item2 > 0)
                {
                    KnownInventoryIdsBuffer.Add(data.Item2);
                }
            }
        }

        private static int GetCount(Dictionary<string, int> counts, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int value;
            return counts.TryGetValue(itemId, out value) ? value : 0;
        }

        internal static void RemoveCrafterState(int worldObjectId)
        {
            if (!Store.Remove(worldObjectId))
            {
                return;
            }

            CachedScans.Remove(worldObjectId);
            BlockReasons.Remove(worldObjectId);
            Store.Save();
        }

        internal sealed class CachedScan
        {
            public float LastScanTime;
            public Dictionary<string, int> Counts;
        }
    }
}
