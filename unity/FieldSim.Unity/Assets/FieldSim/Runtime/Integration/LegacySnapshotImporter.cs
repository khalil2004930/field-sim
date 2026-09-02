using FieldSim.Unity.Core;
using FieldSim.Unity.Environment.Entities;
using UnityEngine;

namespace FieldSim.Unity.Integration
{
    public sealed class LegacySnapshotImporter : MonoBehaviour
    {
        [SerializeField] private FieldSimWorldSpace worldSpace;
        [SerializeField] private TextAsset diagnosticJson;
        [SerializeField] private Transform entityRoot;

        public void Import()
        {
            if (worldSpace == null || diagnosticJson == null)
            {
                Debug.LogWarning("LegacySnapshotImporter requires worldSpace and diagnosticJson.");
                return;
            }

            DiagnosticReportDto report = JsonUtility.FromJson<DiagnosticReportDto>(diagnosticJson.text);
            if (report?.snapshot == null) return;

            if (report.snapshot.units != null)
            {
                foreach (LegacyUnitDto dto in report.snapshot.units)
                {
                    CreateLegacyUnit(dto);
                }
            }

            if (report.snapshot.jointSupportAssets != null)
            {
                foreach (LegacySupportAssetDto dto in report.snapshot.jointSupportAssets)
                {
                    CreateLegacySupportAsset(dto);
                }
            }
        }

        private void CreateLegacyUnit(LegacyUnitDto dto)
        {
            PrimitiveType primitive = string.Equals(dto.unitClass, "Person", System.StringComparison.OrdinalIgnoreCase)
                ? PrimitiveType.Sphere
                : PrimitiveType.Cube;

            GameObject go = GameObject.CreatePrimitive(primitive);
            go.transform.SetParent(entityRoot != null ? entityRoot : transform, false);
            go.transform.position = worldSpace.ToUnity(new FieldSimPosition(dto.x, dto.y, dto.z));
            go.transform.localScale = primitive == PrimitiveType.Sphere ? Vector3.one * 0.6f : Vector3.one * 2f;

            WorldEntity entity = go.AddComponent<WorldEntity>();
            entity.Initialize(dto.entityKey ?? dto.id.ToString(), dto.name, ParseFaction(dto.faction), dto.orbatNodeId);
        }

        private void CreateLegacySupportAsset(LegacySupportAssetDto dto)
        {
            bool fpv = string.Equals(dto.kind, "FpvDrone", System.StringComparison.OrdinalIgnoreCase);
            GameObject go = GameObject.CreatePrimitive(fpv ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            go.transform.SetParent(entityRoot != null ? entityRoot : transform, false);
            go.transform.position = worldSpace.ToUnity(new FieldSimPosition(dto.x, dto.y, dto.z));
            go.transform.localScale = fpv ? Vector3.one * 0.5f : new Vector3(1.2f, 0.5f, 1.2f);

            if (fpv)
            {
                FpvDroneEntity drone = go.AddComponent<FpvDroneEntity>();
                drone.Initialize(dto.id, dto.name, ParseFaction(dto.faction), dto.orbatNodeId);
                // Legacy diagnostics mark these drones as STOWED at speed 0. A future SimCore launch event should call Launch().
            }
            else
            {
                SupportAssetEntity support = go.AddComponent<SupportAssetEntity>();
                support.Initialize(dto.id, dto.name, ParseFaction(dto.faction), dto.orbatNodeId);
                support.Configure(dto.kind, dto.fireProfileId, dto.launcherCapacity, dto.storesRemaining, dto.available);
            }
        }

        private static WorldEntityFaction ParseFaction(string value)
        {
            if (string.Equals(value, "Blue", System.StringComparison.OrdinalIgnoreCase)) return WorldEntityFaction.Blue;
            if (string.Equals(value, "Red", System.StringComparison.OrdinalIgnoreCase)) return WorldEntityFaction.Red;
            return WorldEntityFaction.Neutral;
        }
    }
}
