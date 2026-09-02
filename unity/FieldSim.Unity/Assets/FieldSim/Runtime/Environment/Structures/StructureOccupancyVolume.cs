using FieldSim.Unity.Environment.Entities;
using UnityEngine;

namespace FieldSim.Unity.Environment.Structures
{
    [RequireComponent(typeof(Collider))]
    public sealed class StructureOccupancyVolume : MonoBehaviour
    {
        [SerializeField] private StructureBase structure;

        private void Awake()
        {
            Collider c = GetComponent<Collider>();
            c.isTrigger = true;
            if (structure == null)
            {
                structure = GetComponentInParent<StructureBase>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            WorldEntity entity = other.GetComponentInParent<WorldEntity>();
            if (entity != null)
            {
                structure?.AddOccupant(entity);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            WorldEntity entity = other.GetComponentInParent<WorldEntity>();
            if (entity != null)
            {
                structure?.RemoveOccupant(entity);
            }
        }
    }
}
