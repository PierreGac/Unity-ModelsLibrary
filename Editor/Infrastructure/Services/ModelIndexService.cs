using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for managing model index operations.
    /// Handles loading, caching, and refreshing the global model index.
    /// </summary>
    internal class ModelIndexService
    {
        private const int MAX_INDEX_SAVE_ATTEMPTS = 5;
        private const string INDEX_SAVE_CONFLICT_MESSAGE = "The model index changed repeatedly and the update was not saved.";

        private readonly IModelRepository _repo;
        private ModelIndex _indexCache;

        /// <summary>
        /// Invoked after an edit is applied and before the revision check.
        /// Tests use this to land another save in that window. Production leaves it unset.
        /// </summary>
        internal static Action BeforeIndexSaveAttempt;

        public ModelIndexService(IModelRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Get the cached index, loading it from repository if needed.
        /// </summary>
        public async Task<ModelIndex> GetIndexAsync()
        {
            return await AsyncProfiler.MeasureAsync("Service.GetIndex", async () =>
            {
                _indexCache ??= await _repo.LoadIndexAsync();
                return _indexCache;
            });
        }

        /// <summary>
        /// Force refresh of the index cache from the repository.
        /// </summary>
        public async Task RefreshIndexAsync()
        {
            _indexCache = await AsyncProfiler.MeasureAsync("Service.RefreshIndex", () => _repo.LoadIndexAsync());
        }

        /// <summary>
        /// Invalidates the index cache, forcing a reload on next access.
        /// </summary>
        public void InvalidateCache()
        {
            _indexCache = null;
        }

        /// <summary>
        /// Enumerates all available versions for a specific model by inspecting repository contents.
        /// Returns versions sorted in descending semantic order (latest first).
        /// </summary>
        /// <param name="modelId">Model identifier.</param>
        /// <returns>List of available version strings.</returns>
        public async Task<List<string>> GetAvailableVersionsAsync(string modelId)
        {
            HashSet<string> versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                ModelIndex index = await GetIndexAsync();
                if (index?.versions != null && index.versions.TryGetValue(modelId, out List<string> knownVersions))
                {
                    for (int i = 0; i < knownVersions.Count; i++)
                    {
                        versions.Add(knownVersions[i]);
                    }
                }

                List<string> paths = await _repo.ListFilesAsync(modelId);
                string prefix = PathUtils.SanitizePathSeparator(modelId + "/");

                for (int i = 0; i < paths.Count; i++)
                {
                    string sanitized = PathUtils.SanitizePathSeparator(paths[i]);
                    if (!sanitized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string remainder = sanitized.Substring(prefix.Length);
                    int slashIndex = remainder.IndexOf('/');

                    if (slashIndex <= 0)
                    {
                        continue;
                    }

                    string versionCandidate = remainder.Substring(0, slashIndex).Trim();
                    if (!string.IsNullOrEmpty(versionCandidate))
                    {
                        versions.Add(versionCandidate);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Enumerate Versions Failed",
                    $"Failed to enumerate versions for {modelId}: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}");
            }

            List<string> sorted = versions.ToList();

            sorted.Sort((a, b) =>
            {
                bool leftParsed = SemVer.TryParse(a, out SemVer left);
                bool rightParsed = SemVer.TryParse(b, out SemVer right);

                if (leftParsed && rightParsed)
                {
                    return right.CompareTo(left); // descending order
                }

                if (leftParsed)
                {
                    return -1;
                }

                if (rightParsed)
                {
                    return 1;
                }

                return string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
            });

            if (sorted.Count == 0)
            {
                ModelIndex index = await GetIndexAsync();
                string latest = index?.Get(modelId)?.latestVersion;
                if (!string.IsNullOrEmpty(latest))
                {
                    sorted.Add(latest);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Updates the global model index with the latest metadata from a model version.
        /// Creates a new index entry if the model doesn't exist, or updates the existing entry.
        /// Only updates the latest version if the new version is greater than or equal to the existing latest version.
        /// Updates the cached index to reflect changes immediately.
        /// </summary>
        /// <param name="meta">Model metadata containing the latest version information.</param>
        public Task UpdateIndexWithLatestMetaAsync(ModelMeta meta)
        {
            if (meta == null || meta.identity == null || string.IsNullOrWhiteSpace(meta.identity.id))
            {
                Debug.LogWarning("[ModelIndexService] Cannot update index: metadata or identity is null or invalid.");
                return Task.CompletedTask;
            }

            return SaveApplyingAsync(index =>
            {
                ApplyLatestMeta(index, meta);
                return true;
            });
        }

        /// <summary>
        /// Removes one model from the index.
        /// Reloads and retries when another save changed the revision first.
        /// </summary>
        /// <param name="modelId">Model id to remove.</param>
        /// <returns>True when an entry was removed and the index was saved.</returns>
        public async Task<bool> RemoveFromIndexAsync(string modelId)
        {
            bool removed = false;
            await SaveApplyingAsync(index =>
            {
                removed = RemoveEntry(index, modelId);
                return removed;
            });
            return removed;
        }

        private async Task SaveApplyingAsync(Func<ModelIndex, bool> apply)
        {
            for (int attempt = 0; attempt < MAX_INDEX_SAVE_ATTEMPTS; attempt++)
            {
                ModelIndex index = await _repo.LoadIndexAsync();
                if (index == null)
                {
                    index = new ModelIndex();
                }

                if (index.entries == null)
                {
                    index.entries = new List<ModelIndex.Entry>();
                }

                long seenRevision = index.revision;
                bool shouldSave = apply(index);
                if (!shouldSave)
                {
                    _indexCache = index;
                    return;
                }

                index.revision = seenRevision + 1;
                Action beforeSave = BeforeIndexSaveAttempt;
                if (beforeSave != null)
                {
                    beforeSave();
                }

                bool saved = await _repo.TrySaveIndexIfUnchangedAsync(index, seenRevision);
                if (saved)
                {
                    _indexCache = index;
                    return;
                }
            }

            throw new IOException(INDEX_SAVE_CONFLICT_MESSAGE);
        }

        private static void ApplyLatestMeta(ModelIndex index, ModelMeta meta)
        {
            ModelIndex.Entry entry = index.Get(meta.identity.id);
            if (entry == null)
            {
                index.entries.Add(ModelIndexEntryFactory.FromMeta(meta));
                return;
            }

            bool shouldUpdate = false;
            if (SemVer.TryParse(meta.version, out SemVer vNew) && SemVer.TryParse(entry.latestVersion, out SemVer vOld))
            {
                shouldUpdate = vNew.CompareTo(vOld) >= 0;
            }
            else
            {
                shouldUpdate = true;
            }

            if (!shouldUpdate)
            {
                return;
            }

            ModelIndex.Entry updated = ModelIndexEntryFactory.FromMeta(meta);
            entry.latestVersion = updated.latestVersion;
            entry.name = updated.name;
            entry.description = updated.description;
            entry.updatedTimeTicks = updated.updatedTimeTicks;
            entry.tags = updated.tags;
        }

        private static bool RemoveEntry(ModelIndex index, string modelId)
        {
            if (index.entries == null)
            {
                return false;
            }

            for (int i = index.entries.Count - 1; i >= 0; i--)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry != null && string.Equals(entry.id, modelId, StringComparison.Ordinal))
                {
                    index.entries.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
