using System.Collections.Generic;
using System.IO;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for discovering model library manifest files in the project.
    /// Handles both new naming convention (.modelLibrary.meta.json) and old naming (modelLibrary.meta.json).
    /// </summary>
    public static class ManifestDiscoveryUtility
    {
        /// <summary>
        /// Manifest file names (new and old naming conventions).
        /// </summary>
        private const string NEW_MANIFEST_NAME = ".modelLibrary.meta.json";
        private const string OLD_MANIFEST_NAME = "modelLibrary.meta.json";

        /// <summary>
        /// Discovers all manifest files in the Assets directory.
        /// Searches for both new naming convention (.modelLibrary.meta.json) and old naming (modelLibrary.meta.json).
        /// </summary>
        /// <param name="assetsRoot">Root directory to search (defaults to "Assets").</param>
        /// <returns>List of manifest file paths found in the project.</returns>
        public static List<string> DiscoverAllManifestFiles(string assetsRoot = "Assets")
        {
            List<string> manifestPaths = new List<string>();

            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot))
            {
                return manifestPaths;
            }

            try
            {
                // SECURITY (HIGH-04): Use SafeFileEnumerator to skip reparse points
                // (symlinks, junctions). The previous Directory.EnumerateFiles with
                // SearchOption.AllDirectories followed symlinks, which could cause
                // enumeration of sensitive files outside the project if a symlink
                // to e.g. ~/.ssh/ was placed inside Assets/.
                HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

                foreach (string manifestPath in SafeFileEnumerator.EnumerateFilesSafe(assetsRoot, NEW_MANIFEST_NAME))
                {
                    if (seen.Add(manifestPath))
                    {
                        manifestPaths.Add(manifestPath);
                    }
                }

                foreach (string manifestPath in SafeFileEnumerator.EnumerateFilesSafe(assetsRoot, OLD_MANIFEST_NAME))
                {
                    if (seen.Add(manifestPath))
                    {
                        manifestPaths.Add(manifestPath);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ErrorLogger.LogError("Manifest Discovery Failed",
                    $"Failed to discover manifest files in {assetsRoot}: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"AssetsRoot: {assetsRoot}");
            }

            return manifestPaths;
        }

        /// <summary>
        /// Discovers manifest files asynchronously on a background thread to avoid blocking the UI.
        /// </summary>
        /// <param name="assetsRoot">Root directory to search (defaults to "Assets").</param>
        /// <returns>Task that completes with a list of manifest file paths.</returns>
        public static System.Threading.Tasks.Task<List<string>> DiscoverAllManifestFilesAsync(string assetsRoot = "Assets")
        {
            return System.Threading.Tasks.Task.Run(() => DiscoverAllManifestFiles(assetsRoot));
        }
    }
}

