using System.Collections.Generic;
using FieldSim.Unity.Environment.Structures;
using UnityEngine;

namespace FieldSim.Unity.Environment
{
    /// <summary>
    /// Synthetic structural-effect propagation. Inputs are normalized game values, not real weapon tables.
    /// </summary>
    public sealed class EnvironmentDamageSystem : MonoBehaviour
    {
        [SerializeField] private EnvironmentQueryService queries;
        [SerializeField, Range(0f, 1f)] private float occludedMultiplier = 0.35f;

        public void Configure(EnvironmentQueryService queryService)
        {
            queries = queryService;
        }

        public void ApplyStructuralAreaEffect(Vector3 point, float normalizedEffect, float radiusMeters)
        {
            if (queries == null || radiusMeters <= 0f || normalizedEffect <= 0f) return;

            List<StructureBase> structures = queries.GetStructuresNear(point, radiusMeters);
            foreach (StructureBase structure in structures)
            {
                if (structure == null) continue;

                Vector3 target = structure.transform.position;
                float distance = Vector3.Distance(point, target);
                float normalizedDistance = Mathf.Clamp01(distance / radiusMeters);
                float distanceFactor = Mathf.Exp(-4f * normalizedDistance * normalizedDistance);
                float shield = ComputeShielding(point, structure);
                float effect = Mathf.Clamp01(normalizedEffect * distanceFactor * shield);
                structure.ApplyAreaEffect(point, effect, radiusMeters);
            }
        }

        private float ComputeShielding(Vector3 source, StructureBase targetStructure)
        {
            Vector3 target = targetStructure.transform.position;
            Vector3 delta = target - source;
            float distance = delta.magnitude;
            if (distance <= 0.1f) return 1f;

            RaycastHit[] hits = Physics.RaycastAll(source, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                StructureBase blocker = hit.collider.GetComponentInParent<StructureBase>();
                if (blocker != null && blocker != targetStructure)
                {
                    return occludedMultiplier;
                }
            }

            return 1f;
        }
    }
}
