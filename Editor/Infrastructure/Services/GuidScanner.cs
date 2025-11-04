using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using UnityEditor;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// GUID-based project model detection utility.
    /// Scans the Unity project for installed models by matching asset GUIDs from model metadata
    /// against the GUIDs of assets currently in the project.
    /// This is a fallback detection method when manifest files are not available.
    /// </summary>
    public static class GuidScanner
    {
        /// <summary>
        /// Scans the Unity project to find which models from the index are installed locally.
        /// Uses GUID matching to detect presence of model assets.
        /// Note: This method can detect that a model is installed but cannot determine the exact local version.
        /// </summary>
        /// <param name="entries">List of model index entries to check for installation.</param>
        /// <param name="metaLoader">Function to load metadata for a model ID (used to get GUIDs).</param>
        /// <returns>Dictionary mapping model IDs to their detected local versions (or latest version if exact version cannot be determined).</returns>
        public static async Task<Dictionary<string, string>> FindInstalledModelVersionsAsync(IEnumerable<ModelIndex.Entry> entries, System.Func<string, Task<ModelMeta>> metaLoader)
        {
            // Get all asset GUIDs currently in the Unity project
            string[] all = AssetDatabase.FindAssets(string.Empty);
            HashSet<string> set = new HashSet<string>(all);
            Dictionary<string, string> map = new Dictionary<string, string>(); // modelId → localVersion

            // Check each model entry to see if its assets are present in the project
            foreach (ModelIndex.Entry e in entries)
            {
                // Load metadata to get the list of GUIDs for this model
                ModelMeta meta = await metaLoader(e.id);
                if (meta == null)
                {
                    continue; // Skip if metadata cannot be loaded
                }

                // Check if any of the model's asset GUIDs are present in the project
                if (meta.assetGuids.Any(g => set.Contains(g)))
                {
                    // At least some assets present. We mark as present; version heuristic minimal.
                    // Since we can't determine exact version from GUIDs alone, we use the latest version.
                    map[e.id] = e.latestVersion;
                }
            }
            return map;
        }
    }
}

