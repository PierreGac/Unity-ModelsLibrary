using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        // Caching and Loading State
        /// <summary>Cache of loaded model metadata, keyed by "modelId@version".</summary>
        private readonly Dictionary<string, ModelMeta> _metaCache = new();
        /// <summary>Set of metadata keys currently being loaded to prevent duplicate requests.</summary>
        private readonly HashSet<string> _loadingMeta = new();
        /// <summary>Cache of locally installed model metadata, keyed by model ID.</summary>
        private readonly Dictionary<string, ModelMeta> _localInstallCache = new();
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
        /// <summary>Dictionary counting how many models use each tag (case-insensitive keys).</summary>
        private readonly Dictionary<string, int> _tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Sorted list of all available tags for display in the filter UI.</summary>
        private readonly List<string> _sortedTags = new List<string>();

        // Update Detection
        /// <summary>Total count of models with available updates.</summary>
        private int _updateCount = 0;
        /// <summary>Dictionary tracking which models have updates available, keyed by model ID.</summary>
        private readonly Dictionary<string, bool> _modelUpdateStatus = new Dictionary<string, bool>();
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
                _indexCache = await _service.GetIndexAsync();

                // Check for updates in the background after index loads
                _ = CheckForUpdatesAsync();

                Repaint(); // Refresh the UI when index is loaded
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load index: {ex.Message}");
            }
            finally
            {
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
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
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
                GUILayout.Label("Loading index...");
                return;
            }

            ModelIndex index = _indexCache;
            DrawTagFilter(index);
            if (index == null || index.entries == null)
            {
                GUILayout.Label("No models available.");
                return;
            }

            IEnumerable<ModelIndex.Entry> query = index.entries;
            string trimmedSearch = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                query = query.Where(e => EntryMatchesSearch(e, trimmedSearch));
            }

            // All models are visible (no project restrictions)

            if (_selectedTags.Count > 0)
            {
                query = query.Where(EntryHasAllSelectedTags);
            }

            List<ModelIndex.Entry> q = query.ToList();

            DrawFilterSummary(index.entries.Count, q.Count);
            if (q.Count == 0)
            {
                EditorGUILayout.HelpBox("No models match the current filters.", MessageType.Info);
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

        private bool HasActiveFilters => !string.IsNullOrWhiteSpace(_search) || _selectedTags.Count > 0;

        private static bool EntryMatchesSearch(ModelIndex.Entry entry, string term)
        {
            if (entry == null || string.IsNullOrEmpty(term))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(entry.name) && entry.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (entry.tags != null)
            {
                foreach (string tag in entry.tags)
                {
                    if (!string.IsNullOrEmpty(tag) && tag.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
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

        private void DrawFilterSummary(int totalCount, int filteredCount)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{filteredCount} of {totalCount} models", EditorStyles.boldLabel);

                if (!string.IsNullOrWhiteSpace(_search))
                {
                    GUILayout.Label($"Search: \"{_search.Trim()}\"", EditorStyles.miniLabel);
                }

                if (_selectedTags.Count > 0)
                {
                    string tagPreview = string.Join(", ", _selectedTags.Take(3));
                    if (_selectedTags.Count > 3)
                    {
                        tagPreview += $" (+{_selectedTags.Count - 3})";
                    }
                    GUILayout.Label($"Tags: {tagPreview}", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!HasActiveFilters))
                {
                    if (GUILayout.Button("Clear Filters", GUILayout.Width(110)))
                    {
                        _search = string.Empty;
                        _selectedTags.Clear();
                        GUI.FocusControl(null);
                    }
                }
            }
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
                    UpdateTagCache(index);
                    _tagSource = index;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
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

                if (_sortedTags.Count == 0)
                {
                    EditorGUILayout.LabelField("No tags available.", EditorStyles.miniLabel);
                    return;
                }

                _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, GUILayout.MaxHeight(140));
                foreach (string tag in _sortedTags)
                {
                    bool sel = _selectedTags.Contains(tag);
                    bool newSel = EditorGUILayout.ToggleLeft($"{tag} ({_tagCounts[tag]})", sel);
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

        private void UpdateTagCache(ModelIndex index)
        {
            _tagCounts.Clear();
            _sortedTags.Clear();

            if (index?.entries == null)
            {
                return;
            }

            foreach (ModelIndex.Entry entry in index.entries)
            {
                if (entry?.tags == null)
                {
                    continue;
                }

                foreach (string tag in entry.tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    if (_tagCounts.TryGetValue(tag, out int count))
                    {
                        _tagCounts[tag] = count + 1;
                    }
                    else
                    {
                        _tagCounts[tag] = 1;
                    }
                }
            }

            if (_tagCounts.Count > 0)
            {
                _sortedTags.AddRange(_tagCounts.Keys);
                _sortedTags.Sort(StringComparer.OrdinalIgnoreCase);
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
                hasUpdateLocally = NeedsUpgrade(localMeta.version, e.latestVersion);
            }

            bool hasUpdate = hasUpdateFromCache || hasUpdateLocally;

            // Draw notes badge if model has notes
            if (hasNotes)
            {
                // Try multiple icon names for better compatibility
                GUIContent notesIcon = EditorGUIUtility.IconContent("console.infoicon");
                if (notesIcon == null || notesIcon.image == null)
                {
                    notesIcon = EditorGUIUtility.IconContent("d_console.infoicon");
                }
                if (notesIcon == null || notesIcon.image == null)
                {
                    notesIcon = EditorGUIUtility.IconContent("_Help");
                }

                if (notesIcon != null && notesIcon.image != null)
                {
                    notesIcon.tooltip = "This model has feedback notes";
                    GUILayout.Label(notesIcon, GUILayout.Width(16), GUILayout.Height(16));
                }
                else
                {
                    // Fallback to emoji if icon not available
                    GUILayout.Label("📝", GUILayout.Width(16), GUILayout.Height(16));
                }
            }

            // Draw update badge if model has updates available
            if (hasUpdate)
            {
                // Try multiple icon names for better compatibility
                GUIContent updateIcon = EditorGUIUtility.IconContent("d_Refresh");
                if (updateIcon == null || updateIcon.image == null)
                {
                    updateIcon = EditorGUIUtility.IconContent("Refresh");
                }
                if (updateIcon == null || updateIcon.image == null)
                {
                    updateIcon = EditorGUIUtility.IconContent("TreeEditor.Refresh");
                }

                if (updateIcon != null && updateIcon.image != null)
                {
                    updateIcon.tooltip = "Update available";
                    GUILayout.Label(updateIcon, GUILayout.Width(16), GUILayout.Height(16));
                }
                else
                {
                    // Fallback to emoji if icon not available
                    GUILayout.Label("🔄", GUILayout.Width(16), GUILayout.Height(16));
                }
            }

            // Add spacing if any badges were shown
            if (hasNotes || hasUpdate)
            {
                GUILayout.Space(4);
            }
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
                    bool needsUpgrade = properlyInstalled && NeedsUpgrade(localVersion, e.latestVersion);
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
            if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
            {
                _ = LoadMetaAsync(e.id, e.latestVersion);
            }
            if (_metaCache.TryGetValue(key, out ModelMeta meta) && meta != null)
            {
                string thumbKey = key + "#thumb";
                _thumbnailCache.TryGetValue(thumbKey, out Texture2D thumbnail);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (thumbnail != null)
                    {
                        Rect thumbRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64), GUILayout.Height(64));
                        EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleToFit);
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
                // Load metadata and thumbnail if needed
                string key = entry.id + "@" + entry.latestVersion;
                if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
                {
                    _ = LoadMetaAsync(entry.id, entry.latestVersion);
                }

                // Get thumbnail
                string thumbKey = key + "#thumb";
                _thumbnailCache.TryGetValue(thumbKey, out Texture2D thumbnail);

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
                    // Draw placeholder text
                    GUI.Label(thumbRect, "No Preview", EditorStyles.centeredGreyMiniLabel);
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
                hasUpdate = hasUpdate || NeedsUpgrade(localMeta.version, e.latestVersion);
            }

            if (hasNotes)
            {
                GUILayout.Label("📝", GUILayout.Width(12), GUILayout.Height(12));
            }
            if (hasUpdate)
            {
                GUILayout.Label("🔄", GUILayout.Width(12), GUILayout.Height(12));
            }
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
        /// Determines if a local model version needs to be upgraded to the remote version.
        /// Uses semantic versioning comparison when possible, falls back to string comparison.
        /// Returns false for unknown local versions to prevent false "update available" messages.
        /// </summary>
        /// <param name="localVersion">The version currently installed locally.</param>
        /// <param name="remoteVersion">The latest version available in the repository.</param>
        /// <returns>True if the remote version is newer than the local version, false otherwise.</returns>
        private static bool NeedsUpgrade(string localVersion, string remoteVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion))
            {
                return false;
            }
            if (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)")
            {
                // Don't show as needing upgrade if we can't determine the local version
                return false;
            }
            if (SemVer.TryParse(localVersion, out SemVer local) && SemVer.TryParse(remoteVersion, out SemVer remote))
            {
                return remote.CompareTo(local) > 0;
            }
            return !string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);
        }

        private string DetermineInstallPath(ModelMeta meta)
        {
            string modelName = meta?.identity?.name ?? "Model";
            string candidate;

            // First try the relative path from meta
            if (!string.IsNullOrWhiteSpace(meta?.relativePath))
            {
                candidate = $"Assets/{meta.relativePath}";
            }
            // Then try the install path from meta
            else if (!string.IsNullOrWhiteSpace(meta?.installPath))
            {
                candidate = meta.installPath;
            }
            // Finally fall back to default
            else
            {
                candidate = BuildInstallPath(modelName);
            }

            return NormalizeInstallPath(candidate) ?? BuildInstallPath(modelName);
        }

        private string PromptForInstallPath(string defaultInstallPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string initialAbsolute = Path.Combine(projectRoot, defaultInstallPath.Replace('/', Path.DirectorySeparatorChar));
            string selected = EditorUtility.OpenFolderPanel("Choose install folder", initialAbsolute, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return null;
            }

            if (!TryConvertAbsoluteToProjectRelative(selected, out string relative))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside this Unity project.", "OK");
                return null;
            }

            return NormalizeInstallPath(relative);
        }

        private static bool TryConvertAbsoluteToProjectRelative(string absolutePath, out string relativePath)
        {
            relativePath = null;
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedAbsolute = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!normalizedAbsolute.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rel = normalizedAbsolute[normalizedRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            rel = PathUtils.SanitizePathSeparator(rel);
            if (string.IsNullOrEmpty(rel) || !rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            relativePath = rel;
            return true;
        }

        private string BuildInstallPath(string modelName) => $"Assets/Models/{SanitizeFolderName(modelName)}";

        private static string NormalizeInstallPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalized = PathUtils.SanitizePathSeparator(path.Trim());
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"Assets/{normalized.TrimStart('/')}";
            }
            return normalized;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Model";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(result);
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
            if (_localInstallCache.TryGetValue(entry.id, out meta) && meta != null)
            {
                return true;
            }

            // 1) Look for a marker manifest anywhere under Assets (supports custom install locations)
            try
            {
                foreach (string manifestPath in Directory.EnumerateFiles("Assets", "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        string json = File.ReadAllText(manifestPath);
                        ModelMeta parsed = JsonUtil.FromJson<ModelMeta>(json);
                        if (parsed != null && parsed.identity != null && string.Equals(parsed.identity.id, entry.id, StringComparison.OrdinalIgnoreCase))
                        {
                            _localInstallCache[entry.id] = parsed;
                            meta = parsed;
                            return true;
                        }
                    }
                    catch { /* ignore malformed marker files */ }
                }
            }
            catch { /* directory scan issues */ }

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

            _localInstallCache.Remove(entry.id);
            meta = null;
            return false;
        }

        private void InvalidateLocalInstall(string modelId) => _localInstallCache.Remove(modelId);

        private async Task Download(string id, string version)
        {
            ModelDownloader downloader = new ModelDownloader(_service);
            EditorUtility.DisplayProgressBar("Downloading Model", "Fetching files...", 0.2f);
            (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Downloaded", $"Cached at: {root}", "OK");
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
                ModelDownloader downloader = new ModelDownloader(_service);
                EditorUtility.DisplayProgressBar(progressTitle, "Downloading...", 0.2f);
                (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);

                string defaultInstallPath = DetermineInstallPath(meta);
                string chosenInstallPath = defaultInstallPath;

                int choice = EditorUtility.DisplayDialogComplex(
                    isUpgrade ? "Update Model" : "Import Model",
                    $"Select an install location for '{meta.identity.name}'.\nStored path: {defaultInstallPath}",
                    "Use Stored Path",
                    "Choose Folder...",
                    "Cancel");

                if (choice == 2)
                {
                    return;
                }

                if (choice == 1)
                {
                    string custom = PromptForInstallPath(defaultInstallPath);
                    if (string.IsNullOrEmpty(custom))
                    {
                        return;
                    }
                    chosenInstallPath = custom;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Copying into Assets...", 0.6f);
                await ModelProjectImporter.ImportFromCacheAsync(root, meta, cleanDestination: true, overrideInstallPath: chosenInstallPath, isUpdate: isUpgrade);
                InvalidateLocalInstall(id);
                _localInstallCache[id] = meta;

                string message = isUpgrade && !string.IsNullOrEmpty(previousVersion)
                    ? $"Updated '{meta.identity.name}' from v{previousVersion} to v{meta.version} at {chosenInstallPath}."
                    : $"Imported '{meta.identity.name}' v{meta.version} to {chosenInstallPath}.";
                EditorUtility.DisplayDialog(isUpgrade ? "Update Complete" : "Import Complete", message, "OK");
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(isUpgrade ? "Update Failed" : "Import Failed", ex.Message, "OK");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _importsInProgress.Remove(id);
            }
        }


    }
}





