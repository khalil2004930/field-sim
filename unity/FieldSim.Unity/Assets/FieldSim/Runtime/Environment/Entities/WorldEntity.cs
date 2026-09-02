using UnityEngine;

namespace FieldSim.Unity.Environment.Entities
{
    public enum WorldEntityFaction
    {
        Neutral,
        Blue,
        Red
    }

    public class WorldEntity : MonoBehaviour
    {
        [SerializeField] private string entityId;
        [SerializeField] private string displayName;
        [SerializeField] private WorldEntityFaction faction;
        [SerializeField] private string orbatNodeId;

        public string EntityId => entityId;
        public string DisplayName => displayName;
        public WorldEntityFaction Faction => faction;
        public string OrbatNodeId => orbatNodeId;

        public void Initialize(string id, string name, WorldEntityFaction entityFaction, string orbat)
        {
            entityId = id;
            displayName = name;
            faction = entityFaction;
            orbatNodeId = orbat;
            gameObject.name = string.IsNullOrWhiteSpace(name) ? id : name;
        }
    }
}
