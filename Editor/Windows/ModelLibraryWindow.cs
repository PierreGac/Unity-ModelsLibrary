using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Data;
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
    /// Main browser window for the Model Library system.
    /// Provides a unified interface to browse, search, filter, and manage 3D models from the repository.
    /// Supports both list and grid view modes with thumbnail previews, update detection, and role-based access control.
    /// </summary>
    public class ModelLibraryWindow : EditorWindow
    {
        /// <summary>
        /// Display mode for the model browser.
        /// </summary>
        private enum ViewMode
        {
            /// <summary>Traditional list view with detailed information for each model.</summary>
            List,
            /// <summary>Grid view with thumbnail previews and compact information.</summary>
            Grid
        }

        // Core Services
        /// <summary>Service instance for interacting with the model repository.</summary>
        private ModelLibraryService _service;

        // UI State
        /// <summary>Current search query text entered by the user.</summary>
        private string _search = string.Empty;
        /// <summary>Manager for search history persistence and UI.</summary>
        private SearchHistoryManager _searchHistoryManager;
        /// <summary>Scroll position for the main model list/grid view.</summary>
        private Vector2 _scroll;
        /// <summary>Dictionary tracking which model entries are expanded in list view.</summary>
        private Dictionary<string, bool> _expanded = new();
        /// <summary>Whether the tag filter panel is currently expanded.</summary>
        private bool _showTagFilter;
        /// <summary>Scroll position for the tag filter panel.</summary>
        private Vector2 _tagScroll;
        /// <summary>Current view mode (List or Grid). Defaults to Grid.</summary>
        private ViewMode _viewMode = ViewMode.Grid;

        // Sorting
        /// <summary>Current sort mode.</summary>
        private ModelSortMode _sortMode = ModelSortMode.Name;
        /// <summary>EditorPrefs key for storing sort preference.</summary>
        private const string __SortModePrefKey = "ModelLibrary.SortMode";
        /// <summary>EditorPrefs key for search history.</summary>
        private const string __SearchHistoryPrefKey = "ModelLibrary.SearchHistory";
        /// <summary>Maximum number of search history entries to keep.</summary>
        private const int __MaxSearchHistory = 10;
        /// <summary>EditorPrefs key for filter presets.</summary>
        private const string __FilterPresetsPrefKey = "ModelLibrary.FilterPresets";

        // Caching and Loading State
        /// <summary>Cache of loaded model metadata, keyed by "modelId@version".</summary>
        private readonly Dictionary<string, ModelMeta> _metaCache = new();
        /// <summary>Set of metadata keys currently being loaded to prevent duplicate requests.</summary>
        private readonly HashSet<string> _loadingMeta = new();
        /// <summary>Cache of locally installed model metadata, keyed by model ID.</summary>
        private readonly Dictionary<string, ModelMeta> _localInstallCache = new();
        /// <summary>Cache of all manifest files found in Assets folder, keyed by model ID.</summary>
        private readonly Dictionary<string, ModelMeta> _manifestCache = new();
        /// <summary>Flag indicating if the manifest cache has been initialized.</summary>
        private bool _manifestCacheInitialized = false;
        /// <summary>Set of model IDs that were checked but not found locally (negative cache).</summary>
        private readonly HashSet<string> _negativeCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Cache of loaded thumbnail textures for grid view, keyed by "modelId@version#thumb".</summary>
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new();
        /// <summary>Set of thumbnail keys currently being loaded to prevent duplicate requests.</summary>
        private readonly HashSet<string> _loadingThumbnails = new();
        /// <summary>Cached model index to avoid repeated loading.</summary>
        private ModelIndex _indexCache;
        /// <summary>Flag indicating if the index is currently being loaded.</summary>
        private bool _loadingIndex = false;
        /// <summary>Reference to the index used for tag cache generation (for change detection).</summary>
        private ModelIndex _tagSource;

        // Import/Download State
        /// <summary>Set of model IDs currently being imported to prevent duplicate imports.</summary>
        private readonly HashSet<string> _importsInProgress = new();

        // Tag Filtering
        /// <summary>Set of currently selected tags for filtering models (case-insensitive comparison).</summary>
        private readonly HashSet<string> _selectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Manager for tag cache operations.</summary>
        private TagCacheManager _tagCacheManager = new TagCacheManager();
        /// <summary>Manager for filter preset operations.</summary>
        private FilterPresetManager _filterPresetManager;

        // Bulk Operations
        /// <summary>Set of model IDs selected for bulk operations.</summary>
        private readonly HashSet<string> _selectedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Flag indicating if bulk selection mode is active.</summary>
        private bool _bulkSelectionMode = false;

        // Update Detection
        /// <summary>Total count of models with available updates.</summary>
        private int _updateCount = 0;
        /// <summary>Dictionary tracking which models have updates available, keyed by model ID.</summary>
        private readonly Dictionary<string, bool> _modelUpdateStatus = new Dictionary<string, bool>();

        // Favorites & Recently Used
        /// <summary>Manager for favorites persistence.</summary>
        private FavoritesManager _favoritesManager;
        /// <summary>Manager for recently used models persistence.</summary>
        private RecentlyUsedManager _recentlyUsedManager;
        /// <summary>EditorPrefs key for favorites.</summary>
        private const string __FavoritesPrefKey = "ModelLibrary.Favorites";
        /// <summary>EditorPrefs key for recently used models.</summary>
        private const string __RecentlyUsedPrefKey = "ModelLibrary.RecentlyUsed";
        /// <summary>Maximum number of recently used models to track.</summary>
        private const int __MaxRecentlyUsed = 20;
        /// <summary>Current filter mode.</summary>
        private ModelLibraryUIDrawer.FilterMode _filterMode = ModelLibraryUIDrawer.FilterMode.All;
        /// <summary>Helper for install path operations.</summary>
        private InstallPathHelper _installPathHelper = new InstallPathHelper();
        /// <summary>
        /// Menu item to open the Model Library browser window.
        /// </summary>
        [MenuItem("Tools/Model Library/Browser")]
        public static void Open()
        {
            ModelLibraryWindow win = GetWindow<ModelLibraryWindow>("Model Library");
            win.Show();
        }

        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// Checks if configuration is needed and initializes services if ready.
        /// </summary>
        private void OnEnable()
        {
            // Load sort preference from EditorPrefs
            _sortMode = (ModelSortMode)EditorPrefs.GetInt(__SortModePrefKey, (int)ModelSortMode.Name);

            // Initialize managers
            _searchHistoryManager = new SearchHistoryManager(__SearchHistoryPrefKey, __MaxSearchHistory);
            _filterPresetManager = new FilterPresetManager(__FilterPresetsPrefKey);
            _favoritesManager = new FavoritesManager(__FavoritesPrefKey);
            _recentlyUsedManager = new RecentlyUsedManager(__RecentlyUsedPrefKey, __MaxRecentlyUsed);

            // Check if configuration is needed before creating services
            if (!FirstRunWizard.IsConfigured())
            {
                FirstRunWizard.MaybeShow();
                return; // Don't initialize services until configured
            }

            // Only initialize services if properly configured
            InitializeServices();
        }

        /// <summary>
        /// Initializes the model library service based on current settings.
        /// Creates the appropriate repository (FileSystem or HTTP) and begins loading the index.
        /// </summary>
        private void InitializeServices()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);
            _service = new ModelLibraryService(repo);
            _ = LoadIndexAsync();
            _ = RefreshManifestCacheAsync();
        }

        /// <summary>
        /// Reinitializes the window after configuration changes.
        /// Called by FirstRunWizard when settings are saved.
        /// Clears cached state and reloads services with new configuration.
        /// </summary>
        public void ReinitializeAfterConfiguration()
        {
            // Clear any existing state
            _indexCache = null;
            _loadingIndex = false;

            // Reinitialize services with new configuration
            InitializeServices();
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
        /// Caches the result and triggers background update checking.
        /// Prevents duplicate concurrent loads using the _loadingIndex flag.
        /// </summary>
        private async Task LoadIndexAsync()
        {
            if (_loadingIndex)
            {
                return; // Already loading, skip duplicate request
            }

            _loadingIndex = true;
            try
            {
                titleContent.text = "Model Library - Loading...";
                EditorUtility.DisplayProgressBar("Loading Model Library", "Loading index from repository...", 0.1f);

                _indexCache = await _service.GetIndexAsync();

                EditorUtility.DisplayProgressBar("Loading Model Library", "Processing models...", 0.5f);

                // Check for updates in the background after index loads
                _ = CheckForUpdatesAsync();

                Repaint(); // Refresh the UI when index is loaded
                titleContent.text = "Model Library";
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError("Load Failed", "Failed to load model index from repository", ex);
                titleContent.text = "Model Library - Error";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _loadingIndex = false;
            }
        }

        /// <summary>
        /// Asynchronously checks for available model updates.
        /// Updates both the global update count and per-model update status.
        /// This runs in the background after the index loads to avoid blocking the UI.
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Get total count of models with updates
                _updateCount = await _service.GetUpdateCountAsync();

                // Update individual model status for badge display
                if (_indexCache?.entries != null)
                {
                    _modelUpdateStatus.Clear();
                    foreach (ModelIndex.Entry entry in _indexCache.entries)
                    {
                        bool hasUpdate = await _service.HasUpdateAsync(entry.id);
                        _modelUpdateStatus[entry.id] = hasUpdate;
                    }
                }

                Repaint(); // Refresh UI to show update indicators
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to check for updates: {ex.Message}");
            }
        }

        private void OnGUI()
        {
            // Handle keyboard shortcuts
            HandleKeyboardShortcuts();

            // Check if we need to show configuration first
            if (!FirstRunWizard.IsConfigured())
            {
                DrawConfigurationRequired();
                return;
            }

            // Check if services are initialized
            if (_service == null)
            {
                DrawServicesNotInitialized();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.SetNextControlName("SearchField");
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(200));
                _search = newSearch;

                // Search history dropdown button
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchHistoryManager.ShowSearchHistoryMenu((item) =>
                    {
                        _search = item;
                        GUI.FocusControl(null);
                        Repaint();
                    }, () => { });
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        _search = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    _indexCache = null; // Clear cache to force reload
                    _ = LoadIndexAsync();
                    _ = RefreshManifestCacheAsync(); // Also refresh manifest cache
                }

                // Update indicator and refresh button
                if (_updateCount > 0)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUILayout.Button($"Updates ({_updateCount})", EditorStyles.toolbarButton, GUILayout.Width(100));
                    }
                }
                if (GUILayout.Button("Check Updates", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    _ = CheckForUpdatesAsync();
                }

                // Bulk operations toggle
                bool newBulkMode = GUILayout.Toggle(_bulkSelectionMode, "Select", EditorStyles.toolbarButton, GUILayout.Width(70));
                if (newBulkMode != _bulkSelectionMode)
                {
                    _bulkSelectionMode = newBulkMode;
                    if (!_bulkSelectionMode)
                    {
                        _selectedModels.Clear();
                    }
                    Repaint();
                }

                // Bulk operations buttons
                using (new EditorGUI.DisabledScope(_selectedModels.Count == 0))
                {
                    if (GUILayout.Button($"Import ({_selectedModels.Count})", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    {
                        _ = BulkImportAsync();
                    }
                    if (GUILayout.Button($"Update ({_selectedModels.Count})", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    {
                        _ = BulkUpdateAsync();
                    }
                }

                // Only show Submit Model button for Artists
                SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                if (identityProvider.GetUserRole() == UserRole.Artist)
                {
                    if (GUILayout.Button("Submit Model", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    {
                        ModelSubmitWindow.Open();
                    }
                }
                if (GUILayout.Button("User", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    UserSettingsWindow.Open();
                }

                GUILayout.FlexibleSpace();

                // Sort dropdown
                GUILayout.Label("Sort:", EditorStyles.miniLabel, GUILayout.Width(35));
                ModelSortMode newSortMode = (ModelSortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarPopup, GUILayout.Width(80));
                if (newSortMode != _sortMode)
                {
                    _sortMode = newSortMode;
                    EditorPrefs.SetInt(__SortModePrefKey, (int)_sortMode);
                    Repaint();
                }

                // View mode toggle
                string[] viewModeLabels = { "List", "Grid" };
                int newViewMode = GUILayout.Toolbar((int)_viewMode, viewModeLabels, EditorStyles.toolbarButton, GUILayout.Width(100));
                if (newViewMode != (int)_viewMode)
                {
                    _viewMode = (ViewMode)newViewMode;
                    Repaint();
                }
            }
            // Use cached index if available, otherwise show loading
            if (_indexCache == null)
            {
                ModelLibraryUIDrawer.DrawEmptyState("Loading model index...", "Please wait while we fetch the latest models from the repository.",
                    () =>
                    {
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
                return;
            }

            ModelIndex index = _indexCache;
            DrawTagFilter(index);
            if (index == null || index.entries == null)
            {
                ModelLibraryUIDrawer.DrawEmptyState("No models available",
                    "The repository appears to be empty.\n\nTo get started:\n• Submit your first model using 'Submit Model'\n• Check your repository configuration",
                    () =>
                    {
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
                return;
            }

            IEnumerable<ModelIndex.Entry> query = index.entries;

            // Apply filter mode (All/Favorites/Recent)
            if (_filterMode == ModelLibraryUIDrawer.FilterMode.Favorites)
            {
                query = query.Where(e => _favoritesManager.IsFavorite(e.id));
            }
            else if (_filterMode == ModelLibraryUIDrawer.FilterMode.Recent)
            {
                query = query.Where(e => _recentlyUsedManager.RecentlyUsed.Contains(e.id));
            }

            string trimmedSearch = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                query = query.Where(e => ModelSearchUtils.EntryMatchesAdvancedSearch(e, trimmedSearch));
            }

            // All models are visible (no project restrictions)

            if (_selectedTags.Count > 0)
            {
                query = query.Where(EntryHasAllSelectedTags);
            }

            List<ModelIndex.Entry> q = query.ToList();

            // Sort the results
            q = ModelSortUtils.SortEntries(q, _sortMode);

            // Filter mode tabs (All/Favorites/Recent)
            _filterMode = ModelLibraryUIDrawer.DrawFilterModeTabs(_filterMode, _favoritesManager.Favorites.Count, _recentlyUsedManager.RecentlyUsed.Count, (newMode) =>
            {
                _filterMode = newMode;
                Repaint();
            });

            ModelLibraryUIDrawer.DrawFilterSummary(index.entries.Count, q.Count, _search, _selectedTags, () =>
            {
                _search = string.Empty;
                _selectedTags.Clear();
                GUI.FocusControl(null);
            });
            if (q.Count == 0)
            {
                ModelLibraryUIDrawer.DrawEmptyState("No models match your filters",
                    $"Found {index.entries.Count} model(s) in repository, but none match your current search and filters.\n\nTry:\n• Clearing your search query\n• Removing some tag filters\n• Using different search terms",
                    () =>
                    {
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_viewMode == ViewMode.Grid)
            {
                DrawGridView(q);
            }
            else
            {
                foreach (ModelIndex.Entry e in q)
                {
                    DrawEntry(e);
                }
            }
            EditorGUILayout.EndScrollView();
        }



        private bool EntryHasAllSelectedTags(ModelIndex.Entry entry)
        {
            if (entry?.tags == null || entry.tags.Count == 0)
            {
                return false;
            }

            foreach (string tag in _selectedTags)
            {
                bool match = entry.tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                if (!match)
                {
                    return false;
                }
            }

            return true;
        }


        private void DrawTagFilter(ModelIndex index)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _showTagFilter = EditorGUILayout.Foldout(_showTagFilter, "Filter by Tags", true);
                if (!_showTagFilter)
                {
                    return;
                }

                if (index == null)
                {
                    EditorGUILayout.LabelField("Loading tags...", EditorStyles.miniLabel);
                    return;
                }

                if (!ReferenceEquals(index, _tagSource))
                {
                    _tagCacheManager.UpdateTagCache(index);
                    _tagSource = index;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    // Filter presets dropdown
                    if (GUILayout.Button("Presets ▼", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        _filterPresetManager.ShowFilterPresetsMenu((searchQuery, tags) =>
                        {
                            _search = searchQuery;
                            _selectedTags.Clear();
                            foreach (string tag in tags)
                            {
                                _selectedTags.Add(tag);
                            }
                            GUI.FocusControl(null);
                            Repaint();
                        }, () => _filterPresetManager.ShowManagePresetsDialog());
                    }

                    bool hasActiveFilters = !string.IsNullOrWhiteSpace(_search) || _selectedTags.Count > 0;
                    using (new EditorGUI.DisabledScope(!hasActiveFilters))
                    {
                        if (GUILayout.Button("Save Preset", EditorStyles.toolbarButton, GUILayout.Width(90)))
                        {
                            _filterPresetManager.ShowSavePresetDialog(_search, _selectedTags);
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedTags.Count == 0))
                    {
                        if (GUILayout.Button("Clear Tags", GUILayout.Width(90)))
                        {
                            _selectedTags.Clear();
                            GUI.FocusControl(null);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                if (_tagCacheManager.SortedTags.Count == 0)
                {
                    EditorGUILayout.LabelField("No tags available.", EditorStyles.miniLabel);
                    return;
                }

                _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, GUILayout.MaxHeight(140));
                for (int i = 0; i < _tagCacheManager.SortedTags.Count; i++)
                {
                    string tag = _tagCacheManager.SortedTags[i];
                    bool sel = _selectedTags.Contains(tag);
                    bool newSel = EditorGUILayout.ToggleLeft($"{tag} ({_tagCacheManager.TagCounts[tag]})", sel);
                    if (newSel != sel)
                    {
                        if (newSel)
                        {
                            _selectedTags.Add(tag);
                        }
                        else
                        {
                            _selectedTags.Remove(tag);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }



        /// <summary>
        /// Draws notification badges for notes and updates in list view.
        /// Displays an info icon if the model has feedback notes, and a refresh icon if updates are available.
        /// Uses Unity's built-in icons with fallback to emoji if icons are not available.
        /// </summary>
        /// <param name="e">The model index entry to check for notifications.</param>
        private void DrawNotificationBadges(ModelIndex.Entry e)
        {
            // Check if model has notes (from cached metadata)
            bool hasNotes = HasNotes(e.id, e.latestVersion);

            // Check if model has updates available (check both update status cache and local upgrade status)
            bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(e.id, out bool updateStatus) && updateStatus;

            // Also check local upgrade status for more accurate detection
            bool hasUpdateLocally = false;
            bool installed = TryGetLocalInstall(e, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, e.latestVersion);
            }

            bool hasUpdate = hasUpdateFromCache || hasUpdateLocally;

            ModelLibraryUIDrawer.DrawNotificationBadges(hasNotes, hasUpdate);
        }

        /// <summary>
        /// Checks if a model has feedback notes in its metadata.
        /// Uses the metadata cache to avoid loading metadata multiple times.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version of the model to check.</param>
        /// <returns>True if the model has one or more notes, false otherwise.</returns>
        private bool HasNotes(string modelId, string version)
        {
            string key = modelId + "@" + version;
            if (_metaCache.TryGetValue(key, out ModelMeta meta) && meta != null)
            {
                return meta.notes != null && meta.notes.Count > 0;
            }
            return false;
        }

        private void DrawEntry(ModelIndex.Entry e)
        {

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Favorite star button
                    bool isFavorite = _favoritesManager.IsFavorite(e.id);
                    string starText = isFavorite ? "★" : "☆";
                    Color originalColor = GUI.color;
                    if (isFavorite)
                    {
                        GUI.color = Color.yellow;
                    }
                    if (GUILayout.Button(starText, EditorStyles.label, GUILayout.Width(20)))
                    {
                        _favoritesManager.ToggleFavorite(e.id);
                        Repaint();
                    }
                    GUI.color = originalColor;

                    // Notification badges (notes and updates)
                    DrawNotificationBadges(e);

                    // Model name
                    GUILayout.Label(e.name, EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"v{e.latestVersion}");
                    bool installed = TryGetLocalInstall(e, out ModelMeta localMeta);
                    string localVersion = installed ? localMeta.version : null;

                    // Only consider the model as properly installed if we have a valid local version
                    bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                    bool needsUpgrade = properlyInstalled && ModelVersionUtils.NeedsUpgrade(localVersion, e.latestVersion);
                    bool isBusy = _importsInProgress.Contains(e.id);
                    if (properlyInstalled)
                    {
                        string label = needsUpgrade ? $"Local v{localVersion} (update available)" : $"Local v{localVersion}";
                        GUILayout.Label(label, EditorStyles.miniLabel);
                    }
                    else if (installed && (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)"))
                    {
                        GUILayout.Label("Local (version unknown)", EditorStyles.miniLabel);
                    }
                    if (GUILayout.Button("Details", GUILayout.Width(70)))
                    {
                        ModelDetailsWindow.Open(e.id, e.latestVersion);
                    }
                    bool downloaded = IsDownloaded(e.id, e.latestVersion);
                    using (new EditorGUI.DisabledScope(downloaded || isBusy))
                    {
                        if (GUILayout.Button("Download", GUILayout.Width(90)))
                        {
                            _ = Download(e.id, e.latestVersion);
                        }
                    }
                    using (new EditorGUI.DisabledScope(isBusy || (properlyInstalled && !needsUpgrade)))
                    {
                        string actionLabel = needsUpgrade ? "Update" : "Import";
                        if (GUILayout.Button(actionLabel, GUILayout.Width(80)))
                        {
                            string previousVersion = properlyInstalled ? localVersion : null;
                            _ = Import(e.id, e.latestVersion, needsUpgrade, previousVersion);
                        }
                    }
                }
            }
            EditorGUILayout.LabelField(e.description);
            EditorGUILayout.LabelField("Tags:", string.Join(", ", e.tags));
            EditorGUILayout.LabelField("Updated:", new DateTime(e.updatedTimeTicks).ToString(CultureInfo.CurrentCulture));
            EditorGUILayout.LabelField("Release:", e.releaseTimeTicks <= 0 ? "�" : new DateTime(e.releaseTimeTicks).ToString(CultureInfo.CurrentCulture));
            // Auto-load meta for details display
            string key = e.id + "@" + e.latestVersion;
            bool isLoadingMeta = _loadingMeta.Contains(key);
            if (!_metaCache.ContainsKey(key) && !isLoadingMeta)
            {
                _ = LoadMetaAsync(e.id, e.latestVersion);
            }
            if (_metaCache.TryGetValue(key, out ModelMeta meta) && meta != null)
            {
                string thumbKey = key + "#thumb";
                _thumbnailCache.TryGetValue(thumbKey, out Texture2D thumbnail);
                bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect thumbRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64), GUILayout.Height(64));
                    if (thumbnail != null)
                    {
                        EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleToFit);
                        GUILayout.Space(6);
                    }
                    else if (isLoadingThumbnail || isLoadingMeta)
                    {
                        // Draw loading placeholder
                        EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                        GUI.Label(thumbRect, "Loading...", EditorStyles.centeredGreyMiniLabel);
                        GUILayout.Space(6);
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        if (meta.materials != null && meta.materials.Count > 0)
                        {
                            EditorGUILayout.LabelField("Materials:", string.Join(", ", meta.materials.ConvertAll(m => m.name)));
                        }
                        if (meta.textures != null && meta.textures.Count > 0)
                        {
                            EditorGUILayout.LabelField("Textures:", string.Join(", ", meta.textures.ConvertAll(t => t.name)));
                        }
                        if (meta.vertexCount > 0 || meta.triangleCount > 0)
                        {
                            string vertsLabel = meta.vertexCount.ToString("N0");
                            string trisLabel = meta.triangleCount > 0 ? meta.triangleCount.ToString("N0") : null;
                            string geometry = trisLabel != null ? $"{vertsLabel} verts / {trisLabel} tris" : $"{vertsLabel} verts";
                            EditorGUILayout.LabelField("Geometry:", geometry);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws models in a responsive grid view with thumbnail previews.
        /// Calculates the number of columns based on available window width.
        /// Each model is displayed as a card with a clickable thumbnail, name, version, and status.
        /// </summary>
        /// <param name="entries">List of model entries to display in the grid.</param>
        private void DrawGridView(List<ModelIndex.Entry> entries)
        {
            const float thumbnailSize = 128f;
            const float spacing = 8f;
            const float cardPadding = 4f;
            const float minCardWidth = thumbnailSize + (cardPadding * 2);

            // Calculate number of columns based on window width
            float availableWidth = EditorGUIUtility.currentViewWidth - 20f; // Account for scrollbar
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (minCardWidth + spacing)));

            int row = 0;
            int col = 0;

            foreach (ModelIndex.Entry entry in entries)
            {
                if (col == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                }

                // Draw grid card
                DrawGridCard(entry, thumbnailSize, cardPadding);

                col++;
                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    col = 0;
                    row++;
                }
            }

            // Close any remaining horizontal scope
            if (col > 0)
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Draws a single model card in the grid view.
        /// Displays a thumbnail (or placeholder), model name, version, and installation status.
        /// The thumbnail is clickable and opens the model details window.
        /// </summary>
        /// <param name="entry">The model index entry to display.</param>
        /// <param name="thumbnailSize">The size (width and height) of the thumbnail in pixels.</param>
        /// <param name="padding">The padding around the card content.</param>
        private void DrawGridCard(ModelIndex.Entry entry, float thumbnailSize, float padding)
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(thumbnailSize + (padding * 2)), GUILayout.Height(thumbnailSize + 60)))
            {
                // Selection checkbox for bulk operations
                if (_bulkSelectionMode)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isSelected = _selectedModels.Contains(entry.id);
                        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                        if (newSelected != isSelected)
                        {
                            if (newSelected)
                            {
                                _selectedModels.Add(entry.id);
                            }
                            else
                            {
                                _selectedModels.Remove(entry.id);
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                // Load metadata and thumbnail if needed
                string key = entry.id + "@" + entry.latestVersion;
                if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
                {
                    _ = LoadMetaAsync(entry.id, entry.latestVersion);
                }

                // Get thumbnail
                string thumbKey = key + "#thumb";
                _thumbnailCache.TryGetValue(thumbKey, out Texture2D thumbnail);
                bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                bool isLoadingMeta = _loadingMeta.Contains(key);

                // Thumbnail area (clickable)
                Rect thumbRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));

                // Draw thumbnail or placeholder
                if (thumbnail != null)
                {
                    // Draw thumbnail as button
                    if (GUI.Button(thumbRect, new GUIContent(thumbnail), GUIStyle.none))
                    {
                        ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                    }
                }
                else
                {
                    // Draw placeholder background
                    EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                    // Show loading indicator if loading
                    if (isLoadingThumbnail || isLoadingMeta)
                    {
                        // Draw loading spinner text
                        string loadingText = "Loading...";
                        GUI.Label(thumbRect, loadingText, EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        // Draw placeholder text
                        GUI.Label(thumbRect, "No Preview", EditorStyles.centeredGreyMiniLabel);
                    }

                    // Make placeholder clickable
                    if (GUI.Button(thumbRect, GUIContent.none, GUIStyle.none))
                    {
                        ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                    }
                }

                GUILayout.Space(2);

                // Model name with badges
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Favorite star button (compact)
                    bool isFavorite = _favoritesManager.IsFavorite(entry.id);
                    string starText = isFavorite ? "★" : "☆";
                    Color originalColor = GUI.color;
                    if (isFavorite)
                    {
                        GUI.color = Color.yellow;
                    }
                    if (GUILayout.Button(starText, EditorStyles.miniLabel, GUILayout.Width(16)))
                    {
                        _favoritesManager.ToggleFavorite(entry.id);
                        Repaint();
                    }
                    GUI.color = originalColor;

                    // Notification badges (compact version)
                    DrawCompactNotificationBadges(entry);

                    // Truncate name if too long
                    string displayName = entry.name;
                    if (displayName.Length > 15)
                    {
                        displayName = $"{displayName[..12]}...";
                    }
                    GUILayout.Label(displayName, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                }

                // Version info
                GUILayout.Label($"v{entry.latestVersion}", EditorStyles.centeredGreyMiniLabel);

                // Install status
                bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                string localVersion = installed ? localMeta.version : null;
                bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                bool hasUpdate = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

                if (properlyInstalled)
                {
                    string statusText = hasUpdate ? "Update" : "Installed";
                    GUILayout.Label(statusText, EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        /// <summary>
        /// Draws compact notification badges for grid view.
        /// </summary>
        private void DrawCompactNotificationBadges(ModelIndex.Entry e)
        {
            bool hasNotes = HasNotes(e.id, e.latestVersion);
            bool hasUpdate = _modelUpdateStatus.TryGetValue(e.id, out bool updateStatus) && updateStatus;

            // Also check local upgrade status
            bool installed = TryGetLocalInstall(e, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdate = hasUpdate || ModelVersionUtils.NeedsUpgrade(localMeta.version, e.latestVersion);
            }

            ModelLibraryUIDrawer.DrawCompactNotificationBadges(hasNotes, hasUpdate);
        }

        private void OnDisable()
        {
            _thumbnailCache.Clear();
            _loadingThumbnails.Clear();
        }

        private async Task LoadMetaAsync(string id, string version)
        {
            string key = id + "@" + version;
            _loadingMeta.Add(key);
            try
            {
                ModelMeta meta = await _service.GetMetaAsync(id, version);
                _metaCache[key] = meta;
                string previewPath = meta?.previewImagePath;
                if (!string.IsNullOrEmpty(previewPath))
                {
                    string thumbKey = key + "#thumb";
                    if (!_thumbnailCache.ContainsKey(thumbKey) && !_loadingThumbnails.Contains(thumbKey))
                    {
                        _ = LoadThumbnailAsync(thumbKey, id, version, previewPath);
                    }
                }
                Repaint();
            }
            catch
            {
            }
            finally
            {
                _loadingMeta.Remove(key);
            }
        }

        private async Task LoadThumbnailAsync(string cacheKey, string id, string version, string relativePath)
        {
            _loadingThumbnails.Add(cacheKey);
            try
            {
                Texture2D tex = await _service.GetPreviewTextureAsync(id, version, relativePath);
                if (tex != null)
                {
                    _thumbnailCache[cacheKey] = tex;
                }
                else
                {
                    _thumbnailCache.Remove(cacheKey);
                }
                Repaint();
            }
            catch
            {
                _thumbnailCache.Remove(cacheKey);
            }
            finally
            {
                _loadingThumbnails.Remove(cacheKey);
            }
        }

        private bool IsDownloaded(string id, string version)
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            string cacheRoot = EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, id, version));
            if (!Directory.Exists(cacheRoot))
            {
                return false;
            }
            using (IEnumerator<string> e = Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories).GetEnumerator())
            {
                return e.MoveNext();
            }
        }



        /// <summary>
        /// Attempts to find a locally installed version of the specified model.
        /// Uses a two-step approach:
        /// 1. Searches for manifest files (modelLibrary.meta.json) in the Assets folder.
        /// 2. Falls back to GUID-based detection if no manifest is found.
        /// Results are cached to improve performance.
        /// </summary>
        /// <param name="entry">The model index entry to check for local installation.</param>
        /// <param name="meta">Output parameter containing the local model metadata if found.</param>
        /// <returns>True if the model is installed locally, false otherwise.</returns>
        private bool TryGetLocalInstall(ModelIndex.Entry entry, out ModelMeta meta)
        {
            // Check negative cache first - if model was previously checked and not found, return immediately
            if (_negativeCache.Contains(entry.id))
            {
                meta = null;
                return false;
            }

            // Check local install cache first
            if (_localInstallCache.TryGetValue(entry.id, out meta) && meta != null)
            {
                return true;
            }

            // 1) Look for model in manifest cache (populated at startup)
            if (_manifestCacheInitialized)
            {
                if (_manifestCache.TryGetValue(entry.id, out meta) && meta != null)
                {
                    _localInstallCache[entry.id] = meta;
                    return true;
                }
            }
            else
            {
                // If cache not initialized yet, trigger initialization (non-blocking)
                _ = RefreshManifestCacheAsync();
            }

            // 2) Fallback: GUID-based detection using known asset GUIDs from latest meta (if cached)
            string key = entry.id + "@" + entry.latestVersion;
            if (_metaCache.TryGetValue(key, out ModelMeta latestMeta) && latestMeta != null)
            {
                try
                {
                    string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                    HashSet<string> set = new HashSet<string>(allGuids);
                    bool any = latestMeta.assetGuids != null && latestMeta.assetGuids.Any(g => set.Contains(g));
                    if (any)
                    {
                        // We can't know the exact local version from GUIDs alone; don't mark as installed
                        // This prevents false "update available" messages
                        meta = null;
                        return false;
                    }
                }
                catch { }
            }
            else if (!_loadingMeta.Contains(key))
            {
                // Kick off async load so a future repaint can apply GUID-based detection
                _ = LoadMetaAsync(entry.id, entry.latestVersion);
            }

            // Model not found - add to negative cache to avoid repeated checks
            _negativeCache.Add(entry.id);
            _localInstallCache.Remove(entry.id);
            meta = null;
            return false;
        }

        private void InvalidateLocalInstall(string modelId)
        {
            _localInstallCache.Remove(modelId);
            _negativeCache.Remove(modelId);
        }

        /// <summary>
        /// Refreshes the manifest cache by scanning all modelLibrary.meta.json files in the Assets folder.
        /// This is called once at startup and can be manually triggered via the Refresh button.
        /// </summary>
        private async Task RefreshManifestCacheAsync()
        {
            _manifestCache.Clear();
            _negativeCache.Clear();

            try
            {
                // Enumerate all manifest files in Assets folder once
                foreach (string manifestPath in Directory.EnumerateFiles("Assets", "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(manifestPath);
                        ModelMeta parsed = JsonUtil.FromJson<ModelMeta>(json);
                        if (parsed != null && parsed.identity != null && !string.IsNullOrEmpty(parsed.identity.id))
                        {
                            _manifestCache[parsed.identity.id] = parsed;
                            // Also update _localInstallCache for consistency
                            _localInstallCache[parsed.identity.id] = parsed;
                        }
                    }
                    catch
                    {
                        // Ignore malformed manifest files
                    }
                }

                _manifestCacheInitialized = true;
            }
            catch
            {
                // Ignore directory scan issues
                _manifestCacheInitialized = true; // Still mark as initialized to avoid repeated failures
            }
        }


        /// <summary>
        /// Performs bulk import of all selected models.
        /// </summary>
        private async Task BulkImportAsync()
        {
            if (_selectedModels.Count == 0)
            {
                return;
            }

            List<string> modelIds = _selectedModels.ToList();
            int total = modelIds.Count;
            int current = 0;
            int successCount = 0;
            int failCount = 0;

            foreach (string modelId in modelIds)
            {
                current++;
                try
                {
                    ModelIndex.Entry entry = _indexCache?.entries?.FirstOrDefault(e => e.id == modelId);
                    if (entry == null)
                    {
                        failCount++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Bulk Import", $"Importing {entry.name} ({current}/{total})...", (float)current / total);

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    bool needsUpgrade = installed && !string.IsNullOrEmpty(localMeta.version) && ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);

                    await Import(entry.id, entry.latestVersion, needsUpgrade, installed ? localMeta.version : null);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to import model {modelId}: {ex.Message}");
                    failCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            string message = $"Bulk Import Complete!\n\nSuccessful: {successCount}\nFailed: {failCount}";
            EditorUtility.DisplayDialog("Bulk Import Results", message, "OK");

            _selectedModels.Clear();
            _bulkSelectionMode = false;
            Repaint();
        }

        /// <summary>
        /// Performs bulk update of all selected models that have available updates.
        /// </summary>
        private async Task BulkUpdateAsync()
        {
            if (_selectedModels.Count == 0)
            {
                return;
            }

            // Filter to only models with updates
            List<string> modelsWithUpdates = new List<string>();
            foreach (string modelId in _selectedModels)
            {
                if (_modelUpdateStatus.TryGetValue(modelId, out bool hasUpdate) && hasUpdate)
                {
                    modelsWithUpdates.Add(modelId);
                }
            }

            if (modelsWithUpdates.Count == 0)
            {
                EditorUtility.DisplayDialog("No Updates", "None of the selected models have available updates.", "OK");
                return;
            }

            int total = modelsWithUpdates.Count;
            int current = 0;
            int successCount = 0;
            int failCount = 0;

            foreach (string modelId in modelsWithUpdates)
            {
                current++;
                try
                {
                    ModelIndex.Entry entry = _indexCache?.entries?.FirstOrDefault(e => e.id == modelId);
                    if (entry == null)
                    {
                        failCount++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Bulk Update", $"Updating {entry.name} ({current}/{total})...", (float)current / total);

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    string previousVersion = installed ? localMeta.version : null;

                    await Import(entry.id, entry.latestVersion, isUpgrade: true, previousVersion);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to update model {modelId}: {ex.Message}");
                    failCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            string message = $"Bulk Update Complete!\n\nSuccessful: {successCount}\nFailed: {failCount}";
            EditorUtility.DisplayDialog("Bulk Update Results", message, "OK");

            _selectedModels.Clear();
            _bulkSelectionMode = false;
            Repaint();
        }


        /// <summary>
        /// Handles keyboard shortcuts for common operations.
        /// F5: Refresh index
        /// Ctrl+F: Focus search field
        /// Escape: Clear search
        /// Enter: Add search to history (handled in OnGUI for search field)
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            Event currentEvent = Event.current;

            // Only process key events (not mouse events)
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            // F5: Refresh index
            if (currentEvent.keyCode == KeyCode.F5)
            {
                if (_service != null && !_loadingIndex)
                {
                    _indexCache = null;
                    _ = LoadIndexAsync();
                    currentEvent.Use();
                }
            }
            // Ctrl+F or Cmd+F: Focus search field
            else if ((currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.F)
            {
                GUI.FocusControl("SearchField");
                currentEvent.Use();
            }
            // Enter: Add search to history if search field is focused
            else if ((currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter))
            {
                if (GUI.GetNameOfFocusedControl() == "SearchField" && !string.IsNullOrWhiteSpace(_search))
                {
                    _searchHistoryManager.AddToSearchHistory(_search);
                    currentEvent.Use();
                }
            }
            // Escape: Clear search if search field is focused
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                if (GUI.GetNameOfFocusedControl() == "SearchField" || !string.IsNullOrEmpty(_search))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                    currentEvent.Use();
                }
            }
        }

        private async Task Download(string id, string version)
        {
            try
            {
                ModelDownloader downloader = new ModelDownloader(_service);
                EditorUtility.DisplayProgressBar("Downloading Model", "Connecting to repository...", 0.1f);

                (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);

                EditorUtility.DisplayProgressBar("Downloading Model", "Download complete", 1.0f);
                await Task.Delay(100); // Brief pause to show completion

                EditorUtility.DisplayDialog("Downloaded", $"Cached at: {root}", "OK");
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowErrorWithRetry("Download Failed", $"Failed to download model: {ex.Message}",
                    () => _ = Download(id, version), ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Imports a model version into the Unity project.
        /// Downloads the model if not cached, then copies files into the Assets folder.
        /// Supports both new imports and updates to existing models.
        /// </summary>
        /// <param name="id">The unique identifier of the model to import.</param>
        /// <param name="version">The version of the model to import.</param>
        /// <param name="isUpgrade">True if this is an update to an existing installation, false for new imports.</param>
        /// <param name="previousVersion">The version being upgraded from (used for display messages).</param>
        private async Task Import(string id, string version, bool isUpgrade = false, string previousVersion = null)
        {
            if (_importsInProgress.Contains(id))
            {
                return;
            }

            _importsInProgress.Add(id);
            string progressTitle = isUpgrade ? "Updating Model" : "Importing Model";

            try
            {
                titleContent.text = $"Model Library - {progressTitle}...";

                ModelDownloader downloader = new ModelDownloader(_service);
                EditorUtility.DisplayProgressBar(progressTitle, "Downloading from repository...", 0.1f);
                (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);

                EditorUtility.DisplayProgressBar(progressTitle, "Preparing import...", 0.3f);

                string defaultInstallPath = _installPathHelper.DetermineInstallPath(meta);
                string chosenInstallPath = defaultInstallPath;

                int choice = EditorUtility.DisplayDialogComplex(
                    isUpgrade ? "Update Model" : "Import Model",
                    $"Select an install location for '{meta.identity.name}'.\nStored path: {defaultInstallPath}",
                    "Use Stored Path",
                    "Choose Folder...",
                    "Cancel");

                if (choice == 2)
                {
                    titleContent.text = "Model Library";
                    return;
                }

                if (choice == 1)
                {
                    string custom = _installPathHelper.PromptForInstallPath(defaultInstallPath);
                    if (string.IsNullOrEmpty(custom))
                    {
                        titleContent.text = "Model Library";
                        return;
                    }
                    chosenInstallPath = custom;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Copying files to Assets folder...", 0.5f);
                await ModelProjectImporter.ImportFromCacheAsync(root, meta, cleanDestination: true, overrideInstallPath: chosenInstallPath, isUpdate: isUpgrade);

                EditorUtility.DisplayProgressBar(progressTitle, "Finalizing import...", 0.9f);
                await Task.Delay(100); // Brief pause for UI update

                InvalidateLocalInstall(id);
                _localInstallCache[id] = meta;
                _manifestCache[id] = meta; // Also update manifest cache
                _negativeCache.Remove(id); // Remove from negative cache if present

                // Track as recently used
                _recentlyUsedManager.AddToRecentlyUsed(id);

                // Track analytics
                AnalyticsService.RecordEvent(isUpgrade ? "update" : "import", id, version, meta.identity.name);

                // Show completion dialog
                string message = isUpgrade && !string.IsNullOrEmpty(previousVersion)
                    ? $"Updated '{meta.identity.name}' from v{previousVersion} to v{meta.version} at {chosenInstallPath}."
                    : $"Imported '{meta.identity.name}' v{meta.version} to {chosenInstallPath}.";
                EditorUtility.DisplayDialog(isUpgrade ? "Update Complete" : "Import Complete", message, "OK");
                Repaint();
            }
            catch (Exception ex)
            {
                string operation = isUpgrade ? "Update" : "Import";
                ErrorHandler.ShowErrorWithRetry($"{operation} Failed",
                    $"Failed to {operation.ToLower()} model: {ex.Message}",
                    () => _ = Import(id, version, isUpgrade, previousVersion), ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                titleContent.text = "Model Library";
                _importsInProgress.Remove(id);
            }
        }


    }
}





