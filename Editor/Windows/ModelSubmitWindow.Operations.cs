using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing business logic and operations for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Pre-populates form fields from currently selected assets in Project view.
        /// Extracts model name from file names (for FBX/OBJ), folder structure, or existing model manifests.
        /// Priority: 1) File name (FBX/OBJ), 2) Folder name, 3) Existing model name from manifest.
        /// </summary>
        private void PrePopulateFromSelection()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return;
            }

            // Get valid selected assets
            List<string> validAssets = new List<string>();
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
                if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (validExtensions.Contains(extension))
                {
                    validAssets.Add(assetPath);
                }
            }

            if (validAssets.Count > 0)
            {
                string firstAsset = validAssets[0];
                string suggestedName = null;

                // Priority 1: Try to extract model name from file name (for FBX/OBJ files)
                string fileName = Path.GetFileNameWithoutExtension(firstAsset);
                string extension = Path.GetExtension(firstAsset).ToLowerInvariant();
                if ((extension == FileExtensions.FBX || extension == FileExtensions.OBJ) && !string.IsNullOrWhiteSpace(fileName))
                {
                    suggestedName = fileName;
                }

                // Priority 2: Try to extract model name from folder structure
                if (string.IsNullOrWhiteSpace(suggestedName))
                {
                    string directory = Path.GetDirectoryName(firstAsset);
                    if (directory != null && directory.StartsWith("Assets/"))
                    {
                        string relativeDir = directory.Substring(7); // Remove "Assets/" prefix
                        string[] parts = relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            // Use the last folder name as model name suggestion
                            suggestedName = parts[parts.Length - 1];
                        }
                    }
                }

                // Priority 3: Try to get name from existing model manifest if asset belongs to an installed model
                if (string.IsNullOrWhiteSpace(suggestedName))
                {
                    string selectedGuid = Selection.assetGUIDs[0];
                    string modelId = FindModelIdFromSelectedAsset(selectedGuid);
                    if (!string.IsNullOrEmpty(modelId))
                    {
                        // Asset belongs to an existing model - try to get the model name from the index
                        // Note: This is a synchronous context, so we can't await. Skip index lookup to avoid blocking.
                        // The name will be suggested from file/folder structure instead.
                    }
                }

                // Apply the suggested name if we found one and the name field is still default
                if (!string.IsNullOrWhiteSpace(suggestedName) && _name == "New Model")
                {
                    _name = suggestedName;
                }
            }
        }

        private void OnModeChanged()
        {
            if (_mode == SubmitMode.New)
            {
                if (string.IsNullOrWhiteSpace(_changeSummary))
                {
                    _changeSummary = "Initial submission";
                }
                _installPath = DefaultInstallPath();
                _relativePath = GetDefaultRelativePath();
            }
            else
            {
                _changeSummary = string.Empty;
                if (!_isLoadingIndex && _existingModels.Count > 0)
                {
                    _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1);
                    _ = LoadBaseMetaForSelection();
                }
            }
        }

        private async Task LoadIndexAsync()
        {
            _isLoadingIndex = true;
            try
            {
                ModelIndex index = await _service.GetIndexAsync();
                _existingModels.Clear();
                if (index?.entries != null)
                {
                    _existingModels.AddRange(index.entries.OrderBy(e => e.name));
                }

                if (_mode == SubmitMode.Update && _existingModels.Count > 0)
                {
                    _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1);
                    await LoadBaseMetaForSelection();
                }
                else
                {
                    _latestSelectedMeta = null;
                }
            }
            catch
            {
                _existingModels.Clear();
            }
            finally
            {
                _isLoadingIndex = false;
                Repaint();
            }
        }

        private bool DrawUpdateSelection()
        {
            if (_isLoadingIndex)
            {
                EditorGUILayout.HelpBox("Loading model catalog...", MessageType.Info);
                return false;
            }

            if (_existingModels.Count == 0)
            {
                EditorGUILayout.HelpBox("No existing models available to update.", MessageType.Warning);
                return false;
            }

            string[] options = _existingModels.Select(e => $"{e.name} (latest v{e.latestVersion})").ToArray();
            int newIndex = EditorGUILayout.Popup("Model", _selectedModelIndex, options);
            if (newIndex != _selectedModelIndex)
            {
                _selectedModelIndex = newIndex;
                _ = LoadBaseMetaForSelection();
            }

            ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
            EditorGUILayout.LabelField("Current Latest", entry.latestVersion);
            if (_loadingBaseMeta)
            {
                EditorGUILayout.LabelField("Loading metadata...", EditorStyles.miniLabel);
                return false;
            }

            _name = entry.name;
            return true;
        }

        private async Task LoadBaseMetaForSelection()
        {
            if (_existingModels.Count == 0)
            {
                _latestSelectedMeta = null;
                return;
            }

            ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
            _loadingBaseMeta = true;
            try
            {
                _latestSelectedMeta = await _service.GetMetaAsync(entry.id, entry.latestVersion);
                _name = entry.name;
                _description = _latestSelectedMeta?.description ?? string.Empty;
                _tags = new List<string>(_latestSelectedMeta?.tags?.values ?? new List<string>());
                _installPath = _latestSelectedMeta?.installPath ?? DefaultInstallPath();
                _relativePath = _latestSelectedMeta?.relativePath ?? GetDefaultRelativePath();
                _version = SuggestNextVersion(entry.latestVersion);
            }
            catch
            {
                _latestSelectedMeta = null;
            }
            finally
            {
                _loadingBaseMeta = false;
                Repaint();
            }
        }

        private static string SuggestNextVersion(string current)
        {
            if (SemVer.TryParse(current, out SemVer parsed))
            {
                SemVer next = new SemVer(parsed.major, parsed.minor, parsed.patch + 1);
                return next.ToString();
            }

            return current;
        }

        /// <summary>
        /// Submits the model to the repository.
        /// Builds metadata from the form data, materializes files into a temporary folder,
        /// and uploads to the repository. Handles both new submissions and updates.
        /// </summary>
        private async Task Submit()
        {
            if (_isSubmitting)
            {
                return; // Prevent duplicate submissions
            }

            // Final validation check (should not happen due to UI validation, but safety net)
            List<string> validationErrors = GetValidationErrors();
            if (validationErrors.Count > 0)
            {
                ErrorHandler.ShowErrorDialog("Validation Error",
                    "Please fix the following issues before submitting:\n\n" + string.Join("\n• ", validationErrors),
                    ErrorHandler.ErrorCategory.Validation);
                return;
            }

            string summary = string.IsNullOrWhiteSpace(_changeSummary)
                ? (_mode == SubmitMode.New ? "Initial submission" : "Updated assets")
                : _changeSummary.Trim();

            if (_mode == SubmitMode.Update && string.IsNullOrEmpty(summary))
            {
                EditorUtility.DisplayDialog("Missing Summary", "Please provide a changelog summary for the update.", "OK");
                return;
            }

            string identityId = null;
            string identityName = _name;
            string previousVersion = null;

            if (_mode == SubmitMode.Update)
            {
                ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
                identityId = entry.id;
                identityName = entry.name;
                previousVersion = entry.latestVersion;
            }

            string temp = null;
            _isSubmitting = true;
            _cancelSubmission = false;
            string progressTitle = _mode == SubmitMode.Update ? "Submitting Update" : "Submitting Model";

            try
            {
                titleContent.text = $"Submit Model - {progressTitle}...";

                EditorUtility.DisplayProgressBar(progressTitle, "Preparing metadata...", 0.1f);
                if (_cancelSubmission)
                {
                    return;
                }

                ModelMeta meta = await ModelDeployer.BuildMetaFromSelectionAsync(identityName, identityId, _version, _description, _imageAbsPaths, _tags, _installPath, _relativePath, _idProvider);

                if (_cancelSubmission)
                {
                    return;
                }

                temp = Path.Combine(Path.GetTempPath(), "ModelSubmit_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);

                EditorUtility.DisplayProgressBar(progressTitle, "Copying model files...", 0.3f);
                if (_cancelSubmission)
                {
                    return;
                }

                await ModelDeployer.MaterializeLocalVersionFolderAsync(meta, temp);

                if (_cancelSubmission)
                {
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, $"Copying preview images... (0/{_imageAbsPaths.Count})", 0.5f);
                int imageIndex = 0;
                foreach (string abs in _imageAbsPaths)
                {
                    if (_cancelSubmission)
                    {
                        return;
                    }

                    imageIndex++;
                    EditorUtility.DisplayProgressBar(progressTitle, $"Copying preview images... ({imageIndex}/{_imageAbsPaths.Count})", 0.5f + (0.2f * imageIndex / _imageAbsPaths.Count));
                    try
                    {
                        string dst = Path.Combine(temp, "images", Path.GetFileName(abs));
                        File.Copy(abs, dst, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        string fileName = Path.GetFileName(abs);
                        ErrorLogger.LogError("Copy Image Failed", 
                            $"Failed to copy image '{fileName}': {ex.Message}", 
                            ErrorHandler.CategorizeException(ex), ex, $"ImagePath: {abs}, ImageIndex: {imageIndex}/{_imageAbsPaths.Count}");
                        throw new Exception($"Failed to copy image '{fileName}': {ex.Message}");
                    }
                }

                if (_cancelSubmission)
                {
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Uploading to repository...", 0.7f);
                string remoteRel = await _service.SubmitNewVersionAsync(meta, temp, summary);

                if (_cancelSubmission)
                {
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Updating index...", 0.9f);
                await _service.RefreshIndexAsync();

                if (_cancelSubmission)
                {
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Complete", 1.0f);
                await Task.Delay(100); // Brief pause to show completion

                ShowNotification("Submitted", $"Uploaded to: {remoteRel}");
                Debug.Log($"Model submitted successfully: {remoteRel}");

                // Refresh all open ModelLibraryWindow instances to show the new model
                EditorApplication.delayCall += () =>
                {
                    ModelLibraryWindow[] openWindows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                    for (int i = 0; i < openWindows.Length; i++)
                    {
                        ModelLibraryWindow window = openWindows[i];
                        if (window != null)
                        {
                            // Refresh index and manifest cache to show the newly submitted model
                            window.RefreshIndex();
                        }
                    }
                };

                if (_mode == SubmitMode.Update)
                {
                    _version = SuggestNextVersion(meta.version);
                    _changeSummary = string.Empty;
                    _ = LoadIndexAsync();
                }
                else
                {
                    _changeSummary = "Initial submission";
                }
            }
            catch (Exception ex)
            {
                if (!_cancelSubmission)
                {
                    ErrorHandler.ShowErrorWithRetry("Submission Failed", $"Failed to submit model: {ex.Message}",
                        async () => await Submit(), ex);
                }
                else
                {
                    EditorUtility.DisplayDialog("Submission Cancelled", "The submission was cancelled.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                titleContent.text = "Submit Model";
                _isSubmitting = false;
                _cancelSubmission = false;
                if (!string.IsNullOrEmpty(temp))
                {
                    try 
                    { 
                        Directory.Delete(temp, true); 
                    } 
                    catch (Exception cleanupEx)
                    {
                        // Log cleanup failure but don't throw - submission may have succeeded
                        ErrorLogger.LogError("Cleanup Temp Directory Failed", 
                            $"Failed to clean up temporary directory: {cleanupEx.Message}", 
                            ErrorHandler.CategorizeException(cleanupEx), cleanupEx, $"TempPath: {temp}");
                    }
                }
            }
        }

        private void ProcessKeyboardShortcuts(bool metadataReady)
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            bool ctrlOrCmd = current.control || current.command;

            if (ctrlOrCmd && current.shift && current.keyCode == KeyCode.S)
            {
                SaveDraft();
                ShowNotification("Draft Saved", "Current progress stored as draft.");
                Repaint();
                current.Use();
                return;
            }

            if (ctrlOrCmd && current.keyCode == KeyCode.Return)
            {
                if (TrySubmitFromShortcut(metadataReady))
                {
                    current.Use();
                }
                return;
            }

            if (ctrlOrCmd && current.shift && current.keyCode == KeyCode.L)
            {
                ModelLibraryShortcutsWindow.Open();
                current.Use();
            }
        }

        private bool TrySubmitFromShortcut(bool metadataReady)
        {
            if (_isSubmitting)
            {
                return false;
            }

            List<string> validationErrors = GetValidationErrors();
            bool disableSubmit = ShouldDisableSubmit(validationErrors, metadataReady);
            if (disableSubmit)
            {
                if (validationErrors.Count > 0)
                {
                    ShowNotification("Submit Blocked", "Resolve validation errors before submitting.");
                }
                else if (_mode == SubmitMode.Update && (!_existingModels.Any() || _isLoadingIndex || _loadingBaseMeta || !metadataReady))
                {
                    ShowNotification("Submit Blocked", "Select a valid model and wait for metadata to load.");
                }
                Repaint();
                return false;
            }

            ClearDraft();
            _ = Submit();
            return true;
        }

        /// <summary>
        /// Finds the model ID for a selected asset by checking if it belongs to an installed model.
        /// </summary>
        /// <param name="assetGuid">The GUID of the selected asset.</param>
        /// <returns>The model ID if found, null otherwise.</returns>
        private string FindModelIdFromSelectedAsset(string assetGuid)
        {
            if (string.IsNullOrEmpty(assetGuid))
            {
                return null;
            }

            // Search for manifest files in the project
            // Use file system enumeration because AssetDatabase.FindAssets() cannot find files starting with dot
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
                    
                    if (meta != null && meta.assetGuids != null && meta.assetGuids.Contains(assetGuid))
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

        private string DefaultInstallPath() => "Assets/Models/NewModel";

        /// <summary>
        /// Gets the default relative path for model submission based on selected assets.
        /// Prioritizes finding the model root folder (containing FBX/OBJ files) rather than
        /// using subfolders like Materials. Walks up directory tree if needed to find model root.
        /// </summary>
        private string GetDefaultRelativePath()
        {
            string[] selected = Selection.assetGUIDs;
            if (selected == null || selected.Length == 0)
            {
                return "Models/NewModel";
            }

            // Step 1: Look for FBX/OBJ files in selection first - these indicate model root
            string meshFilePath = null;
            foreach (string guid in selected)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (extension == FileExtensions.FBX || extension == FileExtensions.OBJ)
                {
                    meshFilePath = assetPath;
                    break; // Found a mesh file, use its directory
                }
            }

            // Step 2: If mesh file found, use its directory as model root
            if (!string.IsNullOrEmpty(meshFilePath))
            {
                string directory = Path.GetDirectoryName(meshFilePath);
                if (!string.IsNullOrEmpty(directory) && directory.StartsWith("Assets/"))
                {
                    string relativePath = directory[7..]; // Remove "Assets/" prefix
                    Debug.Log($"[ModelSubmitWindow] Found mesh file, using directory as relative path: {relativePath}");
                    return relativePath;
                }
            }

            // Step 3: No mesh file in selection - walk up directory tree to find model root
            // Start from first selected asset
            string firstAssetPath = AssetDatabase.GUIDToAssetPath(selected[0]);
            if (!string.IsNullOrEmpty(firstAssetPath) && firstAssetPath.StartsWith("Assets/"))
            {
                string currentDir = AssetDatabase.IsValidFolder(firstAssetPath) 
                    ? firstAssetPath 
                    : Path.GetDirectoryName(firstAssetPath);

                if (!string.IsNullOrEmpty(currentDir))
                {
                    // Walk up directory tree looking for folder containing FBX/OBJ files
                    string searchDir = currentDir;
                    int maxDepth = 5; // Prevent infinite loops
                    int depth = 0;

                    while (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets/") && depth < maxDepth)
                    {
                        // Check if this directory contains FBX/OBJ files
                        string[] filesInDir = Directory.GetFiles(searchDir, "*.*", SearchOption.TopDirectoryOnly);
                        bool hasMeshFiles = filesInDir.Any(file =>
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            return ext == FileExtensions.FBX || ext == FileExtensions.OBJ;
                        });

                        if (hasMeshFiles)
                        {
                            // Found model root directory
                            string relativePath = searchDir[7..]; // Remove "Assets/" prefix
                            Debug.Log($"[ModelSubmitWindow] Found model root directory (contains mesh files): {relativePath}");
                            return relativePath;
                        }

                        // Move up one level
                        string parentDir = Path.GetDirectoryName(searchDir);
                        if (string.IsNullOrEmpty(parentDir) || parentDir == searchDir)
                        {
                            break; // Reached root or no parent
                        }
                        searchDir = parentDir;
                        depth++;
                    }

                    // Step 4: Fallback - use directory of first selected asset (current behavior)
                    // But exclude common subfolders like Materials, Textures, etc.
                    string fallbackDir = currentDir;
                    if (fallbackDir.StartsWith("Assets/"))
                    {
                        string relativePath = fallbackDir[7..];
                        
                        // Exclude common subfolders - walk up one level if in Materials/Textures/etc
                        string[] subfoldersToExclude = { "Materials", "Textures", "Prefabs", "Scripts", "Animations" };
                        string[] pathParts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (pathParts.Length > 1 && subfoldersToExclude.Contains(pathParts[pathParts.Length - 1], StringComparer.OrdinalIgnoreCase))
                        {
                            // In a subfolder - use parent directory
                            relativePath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                            Debug.Log($"[ModelSubmitWindow] Excluded subfolder, using parent directory: {relativePath}");
                        }
                        else
                        {
                            Debug.Log($"[ModelSubmitWindow] Using directory of selected asset as relative path: {relativePath}");
                        }
                        
                        return relativePath;
                    }
                }
            }

            // Step 5: Final fallback
            Debug.Log("[ModelSubmitWindow] Could not determine relative path from selection, using default");
            return "Models/NewModel";
        }
    }
}


