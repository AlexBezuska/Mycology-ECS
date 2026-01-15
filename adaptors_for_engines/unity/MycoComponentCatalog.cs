using System;
using System.Collections.Generic;
using System.IO;
using Mycology_ECS.Utils;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity
{
    internal sealed class MycoComponentCatalog
    {
        private readonly Dictionary<string, Dictionary<string, object>> _catalog;

        public MycoComponentCatalog(IEqualityComparer<string> comparer = null)
        {
            _catalog = new Dictionary<string, Dictionary<string, object>>(comparer ?? StringComparer.Ordinal);
        }

        public void Clear()
        {
            _catalog.Clear();
        }

        public bool TryGet(string componentId, out Dictionary<string, object> componentDefinition)
        {
            componentDefinition = null;
            if (string.IsNullOrWhiteSpace(componentId)) return false;
            return _catalog.TryGetValue(componentId, out componentDefinition) && componentDefinition != null;
        }

        public void Load(string componentsFolderPath)
        {
            _catalog.Clear();

            try
            {
                var folders = new List<string>
                {
                    Path.Combine(Application.dataPath, "Mycology_ECS/core_components"),
                    Path.Combine(Application.dataPath, string.IsNullOrWhiteSpace(componentsFolderPath) ? "Mycology_ECS/game_specific_ecs/components" : componentsFolderPath)
                };

                for (var folderIndex = 0; folderIndex < folders.Count; folderIndex++)
                {
                    var folder = folders[folderIndex];
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;

                    var files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
                    for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    {
                        var file = files[fileIndex];
                        if (string.IsNullOrWhiteSpace(file)) continue;

                        try
                        {
                            var json = File.ReadAllText(file);
                            var parsed = SimpleJson.Parse(json);
                            if (parsed is not Dictionary<string, object> root) continue;
                            if (!root.TryGetValue("components", out var componentsObj) || componentsObj == null) continue;
                            if (componentsObj is not Dictionary<string, object> componentsMap) continue;

                            foreach (var kvp in componentsMap)
                            {
                                var componentId = kvp.Key;
                                if (string.IsNullOrWhiteSpace(componentId)) continue;

                                if (kvp.Value is not Dictionary<string, object> componentDef) continue;
                                _catalog[componentId] = componentDef;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[Myco] Failed to parse components JSON '{file}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Myco] Failed to load component catalog: {ex.Message}");
            }
        }
    }
}
