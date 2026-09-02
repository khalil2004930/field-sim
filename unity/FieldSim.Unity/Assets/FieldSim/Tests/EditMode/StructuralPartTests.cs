using FieldSim.Unity.Environment.Structures;
using NUnit.Framework;
using UnityEngine;

namespace FieldSim.Unity.Tests
{
    public class StructuralPartTests
    {
        [Test]
        public void DamageTransitionsAreMonotonic()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            StructuralPart part = go.AddComponent<StructuralPart>();
            part.Configure("wall", go.GetComponent<Collider>(), go, null, null);

            float initial = part.Integrity;
            part.ApplyNormalizedDamage(0.20f);
            float afterFirst = part.Integrity;
            part.ApplyNormalizedDamage(0.30f);
            float afterSecond = part.Integrity;

            Assert.Less(afterFirst, initial);
            Assert.Less(afterSecond, afterFirst);

            Object.DestroyImmediate(go);
        }
    }
}
