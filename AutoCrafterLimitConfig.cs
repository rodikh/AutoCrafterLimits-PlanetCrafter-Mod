using System;
using System.Collections.Generic;
using SpaceCraft;
using UnityEngine;

namespace AutoCrafterLimits
{
    internal sealed class AutoCrafterLimitConfig
    {
        public readonly int OwnerId;
        public string LastOutputGroupId;
        public bool EnableOutputLimit;
        public bool OutputLimitCountsPlanetWide;
        public int TargetOutputAmount;
        public bool EnableInputThreshold;
        public bool InputThresholdCountsPlanetWide;
        public readonly Dictionary<string, int> InputThresholds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public AutoCrafterLimitConfig(int ownerId)
        {
            OwnerId = ownerId;
            EnableOutputLimit = false;
            TargetOutputAmount = 0;
            EnableInputThreshold = false;
        }

        public int GetThreshold(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int value;
            return InputThresholds.TryGetValue(itemId, out value) ? value : 0;
        }

        public void SetThreshold(string itemId, int value)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            InputThresholds[itemId] = Mathf.Max(0, value);
        }

        public bool AdaptToRecipe(List<Group> ingredients)
        {
            bool changed = false;
            HashSet<string> ingredientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ingredients.Count; i++)
            {
                string id = ingredients[i].GetId();
                ingredientIds.Add(id);
                if (!InputThresholds.ContainsKey(id))
                {
                    InputThresholds[id] = 0;
                    changed = true;
                }
            }

            List<string> toRemove = null;
            foreach (string existing in InputThresholds.Keys)
            {
                if (!ingredientIds.Contains(existing))
                {
                    if (toRemove == null)
                    {
                        toRemove = new List<string>();
                    }

                    toRemove.Add(existing);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    InputThresholds.Remove(toRemove[i]);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>If the output recipe changed, resets all limits to defaults and returns true.</summary>
        public bool ResetIfRecipeChanged(string outputGroupId)
        {
            if (string.IsNullOrEmpty(outputGroupId))
            {
                return false;
            }

            if (LastOutputGroupId == outputGroupId)
            {
                return false;
            }

            bool hadPreviousRecipe = !string.IsNullOrEmpty(LastOutputGroupId);
            LastOutputGroupId = outputGroupId;

            if (hadPreviousRecipe)
            {
                ResetToDefaults();
                return true;
            }

            return false;
        }

        public void ResetToDefaults()
        {
            EnableOutputLimit = false;
            OutputLimitCountsPlanetWide = false;
            TargetOutputAmount = 0;
            EnableInputThreshold = false;
            InputThresholdCountsPlanetWide = false;
            InputThresholds.Clear();
        }
    }
}
