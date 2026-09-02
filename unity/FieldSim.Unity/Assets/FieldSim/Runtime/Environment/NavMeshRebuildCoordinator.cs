using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;

namespace FieldSim.Unity.Environment
{
    public sealed class NavMeshRebuildCoordinator : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface surface;
        [SerializeField] private float minimumDelaySeconds = 0.2f;
        private bool queued;

        public void RequestRebuild()
        {
            if (!queued)
            {
                StartCoroutine(RebuildSoon());
            }
        }

        private IEnumerator RebuildSoon()
        {
            queued = true;
            yield return new WaitForSeconds(minimumDelaySeconds);
            if (surface != null)
            {
                surface.BuildNavMesh();
            }
            queued = false;
        }
    }
}
