using UnityEngine;

namespace FieldSim.Unity.Environment.Entities
{
    public class SupportAssetEntity : WorldEntity
    {
        [SerializeField] private string supportKind;
        [SerializeField] private string fireProfileId;
        [SerializeField] private int launcherCapacity;
        [SerializeField] private int storesRemaining;
        [SerializeField] private bool mechanicallyAvailable = true;

        public string SupportKind => supportKind;
        public string FireProfileId => fireProfileId;
        public int LauncherCapacity => launcherCapacity;
        public int StoresRemaining => storesRemaining;
        public bool CanFire => mechanicallyAvailable && storesRemaining > 0;
        public string ReadinessLabel => mechanicallyAvailable ? (storesRemaining > 0 ? "READY" : "EMPTY") : "UNAVAILABLE";

        public void Configure(string kind, string profileId, int capacity, int stores, bool available)
        {
            supportKind = kind;
            fireProfileId = profileId;
            launcherCapacity = Mathf.Max(0, capacity);
            storesRemaining = Mathf.Max(0, stores);
            mechanicallyAvailable = available;
        }

        public bool TryConsumeStore(int count)
        {
            if (count <= 0 || !mechanicallyAvailable || storesRemaining < count)
            {
                return false;
            }

            storesRemaining -= count;
            return true;
        }
    }
}
