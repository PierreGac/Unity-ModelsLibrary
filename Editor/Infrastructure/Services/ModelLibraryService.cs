
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
        /// Finds the local version of a model by scanning for manifest files (modelLibrary.meta.json).
        /// This is the primary method for local version detection, providing accurate version information.
        /// Searches the entire Assets folder for manifest files and matches them by model ID.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model to find.</param>
        /// <returns>The local version string if found, null otherwise.</returns>
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
        /// Finds the local version of a model by matching asset GUIDs from metadata against project GUIDs.
        /// This is a fallback method when manifest files are not available.
        /// Note: This method can only detect the latest version, not intermediate versions.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model to find.</param>
        /// <param name="projectGuids">Set of all asset GUIDs currently in the Unity project.</param>
        /// <returns>The detected local version (typically the latest version), or null if not found.</returns>
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
            => await _updateDetector.GetAvailableUpdatesAsync();

        /// <summary>
        /// Get update information for a specific model.
        /// </summary>
        public async Task<ModelUpdateDetector.ModelUpdateInfo> GetUpdateInfoAsync(string modelId)
            => await _updateDetector.GetUpdateInfoAsync(modelId);

        /// <summary>
        /// Check if a specific model has updates available.
        /// </summary>
        public async Task<bool> HasUpdateAsync(string modelId)
            => await _updateDetector.HasUpdateAsync(modelId);

        /// <summary>
        /// Get the total number of available updates.
        /// </summary>
        public async Task<int> GetUpdateCountAsync()
            => await _updateDetector.GetUpdateCountAsync();

        /// <summary>
        /// Force a refresh of all update information.
        /// </summary>
        public async Task RefreshAllUpdatesAsync()
            => await _updateDetector.RefreshAllUpdatesAsync();

        /// <summary>
        /// Delete a specific model version from the repository.
        /// Note: Deletion of the latest version is not recommended as it requires updating the index.
        /// </summary>
        /// <param name="modelId">The model ID (GUID).</param>
        /// <param name="version">The version string to delete (e.g., "1.0.0").</param>
        /// <returns>True if deletion was successful; false otherwise.</returns>
        public async Task<bool> DeleteVersionAsync(string modelId, string version)
        {
            try
            {
                // Check if this is the latest version (warning only - UI handles confirmation)
                ModelIndex index = await GetIndexAsync();
                ModelIndex.Entry entry = index != null && index.entries != null ? index.entries.FirstOrDefault(e => e.id == modelId) : null;
                if (entry != null && entry.latestVersion == version)
                {
                    Debug.LogWarning($"[ModelLibraryService] Deleting latest version {version} of model {modelId}. The index will still reference this version until manually updated.");
                }

                // Delete the version from repository
                bool deleted = await _repo.DeleteVersionAsync(modelId, version);
                if (deleted)
                {
                    Debug.Log($"[ModelLibraryService] Successfully deleted version {version} of model {modelId}");
                }
                return deleted;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibraryService] Error deleting version {version} of model {modelId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads a specific model version from the repository into the local editor cache.
        /// Downloads all payload files, dependencies, images, and metadata to a local cache directory.
        /// Creates the cache directory structure and saves metadata for quick access.
        /// </summary>
        /// <param name="id">The unique identifier of the model to download.</param>
        /// <param name="version">The version string to download (e.g., "1.0.0").</param>
        /// <returns>A tuple containing the absolute cache root path and the loaded model metadata.</returns>
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
        /// Submits a prepared model version folder to the repository and updates the index.
        /// Uploads all files from the local version folder (payload, dependencies, images) to the repository.
        /// Generates a model ID if not provided, creates changelog entries, and updates the global index.
        /// </summary>
        /// <param name="meta">Complete model metadata ready for submission.</param>
        /// <param name="localVersionRoot">Absolute path to the local version folder containing all files.</param>
        /// <param name="changeSummary">Optional changelog summary for this version (defaults to "Initial submission").</param>
        /// <returns>The repository-relative path of the uploaded version root (e.g., "modelId/version").</returns>
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

        /// <summary>
        /// Publishes a metadata-only update by creating a new version with updated metadata.
        /// Clones all files from the base version, creates a new version with bumped version number,
        /// saves the updated metadata, and updates the index. This is used for metadata edits without file changes.
        /// </summary>
        /// <param name="updatedMeta">Updated model metadata with modified fields (description, tags, etc.).</param>
        /// <param name="baseVersion">The version to clone files from (typically the current latest version).</param>
        /// <param name="changeSummary">Changelog summary describing what metadata was changed.</param>
        /// <param name="author">Author name for the metadata update.</param>
        /// <param name="bumpStrategy">Optional function to customize version bumping (defaults to patch increment).</param>
        /// <returns>The updated ModelMeta with the new version number.</returns>
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

        /// <summary>
        /// Clones all files from a source version to a target version in the repository.
        /// Used when creating metadata-only updates - copies all payload and image files to the new version.
        /// Downloads files to a temporary location, then uploads them to the new version path.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="sourceVersion">The version to clone files from.</param>
        /// <param name="targetVersion">The new version to clone files to.</param>
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

        /// <summary>
        /// Updates the global model index with the latest metadata from a model version.
        /// Creates a new index entry if the model doesn't exist, or updates the existing entry.
        /// Only updates the latest version if the new version is greater than or equal to the existing latest version.
        /// Updates the cached index to reflect changes immediately.
        /// </summary>
        /// <param name="meta">Model metadata containing the latest version information.</param>
        private async Task UpdateIndexWithLatestMetaAsync(ModelMeta meta)
        {
            ModelIndex index = await GetIndexAsync();
            ModelIndex.Entry entry = index.Get(meta.identity.id);
            long timestamp = meta.updatedTimeTicks <= 0 ? DateTime.Now.Ticks : meta.updatedTimeTicks;
            long releaseTimestamp = meta.uploadTimeTicks <= 0 ? timestamp : meta.uploadTimeTicks;
            List<string> tags = meta.tags != null && meta.tags.values != null ? new List<string>(meta.tags.values) : new List<string>();

            if (entry == null)
            {
                // Create a new index entry for this model
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
                // Update existing entry - only update latest version if new version is >= current
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
                // Always update other fields (name, description, tags, timestamps)
                entry.name = meta.identity.name;
                entry.description = meta.description;
                entry.updatedTimeTicks = timestamp;
                entry.tags = tags;
            }

            await _repo.SaveIndexAsync(index);
            _indexCache = index; // Update cache to reflect changes
        }

        /// <summary>
        /// Ensures a changelog entry exists for the specified version in the model metadata.
        /// Creates a new entry if one doesn't exist, or updates the existing entry if it does.
        /// Sanitizes the summary and author fields before adding.
        /// </summary>
        /// <param name="meta">Model metadata to add the changelog entry to.</param>
        /// <param name="summary">Changelog summary text.</param>
        /// <param name="author">Author name for the changelog entry.</param>
        /// <param name="version">Version string for the changelog entry.</param>
        /// <param name="timestamp">Timestamp for the changelog entry (in ticks).</param>
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


