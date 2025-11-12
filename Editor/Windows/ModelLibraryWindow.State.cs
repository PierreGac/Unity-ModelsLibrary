using System;
using System.Collections.Generic;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
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
        private readonly List<ImportHistoryEntry> _importHistory = new List<ImportHistoryEntry>();

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

