using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public partial class ModelLibraryWindow
    {
        private void ClearThumbnailCache()
        {
            foreach (KeyValuePair<string, Texture2D> kvp in _thumbnailCache)
            {
                Texture2D texture = kvp.Value;
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            _thumbnailCache.Clear();
            _thumbnailCacheOrder.Clear();
            _thumbnailCacheNodes.Clear();
        }

        /// <summary>
        /// Clears the entire meta cache.
        /// </summary>
        private void ClearMetaCache()
        {
            _metaCache.Clear();
            _metaCacheOrder.Clear();
            _metaCacheNodes.Clear();
            _loadingMeta.Clear();
        }

        /// <summary>
        /// Invalidates the meta cache for a specific model version.
        /// This forces the metadata to be reloaded from the repository immediately.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="version">The version string.</param>
        public void InvalidateMetaCache(string modelId, string version)
        {
            string key = modelId + "@" + version;
            if (_metaCache.ContainsKey(key))
            {
                _metaCache.Remove(key);
                if (_metaCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
                {
                    _metaCacheOrder.Remove(node);
                    _metaCacheNodes.Remove(key);
                }
            }

            // Trigger immediate reload of metadata to get updated notes
            if (_service != null && !_loadingMeta.Contains(key))
            {
                _ = LoadMetaAsync(modelId, version);
            }

            // Update notes count after reload (will be called again when metadata loads)
            UpdateNotesCount();
            UpdateWindowTitle();
            Repaint();
        }

        private async Task LoadMetaAsync(string id, string version)
        {
            string key = id + "@" + version;
            _loadingMeta.Add(key);
            try
            {
                ModelMeta meta = await _service.GetMetaAsync(id, version);
                AddMetaCacheEntry(key, meta);
                string previewPath = meta?.previewImagePath;
                if (!string.IsNullOrEmpty(previewPath))
                {
                    string thumbKey = key + "#thumb";
                    if (!_thumbnailCache.ContainsKey(thumbKey) && !_loadingThumbnails.Contains(thumbKey))
                    {
                        _ = LoadThumbnailAsync(thumbKey, id, version, previewPath);
                    }
                }

                // Update notes count and window title after metadata is loaded
                UpdateNotesCount();
                UpdateWindowTitle();
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Meta Failed",
                    $"Failed to load metadata for {id} version {version}: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"Key: {key}, ModelId: {id}, Version: {version}");
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
                Texture2D texture = await _service.GetPreviewTextureAsync(id, version, relativePath);
                if (texture != null)
                {
                    AddThumbnailCacheEntry(cacheKey, texture);
                }
                else
                {
                    RemoveThumbnailCacheEntry(cacheKey);
                }
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Thumbnail Failed",
                    $"Failed to load thumbnail: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"CacheKey: {cacheKey}, ModelId: {id}, Version: {version}, RelativePath: {relativePath}");
                RemoveThumbnailCacheEntry(cacheKey);
            }
            finally
            {
                _loadingThumbnails.Remove(cacheKey);
            }
        }

        private bool TryGetMetaFromCache(string key, out ModelMeta meta)
        {
            if (_metaCache.TryGetValue(key, out meta) && meta != null)
            {
                RegisterMetaCacheHit(key);
                return true;
            }

            meta = null;
            return false;
        }

        private void AddMetaCacheEntry(string key, ModelMeta meta)
        {
            _metaCache[key] = meta;
            RegisterMetaCacheHit(key);
            EnsureMetaCacheLimit();
        }

        private void RegisterMetaCacheHit(string key)
        {
            if (_metaCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
            {
                _metaCacheOrder.Remove(node);
                _metaCacheOrder.AddLast(node);
            }
            else
            {
                LinkedListNode<string> newNode = new LinkedListNode<string>(key);
                _metaCacheOrder.AddLast(newNode);
                _metaCacheNodes[key] = newNode;
            }
        }

        private void EnsureMetaCacheLimit()
        {
            while (_metaCache.Count > __MAX_META_CACHE_ENTRIES && _metaCacheOrder.First != null)
            {
                string oldestKey = _metaCacheOrder.First.Value;
                _metaCacheOrder.RemoveFirst();
                _metaCacheNodes.Remove(oldestKey);
                _metaCache.Remove(oldestKey);
            }
        }

        private bool TryGetThumbnailFromCache(string key, out Texture2D texture)
        {
            if (_thumbnailCache.TryGetValue(key, out texture) && texture != null)
            {
                RegisterThumbnailCacheHit(key);
                return true;
            }

            texture = null;
            return false;
        }

        private void AddThumbnailCacheEntry(string key, Texture2D texture)
        {
            if (_thumbnailCache.TryGetValue(key, out Texture2D existing) && existing != null)
            {
                DestroyImmediate(existing);
            }

            _thumbnailCache[key] = texture;
            RegisterThumbnailCacheHit(key);
            EnsureThumbnailCacheLimit();
        }

        private void RegisterThumbnailCacheHit(string key)
        {
            if (_thumbnailCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
            {
                _thumbnailCacheOrder.Remove(node);
                _thumbnailCacheOrder.AddLast(node);
            }
            else
            {
                LinkedListNode<string> newNode = new LinkedListNode<string>(key);
                _thumbnailCacheOrder.AddLast(newNode);
                _thumbnailCacheNodes[key] = newNode;
            }
        }

        private void EnsureThumbnailCacheLimit()
        {
            while (_thumbnailCache.Count > __MAX_THUMBNAIL_CACHE_ENTRIES && _thumbnailCacheOrder.First != null)
            {
                string oldestKey = _thumbnailCacheOrder.First.Value;
                RemoveThumbnailCacheEntry(oldestKey);
            }
        }

        private void RemoveThumbnailCacheEntry(string key)
        {
            if (_thumbnailCache.TryGetValue(key, out Texture2D existing) && existing != null)
            {
                DestroyImmediate(existing);
            }

            _thumbnailCache.Remove(key);
            if (_thumbnailCacheNodes.TryGetValue(key, out LinkedListNode<string> node))
            {
                _thumbnailCacheOrder.Remove(node);
                _thumbnailCacheNodes.Remove(key);
            }
        }

        private void TriggerCacheWarming()
        {
            if (_cacheWarmTriggered)
            {
                return;
            }

            _cacheWarmTriggered = true;

            if (_indexCache == null || _indexCache.entries == null || _indexCache.entries.Count == 0)
            {
                return;
            }

            int warmCount = Math.Min(__CACHE_WARM_ENTRY_COUNT, _indexCache.entries.Count);
            for (int i = 0; i < warmCount; i++)
            {
                ModelIndex.Entry entry = _indexCache.entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.latestVersion))
                {
                    continue;
                }

                string key = entry.id + "@" + entry.latestVersion;
                if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
                {
                    _ = LoadMetaAsync(entry.id, entry.latestVersion);
                }
                else
                {
                    ModelMeta warmedMeta;
                    if (TryGetMetaFromCache(key, out warmedMeta) && warmedMeta != null && !string.IsNullOrEmpty(warmedMeta.previewImagePath))
                    {
                        string thumbKey = key + "#thumb";
                        Texture2D cachedThumbnail;
                        if (TryGetThumbnailFromCache(thumbKey, out cachedThumbnail))
                        {
                            continue;
                        }

                        if (!_loadingThumbnails.Contains(thumbKey))
                        {
                            _ = LoadThumbnailAsync(thumbKey, entry.id, entry.latestVersion, warmedMeta.previewImagePath);
                        }
                    }
                }
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

            using (IEnumerator<string> enumerator = Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories).GetEnumerator())
            {
                return enumerator.MoveNext();
            }
        }

        private bool TryGetLocalInstall(ModelIndex.Entry entry, out ModelMeta meta)
        {
            if (_negativeCache.Contains(entry.id))
            {
                meta = null;
                return false;
            }

            if (_localInstallCache.TryGetValue(entry.id, out meta) && meta != null)
            {
                return true;
            }

            // If cache is not initialized, trigger refresh but don't return false yet
            // Check if cache is currently refreshing
            if (!_manifestCacheInitialized)
            {
                // Only trigger refresh if not already refreshing to avoid duplicate calls
                if (!_refreshingManifest)
                {
                    _ = RefreshManifestCacheAsync();
                }
                // Don't return false immediately - check manifest cache first
                // The cache might be populated by the time we check it
            }

            // Check manifest cache (even if not initialized, it might have been populated)
            if (_manifestCache.TryGetValue(entry.id, out meta) && meta != null)
            {
                _localInstallCache[entry.id] = meta;
                return true;
            }

            // REMOVED: Blocking AssetDatabase.FindAssets call that was causing freezes
            // The manifest cache refresh will handle detection of installed models
            // If we don't have it in cache yet, we'll show "not installed" until cache is ready
            // This is better than blocking the UI thread with expensive operations

            // Only add to negative cache if manifest cache is initialized
            // Otherwise, we might miss old manifests that are still being processed
            if (_manifestCacheInitialized)
            {
                _negativeCache.Add(entry.id);
                _localInstallCache.Remove(entry.id);
            }
            meta = null;
            return false;
        }

        private void InvalidateLocalInstall(string modelId)
        {
            _localInstallCache.Remove(modelId);
            _negativeCache.Remove(modelId);
        }

        private async Task RefreshManifestCacheAsync()
        {
            // Don't refresh during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (_refreshingManifest)
            {
                return;
            }

            _refreshingManifest = true;
            _manifestCache.Clear();
            _negativeCache.Clear();

            try
            {
                EditorUtility.DisplayProgressBar("Refreshing Manifest Cache", "Scanning for model manifests...", 0f);

                // Move file enumeration to background thread to avoid blocking
                List<string> manifestPaths = await ManifestDiscoveryUtility.DiscoverAllManifestFilesAsync("Assets");

                int total = manifestPaths.Count;
                int processed = 0;

                // Load index once to match old manifests by name
                ModelIndex index = await _service.GetIndexAsync();

                // Check again after async operation
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorUtility.ClearProgressBar();
                    _refreshingManifest = false;
                    return;
                }

                // Process files on main thread with progress updates
                for (int i = 0; i < manifestPaths.Count; i++)
                {
                    // Check play mode in loop
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorUtility.ClearProgressBar();
                        _refreshingManifest = false;
                        return;
                    }

                    string manifestPath = manifestPaths[i];
                    processed++;

                    // Update progress bar
                    float progress = (float)processed / total;
                    EditorUtility.DisplayProgressBar("Refreshing Manifest Cache",
                        $"Processing manifest {processed} of {total}...", progress);

                    try
                    {
                        string json = await File.ReadAllTextAsync(manifestPath);
                        ModelMeta parsed = JsonUtil.FromJson<ModelMeta>(json);
                        if (parsed == null)
                        {
                            continue;
                        }

                        // Fallback: If identity is null, create it
                        parsed.identity ??= new ModelIdentity();

                        // Fallback: If identity name is null or empty, use FBX name from folder path
                        if (string.IsNullOrEmpty(parsed.identity.name))
                        {
                            // Extract FBX name from folder path (e.g., "Assets/Models/Benne/Benne.FBX" -> "Benne")
                            string folderPath = Path.GetDirectoryName(manifestPath);
                            string folderName = Path.GetFileName(folderPath);

                            // Remove extension if it's an FBX/OBJ folder name
                            string fbxName = folderName;
                            string ext = Path.GetExtension(folderName).ToLowerInvariant();
                            if (ext == ".fbx" || ext == ".obj")
                            {
                                fbxName = Path.GetFileNameWithoutExtension(folderName);
                            }

                            parsed.identity.name = fbxName;
                        }

                        string modelId = null;

                        // New format: has identity.id
                        if (!string.IsNullOrEmpty(parsed.identity.id))
                        {
                            modelId = parsed.identity.id;
                        }
                        // Old format: try to match by folder name
                        else if (index?.entries != null)
                        {
                            // Extract FBX name from folder path for matching
                            string folderPath = Path.GetDirectoryName(manifestPath);
                            string folderName = Path.GetFileName(folderPath);
                            string fbxNameFromFolder = folderName;
                            string folderExt = Path.GetExtension(folderName).ToLowerInvariant();
                            if (folderExt == ".fbx" || folderExt == ".obj")
                            {
                                fbxNameFromFolder = Path.GetFileNameWithoutExtension(folderName);
                            }

                            // Try to match by name (sanitized or exact)
                            for (int j = 0; j < index.entries.Count; j++)
                            {
                                ModelIndex.Entry entry = index.entries[j];
                                if (entry == null || string.IsNullOrEmpty(entry.name))
                                {
                                    continue;
                                }

                                // Match by FBX name (sanitized model name) or exact name
                                string sanitizedName = InstallPathUtils.SanitizeFolderName(entry.name);
                                if (string.Equals(fbxNameFromFolder, sanitizedName, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(fbxNameFromFolder, entry.name, StringComparison.OrdinalIgnoreCase))
                                {
                                    modelId = entry.id;
                                    // Set the identity.id for future lookups
                                    parsed.identity.id = entry.id;
                                    Debug.Log($"[ModelLibraryWindow] Matched old manifest at {manifestPath} to model ID '{modelId}' by folder name '{fbxNameFromFolder}'");
                                    break;
                                }
                            }
                        }

                        // Cache the manifest if we found a model ID
                        if (!string.IsNullOrEmpty(modelId))
                        {
                            _manifestCache[modelId] = parsed;
                            _localInstallCache[modelId] = parsed;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ModelLibraryWindow] Failed to process manifest {manifestPath}: {ex.Message}");
                    }

                    // Yield periodically to keep UI responsive
                    if (i % 10 == 0)
                    {
                        await Task.Yield();
                    }
                }

                _manifestCacheInitialized = true;
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary] Failed to refresh manifest cache: {ex.Message}");
                _manifestCacheInitialized = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _refreshingManifest = false;
            }
        }

        private bool HasNotes(string modelId, string version)
        {
            string key = modelId + "@" + version;
            if (TryGetMetaFromCache(key, out ModelMeta meta))
            {
                bool hasNotes = meta.notes != null && meta.notes.Count > 0;

                // Only show notification if notes exist AND haven't been read
                if (hasNotes)
                {
                    return !NotificationStateManager.AreNotesRead(modelId, version);
                }
            }

            return false;
        }

        private (bool hasNotes, string tooltip) GetNotesInfo(string modelId, string version)
        {
            string key = modelId + "@" + version;
            if (TryGetMetaFromCache(key, out ModelMeta meta) && meta != null && meta.notes != null && meta.notes.Count > 0)
            {
                // Performance: Use StringBuilder for building tooltip in loop
                StringBuilder tooltipBuilder = new StringBuilder();
                tooltipBuilder.Append($"This model has {meta.notes.Count} note{(meta.notes.Count == 1 ? string.Empty : "s")}:\n");

                foreach (ModelNote note in meta.notes.OrderByDescending(entry => entry.createdTimeTicks).Take(3))
                {
                    string preview = note.message.Length > StringConstants.MAX_TOOLTIP_PREVIEW_LENGTH
                        ? $"{note.message.Substring(0, StringConstants.MAX_TOOLTIP_PREVIEW_LENGTH)}..."
                        : note.message;
                    tooltipBuilder.Append($"• [{note.tag}] {preview}\n");
                }

                if (meta.notes.Count > 3)
                {
                    tooltipBuilder.Append($"... and {meta.notes.Count - 3} more");
                }

                return (true, tooltipBuilder.ToString());
            }

            return (false, null);
        }
    }
}

