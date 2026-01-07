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
        /// Draws the delete buttons with comprehensive safety checks.
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
            bool canDeleteVersion = !onlyVersion && !_deletingVersion && !_deletingModel;
            bool canDeleteModel = !_deletingVersion && !_deletingModel;

            // Only show version deletion UI if there are older versions available
            if (!onlyVersion)
            {
                if (_isLatestVersion)
                {
                    EditorGUILayout.HelpBox("You are viewing the latest version. Deleting it will promote the previous version to be the new latest version.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Deleting removes this version's payload, metadata, and preview files from the repository. Projects that already imported it keep their local copies.", MessageType.Info);
                }

                // Delete version button
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!canDeleteVersion))
                    {
                        Color originalColor = GUI.color;
                        GUI.color = Color.red;
                        if (GUILayout.Button("Delete this version and restore previous", GUILayout.Width(250), GUILayout.Height(26)))
                        {
                            if (ConfirmVersionDeletion())
                            {
                                _ = DeleteVersionAsync();
                            }
                        }
                        GUI.color = originalColor;
                    }
                }

                EditorGUILayout.Space(5);
            }

            // Delete model button or Remove from project button
            if (_isInstalled && !string.IsNullOrEmpty(_installPath))
            {
                // Show "Remove from project" for installed models
                EditorGUILayout.HelpBox("Remove this model from your project. This will delete the model files from Assets but will not remove it from the repository.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(_deletingModel))
                    {
                        Color originalColor = GUI.color;
                        GUI.color = new Color(1f, 0.6f, 0.2f); // Orange for removal from project
                        if (GUILayout.Button("Remove from project", GUILayout.Width(200), GUILayout.Height(26)))
                        {
                            if (ConfirmRemoveFromProject())
                            {
                                _ = RemoveFromProjectAsync();
                            }
                        }
                        GUI.color = originalColor;
                    }
                }
            }
            else
            {
                // Show "Delete this model" for non-installed models (database deletion)
                EditorGUILayout.HelpBox("⚠️ WARNING: Deleting the entire model will permanently remove all versions, metadata, and files from the repository. This action cannot be undone.", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!canDeleteModel))
                    {
                        Color originalColor = GUI.color;
                        GUI.color = new Color(1f, 0.3f, 0.3f); // Darker red for model deletion
                        if (GUILayout.Button("Delete this model", GUILayout.Width(200), GUILayout.Height(26)))
                        {
                            if (ConfirmModelDeletion())
                            {
                                _ = DeleteModelAsync();
                            }
                        }
                        GUI.color = originalColor;
                    }
                }
            }
        }

        /// <summary>
        /// Confirms version deletion with the user through a dialog.
        /// Shows additional warning if deleting the latest version.
        /// </summary>
        /// <returns>True if the user confirmed deletion, false otherwise.</returns>
        private bool ConfirmVersionDeletion()
        {
            if (_meta == null || _meta.identity == null)
            {
                Debug.LogError("[ModelDetailsWindow] Cannot confirm deletion: metadata is null");
                return false;
            }
            
            string modelName = _meta.identity.name ?? "Unknown Model";
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Version",
                $"Are you sure you want to delete version {_version ?? "Unknown"} of '{modelName}'?\n\n" +
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
        /// Confirms model deletion with the user through a dialog with double confirmation.
        /// </summary>
        /// <returns>True if the user confirmed deletion through both dialogs, false otherwise.</returns>
        private bool ConfirmModelDeletion()
        {
            if (_meta == null || _meta.identity == null)
            {
                Debug.LogError("[ModelDetailsWindow] Cannot confirm deletion: metadata is null");
                return false;
            }
            
            string modelName = _meta.identity.name ?? "Unknown Model";
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Model",
                $"⚠️ WARNING: Are you sure you want to delete the entire model '{modelName}'?\n\n" +
                "This will permanently remove:\n" +
                "• All versions of this model\n" +
                "• All payload files, metadata, and preview images\n" +
                "• The model entry from the index\n\n" +
                "This action CANNOT be undone!",
                "Yes, Delete Model",
                "Cancel");

            if (!confirmed)
            {
                return false;
            }

            // Double confirmation for model deletion
            // Reuse modelName variable from above
            return EditorUtility.DisplayDialog(
                "Final Confirmation",
                $"You are about to PERMANENTLY DELETE '{modelName}' and all its versions.\n\n" +
                "Are you absolutely sure?",
                "Yes, I'm Sure",
                "Cancel");
        }

        /// <summary>
        /// Asynchronously deletes the entire model from the repository, updating open windows afterwards.
        /// </summary>
        private async Task DeleteModelAsync()
        {
            if (_deletingModel || _service == null)
            {
                return;
            }

            _deletingModel = true;
            try
            {
                string modelName = _meta?.identity?.name ?? _modelId ?? "Unknown Model";
                EditorUtility.DisplayProgressBar("Deleting Model", $"Deleting {modelName} and all versions...", ProgressBarConstants.MID_OPERATION);

                // Yield to allow UI to update before starting the deletion

                await Task.Yield();


                bool deleted = await _service.DeleteModelAsync(_modelId);
                EditorUtility.ClearProgressBar();

                if (!deleted)
                {
                    // Schedule error dialog on main thread to avoid blocking
                    // Reuse modelName variable from above
                    EditorApplication.delayCall += () =>
                    {
                        ErrorHandler.ShowError("Deletion Failed", $"The repository returned an error while deleting model '{modelName}'.");
                    };
                    return;
                }

                // Store values for use in delayCall (capture before closing window)
                string deletedModelName = _meta?.identity?.name ?? _modelId ?? "Unknown Model";

                // Schedule all UI operations on the main thread to avoid blocking
                EditorApplication.delayCall += () =>
                {
                    // Show success dialog on main thread
                    EditorUtility.DisplayDialog("Model Deleted", $"Removed '{deletedModelName}' and all its versions from the repository.", "OK");

                    // Refresh Model Library windows
                    ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                    for (int i = 0; i < windows.Length; i++)
                    {
                        windows[i].ReinitializeAfterConfiguration();
                    }

                    // Close current window
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

                // Schedule error dialog on main thread to avoid blocking

                string errorMessage = ex.Message;
                EditorApplication.delayCall += () =>
                {
                    ErrorHandler.ShowError("Deletion Failed", $"Failed to delete model: {errorMessage}", ex);
                };
            }
            finally
            {
                _deletingModel = false;
            }
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
                string modelName = _meta?.identity?.name ?? _modelId ?? "Unknown Model";
                string version = _version ?? "Unknown";
                EditorUtility.DisplayProgressBar("Deleting Version", $"Deleting {modelName} v{version}...", ProgressBarConstants.MID_OPERATION);

                // Yield to allow UI to update before starting the deletion

                await Task.Yield();


                bool deleted = await _service.DeleteVersionAsync(_modelId, _version);
                
                if (!deleted)
                {
                    EditorUtility.ClearProgressBar();
                    // Schedule error dialog on main thread to avoid blocking
                    EditorApplication.delayCall += () =>
                    {
                        ErrorHandler.ShowError("Deletion Failed", $"The repository returned an error while deleting version {_version}.");
                    };
                    return;
                }

                // Clear local cache for the deleted version
                EditorUtility.DisplayProgressBar("Deleting Version", "Clearing local cache...", ProgressBarConstants.MID_OPERATION);
                await _service.ClearCacheForModelAsync(_modelId, _version);

                // Invalidate index cache to force refresh
                await _service.RefreshIndexAsync();

                // Refresh version list after deletion to get updated list
                EditorUtility.DisplayProgressBar("Deleting Version", "Refreshing version list...", ProgressBarConstants.MID_OPERATION);
                await LoadVersionListAsync();
                EditorUtility.ClearProgressBar();

                // Store values for use in delayCall (capture before closing window)
                // Reuse modelName and version variables from above
                string deletedVersion = version;
                string modelNameForDelayCall = modelName;
                string modelIdToOpen = _modelId;
                
                // Get next available version from refreshed list
                string nextVersion = _availableVersions != null && _availableVersions.Count > 0
                    ? _availableVersions.FirstOrDefault(v => !string.Equals(v, _version, StringComparison.OrdinalIgnoreCase))
                    : null;
                
                bool hasOtherVersions = !string.IsNullOrEmpty(nextVersion) && _availableVersions != null && _availableVersions.Count > 0;

                // Schedule all UI operations on the main thread to avoid blocking
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        // Show success dialog on main thread
                        string message = hasOtherVersions
                            ? $"Removed version {deletedVersion} of '{modelNameForDelayCall}'."
                            : $"Removed version {deletedVersion} of '{modelNameForDelayCall}'. This was the last version.";
                        EditorUtility.DisplayDialog("Version Deleted", message, "OK");

                        // Refresh Model Library windows
                        ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                        for (int i = 0; i < windows.Length; i++)
                        {
                            windows[i].ReinitializeAfterConfiguration();
                        }

                        // Close current window
                        ModelDetailsWindow currentWindow = GetWindow<ModelDetailsWindow>();
                        if (currentWindow != null)
                        {
                            currentWindow.Close();
                        }

                        // Only open next version if it exists and is valid
                        if (hasOtherVersions && !string.IsNullOrEmpty(nextVersion))
                        {
                            // Verify version still exists before opening
                            try
                            {
                                Open(modelIdToOpen, nextVersion);
                            }
                            catch (Exception openEx)
                            {
                                ErrorLogger.LogError("Open Next Version Failed",
                                    $"Failed to open next version '{nextVersion}' after deletion: {openEx.Message}",
                                    ErrorHandler.CategorizeException(openEx), openEx,
                                    $"ModelId: {modelIdToOpen}, NextVersion: {nextVersion}");
                                
                                // Show error but don't block - deletion succeeded
                                EditorUtility.DisplayDialog("Warning",
                                    $"Version deleted successfully, but failed to open next version '{nextVersion}': {openEx.Message}",
                                    "OK");
                            }
                        }
                        else
                        {
                            // No other versions available - window already closed, show info
                            Debug.Log($"[ModelDetailsWindow] Deleted last version of model '{modelName}'. No other versions available.");
                        }
                    }
                    catch (Exception uiEx)
                    {
                        ErrorLogger.LogError("UI Update After Deletion Failed",
                            $"Error updating UI after version deletion: {uiEx.Message}",
                            ErrorHandler.CategorizeException(uiEx), uiEx,
                            $"ModelId: {modelIdToOpen}, DeletedVersion: {deletedVersion}");
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

        /// <summary>
        /// Confirms removal of the model from the project with the user through a dialog.
        /// </summary>
        /// <returns>True if the user confirmed removal, false otherwise.</returns>
        private bool ConfirmRemoveFromProject()
        {
            if (_meta == null || _meta.identity == null)
            {
                Debug.LogError("[ModelDetailsWindow] Cannot confirm removal: metadata is null");
                return false;
            }
            
            string modelName = _meta.identity.name ?? "Unknown Model";
            return EditorUtility.DisplayDialog(
                "Remove from Project",
                $"Are you sure you want to remove '{modelName}' from your project?\n\n" +
                "This will delete the model files from the Assets folder, but the model will remain in the repository.",
                "Remove",
                "Cancel");
        }

        /// <summary>
        /// Asynchronously removes the model from the project by deleting its install directory.
        /// This does not delete the model from the repository, only from the local project.
        /// </summary>
        private async Task RemoveFromProjectAsync()
        {
            if (_deletingModel || string.IsNullOrEmpty(_installPath))
            {
                return;
            }

            _deletingModel = true;
            try
            {
                string modelName = _meta?.identity?.name ?? _modelId ?? "Unknown Model";
                EditorUtility.DisplayProgressBar("Removing from Project", $"Removing {modelName} from project...", ProgressBarConstants.MID_OPERATION);

                await Task.Yield();

                // Delete the install directory using AssetDatabase
                // The install path should already be relative to project root (e.g., "Assets/Models/ModelName")
                string relativePath = _installPath.Replace('\\', '/');
                
                // Ensure path starts with "Assets/" for AssetDatabase
                if (!relativePath.StartsWith("Assets/"))
                {
                    // If path is absolute, convert to relative
                    string projectRoot = System.IO.Path.GetFullPath("Assets/..").Replace('\\', '/');
                    string normalizedInstallPath = _installPath.Replace('\\', '/');
                    if (normalizedInstallPath.StartsWith(projectRoot))
                    {
                        relativePath = normalizedInstallPath.Substring(projectRoot.Length).TrimStart('/');
                    }
                    else
                    {
                        // Fallback: try to extract relative path from absolute path
                        string assetsPath = System.IO.Path.GetFullPath("Assets").Replace('\\', '/');
                        if (normalizedInstallPath.StartsWith(assetsPath))
                        {
                            relativePath = normalizedInstallPath.Substring(assetsPath.Length).TrimStart('/');
                        }
                    }
                }

                // Delete using AssetDatabase (handles both files and directories)
                if (!string.IsNullOrEmpty(relativePath) && System.IO.Directory.Exists(_installPath))
                {
                    AssetDatabase.DeleteAsset(relativePath);
                    AssetDatabase.Refresh();
                }

                EditorUtility.ClearProgressBar();

                // Clear installation status
                _isInstalled = false;
                _installedVersion = null;
                _installPath = null;

                // Store values for use in delayCall
                string removedModelName = modelName;
                string removedModelId = _modelId;

                // Schedule UI operations on the main thread
                EditorApplication.delayCall += () =>
                {
                    EditorUtility.DisplayDialog("Removed from Project", $"Removed '{removedModelName}' from your project.", "OK");

                    // Refresh Model Library windows to update installation status
                    ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                    for (int i = 0; i < windows.Length; i++)
                    {
                        ModelLibraryWindow window = windows[i];
                        // Invalidate the cache for this specific model
                        window.InvalidateLocalInstallCache(removedModelId);
                        // Refresh the manifest cache to pick up the removal
                        window.RefreshManifestCache();
                        // Navigate back to browser if we're on the details view for this model
                        if (window.GetCurrentView() == ModelLibraryWindow.ViewType.ModelDetails)
                        {
                            string currentModelId = window.GetViewParameter<string>("modelId", string.Empty);
                            if (string.Equals(currentModelId, removedModelId, System.StringComparison.OrdinalIgnoreCase))
                            {
                                window.NavigateToView(ModelLibraryWindow.ViewType.Browser);
                            }
                        }
                    }

                    // Refresh the details window to show updated state (if still open)
                    Repaint();
                };
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();

                string errorMessage = ex.Message;
                EditorApplication.delayCall += () =>
                {
                    ErrorHandler.ShowError("Removal Failed", $"Failed to remove model from project: {errorMessage}", ex);
                };
            }
            finally
            {
                _deletingModel = false;
            }
        }

        private async Task ImportToProject()
        {
            try
            {
                titleContent.text = "Model Details - Importing...";
                EditorUtility.DisplayProgressBar("Importing Model", "Connecting to repository...", ProgressBarConstants.INITIAL);

                IModelRepository repo = RepositoryFactory.CreateRepository();
                ModelLibraryService service = new ModelLibraryService(repo);

                EditorUtility.DisplayProgressBar("Importing Model", "Downloading model files...", ProgressBarConstants.PREPARING);
                (string cacheRoot, ModelMeta meta) = await service.DownloadModelVersionAsync(_modelId, _version);

                EditorUtility.DisplayProgressBar("Importing Model", "Copying files to Assets folder...", ProgressBarConstants.COPYING_IMAGES);
                string installPath = await ModelProjectImporter.ImportFromCacheAsync(cacheRoot, meta, cleanDestination: true);

                EditorUtility.DisplayProgressBar("Importing Model", "Finalizing import...", ProgressBarConstants.FINALIZING);
                await Task.Delay(DelayConstants.UI_UPDATE_DELAY_MS); // Brief pause for UI update

                EditorUtility.ClearProgressBar();

                // Track analytics
                AnalyticsService.RecordEvent("import", _modelId, _version, meta.identity.name);

                // Update ModelLibraryWindow cache to mark model as installed
                // This ensures the browser view immediately shows the model as installed
                ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                for (int i = 0; i < windows.Length; i++)
                {
                    ModelLibraryWindow window = windows[i];
                    if (window != null)
                    {
                        // Read the manifest file and update the cache
                        string manifestPath = System.IO.Path.Combine(installPath, ".modelLibrary.meta.json");
                        if (!System.IO.File.Exists(manifestPath))
                        {
                            manifestPath = System.IO.Path.Combine(installPath, "modelLibrary.meta.json");
                        }
                        
                        if (System.IO.File.Exists(manifestPath))
                        {
                            try
                            {
                                string json = await System.IO.File.ReadAllTextAsync(manifestPath);
                                ModelMeta manifestMeta = JsonUtil.FromJson<ModelMeta>(json);
                                if (manifestMeta != null && manifestMeta.identity != null && manifestMeta.identity.id == _modelId)
                                {
                                    // Update the cache using the public method
                                    window.UpdateLocalInstallCache(_modelId, manifestMeta);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ModelDetailsWindow] Failed to update ModelLibraryWindow cache: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Fallback: use the meta we have if manifest file doesn't exist yet
                            window.UpdateLocalInstallCache(_modelId, meta);
                        }
                    }
                }

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

