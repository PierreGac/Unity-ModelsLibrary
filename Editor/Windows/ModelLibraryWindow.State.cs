using System;
using System.Collections.Generic;
using System.Threading;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public partial class ModelLibraryWindow
    {
        private const string __SortModePrefKey = "ModelLibrary.SortMode";
        private const string __SearchHistoryPrefKey = "ModelLibrary.SearchHistory";
        private const int __MaxSearchHistory = 10;
        private const string __FilterPresetsPrefKey = "ModelLibrary.FilterPresets";
        private const int __MAX_META_CACHE_ENTRIES = 200;
        private const int __MAX_THUMBNAIL_CACHE_ENTRIES = 96;
        private const int __CACHE_WARM_ENTRY_COUNT = 12;
        private const float __LIST_ESTIMATED_ITEM_HEIGHT = 260f;
        private const float __GRID_ESTIMATED_ROW_HEIGHT = 220f;
        private const float __IMAGE_ESTIMATED_ROW_HEIGHT = 220f;
        private const int __VIRTUALIZATION_BUFFER = 3;
        private const string __IMPORT_HISTORY_PREF_KEY = "ModelLibrary.ImportHistory";
        private const int __MAX_IMPORT_HISTORY = 10;
        private const string __FavoritesPrefKey = "ModelLibrary.Favorites";
        private const string __RecentlyUsedPrefKey = "ModelLibrary.RecentlyUsed";
        private const int __MaxRecentlyUsed = 20;
        private const string __ThumbnailSizePrefKey = "ModelLibrary.ThumbnailSize";
        private const float __MIN_THUMBNAIL_SIZE = 64f;
        private const float __MAX_THUMBNAIL_SIZE = 256f;
        private const float __DEFAULT_THUMBNAIL_SIZE = 128f;

        private static readonly TimeSpan NOTIFICATION_DURATION = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MIN_UPDATE_CHECK_INTERVAL = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan BACKGROUND_UPDATE_CHECK_INTERVAL = TimeSpan.FromMinutes(7);

        private ModelLibraryService _service;
        private string _search = string.Empty;
        private SearchHistoryManager _searchHistoryManager;
        private Vector2 _scroll;
        private Dictionary<string, bool> _expanded = new Dictionary<string, bool>();
        private bool _showTagFilter;
        private Vector2 _tagScroll;
        private ViewMode _viewMode = ViewMode.Grid;
        private ModelSortMode _sortMode = ModelSortMode.Name;

        private readonly Dictionary<string, ModelMeta> _metaCache = new Dictionary<string, ModelMeta>();
        private readonly LinkedList<string> _metaCacheOrder = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _metaCacheNodes = new Dictionary<string, LinkedListNode<string>>();
        private readonly HashSet<string> _loadingMeta = new HashSet<string>();
        private readonly Dictionary<string, ModelMeta> _localInstallCache = new Dictionary<string, ModelMeta>();
        private readonly Dictionary<string, ModelMeta> _manifestCache = new Dictionary<string, ModelMeta>();
        private bool _manifestCacheInitialized;
        private bool _refreshingManifest;
        private readonly HashSet<string> _negativeCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();
        private readonly LinkedList<string> _thumbnailCacheOrder = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _thumbnailCacheNodes = new Dictionary<string, LinkedListNode<string>>();
        private readonly HashSet<string> _loadingThumbnails = new HashSet<string>();
        private ModelIndex _indexCache;
        private bool _loadingIndex;
        private bool _cacheWarmTriggered;
        private ModelIndex _tagSource;

        private readonly HashSet<string> _importsInProgress = new HashSet<string>();
        private readonly Dictionary<string, bool> _importCancellation = new Dictionary<string, bool>();
        private readonly Dictionary<string, CancellationTokenSource> _importCancellationTokens = new Dictionary<string, CancellationTokenSource>();
        private readonly List<ImportHistoryEntry> _importHistory = new List<ImportHistoryEntry>();

        private string _authenticationError = null;

        private readonly HashSet<string> _selectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private TagCacheManager _tagCacheManager = new TagCacheManager();
        private FilterPresetManager _filterPresetManager;
        private string _tagSearchFilter = string.Empty;

        private readonly HashSet<string> _selectedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _bulkSelectionMode;

        private readonly List<ModelIndex.Entry> _currentEntries = new List<ModelIndex.Entry>();
        private int _keyboardSelectionIndex = -1;
        private int _lastGridColumns = 1;
        private int _lastImageColumns = 1;

        private int _updateCount;
        private readonly Dictionary<string, bool> _modelUpdateStatus = new Dictionary<string, bool>();
        private int _notesCount;
        private string _notificationMessage;
        private DateTime _notificationTime = DateTime.MinValue;
        private DateTime _lastUpdateCheck = DateTime.MinValue;
        private bool _backgroundUpdateCheckEnabled;

        private FavoritesManager _favoritesManager;
        private RecentlyUsedManager _recentlyUsedManager;
        private ModelLibraryUIDrawer.FilterMode _filterMode = ModelLibraryUIDrawer.FilterMode.All;
        private InstallPathHelper _installPathHelper = new InstallPathHelper();
        private float _thumbnailSize = __DEFAULT_THUMBNAIL_SIZE;

        // Navigation state
        /// <summary>Current active view type.</summary>
        private ViewType _currentView = ViewType.Browser;
        /// <summary>Previous view type for back navigation.</summary>
        private ViewType? _previousView = null;
        /// <summary>Parameters for the previous view.</summary>
        private Dictionary<string, object> _previousViewParameters = new Dictionary<string, object>();
        /// <summary>View-specific parameters stored as key-value pairs.</summary>
        private readonly Dictionary<string, object> _viewParameters = new Dictionary<string, object>();
        /// <summary>Model ID being inspected (used for navigation from details to comparison/preview and back).</summary>
        private string _inspectedModelId = null;
        /// <summary>Version of the model being inspected (used for navigation from details to comparison/preview and back).</summary>
        private string _inspectedModelVersion = null;

        // FirstRunWizard state (for view mode)
        /// <summary>Wizard step state.</summary>
        private FirstRunWizard.WizardStep _wizardStep = FirstRunWizard.WizardStep.Welcome;
        /// <summary>User name for wizard.</summary>
        private string _wizardUserName;
        /// <summary>User role for wizard.</summary>
        private UserRole _wizardUserRole;
        /// <summary>Repository root for wizard.</summary>
        private string _wizardRepoRoot;
        /// <summary>Repository kind for wizard.</summary>
        private ModelLibrarySettings.RepositoryKind _wizardRepoKind;
        /// <summary>Repository validation message.</summary>
        private string _wizardRepoValidationMessage;
        /// <summary>Repository validation message type.</summary>
        private MessageType _wizardRepoValidationType = MessageType.Info;
        /// <summary>Whether repository has been tested.</summary>
        private bool _wizardRepoTested;
        /// <summary>Repository test message.</summary>
        private string _wizardRepoTestMessage;
        /// <summary>Repository test message type.</summary>
        private MessageType _wizardRepoTestMessageType = MessageType.None;
        /// <summary>Whether to open help after finishing wizard.</summary>
        private bool _wizardOpenHelpAfterFinish = true;

        // ModelLibraryHelpWindow state (for view mode)
        /// <summary>Selected help section.</summary>
        private ModelLibraryHelpWindow.HelpSection _helpSelectedSection = ModelLibraryHelpWindow.HelpSection.Overview;
        /// <summary>Scroll position for help content.</summary>
        private Vector2 _helpScrollPosition = Vector2.zero;

        // ModelSubmitWindow state (for view mode) - using a hidden instance for now
        /// <summary>Hidden ModelSubmitWindow instance for reuse.</summary>
        private ModelSubmitWindow _submitWindowInstance;

        // BatchUploadWindow state (for view mode)
        /// <summary>Selected source directory for batch upload.</summary>
        private string _batchUploadSourceDirectory = string.Empty;
        /// <summary>List of batch upload items.</summary>
        private List<BatchUploadService.BatchUploadItem> _batchUploadItems = new List<BatchUploadService.BatchUploadItem>();
        /// <summary>Scroll position for batch upload items list.</summary>
        private Vector2 _batchUploadScrollPosition;
        /// <summary>Whether batch upload is in progress.</summary>
        private bool _batchUploadIsUploading = false;
        /// <summary>Batch upload service instance.</summary>
        private BatchUploadService _batchUploadService;

        // UnifiedSettingsWindow state (for view mode)
        /// <summary>Settings tab selection.</summary>
        private UnifiedSettingsWindow.SettingsTab _settingsSelectedTab = UnifiedSettingsWindow.SettingsTab.User;
        /// <summary>User identity provider for settings.</summary>
        private SimpleUserIdentityProvider _settingsIdentityProvider;
        /// <summary>Current user name in settings form.</summary>
        private string _settingsUserName;
        /// <summary>Current user role in settings form.</summary>
        private UserRole _settingsUserRole;
        /// <summary>Repository settings instance.</summary>
        private ModelLibrarySettings _settingsInstance;
        /// <summary>Repository kind in settings form.</summary>
        private ModelLibrarySettings.RepositoryKind _settingsRepositoryKind;
        /// <summary>Repository root in settings form.</summary>
        private string _settingsRepositoryRoot;
        /// <summary>Local cache root in settings form.</summary>
        private string _settingsLocalCacheRoot;
        /// <summary>Whether connection test is in progress.</summary>
        private bool _settingsTestingConnection = false;
        /// <summary>Last connection test result message.</summary>
        private string _settingsConnectionTestResult = null;
        /// <summary>Whether last connection test was successful.</summary>
        private bool _settingsConnectionTestSuccess = false;
        /// <summary>Timestamp of last connection test.</summary>
        private DateTime _settingsLastConnectionTest = DateTime.MinValue;
        /// <summary>Number of models found in last successful connection test.</summary>
        private int _settingsLastModelCount = 0;
        /// <summary>Whether settings have unsaved changes.</summary>
        private bool _settingsHasUnsavedChanges = false;

        // ModelVersionComparisonWindow state (for view mode)
        /// <summary>Model ID for version comparison.</summary>
        private string _versionComparisonModelId;
        /// <summary>Initial right version for comparison.</summary>
        private string _versionComparisonInitialRightVersion;
        /// <summary>Hidden ModelVersionComparisonWindow instance for reuse.</summary>
        private ModelVersionComparisonWindow _versionComparisonInstance;

        [Serializable]
        private sealed class ImportHistoryEntry
        {
            public string modelId;
            public string version;
            public string installPath;
            public List<string> importedAssets;
            public DateTime timestamp;
        }

        [Serializable]
        private sealed class ImportHistoryWrapper
        {
            public List<ImportHistoryEntry> entries;
        }
    }
}

