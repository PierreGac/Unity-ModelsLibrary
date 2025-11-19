using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing Unity lifecycle and initialization methods for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        /// <summary>
        /// Opens the model details window for a specific model version.
        /// </summary>
        /// <param name="id">The unique identifier of the model.</param>
        /// <param name="version">The version of the model to display.</param>
        public static void Open(string id, string version)
        {
            ModelDetailsWindow w = GetWindow<ModelDetailsWindow>("Model Details");
            w._modelId = id; w._version = version; w.Init();
            w.Show();
        }

        /// <summary>
        /// Initializes the window by setting up services and loading metadata.
        /// </summary>
        private void Init()
        {
            IModelRepository repo = RepositoryFactory.CreateRepository();
            _service = new ModelLibraryService(repo);
            _ = Load();
        }

        /// <summary>
        /// Asynchronously loads the model metadata and initializes the editing state.
        /// </summary>
        private async Task Load()
        {
            try
            {
                _meta = await _service.GetMetaAsync(_modelId, _version);
                if (_meta == null)
                {
                    ErrorLogger.LogError("Load Model Failed", 
                        $"Failed to load model metadata for {_modelId} version {_version}", 
                        ErrorHandler.ErrorCategory.Connection, null, $"ModelId: {_modelId}, Version: {_version}");
                    Repaint();
                    return;
                }

                _baselineMetaJson = JsonUtil.ToJson(_meta);
                _editedDescription = _meta.description ?? string.Empty;
                _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
                _editingTags = false;

                // Track view analytics
                if (_meta.identity != null && !string.IsNullOrEmpty(_meta.identity.name))
                {
                    AnalyticsService.RecordEvent("view", _modelId, _version, _meta.identity.name);
                }

                Repaint();

                await LoadVersionListAsync();
                _ = CheckInstallationStatusAsync();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Model Failed", 
                    $"Failed to load model: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}, Version: {_version}");
                Repaint();
            }
        }

        private async Task LoadVersionListAsync()
        {
            if (_service == null)
            {
                return;
            }

            _loadingVersions = true;
            try
            {
                _availableVersions.Clear();
                List<string> versions = await _service.GetAvailableVersionsAsync(_modelId);
                if (versions != null)
                {
                    _availableVersions.AddRange(versions);
                }

                if (_availableVersions.Count > 0)
                {
                    _isLatestVersion = string.Equals(_version, _availableVersions[0], StringComparison.OrdinalIgnoreCase);
                    _hasOlderVersions = _availableVersions.Any(v => !string.Equals(v, _version, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    _isLatestVersion = false;
                    _hasOlderVersions = false;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Version List Failed", 
                    $"Failed to load version list for {_modelId}: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}");
                _availableVersions.Clear();
                _isLatestVersion = false;
                _hasOlderVersions = false;
            }
            finally
            {
                _loadingVersions = false;
                Repaint();
            }
        }

        /// <summary>
        /// Checks if the model is installed locally and if an update is available.
        /// </summary>
        private async Task CheckInstallationStatusAsync()
        {
            if (_service == null || _checkingInstallStatus)
            {
                return;
            }

            _checkingInstallStatus = true;
            try
            {
                // Check if model is installed by scanning for manifest files
                // Use file system enumeration because AssetDatabase.FindAssets() cannot find files starting with dot
                // Unity doesn't import files starting with dot, so they're not in the AssetDatabase
                List<string> manifestPaths = new List<string>();

                // Search for new naming convention (.modelLibrary.meta.json) first, then old naming for backward compatibility
                manifestPaths.AddRange(ManifestDiscoveryUtility.DiscoverAllManifestFiles("Assets"));

                string foundLocalVersion = null;

                for (int i = 0; i < manifestPaths.Count; i++)
                {
                    string manifestPath = manifestPaths[i];
                    if (string.IsNullOrEmpty(manifestPath))
                    {
                        continue;
                    }

                    try
                    {
                        string json = await System.IO.File.ReadAllTextAsync(manifestPath);
                        ModelMeta meta = JsonUtil.FromJson<ModelMeta>(json);

                        if (meta == null)
                        {
                            continue;
                        }

                        // Fallback: If identity name is null or empty, use FBX name from folder path
                        meta.identity ??= new ModelIdentity();


                        if (string.IsNullOrEmpty(meta.identity.name))
                        {
                            // Extract FBX name from folder path (e.g., "Assets/Models/Benne/Benne.FBX" -> "Benne")
                            string folderPath = System.IO.Path.GetDirectoryName(manifestPath);
                            string folderName = System.IO.Path.GetFileName(folderPath);

                            // Remove extension if it's an FBX/OBJ folder name

                            string fbxName = folderName;
                            string ext = System.IO.Path.GetExtension(folderName).ToLowerInvariant();
                            if (ext == ".fbx" || ext == ".obj")
                            {
                                fbxName = System.IO.Path.GetFileNameWithoutExtension(folderName);
                            }


                            meta.identity.name = fbxName;
                            Debug.Log($"[ModelDetailsWindow] Set identity name fallback to '{fbxName}' from folder '{folderName}' for manifest at {manifestPath}");
                        }

                        // Handle old manifest files that might not have identity field
                        bool matchesModel = false;


                        if (meta.identity != null && !string.IsNullOrEmpty(meta.identity.id))
                        {
                            // New format: check by identity.id
                            matchesModel = string.Equals(meta.identity.id, _modelId, StringComparison.OrdinalIgnoreCase);
                            Debug.Log($"[ModelDetailsWindow] Checking manifest at {manifestPath}: version={meta.version}, identity.id={meta.identity.id}, matches={matchesModel}");
                        }
                        else
                        {
                            // Old format: try to match by folder path or other means
                            // Extract model name from path (e.g., "Assets/Models/Benne/Benne.FBX" -> "Benne")
                            string folderPath = System.IO.Path.GetDirectoryName(manifestPath);
                            string folderName = System.IO.Path.GetFileName(folderPath);

                            // Extract FBX name from folder (remove extension if present)

                            string fbxNameFromFolder = folderName;
                            string folderExt = System.IO.Path.GetExtension(folderName).ToLowerInvariant();
                            if (folderExt == ".fbx" || folderExt == ".obj")
                            {
                                fbxNameFromFolder = System.IO.Path.GetFileNameWithoutExtension(folderName);
                            }

                            // Try to get model name from the index to match

                            try
                            {
                                ModelIndex index = await _service.GetIndexAsync();
                                ModelIndex.Entry entry = index?.entries?.FirstOrDefault(e => string.Equals(e.id, _modelId, StringComparison.OrdinalIgnoreCase));


                                if (entry != null && !string.IsNullOrEmpty(entry.name))
                                {
                                    // Match by FBX name (sanitized model name)
                                    string sanitizedName = InstallPathUtils.SanitizeFolderName(entry.name);
                                    Debug.Log($"[ModelDetailsWindow] Sanitized name {sanitizedName} / entry name {entry.name} for {_modelId}");
                                    matchesModel = string.Equals(fbxNameFromFolder, sanitizedName, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(fbxNameFromFolder, entry.name, StringComparison.OrdinalIgnoreCase);


                                    Debug.Log($"[ModelDetailsWindow] Old manifest format detected at {manifestPath}: folder={folderName}, fbxName={fbxNameFromFolder}, version={meta.version}, modelName={entry.name}, sanitized={sanitizedName}, matches={matchesModel}");
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogError("Match Old Manifest Failed", 
                                    $"Failed to match old manifest: {ex.Message}", 
                                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}, ManifestPath: {manifestPath}");
                            }
                        }


                        if (matchesModel && !string.IsNullOrEmpty(meta.version))
                        {
                            Debug.Log($"[ModelDetailsWindow] Found local version {meta.version} for {_modelId}");
                            foundLocalVersion = meta.version;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("Read Manifest File Failed", 
                            $"Error reading manifest file {manifestPath}: {ex.Message}", 
                            ErrorHandler.CategorizeException(ex), ex, $"ManifestPath: {manifestPath}, ModelId: {_modelId}");
                        // Continue to next manifest file
                    }
                }

                _isInstalled = !string.IsNullOrEmpty(foundLocalVersion);
                _installedVersion = foundLocalVersion;

                // Check if an update is available
                if (_isInstalled && !string.IsNullOrEmpty(_installedVersion))
                {
                    // Get the latest version from the index
                    ModelIndex index = await _service.GetIndexAsync();
                    ModelIndex.Entry entry = index?.entries?.FirstOrDefault(e => string.Equals(e.id, _modelId, StringComparison.OrdinalIgnoreCase));


                    if (entry != null && !string.IsNullOrEmpty(entry.latestVersion))
                    {
                        _hasUpdate = ModelVersionUtils.NeedsUpgrade(_installedVersion, entry.latestVersion);
                    }
                    else
                    {
                        _hasUpdate = false;
                    }
                }
                else
                {
                    _hasUpdate = false;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Check Installation Status Failed", 
                    $"Failed to check installation status: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}");
                _isInstalled = false;
                _installedVersion = null;
                _hasUpdate = false;
            }
            finally
            {
                _checkingInstallStatus = false;
                Repaint();
            }
        }
    }
}

