using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mycology_ECS.Utils
{
    internal static class UnityTagUtility
    {
        /// <summary>
        /// Ensures the given Unity tags exist in ProjectSettings/TagManager.asset.
        /// Editor-only; does nothing in player builds.
        /// </summary>
        public static void EnsureProjectTagsExist(IEnumerable<string> tags, bool logAdded = false)
        {
#if UNITY_EDITOR
            if (tags == null) return;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in tags)
            {
                var tag = (t ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(tag)) continue;
                if (string.Equals(tag, "Untagged", StringComparison.Ordinal)) continue;
                unique.Add(tag);
            }

            if (unique.Count == 0) return;

            try
            {
                UnityEditor.SerializedObject tagManager = null;
                try
                {
                    var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                    if (assets == null || assets.Length == 0) return;
                    tagManager = new UnityEditor.SerializedObject(assets[0]);
                }
                catch
                {
                    return;
                }

                if (tagManager == null) return;

                var tagsProp = tagManager.FindProperty("tags");
                if (tagsProp == null || !tagsProp.isArray) return;

                var changed = false;

                foreach (var tag in unique)
                {
                    if (Contains(tagsProp, tag)) continue;

                    var insertIndex = tagsProp.arraySize;
                    tagsProp.InsertArrayElementAtIndex(insertIndex);
                    tagsProp.GetArrayElementAtIndex(insertIndex).stringValue = tag;
                    changed = true;

                    if (logAdded)
                    {
                        Debug.Log($"[Myco] Added Unity tag '{tag}'");
                    }
                }

                if (!changed) return;

                tagManager.ApplyModifiedProperties();

                var targetObject = tagManager.targetObject;
                if (targetObject != null)
                {
                    UnityEditor.EditorUtility.SetDirty(targetObject);
                }

                UnityEditor.AssetDatabase.SaveAssets();
            }
            catch
            {
                // Swallow: tag creation is a best-effort editor convenience.
            }
#endif
        }

#if UNITY_EDITOR
        private static bool Contains(UnityEditor.SerializedProperty tagsArray, string tag)
        {
            if (tagsArray == null || !tagsArray.isArray) return false;

            for (var i = 0; i < tagsArray.arraySize; i++)
            {
                var element = tagsArray.GetArrayElementAtIndex(i);
                if (element == null) continue;
                if (string.Equals(element.stringValue, tag, StringComparison.Ordinal)) return true;
            }

            return false;
        }
#endif
    }
}
