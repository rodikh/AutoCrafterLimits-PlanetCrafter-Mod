using System;
using System.Collections.Generic;

namespace AutoCrafterLimits
{
    [Serializable]
    internal sealed class PersistedStore
    {
        public List<PersistedCrafterConfig> AutoCrafters = new List<PersistedCrafterConfig>();
    }

    [Serializable]
    internal sealed class PersistedCrafterConfig
    {
        public int Id;
        public bool EnableOutputLimit;
        public int TargetOutputAmount;
        public bool EnableInputThreshold;
        public List<PersistedThreshold> InputThresholds = new List<PersistedThreshold>();
    }

    [Serializable]
    internal sealed class PersistedThreshold
    {
        public string ItemId;
        public int Amount;
    }
}
