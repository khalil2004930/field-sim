using UnityEngine;

namespace FieldSim.Unity.Integration
{
    public static class EnvironmentBridgeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEnvironmentBridge()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Object.FindFirstObjectByType<EnvironmentQueryTcpServer>() != null) return;
            GameObject bridge = new GameObject("FieldSim Unity Environment Bridge");
            Object.DontDestroyOnLoad(bridge);
            bridge.AddComponent<EnvironmentQueryTcpServer>();
#endif
        }
    }
}
