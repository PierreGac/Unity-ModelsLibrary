using System;
using System.Collections.Generic;
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
            _sortMode = (ModelSortMode)EditorPrefs.GetInt(__SortModePrefKey, (int)ModelSortMode.Name);

            _searchHistoryManager = new SearchHistoryManager(__SearchHistoryPrefKey, __MaxSearchHistory);
            _filterPresetManager = new FilterPresetManager(__FilterPresetsPrefKey);
            _favoritesManager = new FavoritesManager(__FavoritesPrefKey);
            _recentlyUsedManager = new RecentlyUsedManager(__RecentlyUsedPrefKey, __MaxRecentlyUsed);

            LoadImportHistory();

            if (!FirstRunWizard.IsConfigured())
            {
                FirstRunWizard.MaybeShow();
                return;
            }

            InitializeServices();

            if (_service != null && (DateTime.Now - _lastUpdateCheck) > MIN_UPDATE_CHECK_INTERVAL)
            {
                _ = CheckForUpdatesAsync();
            }

            EnableBackgroundUpdateChecking();
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
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repository = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);

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
            _indexCache = null;
            _loadingIndex = false;
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
            _indexCache = null;
            _ = LoadIndexAsync();
            _ = RefreshManifestCacheAsync();
        }

        private void DrawConfigurationRequired()
        {
            EditorGUILayout.HelpBox("Model Library needs to be configured before use.", MessageType.Warning);
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Configuration Wizard", GUILayout.Height(30)))
            {
                FirstRunWizard.MaybeShow();
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

                TriggerCacheWarming();

                EditorUtility.DisplayProgressBar("Loading Model Library", "Processing models...", 0.5f);
                _ = CheckForUpdatesAsync();

                Repaint();
                titleContent.text = "Model Library";
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError("Load Failed",
                    "Unable to load the model library from the repository. Please check your connection and settings.", ex);
                titleContent.text = "Model Library - Error";
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
            try
            {
                _lastUpdateCheck = DateTime.Now;
                
                // GetUpdateCountAsync() already refreshes the update cache, so we can read from it directly
                _updateCount = await _service.GetUpdateCountAsync();

                if (_indexCache?.entries != null)
                {
                    _modelUpdateStatus.Clear();
                    
                    // Read directly from cached update info instead of calling HasUpdateAsync for each model
                    for (int i = 0; i < _indexCache.entries.Count; i++)
                    {
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
                Debug.LogError($"Failed to check for updates: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the window title to reflect pending updates and notes.
        /// </summary>
        private void UpdateWindowTitle()
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
        /// Recomputes the number of models that contain notes.
        /// </summary>
        private void UpdateNotesCount()
        {
            _notesCount = 0;
            if (_indexCache?.entries == null)
            {
                return;
            }

            foreach (ModelIndex.Entry entry in _indexCache.entries)
            {
                if (HasNotes(entry.id, entry.latestVersion))
                {
                    _notesCount++;
                }
            }
        }

        /// <summary>
        /// Unity lifecycle method called when the window is disabled.
        /// Ensures background tasks and caches are cleaned up.
        /// </summary>
        private void OnDisable()
        {
            DisableBackgroundUpdateChecking();
            ClearThumbnailCache();
            _loadingThumbnails.Clear();
        }
    }
}

