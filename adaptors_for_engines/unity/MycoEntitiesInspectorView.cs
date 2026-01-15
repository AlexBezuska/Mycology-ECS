using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity
{
    /// <summary>
    /// Inspector-friendly view of all spawned Mycology entity GameObjects.
    ///
    /// This intentionally stores data in a serializable List so Unity can display it
    /// in the Inspector (Unity does not serialize dictionaries by default).
    /// </summary>
    public sealed class MycoEntitiesInspectorView : MonoBehaviour
    {
        [Serializable]
        public sealed class SpawnedEntityEntry
        {
            public string entityId;
            public GameObject instance;
        }

        [Header("Spawned Entities (Inspector)")]
        [SerializeField]
        private List<SpawnedEntityEntry> spawnedEntities = new();

        public IReadOnlyList<SpawnedEntityEntry> SpawnedEntities => spawnedEntities;

        public void SetSnapshot(List<SpawnedEntityEntry> snapshot)
        {
            if (snapshot == null)
            {
                spawnedEntities.Clear();
                return;
            }

            spawnedEntities = snapshot;
        }
    }
}
