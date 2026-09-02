using Unity.AI.Navigation;
using UnityEngine;

namespace FieldSim.Unity.Environment.Structures
{
    public enum StructurePortalKind
    {
        Door,
        Basement,
        TunnelEntrance,
        BunkerEntrance,
        SurfaceExit
    }

    public enum StructurePortalState
    {
        Open,
        Blocked,
        Damaged,
        Collapsed,
        Sealed
    }

    public sealed class StructurePortal : MonoBehaviour
    {
        [SerializeField] private string portalId;
        [SerializeField] private StructurePortalKind kind;
        [SerializeField] private StructurePortalState state = StructurePortalState.Open;
        [SerializeField] private StructurePortal linkedPortal;
        [SerializeField] private NavMeshLink navMeshLink;

        public string PortalId => portalId;
        public StructurePortalKind Kind => kind;
        public StructurePortalState State => state;
        public StructurePortal LinkedPortal => linkedPortal;
        public bool CanTraverse => state == StructurePortalState.Open || state == StructurePortalState.Damaged;

        public void Configure(string id, StructurePortalKind portalKind, StructurePortal other = null)
        {
            portalId = id;
            kind = portalKind;
            linkedPortal = other;
            RefreshNavigation();
        }

        public void SetState(StructurePortalState newState)
        {
            state = newState;
            RefreshNavigation();
        }

        private void RefreshNavigation()
        {
            if (navMeshLink != null)
            {
                navMeshLink.enabled = CanTraverse;
            }
        }
    }
}
