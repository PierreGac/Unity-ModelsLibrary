using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    public class ModelLibraryWindow : EditorWindow
    {
        /// <summary>
        /// Main browser window to list models from the repository and perform actions.
        /// </summary>
        private ModelLibraryService _service;
        private string _search = string.Empty;
        private Vector2 _scroll;
        private Dictionary<string, bool> _expanded = new();
        private readonly Dictionary<string, ModelMeta> _metaCache = new();
        private readonly HashSet<string> _loadingMeta = new();
        private readonly Dictionary<string, ModelMeta> _localInstallCache = new();
        private readonly HashSet<string> _importsInProgress = new();
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new();
        private readonly HashSet<string> _loadingThumbnails = new();
        private string _projectName;
        private bool _showTagFilter;
        private readonly HashSet<string> _selectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _tagScroll;
        private readonly Dictionary<string, int> _tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _sortedTags = new List<string>();
        private ModelIndex _tagSource;
        [MenuItem("Tools/Model Library/Browser")]
        public static void Open()
        {
            ModelLibraryWindow win = GetWindow<ModelLibraryWindow>("Model Library");
            win._projectName = Application.productName;
            win.Show();
        }

        private void OnEnable()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);
            _service = new ModelLibraryService(repo);
            _ = _service.RefreshIndexAsync();
            FirstRunWizard.MaybeShow();
        }

        private void OnGUI()
        {
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
                    _ = _service.RefreshIndexAsync();
                }
                if (GUILayout.Button("Submit Model", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    ModelSubmitWindow.Open();
                }
                if (GUILayout.Button("User", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    UserSettingsWindow.Open();
                }
            }
            Task<ModelIndex> indexTask = _service.GetIndexAsync();
            ModelIndex index = indexTask.IsCompleted ? indexTask.Result : null;

            DrawTagFilter(index);
            if (!indexTask.IsCompleted)
            {
                GUILayout.Label("Loading index...");
                return;
            }
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

            query = query.Where(IsVisibleForCurrentProject);

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
            foreach (ModelIndex.Entry e in q)
            {
                DrawEntry(e);
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
                if (!IsVisibleForCurrentProject(entry) || entry?.tags == null)
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

        private bool IsVisibleForCurrentProject(ModelIndex.Entry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.projectScopes == null || entry.projectScopes.Count == 0)
            {
                return true;
            }

            return entry.projectScopes.Any(scope => string.Equals(scope, _projectName, StringComparison.OrdinalIgnoreCase));
        }

        private void DrawEntry(ModelIndex.Entry e)
        {
            if (!IsVisibleForCurrentProject(e))
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(e.name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"v{e.latestVersion}");
                    bool installed = TryGetLocalInstall(e, out ModelMeta localMeta);
                    string localVersion = installed ? localMeta.version : null;
                    bool needsUpgrade = installed && NeedsUpgrade(localVersion, e.latestVersion);
                    bool isBusy = _importsInProgress.Contains(e.id);
                    if (installed)
                    {
                        string label = needsUpgrade ? $"Local v{localVersion} (update available)" : $"Local v{localVersion}";
                        GUILayout.Label(label, EditorStyles.miniLabel);
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
                    using (new EditorGUI.DisabledScope(isBusy || (installed && !needsUpgrade)))
                    {
                        string actionLabel = needsUpgrade ? "Update" : "Import";
                        if (GUILayout.Button(actionLabel, GUILayout.Width(80)))
                        {
                            string previousVersion = installed ? localVersion : null;
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

        private static bool NeedsUpgrade(string localVersion, string remoteVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion))
            {
                return false;
            }
            if (string.IsNullOrEmpty(localVersion))
            {
                return true;
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
                normalized = $"Assets/{normalized.TrimStart('/') }";
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
                        // We can't know the exact local version from GUIDs alone; mark as installed with unknown version.
                        ModelMeta minimal = new ModelMeta
                        {
                            identity = new ModelIdentity { id = entry.id, name = entry.name },
                            version = "(unknown)"
                        };
                        _localInstallCache[entry.id] = minimal;
                        meta = minimal;
                        return true;
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
                await ModelProjectImporter.ImportFromCacheAsync(root, meta, cleanDestination: true, overrideInstallPath: chosenInstallPath);
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
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _importsInProgress.Remove(id);
            }
        }


    }
}





