using System;
using System.Collections.Generic;
using Mycology_ECS.Utils;

namespace Mycology_ECS.translation_layers.unity
{
    internal sealed class MycoEntityTagIndex
    {
        private readonly Dictionary<string, List<string>> _tagToEntityIds;

        public MycoEntityTagIndex(IEqualityComparer<string> comparer = null)
        {
            _tagToEntityIds = new Dictionary<string, List<string>>(comparer ?? StringComparer.Ordinal);
        }

        public void Clear()
        {
            _tagToEntityIds.Clear();
        }

        public void IndexEntity(string entityId, EntityDef entityDefinition, MycoComponentCatalog componentCatalog)
        {
            if (string.IsNullOrWhiteSpace(entityId) || entityDefinition == null) return;
            if (entityDefinition.components == null || entityDefinition.components.Length == 0) return;

            for (var i = 0; i < entityDefinition.components.Length; i++)
            {
                var componentId = entityDefinition.components[i];
                if (string.IsNullOrWhiteSpace(componentId)) continue;

                if (componentCatalog == null || !componentCatalog.TryGet(componentId, out var componentDefinition) || componentDefinition == null) continue;

                var componentType = TypeConversionUtils.GetString(componentDefinition, "type") ?? string.Empty;
                if (!string.Equals(componentType, "Tag", StringComparison.Ordinal)) continue;

                var tagValue = TypeConversionUtils.GetString(componentDefinition, "value");
                if (string.IsNullOrWhiteSpace(tagValue)) continue;

                if (!_tagToEntityIds.TryGetValue(tagValue, out var list) || list == null)
                {
                    list = new List<string>(capacity: 1);
                    _tagToEntityIds[tagValue] = list;
                }

                list.Add(entityId);
            }
        }

        public bool TryGetFirstEntityId(string tag, out string entityId)
        {
            entityId = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            if (!_tagToEntityIds.TryGetValue(tag, out var list) || list == null || list.Count == 0)
            {
                return false;
            }

            entityId = list[0];
            return !string.IsNullOrWhiteSpace(entityId);
        }

        public bool TryGetEntityIds(string tag, out List<string> entityIds)
        {
            entityIds = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            if (!_tagToEntityIds.TryGetValue(tag, out var list) || list == null || list.Count == 0)
            {
                return false;
            }

            entityIds = list;
            return true;
        }
    }
}
