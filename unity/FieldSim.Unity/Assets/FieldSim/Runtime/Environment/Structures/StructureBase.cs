using System.Collections.Generic;
using FieldSim.Unity.Environment.Entities;
using UnityEngine;

namespace FieldSim.Unity.Environment.Structures
{
    public enum StructureKind
    {
        Building,
        ReinforcedBuilding,
        Bunker,
        UndergroundChamber
    }

    public class StructureBase : MonoBehaviour
    {
        [SerializeField] private string structureId;
        [SerializeField] private StructureKind kind;
        [SerializeField] private List<StructuralPart> parts = new List<StructuralPart>();
        [SerializeField] private List<StructurePortal> portals = new List<StructurePortal>();
        [SerializeField] private List<WorldEntity> occupants = new List<WorldEntity>();

        public string StructureId => structureId;
        public StructureKind Kind => kind;
        public IReadOnlyList<StructuralPart> Parts => parts;
        public IReadOnlyList<StructurePortal> Portals => portals;
        public IReadOnlyList<WorldEntity> Occupants => occupants;

        public float Integrity
        {
            get
            {
                if (parts.Count == 0) return 1f;
                float sum = 0f;
                int count = 0;
                foreach (StructuralPart part in parts)
                {
                    if (part == null) continue;
                    sum += part.Integrity;
                    count++;
                }
                return count == 0 ? 1f : sum / count;
            }
        }

        public StructuralDamageState DamageState => StructuralPart.EvaluateState(Integrity);

        public void Initialize(string id, StructureKind structureKind)
        {
            structureId = id;
            kind = structureKind;
            gameObject.name = id;
        }

        public void RegisterPart(StructuralPart part)
        {
            if (part != null && !parts.Contains(part))
            {
                parts.Add(part);
            }
        }

        public void RegisterPortal(StructurePortal portal)
        {
            if (portal != null && !portals.Contains(portal))
            {
                portals.Add(portal);
            }
        }

        public void AddOccupant(WorldEntity entity)
        {
            if (entity != null && !occupants.Contains(entity))
            {
                occupants.Add(entity);
            }
        }

        public void RemoveOccupant(WorldEntity entity)
        {
            occupants.Remove(entity);
        }

        public void ApplyAreaEffect(Vector3 sourcePoint, float normalizedEffect, float falloffMeters)
        {
            float sigma = Mathf.Max(0.1f, falloffMeters * 0.5f);

            foreach (StructuralPart part in parts)
            {
                if (part == null) continue;

                Vector3 closest = part.PhysicalCollider != null
                    ? part.PhysicalCollider.ClosestPoint(sourcePoint)
                    : part.transform.position;

                float distance = Vector3.Distance(sourcePoint, closest);
                float weight = Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));
                part.ApplyNormalizedDamage(normalizedEffect * weight);
            }
        }
    }
}
