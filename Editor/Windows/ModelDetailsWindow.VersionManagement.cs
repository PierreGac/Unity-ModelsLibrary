using System;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing version management operations (deletion and import) for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        /// <summary>
        /// Draws the delete version button with comprehensive safety checks.
        /// Only visible to Artists/Admins. Shows contextual warnings and blocks deletion when unsafe.
        /// </summary>
        private void DrawDeleteVersionButton()
        {
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            UserRole role = identityProvider.GetUserRole();
            if (role != UserRole.Artist && role != UserRole.Admin)
            {
                return;
            }

            EditorGUILayout.Space(8);

            if (_loadingVersions)
            {
                EditorGUILayout.HelpBox("Checking version list...", MessageType.Info);
                return;
            }

            if (_availableVersions.Count == 0)
            {
                EditorGUILayout.HelpBox("Unable to retrieve version list from the repository. Deletion is disabled.", MessageType.Warning);
                return;
            }

            bool onlyVersion = !_hasOlderVersions;

            if (onlyVersion)
            {
                EditorGUILayout.HelpBox("This is the only version of this model. Keep at least one version in the library, so deletion is disabled.", MessageType.Warning);
            }
            else if (_isLatestVersion)
            {
                EditorGUILayout.HelpBox("You are viewing the latest version. Deleting it will promote the previous version to be the new latest version.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Deleting removes this version's payload, metadata, and preview files from the repository. Projects that already imported it keep their local copies.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(onlyVersion || _deletingVersion))
                {
                    Color originalColor = GUI.color;
                    GUI.color = Color.red;
                    if (GUILayout.Button("Delete This Version", GUILayout.Width(160), GUILayout.Height(26)))
                    {
                        if (ConfirmDeletion())
                        {
                            _ = DeleteVersionAsync();
                        }
                    }
                    GUI.color = originalColor;
                }
            }
        }

        private bool ConfirmDeletion()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Version",
                $"Are you sure you want to delete version {_version} of '{_meta.identity.name}'?\n\n" +
                "This permanently removes the version folder, payload files, preview images, and metadata from the repository.",
                "Delete",
                "Cancel");

            if (!confirmed)
            {
                return false;
            }

            if (_isLatestVersion)
            {
                return EditorUtility.DisplayDialog(
                    "Delete Latest Version",
                    "This is the latest version. Deleting it will promote an older version to be the new latest version.\n\nContinue?",
                    "Yes, Delete",
                    "Cancel");
            }

            return true;
        }

        /// <summary>
        /// Asynchronously deletes the current model version from the repository, updating open windows afterwards.
        /// </summary>
        private async Task DeleteVersionAsync()
        {
            if (_deletingVersion || _service == null)
            {
                return;
            }

            _deletingVersion = true;
            try
            {
                EditorUtility.DisplayProgressBar("Deleting Version", $"Deleting {_meta.identity.name} v{_version}...", 0.4f);
                
                // Yield to allow UI to update before starting the deletion
                await Task.Yield();
                
                bool deleted = await _service.DeleteVersionAsync(_modelId, _version);
                EditorUtility.ClearProgressBar();

                if (!deleted)
                {
                    // Schedule error dialog on main thread to avoid blocking
                    EditorApplication.delayCall += () =>
                    {
                        ErrorHandler.ShowError("Deletion Failed", $"The repository returned an error while deleting version {_version}.");
                    };
                    return;
                }

                // Store values for use in delayCall (capture before closing window)
                string deletedVersion = _version;
                string modelName = _meta.identity.name;
                string nextVersion = _availableVersions.FirstOrDefault(v => !string.Equals(v, _version, StringComparison.OrdinalIgnoreCase));
                string modelIdToOpen = _modelId;

                // Schedule all UI operations on the main thread to avoid blocking
                EditorApplication.delayCall += () =>
                {
                    // Show success dialog on main thread
                    EditorUtility.DisplayDialog("Version Deleted", $"Removed version {deletedVersion} of '{modelName}'.", "OK");

                    // Refresh Model Library windows
                    ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                    for (int i = 0; i < windows.Length; i++)
                    {
                        windows[i].ReinitializeAfterConfiguration();
                    }

                    // Close current window and open next version if available
                    ModelDetailsWindow currentWindow = GetWindow<ModelDetailsWindow>();
                    if (currentWindow != null)
                    {
                        currentWindow.Close();
                    }

                    if (!string.IsNullOrEmpty(nextVersion))
                    {
                        Open(modelIdToOpen, nextVersion);
                    }
                };
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                
                // Schedule error dialog on main thread to avoid blocking
                string errorMessage = ex.Message;
                EditorApplication.delayCall += () =>
                {
                    ErrorHandler.ShowError("Deletion Failed", $"Failed to delete version: {errorMessage}", ex);
                };
            }
            finally
            {
                _deletingVersion = false;
            }
        }

        private async Task ImportToProject()
        {
            try
            {
                titleContent.text = "Model Details - Importing...";
                EditorUtility.DisplayProgressBar("Importing Model", "Connecting to repository...", 0.1f);

                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(settings.repositoryRoot)
                    : new Repository.HttpRepository(settings.repositoryRoot);
                ModelLibraryService service = new ModelLibraryService(repo);

                EditorUtility.DisplayProgressBar("Importing Model", "Downloading model files...", 0.3f);
                (string cacheRoot, ModelMeta meta) = await service.DownloadModelVersionAsync(_modelId, _version);

                EditorUtility.DisplayProgressBar("Importing Model", "Copying files to Assets folder...", 0.6f);
                await ModelProjectImporter.ImportFromCacheAsync(cacheRoot, meta, cleanDestination: true);

                EditorUtility.DisplayProgressBar("Importing Model", "Finalizing import...", 0.9f);
                await Task.Delay(100); // Brief pause for UI update

                EditorUtility.ClearProgressBar();
                
                // Track analytics
                AnalyticsService.RecordEvent("import", _modelId, _version, meta.identity.name);
                
                // Refresh installation status after import
                _ = CheckInstallationStatusAsync();
                
                // Store values for use in delayCall (capture before closing window)
                string importedModelName = meta.identity.name;
                string importedVersion = meta.version;
                
                // Schedule UI operations on the main thread to avoid blocking
                EditorApplication.delayCall += () =>
                {
                    // Show completion dialog on main thread
                    EditorUtility.DisplayDialog("Import Complete", $"Imported '{importedModelName}' v{importedVersion} into Assets.", "OK");
                    
                    // Close the Model Details window after successful import
                    ModelDetailsWindow currentWindow = GetWindow<ModelDetailsWindow>();
                    if (currentWindow != null)
                    {
                        currentWindow.Close();
                    }
                };
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                ErrorHandler.ShowErrorWithRetry("Import Failed", $"Failed to import model: {ex.Message}",
                    async () => await ImportToProject(), ex);
            }
            finally
            {
                titleContent.text = "Model Details";
            }
        }
    }
}

