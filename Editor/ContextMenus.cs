using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using ModelLibrary.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor
{
    /// <summary>
    /// Context menu items for the Unity Editor Project view.
    /// Provides right-click functionality to submit models, open models in the library,
    /// check for updates, and view model details directly from selected assets.
    /// </summary>
    public static class ContextMenus
    {
        /// <summary>
        /// Right-click context menu item to submit selected model assets.
        /// This menu item appears in the Project view when right-clicking on assets.
        /// </summary>
        [MenuItem("Assets/Model Library/Submit Model", false, 1000)]
        public static void SubmitModelFromSelection()
        {
            // Only allow Artists to submit models
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                EditorUtility.DisplayDialog("Access Denied",
                    "Model submission is only available for Artists. Please switch to Artist role in User Settings.",
                    "OK");
                return;
            }

            // Open the ModelSubmitWindow - it will automatically use the current selection
            ModelSubmitWindow.Open();
        }

        /// <summary>
        /// Validation method for the submit model menu item.
        /// The menu item will only be enabled if valid model assets are selected.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if at least one valid model asset is selected;
        /// otherwise <see langword="false"/>.
        /// </returns>
        [MenuItem("Assets/Model Library/Submit Model", true, 1000)]
        public static bool ValidateSubmitModelFromSelection()
        {
            // Only allow Artists to submit models
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                return false;
            }

            // Check if any assets are selected
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return false;
            }

            // Check if at least one selected asset is a valid model file type
            string[] validExtensions = {
                FileExtensions.FBX,
                FileExtensions.OBJ,
                FileExtensions.PNG,
                FileExtensions.TGA,
                FileExtensions.JPG,
                FileExtensions.JPEG,
                FileExtensions.PSD,
                FileExtensions.MAT
            };

            foreach (string guid in Selection.assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                // Skip directories/folders
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (validExtensions.Contains(extension))
                {
                    return true; // Found at least one valid model asset
                }
            }

            return false; // No valid model assets found
        }

        /// <summary>
        /// Right-click context menu item to open the selected model in Model Library browser.
        /// Finds the model by matching the selected asset's GUID against installed models.
        /// </summary>
        [MenuItem("Assets/Model Library/Open in Model Library", false, 1001)]
        public static void OpenModelInLibrary()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return;
            }

            // Try to find which model this asset belongs to
            string selectedGuid = Selection.assetGUIDs[0];
            string modelId = FindModelIdFromGuid(selectedGuid);

            if (string.IsNullOrEmpty(modelId))
            {
                EditorUtility.DisplayDialog("Model Not Found",
                    "The selected asset does not belong to any model in the library.\n\nMake sure the model is installed and the asset is part of a registered model.",
                    "OK");
                return;
            }

            // Open Model Library window and focus on the model
            ModelLibraryWindow.Open();
            ModelLibraryWindow window = EditorWindow.GetWindow<ModelLibraryWindow>();
            _ = FocusModelInLibraryAsync(window, modelId);
        }

        /// <summary>
        /// Validation method for opening model in library menu item.
        /// Only enabled if a valid asset is selected.
        /// </summary>
        [MenuItem("Assets/Model Library/Open in Model Library", true, 1001)]
        public static bool ValidateOpenModelInLibrary()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return false;
            }

            // Check if at least one selected asset belongs to a model
            return Selection.assetGUIDs.Any(guid => !string.IsNullOrEmpty(FindModelIdFromGuid(guid)));
        }

        /// <summary>
        /// Right-click context menu item to check for updates for the selected model.
        /// </summary>
        [MenuItem("Assets/Model Library/Check for Updates", false, 1002)]
        public static void CheckModelForUpdates()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return;
            }

            string selectedGuid = Selection.assetGUIDs[0];
            string modelId = FindModelIdFromGuid(selectedGuid);

            if (string.IsNullOrEmpty(modelId))
            {
                EditorUtility.DisplayDialog("Model Not Found",
                    "The selected asset does not belong to any model in the library.",
                    "OK");
                return;
            }

            _ = CheckModelUpdatesAsync(modelId);
        }

        /// <summary>
        /// Validation method for check updates menu item.
        /// </summary>
        [MenuItem("Assets/Model Library/Check for Updates", true, 1002)]
        public static bool ValidateCheckModelForUpdates() => ValidateOpenModelInLibrary();

        /// <summary>
        /// Right-click context menu item to view model details.
        /// </summary>
        [MenuItem("Assets/Model Library/View Details", false, 1003)]
        public static void ViewModelDetails()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return;
            }

            string selectedGuid = Selection.assetGUIDs[0];
            string modelId = FindModelIdFromGuid(selectedGuid);
            string version = FindModelVersionFromGuid(selectedGuid);

            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                EditorUtility.DisplayDialog("Model Not Found",
                    "The selected asset does not belong to any model in the library.",
                    "OK");
                return;
            }

            ModelDetailsWindow.Open(modelId, version);
        }

        /// <summary>
        /// Validation method for view details menu item.
        /// </summary>
        [MenuItem("Assets/Model Library/View Details", true, 1003)]
        public static bool ValidateViewModelDetails() => ValidateOpenModelInLibrary();

        /// <summary>
        /// Finds the model ID for a given asset GUID by scanning manifest files.
        /// </summary>
        private static string FindModelIdFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            // Search for manifest files in the project
            // Use file system enumeration because AssetDatabase.FindAssets() cannot find files starting with dot
            // Unity doesn't import files starting with dot, so they're not in the AssetDatabase
            List<string> manifestPaths = ManifestDiscoveryUtility.DiscoverAllManifestFiles("Assets");

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    ModelMeta meta = JsonUtility.FromJson<ModelMeta>(json);

                    if (meta != null && meta.assetGuids != null && meta.assetGuids.Contains(guid))
                    {
                        return meta.identity?.id;
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
        /// Finds the model version for a given asset GUID by scanning manifest files.
        /// </summary>
        private static string FindModelVersionFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            // Search for manifest files in the project
            // Use file system enumeration because AssetDatabase.FindAssets() cannot find files starting with dot
            // Unity doesn't import files starting with dot, so they're not in the AssetDatabase
            List<string> manifestPaths = ManifestDiscoveryUtility.DiscoverAllManifestFiles("Assets");

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    ModelMeta meta = JsonUtility.FromJson<ModelMeta>(json);

                    if (meta != null && meta.assetGuids != null && meta.assetGuids.Contains(guid))
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
        /// Focuses on a specific model in the Model Library window.
        /// </summary>
        private static async Task FocusModelInLibraryAsync(ModelLibraryWindow window, string modelId)
        {
            if (window == null || string.IsNullOrEmpty(modelId))
            {
                return;
            }

            // Wait a bit for the window to load
            await Task.Delay(DelayConstants.LONG_DELAY_MS);

            // Set search to filter to this model
            // Note: This requires accessing private fields, so we'll need to add a public method
            // For now, we'll just open the window - the user can search manually
            window.Repaint();
        }

        /// <summary>
        /// Checks for updates for a specific model.
        /// </summary>
        private static async Task CheckModelUpdatesAsync(string modelId)
        {
            try
            {
                Repository.IModelRepository repo = RepositoryFactory.CreateRepository();
                ModelLibraryService service = new ModelLibraryService(repo);

                bool hasUpdate = await service.HasUpdateAsync(modelId);

                if (hasUpdate)
                {
                    ModelIndex.Entry entry = (await service.GetIndexAsync()).Get(modelId);
                    string latestVersion = entry?.latestVersion ?? "unknown";

                    bool update = EditorUtility.DisplayDialog("Update Available",
                        $"Model '{entry?.name ?? modelId}' has an update available.\n\nLatest version: {latestVersion}\n\nWould you like to update now?",
                        "Update", "Cancel");

                    if (update)
                    {
                        // Open Model Library window to perform update
                        ModelLibraryWindow.Open();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("No Updates",
                        $"Model '{modelId}' is up to date.",
                        "OK");
                }
            }
            catch (System.Exception ex)
            {
                ErrorLogger.LogError("Check Updates Failed",
                    $"Failed to check for updates: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to check for updates: {ex.Message}",
                    "OK");
            }
        }
    }
}
