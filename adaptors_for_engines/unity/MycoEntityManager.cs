using System;
using System.Collections.Generic;
using System.IO;
using Mycology_ECS.translation_layers.unity.entity_helpers;
using Mycology_ECS.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mycology_ECS.translation_layers.unity{

    public sealed class Myco_Unity_Entities : MonoBehaviour
    {
        public static Myco_Unity_Entities Instance { get; private set; }

        public string entitiesFolderPath = "Mycology_ECS/game_specific_ecs/entities";
        public string componentsFolderPath = "Mycology_ECS/game_specific_ecs/components";

    
        private static readonly Dictionary<string, ManagedEntity> ManagedEntities = new();

        public TextAsset entitiesJsonOverride;

        public bool logOnCreate = true;

        private readonly Dictionary<string, GameObject> _spawned = new();

        private readonly List<MycoGameObjectPool> _poolsToDispose = new();

        private readonly MycoComponentCatalog _componentCatalog = new(StringComparer.Ordinal);
        private Canvas _mycoCanvas;

        private GameObject _mycoEntitiesRoot;
        private MycoEntitiesInspectorView _mycoEntitiesInspectorView;

        private readonly SpawnedInstanceTracker _instanceTracker = new(StringComparer.Ordinal);

        private readonly MycoEntityTagIndex _tagIndex = new(StringComparer.Ordinal);

        private MycoEntityComponentApplier _componentApplier;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Myco] Multiple Myco_Unity_Entities in scene; using the latest one.");
            }
            Instance = this;

            EnsureMycoEntitiesRootExists();
            _componentApplier = new MycoEntityComponentApplier(_componentCatalog);

            if (GetComponent<MycoSystemsRunner>() == null)
            {
                gameObject.AddComponent<MycoSystemsRunner>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _instanceTracker.Clear();
            _tagIndex.Clear();
            if (_mycoEntitiesInspectorView != null)
            {
                _mycoEntitiesInspectorView.SetSnapshot(new List<MycoEntitiesInspectorView.SpawnedEntityEntry>());
            }

            for (var i = 0; i < _poolsToDispose.Count; i++)
            {
                try { _poolsToDispose[i]?.Dispose(); } catch { }
            }
            _poolsToDispose.Clear();
        }

        private void Start()
        {
            ResetRuntimeState();

            _componentCatalog.Load(componentsFolderPath);

            var entities = LoadMergedEntities();
            if (entities == null || entities.Length == 0)
            {
                Debug.LogWarning("[Myco] Entities JSON empty; nothing to spawn.");
                return;
            }

            // Best-effort editor convenience: ensure any tags referenced by ECS data (and sample systems)
            // exist in Unity's TagManager so runtime CompareTag/tag assignment doesn't error.
            UnityTagUtility.EnsureProjectTagsExist(CollectRequiredUnityTags(entities), logAdded: logOnCreate);

            for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                var entityDefinition = entities[entityIndex];
                if (entityDefinition == null) continue;

                RegisterEntity(entityDefinition);

                if (entityDefinition.create_on_start)
                {
                    var spawnedInstance = Spawn(entityDefinition.id);
                    if (spawnedInstance != null)
                    {
                        _spawned[entityDefinition.id] = spawnedInstance;
                        _instanceTracker.TrackSpawned(entityDefinition.id, spawnedInstance);
                        if (logOnCreate) Debug.Log($"[Myco] Created entity '{entityDefinition.id}' ({entityDefinition.type})");
                    }
                }
            }

            if (AnyEntityUsesUi())
            {
                _mycoCanvas = UI.EnsureCanvas();
            }

            UpdateMycoEntitiesInspectorSnapshot();
        }

        private IEnumerable<string> CollectRequiredUnityTags(EntityDef[] entities)
        {
            var tags = new HashSet<string>(StringComparer.Ordinal);

            // Tags used by sample systems (kept here so projects don't get spammed by CompareTag errors).
            tags.Add("Enemy");
            tags.Add("Player");

            if (entities == null || entities.Length == 0) return tags;

            for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                var entityDefinition = entities[entityIndex];
                if (entityDefinition?.components == null || entityDefinition.components.Length == 0) continue;

                for (var componentIndex = 0; componentIndex < entityDefinition.components.Length; componentIndex++)
                {
                    var componentId = entityDefinition.components[componentIndex];
                    if (string.IsNullOrWhiteSpace(componentId)) continue;

                    if (!_componentCatalog.TryGet(componentId, out var componentDefinition) || componentDefinition == null) continue;

                    var componentType = TypeConversionUtils.GetString(componentDefinition, "type") ?? string.Empty;
                    if (!string.Equals(componentType, "Tag", StringComparison.Ordinal)) continue;

                    var tagValue = TypeConversionUtils.GetString(componentDefinition, "value");
                    if (!string.IsNullOrWhiteSpace(tagValue)) tags.Add(tagValue);
                }
            }

            return tags;
        }

        private EntityDef[] LoadMergedEntities()
        {
            if (entitiesJsonOverride != null)
            {
                return ParseEntitiesJson(entitiesJsonOverride.text);
            }

            var activeSceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(activeSceneName))
            {
                Debug.LogWarning("[Myco] Active scene has no name; cannot select entities JSON.");
                return null;
            }

            var merged = new List<EntityDef>();

            var corePath = Path.Combine(Application.dataPath, "Mycology_ECS/core_entities/core.json");
            if (File.Exists(corePath))
            {
                try
                {
                    var coreText = File.ReadAllText(corePath);
                    var coreEntities = ParseEntitiesJson(coreText);
                    if (coreEntities != null && coreEntities.Length > 0) merged.AddRange(coreEntities);
                }
                catch { }
            }

            var folderPath = string.IsNullOrWhiteSpace(entitiesFolderPath) ? "Mycology_ECS/game_specific_ecs/entities" : entitiesFolderPath;
            var sceneText = JsonLoader.LoadJsonForScene(folderPath, activeSceneName, logOnCreate);
            if (!string.IsNullOrWhiteSpace(sceneText))
            {
                try
                {
                    var sceneEntities = ParseEntitiesJson(sceneText);
                    if (sceneEntities != null && sceneEntities.Length > 0) merged.AddRange(sceneEntities);
                }
                catch { }
            }

            return merged.Count == 0 ? null : merged.ToArray();
        }

        private static EntityDef[] ParseEntitiesJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var parsed = SimpleJson.Parse(json);
                if (parsed is List<object> list)
                {
                    return ParseEntitiesFromList(list);
                }

                if (parsed is Dictionary<string, object> obj)
                {
                    if (obj.TryGetValue("entities", out var entitiesObj) && entitiesObj is List<object> entitiesList)
                    {
                        return ParseEntitiesFromList(entitiesList);
                    }

                    var single = ParseEntityFromObject(obj);
                    return single == null ? null : new[] { single };
                }
            }
            catch { }

            return null;
        }

        private static EntityDef[] ParseEntitiesFromList(List<object> list)
        {
            if (list == null || list.Count == 0) return null;

            var result = new List<EntityDef>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not Dictionary<string, object> entityObj) continue;
                var entity = ParseEntityFromObject(entityObj);
                if (entity != null) result.Add(entity);
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        private static EntityDef ParseEntityFromObject(Dictionary<string, object> obj)
        {
            if (obj == null) return null;

            var entity = new EntityDef();
            entity.id = null;
            entity.name = TypeConversionUtils.GetString(obj, "name");
            entity.type = TypeConversionUtils.GetString(obj, "type");
            entity.create_on_start = TypeConversionUtils.GetBool(obj, "create_on_start", false);
            entity.object_pooling = TypeConversionUtils.GetBool(obj, "object_pooling", false);
            entity.pool_initial_size = TypeConversionUtils.GetInt(obj, "pool_initial_size", 0);
            entity.pool_max_size = TypeConversionUtils.GetInt(obj, "pool_max_size", 0);

            if (obj.TryGetValue("components", out var componentsObj) && componentsObj is List<object> componentsList)
            {
                var components = new List<string>(componentsList.Count);
                for (var i = 0; i < componentsList.Count; i++)
                {
                    var s = componentsList[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) components.Add(s);
                }
                entity.components = components.Count == 0 ? null : components.ToArray();
            }

            entity.UI_image = null;
            entity.UI_text = null;

            return entity;
        }

        private void ResetRuntimeState()
        {
            _spawned.Clear();
            _instanceTracker.Clear();
            _tagIndex.Clear();

            for (var i = 0; i < _poolsToDispose.Count; i++)
            {
                try { _poolsToDispose[i]?.Dispose(); } catch { }
            }
            _poolsToDispose.Clear();
            ManagedEntities.Clear();
        }

        public static GameObject Spawn(string entityId)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[Myco] No Myco_Unity_Entities Instance present in this scene.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(entityId)) return null;

            if (!ManagedEntities.TryGetValue(entityId, out var managedEntity) || managedEntity == null)
            {
                Debug.LogWarning($"[Myco] Unknown entity id: {entityId}");
                return null;
            }

            if (!managedEntity.isPooled)
            {
                if (managedEntity.singletonInstance != null) return managedEntity.singletonInstance;

                managedEntity.singletonInstance = Instance.CreateSingle(managedEntity.entityId);
                Instance._instanceTracker.TrackSpawned(managedEntity.entityId, managedEntity.singletonInstance);
                Instance.UpdateMycoEntitiesInspectorSnapshot();
                return managedEntity.singletonInstance;
            }

            var pooledInstance = managedEntity.pool?.Get();
            if (pooledInstance != null)
            {
                Instance.PlaceEntityInScene(managedEntity, pooledInstance);
                Instance._instanceTracker.TrackSpawned(managedEntity.entityId, pooledInstance);
                Instance.UpdateMycoEntitiesInspectorSnapshot();
            }
            return pooledInstance;
        }

        public static bool TryResolveEntityIdByTag(string tag, out string entityId)
        {
            entityId = null;

            if (Instance == null) return false;
            return Instance._tagIndex.TryGetFirstEntityId(tag, out entityId);
        }

        public static GameObject SpawnByTag(string tag)
        {
            if (!TryResolveEntityIdByTag(tag, out var entityId))
            {
                Debug.LogWarning($"[Myco] Unknown tag: {tag}");
                return null;
            }

            return Spawn(entityId);
        }

        public static bool TryGetByTag(string tag, out GameObject instance)
        {
            instance = null;

            if (!TryResolveEntityIdByTag(tag, out var entityId))
            {
                return false;
            }

            return TryGet(entityId, out instance);
        }

        internal void ForEachSpawnedInstance(Action<string, GameObject> visitor)
        {
            _instanceTracker.ForEachInstance(visitor);
        }

        public static bool TryGet(string entityId, out GameObject instance)
        {
            instance = null;
            if (string.IsNullOrWhiteSpace(entityId)) return false;

            if (!ManagedEntities.TryGetValue(entityId, out var managedEntity) || managedEntity == null)
            {
                return false;
            }

            if (!managedEntity.isPooled)
            {
                instance = managedEntity.singletonInstance;
                return instance != null;
            }

            return false;
        }

        public static void Release(string entityId, GameObject instance)
        {
            if (string.IsNullOrWhiteSpace(entityId) || instance == null) return;

            if (!ManagedEntities.TryGetValue(entityId, out var managedEntity) || managedEntity == null) return;
            if (!managedEntity.isPooled || managedEntity.pool == null) return;

            managedEntity.pool.Release(instance);

            if (Instance != null)
            {
                Instance._instanceTracker.TrackReleased(entityId, instance);
                Instance.UpdateMycoEntitiesInspectorSnapshot();
            }
        }

        private void EnsureMycoEntitiesRootExists()
        {
            if (_mycoEntitiesRoot != null && _mycoEntitiesInspectorView != null)
            {
                return;
            }

            var existingRoot = GameObject.Find("MycoEntities");
            if (existingRoot != null)
            {
                _mycoEntitiesRoot = existingRoot;
                _mycoEntitiesInspectorView = existingRoot.GetComponent<MycoEntitiesInspectorView>();
                if (_mycoEntitiesInspectorView == null)
                {
                    _mycoEntitiesInspectorView = existingRoot.AddComponent<MycoEntitiesInspectorView>();
                }
                return;
            }

            _mycoEntitiesRoot = new GameObject("MycoEntities");
            _mycoEntitiesInspectorView = _mycoEntitiesRoot.AddComponent<MycoEntitiesInspectorView>();
        }

        private void UpdateMycoEntitiesInspectorSnapshot()
        {
            EnsureMycoEntitiesRootExists();
            if (_mycoEntitiesInspectorView == null) return;

            var snapshot = new List<MycoEntitiesInspectorView.SpawnedEntityEntry>();
            _instanceTracker.ForEachInstance((entityId, instance) =>
            {
                snapshot.Add(new MycoEntitiesInspectorView.SpawnedEntityEntry
                {
                    entityId = entityId,
                    instance = instance,
                });
            });

            snapshot.Sort((left, right) => string.CompareOrdinal(left.entityId, right.entityId));
            _mycoEntitiesInspectorView.SetSnapshot(snapshot);
        }

        private void RegisterEntity(EntityDef entityDefinition)
        {
            var entityId = Guid.NewGuid().ToString("N");
            entityDefinition.id = entityId;

            if (ManagedEntities.ContainsKey(entityId))
            {
                if (logOnCreate) Debug.LogWarning($"[Myco] Entity id already registered: {entityId}");
                return;
            }

            var managedEntity = new ManagedEntity
            {
                entityId = entityId,
                entityType = entityDefinition.type,
                isPooled = entityDefinition.object_pooling,
                singletonInstance = null,
                pool = null,
                definition = entityDefinition,
                isUiLayer = false,
                uiSortOrder = 0,
            };

            _componentApplier.AnalyzeUiFromComponents(entityDefinition, out managedEntity.isUiLayer, out managedEntity.uiSortOrder);

            if (entityDefinition.object_pooling)
            {
                Func<GameObject> factory = () => CreateEntityInstance(managedEntity);
                managedEntity.pool = new MycoGameObjectPool(entityId, factory, initialSize: Mathf.Max(0, entityDefinition.pool_initial_size), maxSize: entityDefinition.pool_max_size);
                _poolsToDispose.Add(managedEntity.pool);
            }

            ManagedEntities[entityId] = managedEntity;
            _tagIndex.IndexEntity(entityId, entityDefinition, _componentCatalog);
        }

        private GameObject CreateSingle(string id)
        {
            if (!ManagedEntities.TryGetValue(id, out var managedEntity) || managedEntity?.definition == null)
            {
                Debug.LogWarning($"[Myco] No entity definition stored for '{id}'.");
                return null;
            }

            return CreateEntityInstance(managedEntity);
        }

        private GameObject CreateEntityInstance(ManagedEntity managedEntity)
        {
            if (managedEntity?.definition == null) return null;

            var entityDefinition = managedEntity.definition;
            var entityDisplayName = string.IsNullOrWhiteSpace(entityDefinition.name) ? entityDefinition.id : entityDefinition.name;
            var entityGameObject = new GameObject(string.IsNullOrWhiteSpace(entityDisplayName) ? "MycoEntity" : entityDisplayName);

            PlaceEntityInScene(managedEntity, entityGameObject);

            _componentApplier.ApplyComponentsToGameObject(entityDefinition, entityGameObject, ref _mycoCanvas);

            _componentApplier.ApplyLegacyUi(entityDefinition, entityGameObject, ref _mycoCanvas);

            return entityGameObject;
        }

        private void PlaceEntityInScene(ManagedEntity managedEntity, GameObject entityGameObject)
        {
            if (entityGameObject == null || managedEntity?.definition == null) return;

            if (managedEntity.isUiLayer)
            {
                _mycoCanvas = _mycoCanvas != null ? _mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(_mycoCanvas.transform, false);

                var maxSiblingIndex = Mathf.Max(0, entityGameObject.transform.parent.childCount - 1);
                entityGameObject.transform.SetSiblingIndex(Mathf.Clamp(managedEntity.uiSortOrder, 0, maxSiblingIndex));
                return;
            }

            entityGameObject.transform.SetParent(transform, false);
        }

        private bool AnyEntityUsesUi()
        {
            foreach (var managedEntry in ManagedEntities)
            {
                if (managedEntry.Value != null && managedEntry.Value.isUiLayer) return true;
            }
            return false;
        }
    }
}
