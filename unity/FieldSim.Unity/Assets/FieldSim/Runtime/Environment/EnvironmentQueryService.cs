using System.Collections.Generic;
using FieldSim.Unity.Environment.Structures;
using UnityEngine;

namespace FieldSim.Unity.Environment
{
    public enum VisibilityStatus
    {
        Clear,
        BlockedByTerrain,
        BlockedByStructure,
        BlockedByOther
    }

    public readonly struct VisibilityResult
    {
        public VisibilityResult(VisibilityStatus status, Collider blocker, Vector3 point)
        {
            Status = status;
            Blocker = blocker;
            BlockPoint = point;
        }

        public VisibilityStatus Status { get; }
        public Collider Blocker { get; }
        public Vector3 BlockPoint { get; }
        public bool IsClear => Status == VisibilityStatus.Clear;
    }

    public sealed class EnvironmentQueryService : MonoBehaviour
    {
        [SerializeField] private LayerMask visibilityMask = ~0;
        [SerializeField] private LayerMask structureMask = ~0;
        [SerializeField] private List<Terrain> terrains = new List<Terrain>();

        public VisibilityResult TraceVisibility(Vector3 observerEye, Vector3 targetPoint)
        {
            Vector3 direction = targetPoint - observerEye;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return new VisibilityResult(VisibilityStatus.Clear, null, targetPoint);
            }

            if (!Physics.Raycast(observerEye, direction / distance, out RaycastHit hit, distance, visibilityMask, QueryTriggerInteraction.Ignore))
            {
                return new VisibilityResult(VisibilityStatus.Clear, null, targetPoint);
            }

            if (hit.collider.GetComponentInParent<Terrain>() != null)
            {
                return new VisibilityResult(VisibilityStatus.BlockedByTerrain, hit.collider, hit.point);
            }

            if (hit.collider.GetComponentInParent<StructureBase>() != null)
            {
                return new VisibilityResult(VisibilityStatus.BlockedByStructure, hit.collider, hit.point);
            }

            return new VisibilityResult(VisibilityStatus.BlockedByOther, hit.collider, hit.point);
        }

        public float SampleGroundHeight(Vector3 worldPosition)
        {
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 local = worldPosition - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
                return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
            }

            return worldPosition.y;
        }

        public List<StructureBase> GetStructuresNear(Vector3 point, float radiusMeters)
        {
            Collider[] hits = Physics.OverlapSphere(point, Mathf.Max(0f, radiusMeters), structureMask, QueryTriggerInteraction.Collide);
            HashSet<StructureBase> unique = new HashSet<StructureBase>();
            foreach (Collider hit in hits)
            {
                StructureBase structure = hit.GetComponentInParent<StructureBase>();
                if (structure != null) unique.Add(structure);
            }
            return new List<StructureBase>(unique);
        }

        public void RegisterTerrain(Terrain terrain)
        {
            if (terrain != null && !terrains.Contains(terrain)) terrains.Add(terrain);
        }
    }
}
