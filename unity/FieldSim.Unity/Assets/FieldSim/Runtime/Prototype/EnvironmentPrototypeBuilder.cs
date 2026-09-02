using System.Collections;
using System.Collections.Generic;
using FieldSim.Unity.Environment.Entities;
using FieldSim.Unity.Environment.Structures;
using FieldSim.Unity.Environment.Tunnels;
using Unity.AI.Navigation;
using UnityEngine;

namespace FieldSim.Unity.Prototype
{
    public sealed class EnvironmentPrototypeBuilder : MonoBehaviour
    {
        [SerializeField] private int seed = 1701;
        [SerializeField] private int buildingCount = 28;
        [SerializeField] private Vector3 terrainSize = new Vector3(1000f, 180f, 1000f);

        private Environment.EnvironmentQueryService queries;
        private Environment.EnvironmentDamageSystem damageSystem;
        private NavMeshSurface navSurface;
        private readonly List<StructureBase> structures = new List<StructureBase>();

        private IEnumerator Start()
        {
            Random.InitState(seed);
            BuildServices();
            Terrain terrain = BuildTerrain();
            queries.RegisterTerrain(terrain);
            BuildRoads(terrain);
            BuildBuildings(terrain);
            BuildBunkerAndTunnels(terrain);
            BuildPhysicalEntities(terrain);
            BuildCameraAndLight();

            yield return null;
            navSurface.BuildNavMesh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && structures.Count > 0)
            {
                StructureBase target = structures[Mathf.Min(4, structures.Count - 1)];
                damageSystem.ApplyStructuralAreaEffect(target.transform.position + Vector3.up * 2f, 0.65f, 32f);
                navSurface.BuildNavMesh();
                Debug.Log("Synthetic structural effect applied near " + target.StructureId + ".");
            }
        }

        private void BuildServices()
        {
            GameObject root = new GameObject("EnvironmentServices");
            queries = root.AddComponent<Environment.EnvironmentQueryService>();
            damageSystem = root.AddComponent<Environment.EnvironmentDamageSystem>();
            navSurface = root.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;

            damageSystem.Configure(queries);
        }

