using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for scanning the Unity project to detect installed models and check for updates.
    /// Uses ModelIndexService for local version detection.
    /// </summary>
    public class ModelScanService
    {
        private readonly ModelIndexService _indexService;
        private readonly IModelRepository _repo;

        public ModelScanService(ModelIndexService indexService, IModelRepository repo)
        {
            _indexService = indexService;
            _repo = repo;
        }

        /// <summary>
        /// Scan current project assets by GUIDs to detect presence of models and possible updates.
        /// </summary>
        public async Task<List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)>> ScanProjectForKnownModelsAsync()
        {
            ModelIndex index = await _indexService.GetIndexAsync();
            // Gather all asset GUIDs present in project
            string[] allGuids = AssetDatabase.FindAssets(string.Empty);
            HashSet<string> guidSet = new HashSet<string>(allGuids);

            List<(ModelIndex.Entry, bool, string)> results = new List<(ModelIndex.Entry, bool, string)>();

            foreach (ModelIndex.Entry e in index.entries)
            {
                // All models are visible (no project restrictions)

                // Try to find the actual local version by looking for .modelLibrary.meta.json files
                string foundLocalVersion = await FindLocalVersionAsync(e.id);

                // If no local version found via manifest files, try GUID-based detection as fallback
                if (string.IsNullOrEmpty(foundLocalVersion))
                {
                    foundLocalVersion = await FindLocalVersionByGuidsAsync(e.id, guidSet);
                }

                bool hasUpdate = false;
                if (!string.IsNullOrEmpty(foundLocalVersion))
                {
                    if (SemVer.TryParse(foundLocalVersion, out SemVer local) && SemVer.TryParse(e.latestVersion, out SemVer remote))
                    {
                        hasUpdate = remote.CompareTo(local) > 0;
                    }
                }

                results.Add((e, hasUpdate, foundLocalVersion));
            }

            return results;
        }

        /// <summary>
        /// Finds the local version of a model by scanning for .modelLibrary.meta.json manifest files (with backward compatibility for old naming).
        /// </summary>
        private async Task<string> FindLocalVersionAsync(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return null;
            }

            // Search for manifest files in the project
            // Use file system enumeration because AssetDatabase.FindAssets() cannot find files starting with dot
            // Unity doesn't import files starting with dot, so they're not in the AssetDatabase
            List<string> manifestPaths = new List<string>();

            // Search for new naming convention (.modelLibrary.meta.json) first, then old naming for backward compatibility
            foreach (string manifestPath in Directory.EnumerateFiles("Assets", ".modelLibrary.meta.json", SearchOption.AllDirectories))
            {
                manifestPaths.Add(manifestPath);
            }
            // Fallback for old files created before the naming change
            foreach (string manifestPath in Directory.EnumerateFiles("Assets", "modelLibrary.meta.json", SearchOption.AllDirectories))
            {
                manifestPaths.Add(manifestPath);
            }

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = await File.ReadAllTextAsync(manifestPath);
                    ModelMeta meta = JsonUtility.FromJson<ModelMeta>(json);

                    if (meta != null && meta.identity != null && string.Equals(meta.identity.id, modelId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return meta.version;
                    }
                }
                catch
                {
                    // Ignore errors reading manifest files
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the local version of a model by matching GUIDs from metadata against project assets.
        /// This is a fallback method when manifest files are not available.
        /// </summary>
        private async Task<string> FindLocalVersionByGuidsAsync(string modelId, HashSet<string> projectGuids)
        {
            if (string.IsNullOrEmpty(modelId) || _repo == null)
            {
                return null;
            }

            try
            {
                // Get available versions for this model
                List<string> versions = await _indexService.GetAvailableVersionsAsync(modelId);

                // Try versions in descending order (latest first)
                foreach (string version in versions)
                {
                    try
                    {
                        ModelMeta meta = await _repo.LoadMetaAsync(modelId, version);
                        if (meta != null && meta.assetGuids != null && meta.assetGuids.Any(guid => projectGuids.Contains(guid)))
                        {
                            return version;
                        }
                    }
                    catch
                    {
                        // Ignore errors loading metadata
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }
    }
}


