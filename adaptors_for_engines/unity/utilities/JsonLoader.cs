using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mycology_ECS.Utils
{
    public static class JsonLoader
    {
        public static string LoadJsonForScene(string folderPath, string sceneName, bool logOnCreate = false)
        {
            var entitiesFolderFullPath = Path.Combine(Application.dataPath, folderPath);
            if (!Directory.Exists(entitiesFolderFullPath))
            {
                if (logOnCreate) Debug.LogWarning($"[JsonLoader] Entities folder not found: {entitiesFolderFullPath}");
                return null;
            }

            var entityJsonFiles = Directory.GetFiles(entitiesFolderFullPath, "*.json", SearchOption.TopDirectoryOnly);
            if (entityJsonFiles == null || entityJsonFiles.Length == 0)
            {
                if (logOnCreate) Debug.LogWarning($"[JsonLoader] No entity JSON files found in: {entitiesFolderFullPath}");
                return null;
            }

            var desiredFilename = sceneName + ".json";
            string bestMatchFullPath = null;

            foreach (var fileFullPath in entityJsonFiles)
            {
                var fileName = Path.GetFileName(fileFullPath);
                if (string.Equals(fileName, desiredFilename, StringComparison.OrdinalIgnoreCase))
                {
                    bestMatchFullPath = fileFullPath;
                    break;
                }
            }

            if (bestMatchFullPath == null)
            {
                foreach (var fileFullPath in entityJsonFiles)
                {
                    var fileStem = Path.GetFileNameWithoutExtension(fileFullPath);
                    if (string.Equals(fileStem, sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        bestMatchFullPath = fileFullPath;
                        break;
                    }
                }
            }

            if (bestMatchFullPath == null)
            {
                if (logOnCreate) Debug.LogWarning($"[JsonLoader] No entities JSON matched scene '{sceneName}'. Expected '{desiredFilename}' under {entitiesFolderFullPath}");
                return null;
            }

            if (logOnCreate)
            {
                Debug.Log($"[JsonLoader] Loading entities for scene '{sceneName}' from: {bestMatchFullPath}");
            }

            return File.ReadAllText(bestMatchFullPath);
        }
    }
}
