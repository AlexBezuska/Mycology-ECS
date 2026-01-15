using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mycology_ECS.Utils
{
    public sealed class SpawnedInstanceTracker
    {
        private readonly Dictionary<string, List<GameObject>> _activeInstancesByEntityId;

        public SpawnedInstanceTracker(IEqualityComparer<string> comparer = null)
        {
            _activeInstancesByEntityId = new Dictionary<string, List<GameObject>>(comparer ?? StringComparer.Ordinal);
        }

        public void Clear()
        {
            _activeInstancesByEntityId.Clear();
        }

        public void TrackSpawned(string entityId, GameObject instance)
        {
            if (string.IsNullOrWhiteSpace(entityId) || instance == null) return;

            if (!_activeInstancesByEntityId.TryGetValue(entityId, out var instancesForEntity) || instancesForEntity == null)
            {
                instancesForEntity = new List<GameObject>();
                _activeInstancesByEntityId[entityId] = instancesForEntity;
            }

            if (!instancesForEntity.Contains(instance))
            {
                instancesForEntity.Add(instance);
            }
        }

        public void TrackReleased(string entityId, GameObject instance)
        {
            if (string.IsNullOrWhiteSpace(entityId) || instance == null) return;

            if (_activeInstancesByEntityId.TryGetValue(entityId, out var instancesForEntity) && instancesForEntity != null)
            {
                instancesForEntity.Remove(instance);
                if (instancesForEntity.Count == 0)
                {
                    _activeInstancesByEntityId.Remove(entityId);
                }
            }
        }

        public void ForEachInstance(Action<string, GameObject> visitor)
        {
            if (visitor == null) return;
            foreach (var entry in _activeInstancesByEntityId)
            {
                var entityId = entry.Key;
                var instances = entry.Value;
                if (instances == null) continue;

                for (var instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    var instance = instances[instanceIndex];
                    if (instance == null) continue;
                    visitor(entityId, instance);
                }
            }
        }
    }
}
