using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            catch
            {
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

            // Fallback: Check if assets exist in project by GUID
            // This works for models with identity.id but not for old manifests without it
            string key = entry.id + "@" + entry.latestVersion;
            if (TryGetMetaFromCache(key, out ModelMeta latestMeta) && latestMeta != null)
            {
                try
                {
                    string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                    HashSet<string> set = new HashSet<string>(allGuids);
                    bool any = latestMeta.assetGuids != null && latestMeta.assetGuids.Any(guid => set.Contains(guid));
                    if (any)
                    {
                        meta = null;
                        return false;
                    }
                }
                catch
                {
                }
            }
            else if (!_loadingMeta.Contains(key))
            {
                _ = LoadMetaAsync(entry.id, entry.latestVersion);
            }

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
                List<string> manifestPaths = await Task.Run(() =>
                {
                    List<string> paths = new List<string>();
                    // Search for new naming convention (.modelLibrary.meta.json) first, then old naming for backward compatibility
                    foreach (string manifestPath in Directory.EnumerateFiles("Assets", ".modelLibrary.meta.json", SearchOption.AllDirectories))
                    {
                        paths.Add(manifestPath);
                    }
                    // Fallback for old files created before the naming change
                    foreach (string manifestPath in Directory.EnumerateFiles("Assets", "modelLibrary.meta.json", SearchOption.AllDirectories))
                    {
                        paths.Add(manifestPath);
                    }
                    return paths;
                });

                int total = manifestPaths.Count;
                int processed = 0;

                // Load index once to match old manifests by name
                ModelIndex index = await _service.GetIndexAsync();

                // Process files on main thread with progress updates
                for (int i = 0; i < manifestPaths.Count; i++)
                {
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
                return meta.notes != null && meta.notes.Count > 0;
            }

            return false;
        }

        private (bool hasNotes, string tooltip) GetNotesInfo(string modelId, string version)
        {
            string key = modelId + "@" + version;
            if (TryGetMetaFromCache(key, out ModelMeta meta) && meta != null && meta.notes != null && meta.notes.Count > 0)
            {
                string tooltip = $"This model has {meta.notes.Count} note{(meta.notes.Count == 1 ? string.Empty : "s")}:\n";
                foreach (ModelNote note in meta.notes.OrderByDescending(entry => entry.createdTimeTicks).Take(3))
                {
                    string preview = note.message.Length > 50 ? string.Concat(note.message.Substring(0, 50), "...") : note.message;
                    tooltip += $"• [{note.tag}] {preview}\n";
                }

                if (meta.notes.Count > 3)
                {
                    tooltip += $"... and {meta.notes.Count - 3} more";
                }

                return (true, tooltip);
            }

            return (false, null);
        }
    }
}

