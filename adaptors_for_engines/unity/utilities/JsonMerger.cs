using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mycology_ECS.Utils
{
    public static class JsonMerger
    {
        /// <summary>
        /// Merges all JSON files in the given folders into a single list of T, using Unity's JsonUtility.
        /// </summary>
        /// <typeparam name="TRoot">Root type containing an array/list of T.</typeparam>
        /// <typeparam name="T">Type of the array/list element.</typeparam>
        /// <param name="folders">Folders to search for JSON files.</param>
        /// <param name="extractor">Function to extract the array/list from the root object.</param>
        /// <returns>List of merged T objects.</returns>
        public static List<T> MergeJsonArrays<TRoot, T>(IEnumerable<string> folders, Func<TRoot, IEnumerable<T>> extractor)
            where TRoot : class
        {
            var merged = new List<T>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                var jsonFiles = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var root = JsonUtility.FromJson<TRoot>(json);
                        if (root != null)
                        {
                            var items = extractor(root);
                            if (items != null)
                                merged.AddRange(items);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[JsonMerger] Failed to load from '{file}': {ex.Message}");
                    }
                }
            }
            return merged;
        }
    }
}
