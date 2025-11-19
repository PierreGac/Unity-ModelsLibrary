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
                // Search for new naming convention (.modelLibrary.meta.json) first
                foreach (string manifestPath in Directory.EnumerateFiles(assetsRoot, NEW_MANIFEST_NAME, SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }

                // Fallback for old files created before the naming change
                foreach (string manifestPath in Directory.EnumerateFiles(assetsRoot, OLD_MANIFEST_NAME, SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
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

