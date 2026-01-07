using System;
using System.Collections.Generic;
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
    public class ModelIndexService
    {
        private readonly IModelRepository _repo;
        private ModelIndex _indexCache;

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
        public async Task UpdateIndexWithLatestMetaAsync(ModelMeta meta)
        {
            if (meta == null || meta.identity == null || string.IsNullOrWhiteSpace(meta.identity.id))
            {
                Debug.LogWarning("[ModelIndexService] Cannot update index: metadata or identity is null or invalid.");
                return;
            }

            ModelIndex index = await GetIndexAsync();
            if (index == null)
            {
                index = new ModelIndex();
                _indexCache = index;
            }

            ModelIndex.Entry entry = index.Get(meta.identity.id);
            if (entry == null)
            {
                entry = new ModelIndex.Entry
                {
                    id = meta.identity.id,
                    name = meta.identity.name ?? string.Empty,
                    latestVersion = meta.version ?? string.Empty,
                    description = meta.description ?? string.Empty,
                    tags = meta.tags?.values != null ? new List<string>(meta.tags.values) : new List<string>(),
                    updatedTimeTicks = meta.updatedTimeTicks > 0 ? meta.updatedTimeTicks : DateTime.Now.Ticks,
                    releaseTimeTicks = meta.uploadTimeTicks > 0 ? meta.uploadTimeTicks : meta.createdTimeTicks > 0 ? meta.createdTimeTicks : DateTime.Now.Ticks
                };
                index.entries.Add(entry);
            }
            else
            {
                // Update latest version if the new version is >= the old version
                bool shouldUpdate = false;
                if (SemVer.TryParse(meta.version, out SemVer vNew) && SemVer.TryParse(entry.latestVersion, out SemVer vOld))
                {
                    shouldUpdate = vNew.CompareTo(vOld) >= 0;
                }
                else
                {
                    // If version parsing fails, update anyway (fallback behavior)
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    entry.latestVersion = meta.version ?? entry.latestVersion;
                    entry.name = meta.identity.name ?? entry.name;
                    entry.description = meta.description ?? entry.description;
                    entry.updatedTimeTicks = meta.updatedTimeTicks > 0 ? meta.updatedTimeTicks : DateTime.Now.Ticks;
                    if (meta.tags?.values != null)
                    {
                        entry.tags = new List<string>(meta.tags.values);
                    }
                }
            }

            // Save the updated index to the repository
            await AsyncProfiler.MeasureAsync("Service.SaveIndex", async () => await _repo.SaveIndexAsync(index));
            
            // Update cache to reflect changes
            _indexCache = index;
        }
    }
}

