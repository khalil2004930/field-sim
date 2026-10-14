using System.Reflection;
using FieldSim.Unity.Core;
using FieldSim.Unity.Environment;
using FieldSim.Unity.Integration;
using NUnit.Framework;
using UnityEngine;

namespace FieldSim.Unity.Tests
{
    public class GroundHeightQueryTests
    {
        private GameObject services;
        private GameObject terrainObject;
        private TerrainData terrainData;
        private Terrain terrain;
        private EnvironmentQueryService queries;
        private EnvironmentQueryTcpServer server;

        [SetUp]
        public void SetUp()
        {
            services = new GameObject("ground-height-test");
            // Keep Awake from starting a socket; exercise the real JSON handler directly.
            services.SetActive(false);
            FieldSimWorldSpace world = services.AddComponent<FieldSimWorldSpace>();
            world.SetOrigin(40000, 24000, 100);
            queries = services.AddComponent<EnvironmentQueryService>();
            server = services.AddComponent<EnvironmentQueryTcpServer>();
            SetServerField("worldSpace", world);
            SetServerField("environmentQueries", queries);
            SetServerField("startOnAwake", false);

            terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(100, 20, 100) };
            float[,] heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
                for (int x = 0; x < 33; x++)
                    heights[z, x] = 0.5f;
            terrainData.SetHeights(0, 0, heights);
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.transform.position = new Vector3(10, 25, 20);
            terrain = terrainObject.GetComponent<Terrain>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(services);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(terrainData);
        }

        [Test]
        public void RegisteredTerrainReturnsSampleAndFieldSimElevation()
        {
            queries.RegisterTerrain(terrain);
            Assert.That(queries.TrySampleGroundHeight(new Vector3(50, -100, 60), out float height), Is.True);
            Assert.That(height, Is.EqualTo(35f).Within(0.001f));
            EnvironmentBridgeResponse response = Query(40050, 24060);
            Assert.That(response.handled, Is.True);
            Assert.That(response.numberValue, Is.EqualTo(135).Within(0.001));
        }

        [Test]
        public void ZeroMeterElevationIsHandled()
        {
            terrainObject.transform.position = new Vector3(10, -110, 20);
            queries.RegisterTerrain(terrain);
            EnvironmentBridgeResponse response = Query(40050, 24060);
            Assert.That(response.handled, Is.True);
            Assert.That(response.numberValue, Is.EqualTo(0).Within(0.001));
        }

        [Test]
        public void ActiveButUnregisteredTerrainIsUnavailable()
        {
            AssertUnavailable(50, 60);
        }

        [Test]
        public void NoTerrainIsUnavailable()
        {
            Object.DestroyImmediate(terrainObject);
            terrainObject = null;
            AssertUnavailable(50, 60);
        }

        [TestCase(9.5f, 60f)]
        [TestCase(110.5f, 60f)]
        [TestCase(50f, 19.5f)]
        [TestCase(50f, 120.5f)]
        public void OutsideRegisteredTerrainIsUnavailable(float x, float z)
        {
            queries.RegisterTerrain(terrain);
            AssertUnavailable(x, z);
        }

        [TestCase(10f, 20f)]
        [TestCase(110f, 120f)]
        public void TerrainBoundaryIsIncluded(float x, float z)
        {
            queries.RegisterTerrain(terrain);
            Assert.That(Query(40000 + x, 24000 + z).handled, Is.True);
        }

        private void AssertUnavailable(float x, float z)
        {
            Assert.That(queries.TrySampleGroundHeight(new Vector3(x, 777, z), out _), Is.False);
            EnvironmentBridgeResponse response = Query(40000 + x, 24000 + z);
            Assert.That(response.id, Is.EqualTo("ground-height-test"));
            Assert.That(response.handled, Is.False);
            Assert.That(response.status, Is.EqualTo("unavailable"));
        }

        private EnvironmentBridgeResponse Query(double x, double y)
        {
            string request = JsonUtility.ToJson(new EnvironmentBridgeRequest
            {
                id = "ground-height-test",
                type = "groundHeight",
                point = new FieldSimPosition(x, y, 0)
            });
            MethodInfo handler = typeof(EnvironmentQueryTcpServer).GetMethod("HandleRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return JsonUtility.FromJson<EnvironmentBridgeResponse>((string)handler.Invoke(server, new object[] { request }));
        }

        private void SetServerField(string name, object value)
        {
            typeof(EnvironmentQueryTcpServer).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(server, value);
        }
    }
}
