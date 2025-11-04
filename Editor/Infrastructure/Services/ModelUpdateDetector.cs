using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for detecting model updates by comparing local and remote versions.
    /// Provides background update checking and visual indicators for available updates.
    /// </summary>
    public class ModelUpdateDetector
    {
        private readonly ModelLibraryService _service;
        private readonly Dictionary<string, ModelUpdateInfo> _updateCache = new Dictionary<string, ModelUpdateInfo>();
        private DateTime _lastUpdateCheck = DateTime.MinValue;
        private readonly TimeSpan _updateCheckInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
        private readonly object _cacheLock = new object();

        public ModelUpdateDetector(ModelLibraryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Information about a model update status.
        /// Contains both local and remote version information, update availability, and descriptive text.
        /// </summary>
        public class ModelUpdateInfo
        {
            /// <summary>The unique identifier of the model.</summary>
            public string modelId { get; set; }
            /// <summary>The display name of the model.</summary>
            public string modelName { get; set; }
            /// <summary>The version currently installed locally.</summary>
            public string localVersion { get; set; }
            /// <summary>The latest version available in the repository.</summary>
            public string remoteVersion { get; set; }
            /// <summary>Whether an update is available (remote version is newer than local).</summary>
            public bool hasUpdate { get; set; }
            /// <summary>Timestamp when this update information was last checked.</summary>
            public DateTime lastChecked { get; set; }
            /// <summary>Human-readable description of the update (from changelog or version comparison).</summary>
            public string updateDescription { get; set; }
        }

        /// <summary>
        /// Get all models with available updates.
        /// </summary>
        public async Task<List<ModelUpdateInfo>> GetAvailableUpdatesAsync()
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                return _updateCache.Values.Where(info => info.hasUpdate).ToList();
            }
        }

        /// <summary>
        /// Get update information for a specific model.
        /// </summary>
        public async Task<ModelUpdateInfo> GetUpdateInfoAsync(string modelId)
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                return _updateCache.TryGetValue(modelId, out ModelUpdateInfo info) ? info : null;
            }
        }

        /// <summary>
        /// Check if a specific model has updates available.
        /// </summary>
        public async Task<bool> HasUpdateAsync(string modelId)
        {
            ModelUpdateInfo info = await GetUpdateInfoAsync(modelId);
            return info?.hasUpdate ?? false;
        }

        /// <summary>
        /// Get the total number of available updates.
        /// </summary>
        public async Task<int> GetUpdateCountAsync()
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                return _updateCache.Values.Count(info => info.hasUpdate);
            }
        }

        /// <summary>
        /// Force a refresh of all update information.
        /// </summary>
        public async Task RefreshAllUpdatesAsync()
        {
            _lastUpdateCheck = DateTime.MinValue; // Force refresh
            await RefreshUpdateInfoAsync();
        }

        /// <summary>
        /// Clear the update cache.
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _updateCache.Clear();
            }
            _lastUpdateCheck = DateTime.MinValue;
        }

        /// <summary>
        /// Refreshes update information for all models in the index.
        /// Uses caching to avoid excessive checks - only refreshes if cache is expired or empty.
        /// Skips update checks for models that are not installed locally.
        /// </summary>
        private async Task RefreshUpdateInfoAsync()
        {
            // Check if we need to refresh based on time interval
            if (DateTime.Now - _lastUpdateCheck < _updateCheckInterval)
            {
                lock (_cacheLock)
                {
                    if (_updateCache.Count > 0)
                    {
                        return; // Too soon to refresh and we have cached data
                    }
                }
            }

            try
            {
                // Get all models with their update status
                List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)> scanResults =
                    await _service.ScanProjectForKnownModelsAsync();

                // Process each model
                Dictionary<string, ModelUpdateInfo> newCache = new Dictionary<string, ModelUpdateInfo>();
                foreach ((ModelIndex.Entry entry, bool hasUpdate, string localVersion) in scanResults)
                {
                    // Only show updates for models that are actually installed locally
                    if (!string.IsNullOrEmpty(localVersion))
                    {
                        ModelUpdateInfo updateInfo = new ModelUpdateInfo
                        {
                            modelId = entry.id,
                            modelName = entry.name,
                            localVersion = localVersion,
                            remoteVersion = entry.latestVersion,
                            hasUpdate = hasUpdate,
                            lastChecked = DateTime.Now,
                            updateDescription = await GetUpdateDescriptionAsync(entry.id, localVersion, entry.latestVersion)
                        };

                        newCache[entry.id] = updateInfo;
                    }
                    else
                    {
                        // Model not installed locally - don't show as having updates
                        Debug.Log($"[ModelUpdateDetector] Model {entry.name} not installed locally, skipping update check");
                    }
                }

                // Update cache atomically
                lock (_cacheLock)
                {
                    _updateCache.Clear();
                    foreach (KeyValuePair<string, ModelUpdateInfo> kvp in newCache)
                    {
                        _updateCache[kvp.Key] = kvp.Value;
                    }
                }

                _lastUpdateCheck = DateTime.Now;
                Debug.Log($"[ModelUpdateDetector] Refreshed update info for {newCache.Count} models, {newCache.Values.Count(v => v.hasUpdate)} have updates");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelUpdateDetector] Error refreshing update info: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a human-readable description of an available update.
        /// Attempts to extract the changelog summary from the latest version metadata.
        /// Falls back to a version comparison description if changelog is not available.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="localVersion">The currently installed local version.</param>
        /// <param name="remoteVersion">The latest available remote version.</param>
        /// <returns>A descriptive string about the update, or a generic version comparison message.</returns>
        private async Task<string> GetUpdateDescriptionAsync(string modelId, string localVersion, string remoteVersion)
        {
            try
            {
                // Try to get the changelog for the latest version
                ModelMeta latestMeta = await _service.GetMetaAsync(modelId, remoteVersion);
                if (latestMeta?.changelog != null && latestMeta.changelog.Count > 0)
                {
                    // Get the most recent changelog entry
                    ModelChangelogEntry latestEntry = latestMeta.changelog
                        .OrderByDescending(e => e.timestamp)
                        .FirstOrDefault();

                    if (latestEntry != null)
                    {
                        return latestEntry.summary;
                    }
                }

                // Fallback to version comparison
                if (SemVer.TryParse(localVersion, out SemVer local) && SemVer.TryParse(remoteVersion, out SemVer remote))
                {
                    if (remote.major > local.major)
                    {
                        return $"Major update available: {localVersion} → {remoteVersion}";
                    }
                    else if (remote.minor > local.minor)
                    {
                        return $"Minor update available: {localVersion} → {remoteVersion}";
                    }
                    else if (remote.patch > local.patch)
                    {
                        return $"Patch update available: {localVersion} → {remoteVersion}";
                    }
                }

                return $"Update available: {localVersion} → {remoteVersion}";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModelUpdateDetector] Error getting update description for {modelId}: {ex.Message}");
                return $"Update available: {localVersion} → {remoteVersion}";
            }
        }
    }
}
