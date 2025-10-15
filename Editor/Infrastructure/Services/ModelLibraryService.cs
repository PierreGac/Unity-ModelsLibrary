
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Facade for all model library operations.
    /// Handles reading/writing the index and model metadata, downloads, submissions, and project scanning.
    /// </summary>
    public class ModelLibraryService
    {
        private readonly IModelRepository _repo;
        private ModelIndex _indexCache;
        private ModelUpdateDetector _updateDetector;
        private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();

        public ModelLibraryService(IModelRepository repo)
        {
            _repo = repo;
            _updateDetector = new ModelUpdateDetector(this);
        }

        /// <summary>
        /// Get the cached index, loading it from repository if needed.
        /// </summary>
        public async Task<ModelIndex> GetIndexAsync()
        {
            _indexCache ??= await _repo.LoadIndexAsync();

            return _indexCache;
        }

        /// <summary>
        /// Force refresh of the index cache from the repository.
        /// </summary>
        public async Task RefreshIndexAsync() => _indexCache = await _repo.LoadIndexAsync();

        public async Task<ModelMeta> GetMetaAsync(string id, string version) => await _repo.LoadMetaAsync(id, version);

        public async Task<Texture2D> GetPreviewTextureAsync(string id, string version, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            string key = PathUtils.SanitizePathSeparator(($"{id}/{version}/{relativePath}")).ToLowerInvariant();
            if (_previewCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            string cacheRoot = EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, id, version));
            string localPath = Path.Combine(cacheRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(localPath))
            {
                string directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string repoPath = PathUtils.SanitizePathSeparator(($"{id}/{version}/{relativePath}"));
                await _repo.DownloadFileAsync(repoPath, localPath);
            }

            if (!File.Exists(localPath))
            {
                return null;
            }

            byte[] data = await File.ReadAllBytesAsync(localPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!texture.LoadImage(data))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            texture.Apply();

            _previewCache[key] = texture;
            return texture;
        }

        /// <summary>
        /// Scan current project assets by GUIDs to detect presence of models and possible updates.
        /// </summary>
        public async Task<List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)>> ScanProjectForKnownModelsAsync()
        {
            ModelIndex index = await GetIndexAsync();
            // Gather all asset GUIDs present in project
            string[] allGuids = UnityEditor.AssetDatabase.FindAssets(string.Empty);
            HashSet<string> guidSet = new HashSet<string>(allGuids);

            List<(ModelIndex.Entry, bool, string)> results = new List<(ModelIndex.Entry, bool, string)>();

            foreach (ModelIndex.Entry e in index.entries)
            {
                // All models are visible (no project restrictions)

                // Try to find the actual local version by looking for modelLibrary.meta.json files
                string foundLocalVersion = await FindLocalVersionAsync(e.id);

                // If no local version found via manifest files, try GUID-based detection as fallback
                if (string.IsNullOrEmpty(foundLocalVersion))
                {
                    foundLocalVersion = await FindLocalVersionByGuidsAsync(e.id, guidSet);
                }

                bool hasUpdate = false;
                if (!string.IsNullOrEmpty(foundLocalVersion))
                {
                    if (SemVer.TryParse(foundLocalVersion, out SemVer local) && SemVer.TryParse(e.latestVersion, out SemVer remote))
                    {
                        hasUpdate = remote.CompareTo(local) > 0;
                    }
                }

                results.Add((e, hasUpdate, foundLocalVersion));
            }

            return results;
        }

        /// <summary>
        /// Find the local version by looking for modelLibrary.meta.json manifest files.
        /// </summary>
        private async Task<string> FindLocalVersionAsync(string modelId)
        {
            try
            {
                // Look for modelLibrary.meta.json files in the project
                string[] manifestFiles = UnityEditor.AssetDatabase.FindAssets("modelLibrary.meta");

                foreach (string guid in manifestFiles)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    try
                    {
                        string content = await File.ReadAllTextAsync(path);
                        if (string.IsNullOrEmpty(content))
                        {
                            Debug.LogWarning($"[ModelLibraryService] Manifest file {path} is empty");
                            continue;
                        }

                        ModelMeta localMeta = JsonUtil.FromJson<ModelMeta>(content);
                        if (localMeta == null)
                        {
                            Debug.LogWarning($"[ModelLibraryService] Failed to parse manifest file {path} - JSON deserialization returned null");
                            continue;
                        }

                        if (localMeta.identity == null)
                        {
                            Debug.LogWarning($"[ModelLibraryService] Manifest file {path} has null identity");
                            continue;
                        }

                        if (string.IsNullOrEmpty(localMeta.identity.id))
                        {
                            Debug.LogWarning($"[ModelLibraryService] Manifest file {path} has empty model ID");
                            continue;
                        }

                        if (string.IsNullOrEmpty(localMeta.version))
                        {
                            Debug.LogWarning($"[ModelLibraryService] Manifest file {path} has empty version for model {localMeta.identity.id}");
                            continue;
                        }

                        // Check if this manifest belongs to the model we're looking for
                        if (localMeta.identity.id == modelId)
                        {
                            Debug.Log($"[ModelLibraryService] Found local version {localMeta.version} for model {modelId} at {path}");
                            return localMeta.version;
                        }
                        else
                        {
                            Debug.Log($"[ModelLibraryService] Manifest file {path} belongs to model {localMeta.identity.id}, not {modelId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModelLibraryService] Failed to read manifest file {path}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModelLibraryService] Error scanning for local versions of {modelId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find the local version by checking which version's GUIDs are present in the project.
        /// </summary>
        private async Task<string> FindLocalVersionByGuidsAsync(string modelId, HashSet<string> projectGuids)
        {
            try
            {
                // Get the model index to find available versions
                ModelIndex index = await GetIndexAsync();
                ModelIndex.Entry entry = index.Get(modelId);
                if (entry == null)
                {
                    return null;
                }

                // For now, we'll check the latest version since we don't have a versions list
                // In a more complete implementation, we'd need to query the repository for all available versions
                try
                {
                    ModelMeta meta = await _repo.LoadMetaAsync(modelId, entry.latestVersion);
                    if (meta == null)
                    {
                        Debug.LogWarning($"[ModelLibraryService] Failed to load metadata for model {modelId} version {entry.latestVersion}");
                        return null;
                    }

                    if (meta.assetGuids == null || meta.assetGuids.Count == 0)
                    {
                        Debug.LogWarning($"[ModelLibraryService] Model {modelId} version {entry.latestVersion} has no asset GUIDs");
                        return null;
                    }

                    // Check if any GUIDs from this version exist in the project
                    List<string> foundGuids = new List<string>();
                    foreach (string guid in meta.assetGuids)
                    {
                        if (projectGuids.Contains(guid))
                        {
                            foundGuids.Add(guid);
                        }
                    }

                    if (foundGuids.Count > 0)
                    {
                        Debug.Log($"[ModelLibraryService] Found local version {entry.latestVersion} for model {modelId} via GUID detection. Found {foundGuids.Count}/{meta.assetGuids.Count} GUIDs: {string.Join(", ", foundGuids.Take(3))}{(foundGuids.Count > 3 ? "..." : "")}");
                        return entry.latestVersion;
                    }
                    else
                    {
                        Debug.Log($"[ModelLibraryService] Model {modelId} version {entry.latestVersion} has {meta.assetGuids.Count} GUIDs but none found in project");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModelLibraryService] Error checking GUIDs for model {modelId}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModelLibraryService] Error in GUID-based version detection for {modelId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all models with available updates.
        /// </summary>
        public async Task<List<ModelUpdateDetector.ModelUpdateInfo>> GetAvailableUpdatesAsync()
        {
            return await _updateDetector.GetAvailableUpdatesAsync();
        }

        /// <summary>
        /// Get update information for a specific model.
        /// </summary>
        public async Task<ModelUpdateDetector.ModelUpdateInfo> GetUpdateInfoAsync(string modelId)
        {
            return await _updateDetector.GetUpdateInfoAsync(modelId);
        }

        /// <summary>
        /// Check if a specific model has updates available.
        /// </summary>
        public async Task<bool> HasUpdateAsync(string modelId)
        {
            return await _updateDetector.HasUpdateAsync(modelId);
        }

        /// <summary>
        /// Get the total number of available updates.
        /// </summary>
        public async Task<int> GetUpdateCountAsync()
        {
            return await _updateDetector.GetUpdateCountAsync();
        }

        /// <summary>
        /// Force a refresh of all update information.
        /// </summary>
        public async Task RefreshAllUpdatesAsync()
        {
            await _updateDetector.RefreshAllUpdatesAsync();
        }

        /// <summary>
        /// Download a specific model version into the local editor cache.
        /// Returns the absolute cache folder and loaded meta.
        /// </summary>
        public async Task<(string versionRoot, ModelMeta meta)> DownloadModelVersionAsync(string id, string version)
        {
            // Download all payload & images to local cache folder
            ModelMeta meta = await _repo.LoadMetaAsync(id, version);

            string cacheRoot = EditorPaths.LibraryPath(Path.Combine(ModelLibrarySettings.GetOrCreate().localCacheRoot, id, version));
            Directory.CreateDirectory(cacheRoot);

            // Save meta too (for quick access)
            string localMetaPath = Path.Combine(cacheRoot, ModelMeta.MODEL_JSON);
            File.WriteAllText(localMetaPath, JsonUtil.ToJson(meta));

            // Pull all files present in the repository under id/version (payload, deps, images, etc.)
            string versionRootRel = PathUtils.SanitizePathSeparator(Path.Combine(id, version));
            List<string> files = await _repo.ListFilesAsync(versionRootRel);
            string prefix = PathUtils.SanitizePathSeparator((id + "/" + version + "/"));
            foreach (string repoRel in files)
            {
                string rel = PathUtils.SanitizePathSeparator(repoRel);
                if (!rel.StartsWith(prefix))
                {
                    continue;
                }
                string subRel = rel[prefix.Length..];
                if (string.Equals(subRel, ModelMeta.MODEL_JSON, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // already wrote meta locally
                }
                string localAbs = PathUtils.SanitizePathSeparator(Path.Combine(cacheRoot, subRel));
                await _repo.DownloadFileAsync(rel, localAbs);
            }
            return (cacheRoot, meta);
        }

        /// <summary>
        /// Submit a prepared model version folder (with payload/images) to the repository and update the index.
        /// Returns the repository-relative path of the version root.
        /// </summary>
        public async Task<string> SubmitNewVersionAsync(ModelMeta meta, string localVersionRoot, string changeSummary = null)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            meta.identity ??= new ModelIdentity();
            if (string.IsNullOrWhiteSpace(meta.identity.id))
            {
                meta.identity.id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(meta.version))
            {
                throw new InvalidOperationException("Meta.Version required");
            }

            long nowUtc = DateTime.Now.Ticks;
            if (meta.createdTimeTicks <= 0)
            {
                meta.createdTimeTicks = nowUtc;
            }
            meta.updatedTimeTicks = nowUtc;

            string author = string.IsNullOrWhiteSpace(meta.author) ? "unknown" : meta.author;
            meta.author = author;
            EnsureChangelogEntry(meta, string.IsNullOrWhiteSpace(changeSummary) ? "Initial submission" : changeSummary, author, meta.version, nowUtc);

            string versionRootRel = PathUtils.SanitizePathSeparator(Path.Combine(meta.identity.id, meta.version));
            await _repo.EnsureDirectoryAsync(versionRootRel);
            foreach (string file in Directory.GetFiles(localVersionRoot, "*", SearchOption.AllDirectories))
            {
                string rel = PathUtils.SanitizePathSeparator(file[localVersionRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.Equals(rel, ModelMeta.MODEL_JSON, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                await _repo.UploadFileAsync(versionRootRel + "/" + rel, file);
            }

            await _repo.SaveMetaAsync(meta.identity.id, meta.version, meta);
            await UpdateIndexWithLatestMetaAsync(meta);
            return versionRootRel;
        }

        public async Task<ModelMeta> PublishMetadataUpdateAsync(ModelMeta updatedMeta, string baseVersion, string changeSummary, string author, Func<SemVer, SemVer> bumpStrategy = null)
        {
            if (updatedMeta == null)
            {
                throw new ArgumentNullException(nameof(updatedMeta));
            }

            if (updatedMeta.identity == null || string.IsNullOrWhiteSpace(updatedMeta.identity.id))
            {
                throw new InvalidOperationException("Model identity required for metadata update.");
            }

            string sourceVersion = string.IsNullOrWhiteSpace(baseVersion) ? updatedMeta.version : baseVersion;
            if (string.IsNullOrWhiteSpace(sourceVersion))
            {
                throw new InvalidOperationException("Base version required for metadata update.");
            }

            if (!SemVer.TryParse(sourceVersion, out SemVer parsedSource))
            {
                throw new InvalidOperationException($"Invalid base version '{sourceVersion}'.");
            }

            SemVer bumped = bumpStrategy != null ? bumpStrategy(parsedSource) : new SemVer(parsedSource.major, parsedSource.minor, parsedSource.patch + 1);
            string newVersion = bumped.ToString();

            long nowUtc = DateTime.Now.Ticks;
            if (updatedMeta.createdTimeTicks <= 0)
            {
                updatedMeta.createdTimeTicks = nowUtc;
            }
            updatedMeta.updatedTimeTicks = nowUtc;
            updatedMeta.version = newVersion;

            string resolvedAuthor = string.IsNullOrWhiteSpace(author) ? "unknown" : author;
            if (string.IsNullOrWhiteSpace(updatedMeta.author))
            {
                updatedMeta.author = resolvedAuthor;
            }

            EnsureChangelogEntry(updatedMeta, string.IsNullOrWhiteSpace(changeSummary) ? "Metadata updated" : changeSummary, resolvedAuthor, newVersion, nowUtc);

            await CloneVersionFilesAsync(updatedMeta.identity.id, sourceVersion, newVersion);
            await _repo.SaveMetaAsync(updatedMeta.identity.id, newVersion, updatedMeta);
            await UpdateIndexWithLatestMetaAsync(updatedMeta);
            return updatedMeta;
        }

        private async Task CloneVersionFilesAsync(string modelId, string sourceVersion, string targetVersion)
        {
            if (string.Equals(sourceVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string sourceRootRel = $"{modelId}/{sourceVersion}".Replace('\\', '/');
            string targetRootRel = $"{modelId}/{targetVersion}".Replace('\\', '/');

            await _repo.EnsureDirectoryAsync(targetRootRel);

            List<string> files = await _repo.ListFilesAsync(sourceRootRel) ?? new List<string>();
            if (files.Count == 0)
            {
                return;
            }

            string prefix = sourceRootRel.TrimEnd('/') + "/";
            string tempRoot = Path.Combine(Path.GetTempPath(), "ModelClone_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (string repoRel in files)
                {
                    string normalized = PathUtils.SanitizePathSeparator(repoRel);
                    if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string subRel = normalized[prefix.Length..];
                    if (string.Equals(subRel, ModelMeta.MODEL_JSON, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string tempPath = Path.Combine(tempRoot, subRel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? tempRoot);
                    await _repo.DownloadFileAsync(normalized, tempPath);
                    await _repo.UploadFileAsync($"{targetRootRel}/{subRel}", tempPath);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        private async Task UpdateIndexWithLatestMetaAsync(ModelMeta meta)
        {
            ModelIndex index = await GetIndexAsync();
            ModelIndex.Entry entry = index.Get(meta.identity.id);
            long timestamp = meta.updatedTimeTicks <= 0 ? DateTime.Now.Ticks : meta.updatedTimeTicks;
            long releaseTimestamp = meta.uploadTimeTicks <= 0 ? timestamp : meta.uploadTimeTicks;
            List<string> tags = meta.tags?.values != null ? new List<string>(meta.tags.values) : new List<string>();

            if (entry == null)
            {
                entry = new ModelIndex.Entry
                {
                    id = meta.identity.id,
                    name = meta.identity.name,
                    description = meta.description,
                    latestVersion = meta.version,
                    updatedTimeTicks = timestamp,
                    releaseTimeTicks = releaseTimestamp,
                    tags = tags,
                };
                index.entries.Add(entry);
            }
            else
            {
                bool shouldUpdateVersion = true;
                if (!string.IsNullOrEmpty(entry.latestVersion) && SemVer.TryParse(entry.latestVersion, out SemVer existing) && SemVer.TryParse(meta.version, out SemVer incoming))
                {
                    shouldUpdateVersion = incoming.CompareTo(existing) >= 0;
                }

                if (shouldUpdateVersion)
                {
                    entry.latestVersion = meta.version;
                    entry.releaseTimeTicks = releaseTimestamp;
                }
                entry.name = meta.identity.name;
                entry.description = meta.description;
                entry.updatedTimeTicks = timestamp;
                entry.tags = tags;
            }

            await _repo.SaveIndexAsync(index);
            _indexCache = index;
        }

        private static void EnsureChangelogEntry(ModelMeta meta, string summary, string author, string version, long timestamp)
        {
            meta.changelog ??= new List<ModelChangelogEntry>();

            string sanitizedSummary = string.IsNullOrWhiteSpace(summary) ? "Updated" : summary.Trim();
            string sanitizedAuthor = string.IsNullOrWhiteSpace(author) ? "unknown" : author.Trim();
            long sanitizedTimestamp = timestamp <= 0 ? DateTime.Now.Ticks : timestamp;

            ModelChangelogEntry existing = meta.changelog.LastOrDefault(e => string.Equals(e.version, version, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                meta.changelog.Add(new ModelChangelogEntry
                {
                    version = version,
                    summary = sanitizedSummary,
                    author = sanitizedAuthor,
                    timestamp = sanitizedTimestamp
                });
            }
            else
            {
                existing.summary = sanitizedSummary;
                existing.author = sanitizedAuthor;
                existing.timestamp = sanitizedTimestamp;
            }
        }

    }
}


