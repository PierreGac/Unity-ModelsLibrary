using System;
using System.Collections.Generic;
using System.IO;
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
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// Checks if configuration is needed and initializes services if ready.
        /// </summary>
        private void OnEnable()
        {
            // Don't initialize during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _sortMode = (ModelSortMode)EditorPrefs.GetInt(__SortModePrefKey, (int)ModelSortMode.Name);
            _thumbnailSize = EditorPrefs.GetFloat(__ThumbnailSizePrefKey, __DEFAULT_THUMBNAIL_SIZE);
            _thumbnailSize = Mathf.Clamp(_thumbnailSize, __MIN_THUMBNAIL_SIZE, __MAX_THUMBNAIL_SIZE);

            _searchHistoryManager = new SearchHistoryManager(__SearchHistoryPrefKey, __MaxSearchHistory);
            _filterPresetManager = new FilterPresetManager(__FilterPresetsPrefKey);
            _favoritesManager = new FavoritesManager(__FavoritesPrefKey);
            _recentlyUsedManager = new RecentlyUsedManager(__RecentlyUsedPrefKey, __MaxRecentlyUsed);

            LoadImportHistory();

            if (!FirstRunWizard.IsConfigured())
            {
                _currentView = ViewType.FirstRunWizard;
                InitializeWizardState();
                return;
            }

            InitializeServices();

            if (_service != null && (DateTime.Now - _lastUpdateCheck) > MIN_UPDATE_CHECK_INTERVAL)
            {
                _ = CheckForUpdatesAsync();
            }

            EnableBackgroundUpdateChecking();

            // Subscribe to play mode state changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Enables periodic background update checking using <see cref="EditorApplication.update" />.
        /// </summary>
        private void EnableBackgroundUpdateChecking()
        {
            if (!_backgroundUpdateCheckEnabled)
            {
                EditorApplication.update += OnEditorUpdate;
                _backgroundUpdateCheckEnabled = true;
            }
        }

        /// <summary>
        /// Disables periodic background update checking.
        /// </summary>
        private void DisableBackgroundUpdateChecking()
        {
            if (_backgroundUpdateCheckEnabled)
            {
                EditorApplication.update -= OnEditorUpdate;
                _backgroundUpdateCheckEnabled = false;
            }
        }

        /// <summary>
        /// Called every frame by <see cref="EditorApplication.update" /> to trigger background update checks.
        /// </summary>
        private void OnEditorUpdate()
        {
            // Don't run background checks during play mode
            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (_service != null && _indexCache != null && (DateTime.Now - _lastUpdateCheck) > BACKGROUND_UPDATE_CHECK_INTERVAL)
            {
                _ = CheckForUpdatesAsync();
            }
        }

        /// <summary>
        /// Initializes the model library service based on current settings and begins loading the index.
        /// </summary>
        private void InitializeServices()
        {
            IModelRepository repository = RepositoryFactory.CreateRepository();
            _service = new ModelLibraryService(repository);
            _ = LoadIndexAsync();
            _ = RefreshManifestCacheAsync();
        }

        /// <summary>
        /// Fully refreshes the model library by recreating services and reloading all caches.
        /// This ensures that changes to repository location or settings are properly reflected.
        /// Use this method for a complete refresh that matches the initialization process.
        /// </summary>
        public void FullRefresh()
        {
            // Don't refresh during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ModelLibraryWindow] Cannot refresh during play mode.");
                return;
            }

            _indexCache = null;
            _loadingIndex = false;
            _authenticationError = null; // Clear any previous authentication errors
            ClearMetaCache();
            InitializeServices();
        }

        /// <summary>
        /// Reinitializes services after configuration changes.
        /// This is an alias for FullRefresh() to maintain backward compatibility.
        /// </summary>
        public void ReinitializeAfterConfiguration()
        {
            FullRefresh();
        }

        /// <summary>
        /// Refreshes the index cache and manifest cache to show newly submitted models.
        /// This does NOT recreate the service, so it won't pick up repository location changes.
        /// For a full refresh that includes service recreation, use FullRefresh() instead.
        /// Can be called from other windows (e.g., ModelSubmitWindow) after submission.
        /// </summary>
        public void RefreshIndex()
        {
            // Don't refresh during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ModelLibraryWindow] Cannot refresh index during play mode.");
                return;
            }

            _indexCache = null;
            _authenticationError = null; // Clear any previous authentication errors
            // Clear local install cache to force re-check of installation status
            _localInstallCache.Clear();
            _negativeCache.Clear();
            _ = LoadIndexAsync();
            _ = RefreshManifestCacheAsync();
        }

        /// <summary>
        /// Refreshes the manifest cache to update installation status.
        /// This should be called after removing a model from the project.
        /// </summary>
        public void RefreshManifestCache()
        {
            // Clear caches to force re-scan
            _localInstallCache.Clear();
            _negativeCache.Clear();
            _manifestCache.Clear();
            _manifestCacheInitialized = false;
            _ = RefreshManifestCacheAsync();
        }

        private void DrawConfigurationRequired()
        {
            EditorGUILayout.HelpBox("Model Library needs to be configured before use.", MessageType.Warning);
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Configuration Wizard", GUILayout.Height(30)))
            {
                NavigateToView(ViewType.FirstRunWizard);
                InitializeWizardState();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("The configuration wizard will help you set up:\n• Your user name\n• Repository location (local folder or server URL)", MessageType.Info);
        }

        private void DrawServicesNotInitialized()
        {
            EditorGUILayout.HelpBox("Services are not initialized. This should not happen.", MessageType.Error);
            EditorGUILayout.Space();

            if (GUILayout.Button("Retry Initialization", GUILayout.Height(30)))
            {
                InitializeServices();
            }
        }

        /// <summary>
        /// Asynchronously loads the model index from the repository.
        /// </summary>
        private async Task LoadIndexAsync()
        {
            // Don't load during play mode or when about to enter play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (_loadingIndex)
            {
                return;
            }

            _loadingIndex = true;
            _cacheWarmTriggered = false;
            try
            {
                titleContent.text = "Model Library - Loading...";
                EditorUtility.DisplayProgressBar("Loading Model Library", "Loading index from repository...", 0.1f);

                _indexCache = await _service.GetIndexAsync();
                _authenticationError = null; // Clear any previous authentication errors

                // Check again after async operation - play mode might have started
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorUtility.ClearProgressBar();
                    _loadingIndex = false;
                    return;
                }

                // If index is empty and repository is a network path, check if it's due to authentication
                if (_indexCache != null && (_indexCache.entries == null || _indexCache.entries.Count == 0))
                {
                    IModelRepository repository = _service != null ? RepositoryFactory.CreateRepository() : null;
                    if (repository is FileSystemRepository fsRepo && !string.IsNullOrEmpty(fsRepo.Root))
                    {
                        string rootPath = fsRepo.Root;
                        if (IsNetworkPathHelper(rootPath) && !CanAccessNetworkPathHelper(rootPath))
                        {
                            _authenticationError = $"Cannot access network repository: {rootPath}. Network authentication required. Please verify:\n" +
                                "• Your Windows credentials are correct\n" +
                                "• You are logged into the server\n" +
                                "• VPN connection is active (if required)\n" +
                                "• Network path is accessible";
                            titleContent.text = "Model Library - Authentication Error";
                            Repaint();
                            return;
                        }
                    }
                }

                TriggerCacheWarming();

                EditorUtility.DisplayProgressBar("Loading Model Library", "Processing models...", 0.5f);
                _ = CheckForUpdatesAsync();

                Repaint();
                titleContent.text = "Model Library";
            }
            catch (UnauthorizedAccessException ex)
            {
                // Only show errors if not entering play mode
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    _authenticationError = ex.Message;
                    string errorMessage = "Unable to access the repository. ";
                    if (ex.Message.Contains("authentication") || ex.Message.Contains("credentials") || ex.Message.Contains("Network authentication"))
                    {
                        errorMessage = ex.Message; // Use the detailed message from the exception
                    }
                    else
                    {
                        errorMessage += ex.Message;
                    }
                    ErrorHandler.ShowError("Authentication Required", errorMessage, ex);
                    titleContent.text = "Model Library - Authentication Error";
                }
            }
            catch (Exception ex)
            {
                // Only show errors if not entering play mode
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Check if index is empty vs. actual error
                    if (_indexCache != null && (_indexCache.entries == null || _indexCache.entries.Count == 0))
                    {
                        // Check if this might be an authentication issue
                        IModelRepository repository = _service != null ? RepositoryFactory.CreateRepository() : null;
                        if (repository is FileSystemRepository fsRepo && !string.IsNullOrEmpty(fsRepo.Root))
                        {
                            string rootPath = fsRepo.Root;
                            if (IsNetworkPathHelper(rootPath) && !CanAccessNetworkPathHelper(rootPath))
                            {
                                _authenticationError = $"Cannot access network repository: {rootPath}. Network authentication required. Please verify:\n" +
                                    "• Your Windows credentials are correct\n" +
                                    "• You are logged into the server\n" +
                                    "• VPN connection is active (if required)\n" +
                                    "• Network path is accessible";
                                ErrorHandler.ShowError("Authentication Required", _authenticationError, ex);
                                titleContent.text = "Model Library - Authentication Error";
                                return;
                            }
                        }

                        ErrorHandler.ShowError("No Models Found",
                            "The repository appears to be empty or inaccessible. Please verify:\n" +
                            "• Repository path is correct\n" +
                            "• Network connection is available\n" +
                            "• Repository server is accessible", ex);
                    }
                    else
                    {
                        ErrorHandler.ShowError("Load Failed",
                            "Unable to load the model library from the repository. Please check your connection and settings.", ex);
                    }
                    titleContent.text = "Model Library - Error";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _loadingIndex = false;
            }
        }

        /// <summary>
        /// Checks for model updates in the background.
        /// Optimized to read directly from the update cache instead of making individual calls.
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            // Don't check during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            try
            {
                _lastUpdateCheck = DateTime.Now;

                // GetUpdateCountAsync() already refreshes the update cache, so we can read from it directly
                _updateCount = await _service.GetUpdateCountAsync();

                // Check again after async operation
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                if (_indexCache?.entries != null)
                {
                    _modelUpdateStatus.Clear();

                    // Read directly from cached update info instead of calling HasUpdateAsync for each model
                    for (int i = 0; i < _indexCache.entries.Count; i++)
                    {
                        // Check play mode in loop as well
                        if (EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            return;
                        }

                        ModelIndex.Entry entry = _indexCache.entries[i];
                        ModelUpdateDetector.ModelUpdateInfo updateInfo = await _service.GetUpdateInfoAsync(entry.id);
                        _modelUpdateStatus[entry.id] = updateInfo?.hasUpdate ?? false;
                    }
                }

                UpdateNotesCount();
                UpdateWindowTitle();
                Repaint();
            }
            catch (Exception ex)
            {
                // Only log if not entering play mode
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogError($"Failed to check for updates: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Updates the window title to reflect pending updates and notes.
        /// Can be called externally to refresh the title after notification state changes.
        /// </summary>
        public void UpdateWindowTitle()
        {
            List<string> indicators = new List<string>();
            if (_updateCount > 0)
            {
                indicators.Add($"🔄 {_updateCount} update{(_updateCount == 1 ? string.Empty : "s")}");
            }

            if (_notesCount > 0)
            {
                indicators.Add($"📝 {_notesCount} note{(_notesCount == 1 ? string.Empty : "s")}");
            }

            titleContent.text = indicators.Count > 0
                ? $"Model Library ({string.Join(", ", indicators)})"
                : "Model Library";
        }

        /// <summary>
        /// Returns the colour associated with a user role.
        /// </summary>
        private Color GetRoleColor(UserRole role)
        {
            switch (role)
            {
                case UserRole.Artist:
                    return new Color(0.4f, 0.8f, 0.4f);
                case UserRole.Admin:
                    return new Color(0.8f, 0.4f, 0.4f);
                default:
                    return new Color(0.6f, 0.6f, 0.8f);
            }
        }

        /// <summary>
        /// Builds the tooltip describing capabilities of the active user role.
        /// </summary>
        private string GetRoleTooltip(UserRole role)
        {
            string baseText = $"Current role: {role}\n\n";
            switch (role)
            {
                case UserRole.Artist:
                    return baseText + "Available features:\n• Browse and import models\n• Submit new models\n• Update existing models\n• Manage versions\n• Leave feedback notes\n\nClick to change role in User Settings.";
                case UserRole.Admin:
                    return baseText + "Available features:\n• All Artist features\n• View analytics\n• Delete versions\n• System management\n\nClick to change role in User Settings.";
                default:
                    return baseText + "Available features:\n• Browse and import models\n• Leave feedback notes\n\nUpgrade to Artist role to submit models.\nClick to change role in User Settings.";
            }
        }

        /// <summary>
        /// Recomputes the number of models that contain unread notes.
        /// Can be called externally to refresh the count after notification state changes.
        /// </summary>
        public void UpdateNotesCount()
        {
            _notesCount = 0;
            if (_indexCache?.entries == null)
            {
                return;
            }

            foreach (ModelIndex.Entry entry in _indexCache.entries)
            {
                // HasNotes() now checks read state, so this count reflects unread notes only
                if (HasNotes(entry.id, entry.latestVersion))
                {
                    _notesCount++;
                }
            }
        }

        /// <summary>
        /// Recomputes the number of models that have unread updates.
        /// Can be called externally to refresh the count after notification state changes.
        /// </summary>
        public void UpdateUpdateCount()
        {
            _updateCount = 0;
            if (_indexCache?.entries == null)
            {
                return;
            }

            foreach (ModelIndex.Entry entry in _indexCache.entries)
            {
                // Check if model has update AND hasn't been marked as read
                bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;
                bool hasUpdateLocally = false;
                bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
                {
                    hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
                }

                bool hasUnreadUpdate = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);
                if (hasUnreadUpdate)
                {
                    _updateCount++;
                }
            }
        }

        /// <summary>
        /// Handles Unity play mode state changes to cancel ongoing operations and close windows.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            try
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                {
                    // Cancel ongoing operations
                    _loadingIndex = false;
                    _refreshingManifest = false;

                    // Clear progress bar safely
                    try
                    {
                        EditorUtility.ClearProgressBar();
                    }
                    catch
                    {
                        // Ignore errors clearing progress bar during play mode transition
                    }

                    // Disable background update checking during play mode
                    DisableBackgroundUpdateChecking();

                    // Close all Model Library windows when entering play mode
                    CloseAllModelLibraryWindows();
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    // Re-enable background checking when returning to edit mode
                    if (!_backgroundUpdateCheckEnabled && !EditorApplication.isPlaying)
                    {
                        EnableBackgroundUpdateChecking();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - play mode transition should continue
                Debug.LogWarning($"[ModelLibraryWindow] Error in play mode state change handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes all Model Library plugin windows when entering play mode.
        /// </summary>
        private static void CloseAllModelLibraryWindows()
        {
            try
            {
                // Close Model Library Browser
                ModelLibraryWindow[] browserWindows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
                if (browserWindows != null)
                {
                    for (int i = 0; i < browserWindows.Length; i++)
                    {
                        try
                        {
                            browserWindows[i].CloseWindow();
                        }
                        catch
                        {
                            // Silently ignore errors closing individual windows
                        }
                    }
                }

                // Close Model Details windows
                ModelDetailsWindow[] detailsWindows = Resources.FindObjectsOfTypeAll<ModelDetailsWindow>();
                if (detailsWindows != null)
                {
                    for (int i = 0; i < detailsWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = detailsWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                // Close Model Submit windows
                ModelSubmitWindow[] submitWindows = Resources.FindObjectsOfTypeAll<ModelSubmitWindow>();
                if (submitWindows != null)
                {
                    for (int i = 0; i < submitWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = submitWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                // Close Model Preview 3D windows
                ModelPreview3DWindow[] previewWindows = Resources.FindObjectsOfTypeAll<ModelPreview3DWindow>();
                if (previewWindows != null)
                {
                    for (int i = 0; i < previewWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = previewWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                // Close other Model Library windows
                BatchUploadWindow[] batchWindows = Resources.FindObjectsOfTypeAll<BatchUploadWindow>();
                if (batchWindows != null)
                {
                    for (int i = 0; i < batchWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = batchWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                AnalyticsWindow[] analyticsWindows = Resources.FindObjectsOfTypeAll<AnalyticsWindow>();
                if (analyticsWindows != null)
                {
                    for (int i = 0; i < analyticsWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = analyticsWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                ModelVersionComparisonWindow[] comparisonWindows = Resources.FindObjectsOfTypeAll<ModelVersionComparisonWindow>();
                if (comparisonWindows != null)
                {
                    for (int i = 0; i < comparisonWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = comparisonWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }

                ModelBulkTagWindow[] bulkTagWindows = Resources.FindObjectsOfTypeAll<ModelBulkTagWindow>();
                if (bulkTagWindows != null)
                {
                    for (int i = 0; i < bulkTagWindows.Length; i++)
                    {
                        try
                        {
                            EditorWindow window = bulkTagWindows[i];
                            if (window != null)
                            {
                                window.Close();
                            }
                        }
                        catch
                        {
                            // Silently ignore errors
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore all errors - play mode transition should continue
            }
        }

        /// <summary>
        /// Unity lifecycle method called when the window is disabled.
        /// Ensures background tasks and caches are cleaned up.
        /// </summary>
        private void OnDisable()
        {
            DisableBackgroundUpdateChecking();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ClearThumbnailCache();
            _loadingThumbnails.Clear();
        }

        /// <summary>
        /// Helper method to check if a path is on a network drive (UNC path or mapped network drive).
        /// </summary>
        private static bool IsNetworkPathHelper(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Check for UNC path (\\server\share)
            if (path.StartsWith(@"\\"))
            {
                return true;
            }

            // Check for mapped network drive (Z: where Z is mapped to network)
            if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
            {
                try
                {
                    DriveInfo drive = new DriveInfo(path.Substring(0, 2));
                    return drive.DriveType == DriveType.Network;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if a network path is accessible.
        /// </summary>
        private static bool CanAccessNetworkPathHelper(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                string rootPath = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(rootPath))
                {
                    // Local path without drive letter, assume accessible
                    return true;
                }

                // Use lightweight Directory.Exists check
                return Directory.Exists(rootPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - path exists but we don't have permission
                return false;
            }
            catch (IOException)
            {
                // Network error or path doesn't exist
                return false;
            }
            catch (Exception)
            {
                // Any other exception indicates the path is not accessible
                return false;
            }
        }
    }
}

