
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
    /// Delegates to specialized services for index, metadata, preview, and scanning operations.
    /// </summary>
    public class ModelLibraryService
    {
        private readonly IModelRepository _repo;
        private readonly ModelIndexService _indexService;
        private readonly ModelMetadataService _metadataService;
        private readonly ModelPreviewService _previewService;
        private readonly ModelScanService _scanService;
        private ModelUpdateDetector _updateDetector;

        public ModelLibraryService(IModelRepository repo)
        {
            _repo = repo;
            _indexService = new ModelIndexService(repo);
            _metadataService = new ModelMetadataService(repo);
            _previewService = new ModelPreviewService(repo);
            _scanService = new ModelScanService(_indexService, repo);
            _updateDetector = new ModelUpdateDetector(this);
        }

        /// <summary>
        /// Get the cached index, loading it from repository if needed.
        /// </summary>
        public async Task<ModelIndex> GetIndexAsync() => await _indexService.GetIndexAsync();

        /// <summary>
        /// Force refresh of the index cache from the repository.
        /// </summary>
        public async Task RefreshIndexAsync() => await _indexService.RefreshIndexAsync();

        public async Task<ModelMeta> GetMetaAsync(string id, string version)
            => await _metadataService.GetMetaAsync(id, version);

        public async Task<Texture2D> GetPreviewTextureAsync(string id, string version, string relativePath)
            => await _previewService.GetPreviewTextureAsync(id, version, relativePath);

        /// <summary>
        /// Scan current project assets by GUIDs to detect presence of models and possible updates.
        /// </summary>
        public async Task<List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)>> ScanProjectForKnownModelsAsync()
            => await _scanService.ScanProjectForKnownModelsAsync();

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
        /// Enumerates all available versions for a specific model by inspecting repository contents.
        /// Returns versions sorted in descending semantic order (latest first).
        /// </summary>
        /// <param name="modelId">Model identifier.</param>
        /// <returns>List of available version strings.</returns>
        public async Task<List<string>> GetAvailableVersionsAsync(string modelId)
            => await _indexService.GetAvailableVersionsAsync(modelId);

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
                ErrorLogger.LogError("Delete Version Failed", 
                    $"Error deleting version {version} of model {modelId}: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}, Version: {version}");
                return false;
            }
        }

        /// <summary>
        /// Delete an entire model from the repository and remove it from the index.
        /// This permanently removes all versions of the model, including metadata, payload, and images.
        /// </summary>
        /// <param name="modelId">The model ID (GUID).</param>
        /// <returns>True if deletion was successful; false otherwise.</returns>
        public async Task<bool> DeleteModelAsync(string modelId)
        {
            try
            {
                // Delete the model from repository
                bool deleted = await _repo.DeleteModelAsync(modelId);
                if (!deleted)
                {
                    return false;
                }

                // Remove the model from the index
                ModelIndex index = await GetIndexAsync();
                if (index?.entries != null)
                {
                    ModelIndex.Entry entryToRemove = index.entries.FirstOrDefault(e => e.id == modelId);
                    if (entryToRemove != null)
                    {
                        index.entries.Remove(entryToRemove);
                        await _repo.SaveIndexAsync(index);
                        // Update cache to reflect changes
                        await _indexService.RefreshIndexAsync();
                        Debug.Log($"[ModelLibraryService] Removed model {modelId} from index");
                    }
                }

                Debug.Log($"[ModelLibraryService] Successfully deleted model {modelId}");
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Delete Model Failed", 
                    $"Error deleting model {modelId}: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}");
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
            // Use dot prefix to hide from Unity Project window
            string localMetaPath = Path.Combine(cacheRoot, "." + ModelMeta.MODEL_JSON);
            string metaJson = JsonUtil.ToJson(meta);
            
            // Use retry logic for file write to handle locked files
            await RetryFileOperationAsync(async () =>
            {
                await Task.Run(() => File.WriteAllText(localMetaPath, metaJson));
            }, maxRetries: 3, initialDelayMs: 200);

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
            string[] files = Directory.GetFiles(localVersionRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
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
            ModelMeta result = await _metadataService.PublishMetadataUpdateAsync(updatedMeta, baseVersion, changeSummary, author, bumpStrategy);
            await _indexService.UpdateIndexWithLatestMetaAsync(result);
            return result;
        }

        /// <summary>
        /// Updates the global model index with the latest metadata from a model version.
        /// Creates a new index entry if the model doesn't exist, or updates the existing entry.
        /// Only updates the latest version if the new version is greater than or equal to the existing latest version.
        /// Updates the cached index to reflect changes immediately.
        /// </summary>
        /// <param name="meta">Model metadata containing the latest version information.</param>
        private async Task UpdateIndexWithLatestMetaAsync(ModelMeta meta)
            => await _indexService.UpdateIndexWithLatestMetaAsync(meta);

        /// <summary>
        /// Clears the local cache for a specific model version, releasing file handles.
        /// Forces garbage collection and waits for pending finalizers to ensure all file handles are released.
        /// Uses retry logic with exponential backoff to handle locked files.
        /// </summary>
        /// <param name="modelId">The model ID to clear cache for. Must not be null or empty.</param>
        /// <param name="version">The version to clear cache for. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown if modelId or version is null or empty.</exception>
        public async Task ClearCacheForModelAsync(string modelId, string version)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
            }
            
            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            }

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            string cacheRoot = EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, modelId, version));
            
            if (!Directory.Exists(cacheRoot))
            {
                // Already cleared or doesn't exist - nothing to do
                return;
            }

            await RetryFileOperationAsync(async () =>
            {
                // Force garbage collection to release any file handles that might be holding locks
                // Note: GC.Collect() is expensive and should only be used when necessary (e.g., file handle cleanup)
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                
                // Double-check directory still exists after GC (it might have been deleted by another process)
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, recursive: true);
                }
            }, maxRetries: 3, initialDelayMs: 500);
        }

        /// <summary>
        /// Retries a file operation with exponential backoff to handle locked files.
        /// Attempts the operation up to maxRetries times, with exponential backoff between retries.
        /// If all retries fail, the last exception is propagated.
        /// </summary>
        /// <param name="operation">The async operation to retry. Must not be null.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3). Must be at least 1.</param>
        /// <param name="initialDelayMs">Initial delay in milliseconds before first retry (default: 500). Used for exponential backoff.</param>
        /// <exception cref="ArgumentNullException">Thrown if operation is null.</exception>
        /// <exception cref="ArgumentException">Thrown if maxRetries is less than 1.</exception>
        private static async Task RetryFileOperationAsync(Func<Task> operation, int maxRetries = 3, int initialDelayMs = 500)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }
            
            if (maxRetries < 1)
            {
                throw new ArgumentException("maxRetries must be at least 1", nameof(maxRetries));
            }

            Exception lastException = null;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return; // Success - exit immediately
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    // Store exception for potential re-throw if all retries fail
                    lastException = ex;
                    
                    // Exponential backoff: delay increases with each attempt (500ms, 1000ms, 2000ms, etc.)
                    int delayMs = initialDelayMs * (int)Math.Pow(2, attempt);
                    await Task.Delay(delayMs);
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
                {
                    // Store exception for potential re-throw if all retries fail
                    lastException = ex;
                    
                    // Exponential backoff for access denied errors
                    int delayMs = initialDelayMs * (int)Math.Pow(2, attempt);
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    // For non-retryable exceptions or final attempt, propagate immediately
                    throw;
                }
            }
            
            // All retries exhausted - throw the last exception we encountered
            if (lastException != null)
            {
                throw lastException;
            }
            
            // This should never be reached, but included for safety
            throw new InvalidOperationException($"Operation failed after {maxRetries} attempts with no exception captured.");
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


