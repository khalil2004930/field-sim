using System;
using UnityEngine;

namespace FieldSim.Unity.Environment.Structures
{
    public enum StructuralDamageState
    {
        Intact,
        Damaged,
        HeavilyDamaged,
        Critical,
        Collapsed
    }

    public sealed class StructuralPart : MonoBehaviour
    {
        [SerializeField] private string partId;
        [SerializeField, Range(0f, 1f)] private float integrity = 1f;
        [SerializeField] private float damageResistance = 1f;
        [SerializeField] private Collider physicalCollider;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject damagedVisual;
        [SerializeField] private GameObject collapsedVisual;

        public event Action<StructuralPart, StructuralDamageState> StateChanged;

        public string PartId => partId;
        public float Integrity => integrity;
        public StructuralDamageState State => EvaluateState(integrity);
        public Collider PhysicalCollider => physicalCollider;

        private void Awake()
        {
            if (physicalCollider == null)
            {
                physicalCollider = GetComponent<Collider>();
            }
            RefreshVisuals();
        }

        public void Configure(string id, Collider collider, GameObject intact, GameObject damaged, GameObject collapsed)
        {
            partId = id;
            physicalCollider = collider;
            intactVisual = intact;
            damagedVisual = damaged;
            collapsedVisual = collapsed;
            RefreshVisuals();
        }

        public void ApplyNormalizedDamage(float normalizedDamage)
        {
            StructuralDamageState before = State;
            float effectiveDamage = Mathf.Max(0f, normalizedDamage) / Mathf.Max(0.05f, damageResistance);
            integrity = Mathf.Clamp01(integrity - effectiveDamage);
            StructuralDamageState after = State;
            RefreshVisuals();

            if (before != after)
            {
                StateChanged?.Invoke(this, after);
            }
        }

        private void RefreshVisuals()
        {
            StructuralDamageState state = State;
            if (intactVisual != null) intactVisual.SetActive(state == StructuralDamageState.Intact);
            if (damagedVisual != null) damagedVisual.SetActive(state == StructuralDamageState.Damaged || state == StructuralDamageState.HeavilyDamaged || state == StructuralDamageState.Critical);
            if (collapsedVisual != null)
            {
                bool collapsed = state == StructuralDamageState.Collapsed;
                collapsedVisual.SetActive(collapsed);
                Collider rubbleCollider = collapsedVisual.GetComponent<Collider>();
                if (rubbleCollider != null) rubbleCollider.enabled = collapsed;
            }

            if (physicalCollider != null)
            {
                physicalCollider.enabled = state != StructuralDamageState.Collapsed;
            }
        }

        public static StructuralDamageState EvaluateState(float value)
        {
            if (value <= 0f) return StructuralDamageState.Collapsed;
            if (value <= 0.25f) return StructuralDamageState.Critical;
            if (value <= 0.50f) return StructuralDamageState.HeavilyDamaged;
            if (value <= 0.75f) return StructuralDamageState.Damaged;
            return StructuralDamageState.Intact;
        }
    }
}
