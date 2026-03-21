namespace AutoCrafterLimits
{
    internal sealed class PersistedStore
    {
        public PersistedCrafterConfig[] AutoCrafters { get; set; }
    }

    internal sealed class PersistedCrafterConfig
    {
        public int Id { get; set; }
        public string LastOutputGroupId { get; set; }
        public bool EnableOutputLimit { get; set; }
        public bool OutputLimitCountsPlanetWide { get; set; }
        public int TargetOutputAmount { get; set; }
        public bool EnableInputThreshold { get; set; }
        public bool InputThresholdCountsPlanetWide { get; set; }
        public PersistedThreshold[] InputThresholds { get; set; }
    }

    internal sealed class PersistedThreshold
    {
        public string ItemId { get; set; }
        public int Amount { get; set; }
    }
}
