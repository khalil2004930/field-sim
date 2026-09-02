using System;
using UnityEngine;

namespace FieldSim.Unity.Core
{
    [Serializable]
    public struct FieldSimPosition
    {
        public double xMeters;
        public double yMeters;
        public double zMeters;

        public FieldSimPosition(double xMeters, double yMeters, double zMeters)
        {
            this.xMeters = xMeters;
            this.yMeters = yMeters;
            this.zMeters = zMeters;
        }
    }

    /// <summary>
    /// Maps FieldSim meter-native coordinates to Unity coordinates.
    /// FieldSim: X/Y are horizontal map axes and Z is elevation.
    /// Unity: X/Z are horizontal and Y is vertical.
    /// Re-centering prevents large public-map coordinates from reducing float precision.
    /// </summary>
    public sealed class FieldSimWorldSpace : MonoBehaviour
    {
        [SerializeField] private double originXMeters = 40000.0;
        [SerializeField] private double originYMeters = 24000.0;
        [SerializeField] private double originZMeters = 100.0;

        public Vector3 ToUnity(FieldSimPosition p)
        {
            return new Vector3(
                (float)(p.xMeters - originXMeters),
                (float)(p.zMeters - originZMeters),
                (float)(p.yMeters - originYMeters));
        }

        public FieldSimPosition ToFieldSim(Vector3 p)
        {
            return new FieldSimPosition(
                p.x + originXMeters,
                p.z + originYMeters,
                p.y + originZMeters);
        }

        public void SetOrigin(double xMeters, double yMeters, double zMeters)
        {
            originXMeters = xMeters;
            originYMeters = yMeters;
            originZMeters = zMeters;
        }
    }
}
