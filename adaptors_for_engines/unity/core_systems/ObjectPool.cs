using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity.entity_helpers
{
    public sealed class MycoGameObjectPool
    {
        private readonly Func<GameObject> _factory;
        private readonly Transform _poolRoot;
        private readonly Stack<GameObject> _inactive;

        private readonly int _maxSize;
        private int _totalCreated;

        public MycoGameObjectPool(string name, Func<GameObject> factory, int initialSize, int maxSize, Transform poolRoot = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _factory = factory;
            _inactive = new Stack<GameObject>(Mathf.Max(0, initialSize));
            _maxSize = maxSize <= 0 ? int.MaxValue : maxSize;

            var root = new GameObject(string.IsNullOrWhiteSpace(name) ? "MycoPool" : $"MycoPool:{name}");
            _poolRoot = root.transform;
            if (poolRoot != null)
            {
                _poolRoot.SetParent(poolRoot, false);
            }

            Prewarm(initialSize);
        }

        public void Prewarm(int count)
        {
            count = Mathf.Max(0, count);
            for (var i = 0; i < count; i++)
            {
                if (_totalCreated >= _maxSize) break;

                var go = _factory();
                if (go == null) break;

                _totalCreated++;
                PrepareForPool(go);
                _inactive.Push(go);
            }
        }

        public GameObject Get()
        {
            while (_inactive.Count > 0)
            {
                var go = _inactive.Pop();
                if (go == null) continue;

                go.SetActive(true);
                return go;
            }

            if (_totalCreated >= _maxSize)
            {
                return null;
            }

            var created = _factory();
            if (created == null) return null;

            _totalCreated++;
            created.SetActive(true);
            return created;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;

            PrepareForPool(instance);
            _inactive.Push(instance);
        }

        public void Dispose()
        {
            while (_inactive.Count > 0)
            {
                var go = _inactive.Pop();
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }

            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot.gameObject);
            }
        }

        private void PrepareForPool(GameObject go)
        {
            if (go == null) return;

            go.SetActive(false);
            if (_poolRoot != null)
            {
                go.transform.SetParent(_poolRoot, false);
            }
        }
    }
}
