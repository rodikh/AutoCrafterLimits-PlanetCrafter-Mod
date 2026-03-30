using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SpaceCraft;
using Unity.Netcode;
using UnityEngine;

namespace AutoCrafterLimits
{
    internal static class ModRuntime
    {
        internal const float ScanRefreshSeconds = 2f;

        internal static AutoCrafterConfigStore Store;
        internal static AutoCrafterLimitsUi Ui;

        private static ConfigEntry<int> _defaultOutputLimitEntry;
        private static ConfigEntry<int> _defaultInputThresholdEntry;
        internal static int DefaultOutputLimitWhenEnabled => Mathf.Max(0, _defaultOutputLimitEntry?.Value ?? 0);
        internal static int DefaultInputThresholdWhenEnabled => Mathf.Max(0, _defaultInputThresholdEntry?.Value ?? 0);

        internal static readonly Dictionary<int, CachedScan> CachedScans = new Dictionary<int, CachedScan>();
        internal static readonly Dictionary<string, CachedScan> CachedPlanetWideScansByOutput = new Dictionary<string, CachedScan>(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<int, string> BlockReasons = new Dictionary<int, string>();
        internal static readonly HashSet<int> KnownInventoryIdsBuffer = new HashSet<int>();
        internal static readonly List<InventoryAssociated> InventoryAssociatedBuffer = new List<InventoryAssociated>();
        internal static readonly List<InventoryAssociatedProxy> InventoryProxyBuffer = new List<InventoryAssociatedProxy>();
        private static readonly List<ValueTuple<GameObject, Group>> RangeListingBuffer = new List<ValueTuple<GameObject, Group>>();

        internal static void Initialize(ManualLogSource logger, ConfigEntry<int> defaultOutputLimit, ConfigEntry<int> defaultInputThreshold)
        {
            Store = AutoCrafterConfigStore.Create(logger);
            _defaultOutputLimitEntry = defaultOutputLimit;
            _defaultInputThresholdEntry = defaultInputThreshold;
        }

        /// <summary>
        /// Reload config for the current save file. Call when a save is loaded or a new game is created.
        /// </summary>
        internal static void ReloadForSave(string saveFileName)
        {
            if (Store == null || string.IsNullOrEmpty(saveFileName))
            {
                return;
            }

            CachedScans.Clear();
            CachedPlanetWideScansByOutput.Clear();
            BlockReasons.Clear();
            Store.ReloadForSave(saveFileName);
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
            config.ResetIfRecipeChanged(outputGroup.GetId());

            Dictionary<string, int> inRangeCounts = GetOrRefreshCounts(crafter, worldObjectId);
            List<Group> recipeIngredients = outputGroup.GetRecipe().GetIngredientsGroupInRecipe();
            config.AdaptToRecipe(recipeIngredients);

            if (config.EnableOutputLimit && config.TargetOutputAmount > 0)
            {
                Dictionary<string, int> outputCounts = config.OutputLimitCountsPlanetWide ? GetOrRefreshCountsPlanetWide(outputGroup) : inRangeCounts;
                int outputCount = GetCount(outputCounts, outputGroup.GetId());
                if (outputCount >= config.TargetOutputAmount)
                {
                    reason = "Blocked: output limit reached (" + outputCount + "/" + config.TargetOutputAmount + ")";
                    BlockReasons[worldObjectId] = reason;
                    return false;
                }
            }

            if (config.EnableInputThreshold)
            {
                Dictionary<string, int> inputCounts = config.InputThresholdCountsPlanetWide ? GetOrRefreshCountsPlanetWide(outputGroup) : inRangeCounts;
                for (int i = 0; i < recipeIngredients.Count; i++)
                {
                    Group ingredient = recipeIngredients[i];
                    int threshold = config.GetThreshold(ingredient.GetId());
                    if (threshold <= 0)
                    {
                        continue;
                    }

                    int available = GetCount(inputCounts, ingredient.GetId());
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

        internal static Dictionary<string, int> GetCountsPlanetWide(Group outputGroup)
        {
            return GetOrRefreshCountsPlanetWide(outputGroup);
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

        private static Dictionary<string, int> GetOrRefreshCountsPlanetWide(Group outputGroup)
        {
            if (outputGroup == null)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            string outputKey = outputGroup.GetId();
            if (string.IsNullOrEmpty(outputKey))
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            if (CachedPlanetWideScansByOutput.TryGetValue(outputKey, out CachedScan cached)
                && Time.time - cached.LastScanTime <= ScanRefreshSeconds)
            {
                return cached.Counts;
            }

            Dictionary<string, int> fresh = ScanAndCountPlanetWide(outputGroup);
            CachedPlanetWideScansByOutput[outputKey] = new CachedScan
            {
                LastScanTime = Time.time,
                Counts = fresh
            };
            return fresh;
        }

        private static bool IsPlayerBuiltInventory(Inventory inventory)
        {
            if (inventory == null || WorldObjectsHandler.Instance == null)
            {
                return false;
            }

            WorldObject wo = WorldObjectsHandler.Instance.GetWorldObjectForInventory(inventory);
            return wo != null;
        }

        private static Dictionary<string, int> ScanAndCountPlanetWide(Group outputGroup)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (InventoriesHandler.Instance == null || outputGroup == null)
            {
                return counts;
            }

            Dictionary<int, Inventory> allInventories = InventoriesHandler.Instance.GetAllInventories();
            if (allInventories == null)
            {
                return counts;
            }

            foreach (Inventory inventory in allInventories.Values)
            {
                if (inventory == null)
                {
                    continue;
                }

                if (!IsPlayerBuiltInventory(inventory))
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

            RangeListingBuffer.Clear();
            MachineGetInRange machineGetInRange = crafter.GetComponent<MachineGetInRange>();
            if (machineGetInRange != null)
            {
                machineGetInRange.GetGroupsInRange(crafter.GetRange(), (go, group) => AddAutoCrafterRangeListingEntry(RangeListingBuffer, go, group));
            }

            for (int i = 0; i < RangeListingBuffer.Count; i++)
            {
                GameObject go = RangeListingBuffer[i].Item1;
                Group group = RangeListingBuffer[i].Item2;
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

        /// <summary>
        /// Matches which entries MachineAutoCrafter puts in its range listing, without touching static fields
        /// shared with CraftIfPossible (calling GetGroupsInRangeForListing corrupts those).
        /// </summary>
        private static void AddAutoCrafterRangeListingEntry(List<ValueTuple<GameObject, Group>> buffer, GameObject go, Group group)
        {
            if (go == null || group == null || buffer == null)
            {
                return;
            }

            if (group is GroupItem)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    WorldObjectAssociated componentInChildren = go.GetComponentInChildren<WorldObjectAssociated>(true);
                    if (componentInChildren == null || (componentInChildren.GetWorldObject().GetGrowth() > 0f && componentInChildren.GetWorldObject().GetGrowth() < 100f))
                    {
                        return;
                    }
                }

                buffer.Add(new ValueTuple<GameObject, Group>(go, group));
                return;
            }

            if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.Disabled
                && (go.GetComponentInChildren<InventoryAssociated>(true) != null || go.GetComponentInChildren<InventoryAssociatedProxy>(true) != null))
            {
                buffer.Add(new ValueTuple<GameObject, Group>(go, group));
            }
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
        }

        internal sealed class CachedScan
        {
            public float LastScanTime;
            public Dictionary<string, int> Counts;
        }
    }
}
