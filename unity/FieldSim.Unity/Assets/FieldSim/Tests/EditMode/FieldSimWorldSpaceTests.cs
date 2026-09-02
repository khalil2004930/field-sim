using FieldSim.Unity.Core;
using NUnit.Framework;
using UnityEngine;

namespace FieldSim.Unity.Tests
{
    public class FieldSimWorldSpaceTests
    {
        [Test]
        public void RoundTripPreservesMeterNativePosition()
        {
            GameObject go = new GameObject("world-space-test");
            FieldSimWorldSpace world = go.AddComponent<FieldSimWorldSpace>();
            world.SetOrigin(40000.0, 24000.0, 100.0);

            FieldSimPosition input = new FieldSimPosition(40247.885, 25125.080, 126.546);
            Vector3 unity = world.ToUnity(input);
            FieldSimPosition output = world.ToFieldSim(unity);

            Assert.That(output.xMeters, Is.EqualTo(input.xMeters).Within(0.001));
            Assert.That(output.yMeters, Is.EqualTo(input.yMeters).Within(0.001));
            Assert.That(output.zMeters, Is.EqualTo(input.zMeters).Within(0.001));

            Object.DestroyImmediate(go);
        }
    }
}