        private Terrain BuildTerrain()
        {
            const int resolution = 257;
            TerrainData data = new TerrainData
            {
                heightmapResolution = resolution,
                size = terrainSize
            };

            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1);
                    float nz = z / (float)(resolution - 1);
                    float ridge = Mathf.Abs(Mathf.Sin((nx * 2.2f + nz * 1.3f) * Mathf.PI));
                    float broad = Mathf.PerlinNoise(nx * 2.1f + 1.7f, nz * 2.1f + 4.2f);
                    heights[z, x] = Mathf.Clamp01(0.08f + broad * 0.35f + ridge * 0.16f);
                }
            }
            data.SetHeights(0, 0, heights);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "SyntheticMountainTerrain";
            Terrain terrain = go.GetComponent<Terrain>();
            return terrain;
        }

        private void BuildRoads(Terrain terrain)
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.name = "Road_" + i.ToString("00");
                float z = 300f + i * 85f;
                float y = terrain.SampleHeight(new Vector3(500f, 0f, z)) + 0.15f;
                road.transform.position = new Vector3(500f, y, z);
                road.transform.localScale = new Vector3(720f, 0.3f, 8f);
            }
        }

        private void BuildBuildings(Terrain terrain)
        {
            for (int i = 0; i < buildingCount; i++)
            {
                float x = 230f + (i % 7) * 82f + Random.Range(-12f, 12f);
                float z = 245f + (i / 7) * 105f + Random.Range(-10f, 10f);
                float width = Random.Range(16f, 26f);
                float depth = Random.Range(14f, 24f);
                float height = Random.Range(7f, 16f);
                float ground = terrain.SampleHeight(new Vector3(x, 0f, z));
                StructureBase building = CreateSimpleBuilding("BLDG-" + i.ToString("000"), new Vector3(x, ground, z), width, depth, height, StructureKind.Building);
                structures.Add(building);
            }
        }

        private StructureBase CreateSimpleBuilding(string id, Vector3 groundCenter, float width, float depth, float height, StructureKind kind)
        {
            GameObject root = new GameObject(id);
            root.transform.position = groundCenter;
            StructureBase structure = root.AddComponent<StructureBase>();
            structure.Initialize(id, kind);

            float wallThickness = 0.5f;
            float doorWidth = 2.5f;
            float halfDoorSide = (width - doorWidth) * 0.25f;

            CreateWall(structure, "North", new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, wallThickness));
            CreateWall(structure, "West", new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(wallThickness, height, depth));
            CreateWall(structure, "East", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(wallThickness, height, depth));
            CreateWall(structure, "SouthL", new Vector3(-(doorWidth * 0.5f + halfDoorSide), height * 0.5f, -depth * 0.5f), new Vector3(width * 0.5f - doorWidth * 0.5f, height, wallThickness));
            CreateWall(structure, "SouthR", new Vector3(doorWidth * 0.5f + halfDoorSide, height * 0.5f, -depth * 0.5f), new Vector3(width * 0.5f - doorWidth * 0.5f, height, wallThickness));
            CreateWall(structure, "Roof", new Vector3(0f, height, 0f), new Vector3(width, wallThickness, depth));

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            floor.transform.localScale = new Vector3(width, 0.1f, depth);

            GameObject occupancy = new GameObject("OccupancyVolume");
            occupancy.transform.SetParent(root.transform, false);
            occupancy.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
            BoxCollider occupancyCollider = occupancy.AddComponent<BoxCollider>();
            occupancyCollider.size = new Vector3(width - 1f, height * 0.85f, depth - 1f);
            occupancyCollider.isTrigger = true;
            occupancy.AddComponent<StructureOccupancyVolume>();

            GameObject portalObject = new GameObject("DoorPortal");
            portalObject.transform.SetParent(root.transform, false);
            portalObject.transform.localPosition = new Vector3(0f, 1f, -depth * 0.5f);
            StructurePortal portal = portalObject.AddComponent<StructurePortal>();
            portal.Configure(id + "-DOOR-01", StructurePortalKind.Door);
            structure.RegisterPortal(portal);

            return structure;
        }

        private void CreateWall(StructureBase structure, string label, Vector3 localPosition, Vector3 localScale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = label;
            wall.transform.SetParent(structure.transform, false);
            wall.transform.localPosition = localPosition;
            wall.transform.localScale = localScale;

            GameObject rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rubble.name = label + "_Rubble";
            rubble.transform.SetParent(structure.transform, false);
            rubble.transform.localPosition = new Vector3(localPosition.x, Mathf.Max(0.25f, localScale.y * 0.08f), localPosition.z);
            rubble.transform.localScale = new Vector3(localScale.x, Mathf.Max(0.35f, localScale.y * 0.12f), Mathf.Max(localScale.z, 1.5f));
            Collider rubbleCollider = rubble.GetComponent<Collider>();
            if (rubbleCollider != null) rubbleCollider.enabled = false;
            rubble.SetActive(false);

            StructuralPart part = wall.AddComponent<StructuralPart>();
            part.Configure(structure.StructureId + "-" + label, wall.GetComponent<Collider>(), wall, wall, rubble);
            part.StateChanged += OnStructuralPartStateChanged;
            structure.RegisterPart(part);
        }

        private void OnStructuralPartStateChanged(StructuralPart part, StructuralDamageState state)
        {
            if (state == StructuralDamageState.Collapsed)
            {
                navSurface.BuildNavMesh();
            }
        }

        private void BuildBunkerAndTunnels(Terrain terrain)
        {
            Vector3 bunkerPosition = new Vector3(720f, 0f, 650f);
            bunkerPosition.y = terrain.SampleHeight(bunkerPosition) - 1.5f;
            StructureBase bunker = CreateSimpleBuilding("BUNKER-001", bunkerPosition, 22f, 20f, 6f, StructureKind.Bunker);
            structures.Add(bunker);

            GameObject graphObject = new GameObject("TunnelGraph");
            TunnelGraph graph = graphObject.AddComponent<TunnelGraph>();

            TunnelNode a = CreateTunnelNode(graph, "TUN-A", bunkerPosition + new Vector3(0f, -5f, 0f));
            TunnelNode b = CreateTunnelNode(graph, "TUN-B", bunkerPosition + new Vector3(-55f, -8f, 35f));
            TunnelNode c = CreateTunnelNode(graph, "TUN-C", bunkerPosition + new Vector3(-105f, -10f, 15f));
            graph.AddEdge(a, b);
            graph.AddEdge(b, c);
        }

        private TunnelNode CreateTunnelNode(TunnelGraph graph, string id, Vector3 position)
        {
            GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nodeObject.transform.position = position;
            nodeObject.transform.localScale = Vector3.one * 1.5f;
            TunnelNode node = nodeObject.AddComponent<TunnelNode>();
            node.Initialize(id);
            graph.AddNode(node);
            return node;
        }

        private void BuildPhysicalEntities(Terrain terrain)
        {
            for (int i = 0; i < 10; i++)
            {
                float x = 180f + i * 5f;
                float z = 210f + (i % 3) * 4f;
                CreateSoldierDot("Blue Soldier " + (i + 1), WorldEntityFaction.Blue, terrain, new Vector3(x, 0f, z));
            }

            for (int i = 0; i < 10; i++)
            {
                float x = 650f + i * 5f;
                float z = 630f + (i % 3) * 4f;
                CreateSoldierDot("Red Soldier " + (i + 1), WorldEntityFaction.Red, terrain, new Vector3(x, 0f, z));
            }

            Vector3 mortarPosition = new Vector3(685f, 0f, 690f);
            mortarPosition.y = terrain.SampleHeight(mortarPosition) + 0.6f;
            GameObject mortar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mortar.transform.position = mortarPosition;
            mortar.transform.localScale = new Vector3(1.1f, 0.5f, 1.1f);
            SupportAssetEntity support = mortar.AddComponent<SupportAssetEntity>();
            support.Initialize("red-mortar-01", "Synthetic Mortar Section", WorldEntityFaction.Red, "red-mortar");
            support.Configure("TubeArtillery", "mortar-abstract", 4, 4, true);

            Vector3 fpvStart = new Vector3(700f, 0f, 610f);
            fpvStart.y = terrain.SampleHeight(fpvStart) + 2f;
            GameObject fpvObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fpvObject.transform.position = fpvStart;
            fpvObject.transform.localScale = Vector3.one * 0.7f;
            FpvDroneEntity fpv = fpvObject.AddComponent<FpvDroneEntity>();
            fpv.Initialize("red-fpv-prototype", "Synthetic FPV", WorldEntityFaction.Red, "red-fpv-1");
            fpv.SetWaypoints(new[]
            {
                fpvStart + new Vector3(-30f, 35f, -20f),
                fpvStart + new Vector3(-110f, 55f, -70f),
                fpvStart + new Vector3(-185f, 45f, -30f),
                fpvStart + new Vector3(-230f, 30f, 25f)
            });
            fpv.Launch();
        }

        private void CreateSoldierDot(string name, WorldEntityFaction faction, Terrain terrain, Vector3 position)
        {
            position.y = terrain.SampleHeight(position) + 0.35f;
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.position = position;
            dot.transform.localScale = Vector3.one * 0.7f;
            WorldEntity entity = dot.AddComponent<WorldEntity>();
            entity.Initialize(name.Replace(" ", "-").ToLowerInvariant(), name, faction, faction == WorldEntityFaction.Blue ? "blue-prototype" : "red-prototype");
        }

        private void BuildCameraAndLight()
        {
            if (FindFirstObjectByType<Camera>() == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                camera.transform.position = new Vector3(500f, 620f, -260f);
                camera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
                camera.farClipPlane = 3000f;
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }
}
