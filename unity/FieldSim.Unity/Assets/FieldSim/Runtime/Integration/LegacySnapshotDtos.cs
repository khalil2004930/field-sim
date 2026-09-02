using System;

namespace FieldSim.Unity.Integration
{
    [Serializable]
    public sealed class DiagnosticReportDto
    {
        public SnapshotDto snapshot;
    }

    [Serializable]
    public sealed class SnapshotDto
    {
        public LegacyUnitDto[] units;
        public LegacySupportAssetDto[] jointSupportAssets;
    }

    [Serializable]
    public sealed class LegacyUnitDto
    {
        public int id;
        public string entityKey;
        public string name;
        public string faction;
        public string unitClass;
        public string orbatNodeId;
        public string role;
        public double x;
        public double y;
        public double z;
        public double speedMetersPerSecond;
        public bool alive;
    }

    [Serializable]
    public sealed class LegacySupportAssetDto
    {
        public string id;
        public string name;
        public string faction;
        public string kind;
        public string role;
        public string orbatNodeId;
        public string fireProfileId;
        public int launcherCapacity;
        public int storesRemaining;
        public bool available;
        public string status;
        public double x;
        public double y;
        public double z;
        public double speedMetersPerSecond;
        public double maxSpeedMetersPerSecond;
    }
}
