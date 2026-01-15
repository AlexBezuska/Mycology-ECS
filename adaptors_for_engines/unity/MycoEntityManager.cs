using System;
using System.Collections.Generic;
using System.IO;
using Mycology_ECS.translation_layers.unity.entity_helpers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mycology_ECS.translation_layers.unity
{
    /// <summary>
    /// Mycology ECS (Unity translation layer) entity spawner.
    ///
    /// Usage:
    /// - Create an empty GameObject at the ROOT of your Unity scene (e.g. named "Myco Entities").
    /// - Add this component to that GameObject.
    /// - On play, it will find the entities JSON for the current Unity scene and create any entities with
    ///   "create_on_start": true.
    ///
    /// Entities are loaded by scanning: Assets/Mycology_ECS/entities/
    /// The JSON filename should match the scene name (case-insensitive), e.g. "Main.json" for scene "Main".
    ///
    /// Optional: You can still assign a TextAsset override via the inspector.
    /// </summary>
    public sealed class Myco_Unity_Entities : MonoBehaviour
    {
        public static Myco_Unity_Entities Instance { get; private set; }

        [Header("Folder Paths")]
        [Tooltip("Optional. If set, overrides the default entities folder path under Assets.")]
        public string entitiesFolderPath = "Mycology_ECS/entities";

        [Tooltip("Optional. If set, overrides the default components folder path under Assets.")]
        public string componentsFolderPath = "Mycology_ECS/components";

        /// <summary>
        /// Global lookup table for quickly retrieving spawned objects by entity id,
        /// without using GameObject.Find.
        /// </summary>
        private static readonly Dictionary<string, ManagedEntity> ManagedEntities = new();

        [Header("Entity Source")]
        [Tooltip("Optional. If assigned, this JSON is used instead of scanning the entities folder.")]
        public TextAsset entitiesJsonOverride;

        [Header("Debug")]
        public bool logOnCreate = true;

        private readonly Dictionary<string, GameObject> _spawned = new();

        private readonly List<MycoGameObjectPool> _poolsToDispose = new();

        private readonly Dictionary<string, Dictionary<string, object>> _componentCatalog = new(StringComparer.Ordinal);
        private Canvas _mycoCanvas;

        private GameObject _mycoEntitiesRoot;
        private MycoEntitiesInspectorView _mycoEntitiesInspectorView;

        // Tracks all currently spawned instances so the inspector can show them.
        private readonly Dictionary<string, List<GameObject>> _activeInstancesByEntityId = new(StringComparer.Ordinal);

        private sealed class ManagedEntity
        {
            public string entityId;
            public string entityType;
            public bool isPooled;
            public MycoGameObjectPool pool;
            public GameObject singletonInstance;
            public EntityDef definition;

            public bool isUiLayer;
            public int uiSortOrder;
        }

        [Serializable]
        private sealed class EntitiesRoot
        {
            public EntityDef[] entities;
        }

        [Serializable]
        private sealed class EntityDef
        {
            public string id;
            public string name;
            public string type;
            public bool create_on_start;

            public string[] components;

            public bool object_pooling;
            public int pool_initial_size;
            public int pool_max_size;

            public UI.UiImageDef UI_image;
            public UI.UiTextDef UI_text;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Myco] Multiple Myco_Unity_Entities in scene; using the latest one.");
            }
            Instance = this;

            EnsureMycoEntitiesRootExists();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _activeInstancesByEntityId.Clear();
            if (_mycoEntitiesInspectorView != null)
            {
                _mycoEntitiesInspectorView.SetSnapshot(new List<MycoEntitiesInspectorView.SpawnedEntityEntry>());
            }

            for (var i = 0; i < _poolsToDispose.Count; i++)
            {
                try { _poolsToDispose[i]?.Dispose(); } catch { /* ignore */ }
            }
            _poolsToDispose.Clear();
        }

        private void Start()
        {
            var json = LoadJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[Myco] Entities JSON empty; nothing to spawn.");
                return;
            }

            LoadComponentCatalog();

            EntitiesRoot root;
            try
            {
                root = JsonUtility.FromJson<EntitiesRoot>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Myco] Failed to parse entities JSON: {ex}");
                return;
            }

            if (root?.entities == null || root.entities.Length == 0)
            {
                if (logOnCreate) Debug.Log("[Myco] No entities found in JSON.");
                return;
            }

            for (var entityIndex = 0; entityIndex < root.entities.Length; entityIndex++)
            {
                var entityDefinition = root.entities[entityIndex];
                if (entityDefinition == null) continue;

                // Always register a handle so other scripts can spawn/ask for it later.
                RegisterEntity(entityDefinition);

                if (entityDefinition.create_on_start)
                {
                    var spawnedInstance = Spawn(entityDefinition.id);
                    if (spawnedInstance != null)
                    {
                        _spawned[entityDefinition.id] = spawnedInstance;
                        TrackSpawnedInstance(entityDefinition.id, spawnedInstance);
                        if (logOnCreate) Debug.Log($"[Myco] Created entity '{entityDefinition.id}' ({entityDefinition.type})");
                    }
                }
            }

            // Ensure the Mycology canvas exists only if needed.
            if (AnyEntityUsesUi())
            {
                _mycoCanvas = UI.EnsureCanvas();
            }

            UpdateMycoEntitiesInspectorSnapshot();
        }

        /// <summary>
        /// Spawn an entity instance by id.
        /// - If not pooled: returns the singleton instance (creates it if needed).
        /// - If pooled: returns a pooled instance.
        /// </summary>
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
                Instance.TrackSpawnedInstance(managedEntity.entityId, managedEntity.singletonInstance);
                Instance.UpdateMycoEntitiesInspectorSnapshot();
                return managedEntity.singletonInstance;
            }

            var pooledInstance = managedEntity.pool?.Get();
            if (pooledInstance != null)
            {
                Instance.PlaceEntityInScene(managedEntity, pooledInstance);
                Instance.TrackSpawnedInstance(managedEntity.entityId, pooledInstance);
                Instance.UpdateMycoEntitiesInspectorSnapshot();
            }
            return pooledInstance;
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

            // For pooled entities, prefer spawning explicitly (Spawn) since there can be many.
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
                Instance.TrackReleasedInstance(entityId, instance);
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

        private void TrackSpawnedInstance(string entityId, GameObject instance)
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

        private void TrackReleasedInstance(string entityId, GameObject instance)
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

        private void UpdateMycoEntitiesInspectorSnapshot()
        {
            EnsureMycoEntitiesRootExists();
            if (_mycoEntitiesInspectorView == null) return;

            var snapshot = new List<MycoEntitiesInspectorView.SpawnedEntityEntry>();
            foreach (var entry in _activeInstancesByEntityId)
            {
                var entityId = entry.Key;
                var instances = entry.Value;
                if (instances == null) continue;

                for (var instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    var instance = instances[instanceIndex];
                    if (instance == null) continue;

                    snapshot.Add(new MycoEntitiesInspectorView.SpawnedEntityEntry
                    {
                        entityId = entityId,
                        instance = instance,
                    });
                }
            }

            snapshot.Sort((left, right) => string.CompareOrdinal(left.entityId, right.entityId));
            _mycoEntitiesInspectorView.SetSnapshot(snapshot);
        }

        private string LoadJson()
        {
            if (entitiesJsonOverride != null)
            {
                return entitiesJsonOverride.text;
            }

            // Read from disk (intended for editor/dev workflows).
            try
            {
                var activeSceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrWhiteSpace(activeSceneName))
                {
                    Debug.LogWarning("[Myco] Active scene has no name; cannot select entities JSON.");
                    return null;
                }

                var folderPath = string.IsNullOrWhiteSpace(entitiesFolderPath) ? "Mycology_ECS/entities" : entitiesFolderPath;
                var entitiesFolderFullPath = Path.Combine(Application.dataPath, folderPath);
                if (!Directory.Exists(entitiesFolderFullPath))
                {
                    Debug.LogWarning($"[Myco] Entities folder not found: {entitiesFolderFullPath}");
                    return null;
                }

                var entityJsonFiles = Directory.GetFiles(entitiesFolderFullPath, "*.json", SearchOption.TopDirectoryOnly);
                if (entityJsonFiles == null || entityJsonFiles.Length == 0)
                {
                    Debug.LogWarning($"[Myco] No entity JSON files found in: {entitiesFolderFullPath}");
                    return null;
                }

                // Prefer exact filename match (case-insensitive).
                var desiredFilename = activeSceneName + ".json";
                string bestMatchFullPath = null;

                for (var fileIndex = 0; fileIndex < entityJsonFiles.Length; fileIndex++)
                {
                    var fileFullPath = entityJsonFiles[fileIndex];
                    var fileName = Path.GetFileName(fileFullPath);
                    if (string.Equals(fileName, desiredFilename, StringComparison.OrdinalIgnoreCase))
                    {
                        bestMatchFullPath = fileFullPath;
                        break;
                    }
                }

                // Secondary match: filename-without-extension equals scene name.
                if (bestMatchFullPath == null)
                {
                    for (var fileIndex = 0; fileIndex < entityJsonFiles.Length; fileIndex++)
                    {
                        var fileFullPath = entityJsonFiles[fileIndex];
                        var fileStem = Path.GetFileNameWithoutExtension(fileFullPath);
                        if (string.Equals(fileStem, activeSceneName, StringComparison.OrdinalIgnoreCase))
                        {
                            bestMatchFullPath = fileFullPath;
                            break;
                        }
                    }
                }

                if (bestMatchFullPath == null)
                {
                    Debug.LogWarning($"[Myco] No entities JSON matched scene '{activeSceneName}'. Expected '{desiredFilename}' under {entitiesFolderFullPath}");
                    return null;
                }

                if (logOnCreate)
                {
                    Debug.Log($"[Myco] Loading entities for scene '{activeSceneName}' from: {bestMatchFullPath}");
                }

                return File.ReadAllText(bestMatchFullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Myco] Failed reading entities JSON from disk: {ex}");
                return null;
            }
        }

        private void RegisterEntity(EntityDef entityDefinition)
        {
            var entityId = string.IsNullOrWhiteSpace(entityDefinition.id) ? Guid.NewGuid().ToString("N") : entityDefinition.id;
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

            AnalyzeUiFromComponents(entityDefinition, out managedEntity.isUiLayer, out managedEntity.uiSortOrder);

            if (entityDefinition.object_pooling)
            {
                Func<GameObject> factory = () => CreateEntityInstance(managedEntity);
                managedEntity.pool = new MycoGameObjectPool(entityId, factory, initialSize: Mathf.Max(0, entityDefinition.pool_initial_size), maxSize: entityDefinition.pool_max_size);
                _poolsToDispose.Add(managedEntity.pool);
            }

            ManagedEntities[entityId] = managedEntity;
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

        private GameObject CreateEntityInstance(SpawnHandle spawnHandle)
        {
            if (managedEntity?.definition == null) return null;

            var entityDefinition = managedEntity.definition;
            var entityDisplayName = string.IsNullOrWhiteSpace(entityDefinition.name) ? entityDefinition.id : entityDefinition.name;
            var entityGameObject = new GameObject(string.IsNullOrWhiteSpace(entityDisplayName) ? "MycoEntity" : entityDisplayName);

            // Parent based on RenderLayer.
            PlaceEntityInScene(managedEntity, entityGameObject);

            // Apply ECS components.
            ApplyComponentsToGameObject(entityDefinition, entityGameObject);

            // Legacy escape hatch (temporary): allow old type-based UI definitions.
            ApplyLegacyUi(entityDefinition, managedEntity, entityGameObject);

            return entityGameObject;
        }

        private void PlaceEntityInScene(SpawnHandle spawnHandle, GameObject entityGameObject)
        {
            if (entityGameObject == null || managedEntity?.definition == null) return;

            // If it's UI, ensure the single Myco canvas exists.
            if (managedEntity.isUiLayer)
            {
                _mycoCanvas = _mycoCanvas != null ? _mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(_mycoCanvas.transform, false);

                // Render order -> sibling index.
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

        private void AnalyzeUiFromComponents(EntityDef entityDefinition, out bool isUi, out int uiOrder)
        {
            isUi = false;
            uiOrder = 0;

            if (entityDefinition == null) return;

            // Legacy: treat these as UI.
            if (!string.IsNullOrWhiteSpace(entityDefinition.type) &&
                (string.Equals(entityDefinition.type, "UI_text", StringComparison.OrdinalIgnoreCase) || string.Equals(entityDefinition.type, "UI_image", StringComparison.OrdinalIgnoreCase)))
            {
                isUi = true;
                uiOrder = 0;
            }

            if (entityDefinition.components == null || entityDefinition.components.Length == 0) return;

            for (var componentIndex = 0; componentIndex < entityDefinition.components.Length; componentIndex++)
            {
                var componentId = entityDefinition.components[componentIndex];
                if (string.IsNullOrWhiteSpace(componentId)) continue;
                if (!_componentCatalog.TryGetValue(componentId, out var componentDefinition) || componentDefinition == null) continue;

                var componentType = GetString(componentDefinition, "type") ?? string.Empty;
                if (!string.Equals(componentType, "RenderLayer", StringComparison.Ordinal)) continue;

                var renderLayerName = GetString(componentDefinition, "layer") ?? string.Empty;
                if (string.Equals(renderLayerName, "ui", StringComparison.OrdinalIgnoreCase))
                {
                    isUi = true;
                    uiOrder = GetInt(componentDefinition, "order", 0);
                    return;
                }
            }
        }

        private void ApplyComponentsToGameObject(EntityDef entityDefinition, GameObject entityGameObject)
        {
            if (entityDefinition == null || entityGameObject == null) return;
            if (entityDefinition.components == null || entityDefinition.components.Length == 0) return;

            // Gather text + transform data first.
            string uiAnchor = null;
            var uiAnchoredPos = Vector2.zero;
            var uiSize = Vector2.zero;
            var hasUiTransform = false;

            string textValue = null;
            float fontSize = 36;
            float letterSpacing = 0;
            Color textColor = Color.white;
            var hasText = false;

            var hasRenderLayerUi = false;
            var renderLayerOrder = 0;

            for (var componentIndex = 0; componentIndex < entityDefinition.components.Length; componentIndex++)
            {
                var componentId = entityDefinition.components[componentIndex];
                if (string.IsNullOrWhiteSpace(componentId)) continue;

                if (!_componentCatalog.TryGetValue(componentId, out var componentDefinition) || componentDefinition == null)
                {
                    Debug.LogWarning($"[Myco] Unknown component id '{componentId}' on entity '{entityDefinition.id}'.");
                    continue;
                }

                var componentType = GetString(componentDefinition, "type") ?? string.Empty;
                switch (componentType)
                {
                    case "Tag":
                        break;

                    case "RenderLayer":
                    {
                        var renderLayerName = GetString(componentDefinition, "layer") ?? string.Empty;
                        if (string.Equals(renderLayerName, "ui", StringComparison.OrdinalIgnoreCase))
                        {
                            hasRenderLayerUi = true;
                            renderLayerOrder = GetInt(componentDefinition, "order", renderLayerOrder);
                        }
                        break;
                    }

                    case "UITransform":
                        uiAnchor = GetString(componentDefinition, "anchor") ?? uiAnchor;
                        uiAnchoredPos = GetVector2(componentDefinition, "anchored_position", uiAnchoredPos);
                        uiSize = GetVector2(componentDefinition, "size", uiSize);
                        hasUiTransform = true;
                        break;

                    case "TextContent":
                        textValue = GetString(componentDefinition, "value") ?? textValue;
                        hasText = true;
                        break;

                    case "TextStyle":
                        fontSize = GetFloat(componentDefinition, "font_size", fontSize);
                        letterSpacing = GetFloat(componentDefinition, "letter_spacing", letterSpacing);
                        hasText = true;
                        break;

                    case "TextColor":
                        textColor = UI.ParseColor(GetString(componentDefinition, "value"), fallbackColor: textColor);
                        hasText = true;
                        break;
                }
            }

            // Apply UI-specific Unity components.
            if (hasRenderLayerUi || hasUiTransform || hasText)
            {
                // ECS rule: RenderLayer decides UI parenting. But text/UITransform need a Canvas anyway,
                // so if RenderLayer is missing we still create the canvas to keep the entity visible.
                if (!hasRenderLayerUi)
                {
                    Debug.LogWarning($"[Myco] Entity '{entityDefinition.id}' has UI components but no RenderLayer(ui). Treating it as UI.");
                }

                _mycoCanvas = _mycoCanvas != null ? _mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(_mycoCanvas.transform, false);

                if (hasUiTransform)
                {
                    var rectTransform = UI.EnsureRectTransform(entityGameObject);
                    UI.ApplyUITransform(rectTransform, uiAnchor, uiAnchoredPos, uiSize, fallbackSizeDelta: new Vector2(600, 120));
                }

                if (hasText)
                {
                    var rectTransform = UI.EnsureRectTransform(entityGameObject);
                    if (!hasUiTransform)
                    {
                        UI.ApplyUITransform(rectTransform, "middle_center", Vector2.zero, Vector2.zero, fallbackSizeDelta: new Vector2(600, 120));
                    }

                    var textMeshProText = UI.EnsureText(entityGameObject);
                    textMeshProText.text = textValue ?? textMeshProText.text ?? string.Empty;
                    textMeshProText.fontSize = fontSize > 0 ? fontSize : 36;
                    textMeshProText.color = textColor;
                    textMeshProText.characterSpacing = letterSpacing;
                }

                // Apply render order last.
                if (hasRenderLayerUi)
                {
                    var uiParentTransform = entityGameObject.transform.parent;
                    if (uiParentTransform != null)
                    {
                        var maxSiblingIndex = Mathf.Max(0, uiParentTransform.childCount - 1);
                        entityGameObject.transform.SetSiblingIndex(Mathf.Clamp(renderLayerOrder, 0, maxSiblingIndex));
                    }
                }
            }
        }

        private void ApplyLegacyUi(EntityDef entityDefinition, SpawnHandle spawnHandle, GameObject entityGameObject)
        {
            if (entityDefinition == null || managedEntity == null || entityGameObject == null) return;
            if (string.IsNullOrWhiteSpace(entityDefinition.type)) return;

            if (string.Equals(entityDefinition.type, "UI_text", StringComparison.OrdinalIgnoreCase))
            {
                _mycoCanvas = _mycoCanvas != null ? _mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(_mycoCanvas.transform, false);

                var rectTransform = UI.EnsureRectTransform(entityGameObject);
                var anchoredPos = entityDefinition.UI_text != null ? new Vector2(entityDefinition.UI_text.anchored_position?[0] ?? 0, entityDefinition.UI_text.anchored_position?[1] ?? 0) : Vector2.zero;
                UI.ApplyUITransform(rectTransform, entityDefinition.UI_text?.anchor, anchoredPos, Vector2.zero, fallbackSizeDelta: new Vector2(600, 120));

                var textMeshProText = UI.EnsureText(entityGameObject);
                textMeshProText.text = entityDefinition.UI_text?.text ?? textMeshProText.text ?? string.Empty;
                textMeshProText.fontSize = entityDefinition.UI_text != null && entityDefinition.UI_text.font_size > 0 ? entityDefinition.UI_text.font_size : 36;
                textMeshProText.color = UI.ParseColor(entityDefinition.UI_text?.color, fallbackColor: Color.white);
                return;
            }

            if (string.Equals(entityDefinition.type, "UI_image", StringComparison.OrdinalIgnoreCase))
            {
                _mycoCanvas = _mycoCanvas != null ? _mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(_mycoCanvas.transform, false);

                var rectTransform = UI.EnsureRectTransform(entityGameObject);
                var anchoredPos = entityDefinition.UI_image != null ? new Vector2(entityDefinition.UI_image.anchored_position?[0] ?? 0, entityDefinition.UI_image.anchored_position?[1] ?? 0) : Vector2.zero;
                var size = entityDefinition.UI_image != null ? new Vector2(entityDefinition.UI_image.size?[0] ?? 128, entityDefinition.UI_image.size?[1] ?? 128) : new Vector2(128, 128);
                UI.ApplyUITransform(rectTransform, entityDefinition.UI_image?.anchor, anchoredPos, size, fallbackSizeDelta: new Vector2(128, 128));

                var imageComponent = UI.EnsureImage(entityGameObject);
                imageComponent.color = UI.ParseColor(entityDefinition.UI_image?.color, fallbackColor: Color.white);
                return;
            }
        }

        private void LoadComponentCatalog()
        {
            _componentCatalog.Clear();

            try
            {
                var folderPath = string.IsNullOrWhiteSpace(componentsFolderPath) ? "Mycology_ECS/components" : componentsFolderPath;
                var componentsFolder = Path.Combine(Application.dataPath, folderPath);
                if (!Directory.Exists(componentsFolder))
                {
                    if (logOnCreate) Debug.LogWarning($"[Myco] Components folder not found: {componentsFolder}");
                    return;
                }

                var componentFiles = Directory.GetFiles(componentsFolder, "*.json", SearchOption.TopDirectoryOnly);
                for (var fileIndex = 0; fileIndex < componentFiles.Length; fileIndex++)
                {
                    MergeComponentsFromJsonFile(componentFiles[fileIndex]);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Myco] Failed to load component catalog: {ex.Message}");
            }
        }

        private void MergeComponentsFromJsonFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return;

            try
            {
                var json = File.ReadAllText(fullPath);
                var parsed = MiniJson.Parse(json) as Dictionary<string, object>;
                if (parsed == null) return;

                if (!parsed.TryGetValue("components", out var compsObj)) return;
                if (compsObj is not Dictionary<string, object> comps) return;

                foreach (var kvp in comps)
                {
                    if (kvp.Value is Dictionary<string, object> compDef)
                    {
                        _componentCatalog[kvp.Key] = compDef;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Myco] Failed to merge components from '{fullPath}': {ex.Message}");
            }
        }

        private static Vector2 GetVector2(Dictionary<string, object> obj, string key, Vector2 fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is not List<object> list || list.Count < 2) return fallback;
            return new Vector2((float)ToDouble(list[0], fallback.x), (float)ToDouble(list[1], fallback.y));
        }

        private static string GetString(Dictionary<string, object> obj, string key)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return null;
            if (!obj.TryGetValue(key, out var v) || v == null) return null;
            return v as string;
        }

        private static float GetFloat(Dictionary<string, object> obj, string key, float fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            return (float)ToDouble(v, fallback);
        }

        private static int GetInt(Dictionary<string, object> obj, string key, int fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            return (int)ToDouble(v, fallback);
        }

        private static double ToDouble(object v, double fallback)
        {
            if (v == null) return fallback;
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return fallback;
        }

        private static class MiniJson
        {
            public static object Parse(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                return new Parser(json).ParseValue();
            }

            private sealed class Parser
            {
                private readonly string _s;
                private int _i;

                public Parser(string s)
                {
                    _s = s;
                    _i = 0;
                }

                public object ParseValue()
                {
                    SkipWs();
                    if (_i >= _s.Length) return null;

                    var c = _s[_i];
                    switch (c)
                    {
                        case '{':
                            return ParseObject();
                        case '[':
                            return ParseArray();
                        case '"':
                            return ParseString();
                        case 't':
                            return ConsumeLiteral("true", true);
                        case 'f':
                            return ConsumeLiteral("false", false);
                        case 'n':
                            return ConsumeLiteral("null", null);
                        default:
                            if (c == '-' || char.IsDigit(c)) return ParseNumber();
                            return null;
                    }
                }

                private Dictionary<string, object> ParseObject()
                {
                    _i++; // '{'
                    var dict = new Dictionary<string, object>(StringComparer.Ordinal);

                    SkipWs();
                    if (_i < _s.Length && _s[_i] == '}')
                    {
                        _i++;
                        return dict;
                    }

                    while (_i < _s.Length)
                    {
                        SkipWs();
                        var key = ParseString();

                        SkipWs();
                        if (_i < _s.Length && _s[_i] == ':') _i++;

                        var val = ParseValue();
                        dict[key ?? string.Empty] = val;

                        SkipWs();
                        if (_i >= _s.Length) break;

                        var c = _s[_i];
                        if (c == ',')
                        {
                            _i++;
                            continue;
                        }
                        if (c == '}')
                        {
                            _i++;
                            break;
                        }
                    }

                    return dict;
                }

                private List<object> ParseArray()
                {
                    _i++; // '['
                    var list = new List<object>();

                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ']')
                    {
                        _i++;
                        return list;
                    }

                    while (_i < _s.Length)
                    {
                        var val = ParseValue();
                        list.Add(val);

                        SkipWs();
                        if (_i >= _s.Length) break;

                        var c = _s[_i];
                        if (c == ',')
                        {
                            _i++;
                            continue;
                        }
                        if (c == ']')
                        {
                            _i++;
                            break;
                        }
                    }

                    return list;
                }

                private string ParseString()
                {
                    if (_i >= _s.Length || _s[_i] != '"') return null;
                    _i++;

                    var sb = new System.Text.StringBuilder();
                    while (_i < _s.Length)
                    {
                        var c = _s[_i++];
                        if (c == '"')
                        {
                            return sb.ToString();
                        }

                        if (c == '\\' && _i < _s.Length)
                        {
                            var esc = _s[_i++];
                            switch (esc)
                            {
                                case '"': sb.Append('"'); break;
                                case '\\': sb.Append('\\'); break;
                                case '/': sb.Append('/'); break;
                                case 'b': sb.Append('\b'); break;
                                case 'f': sb.Append('\f'); break;
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case 'u':
                                    if (_i + 3 < _s.Length)
                                    {
                                        var hex = _s.Substring(_i, 4);
                                        if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                                        {
                                            sb.Append((char)code);
                                        }
                                        _i += 4;
                                    }
                                    break;
                            }
                            continue;
                        }

                        sb.Append(c);
                    }

                    return sb.ToString();
                }

                private object ParseNumber()
                {
                    var start = _i;
                    while (_i < _s.Length)
                    {
                        var c = _s[_i];
                        if (!(char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')) break;
                        _i++;
                    }

                    var token = _s.Substring(start, _i - start);
                    if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    {
                        return d;
                    }
                    return 0d;
                }

                private object ConsumeLiteral(string literal, object value)
                {
                    if (_s.Length - _i >= literal.Length && string.CompareOrdinal(_s, _i, literal, 0, literal.Length) == 0)
                    {
                        _i += literal.Length;
                        return value;
                    }
                    return null;
                }

                private void SkipWs()
                {
                    while (_i < _s.Length)
                    {
                        var c = _s[_i];
                        if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
                        _i++;
                    }
                }
            }
        }
    }
}
