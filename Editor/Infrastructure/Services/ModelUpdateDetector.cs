using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Detects model updates by comparing locally installed versions with the repository index.
    /// Refreshes are cached and concurrent callers share the same in-flight task.
    /// </summary>
    public class ModelUpdateDetector
    {
        private readonly ModelLibraryService _service;
        private readonly Dictionary<string, ModelUpdateInfo> _updateCache =
            new Dictionary<string, ModelUpdateInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _updateCheckInterval = TimeSpan.FromMinutes(5);
        private readonly object _cacheLock = new object();

        private DateTime _lastUpdateCheckUtc = DateTime.MinValue;
        private bool _hasCompletedRefresh;
        private Task _refreshTask;

        public ModelUpdateDetector(ModelLibraryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Information about a model update status.
        /// </summary>
        public class ModelUpdateInfo
        {
            public string modelId { get; set; }
            public string modelName { get; set; }
            public string localVersion { get; set; }
            public string remoteVersion { get; set; }
            public bool hasUpdate { get; set; }
            public DateTime lastChecked { get; set; }
            public string updateDescription { get; set; }
        }

        /// <summary>
        /// Gets all models with available updates.
        /// </summary>
        public async Task<List<ModelUpdateInfo>> GetAvailableUpdatesAsync()
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                List<ModelUpdateInfo> updates = new List<ModelUpdateInfo>();
                foreach (ModelUpdateInfo info in _updateCache.Values)
                {
                    if (info.hasUpdate)
                    {
                        updates.Add(info);
                    }
                }
                return updates;
            }
        }

        /// <summary>
        /// Gets a stable copy of the complete update cache after one refresh.
        /// This avoids N calls to GetUpdateInfoAsync from the browser window.
        /// </summary>
        public async Task<Dictionary<string, ModelUpdateInfo>> GetUpdateSnapshotAsync()
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                return new Dictionary<string, ModelUpdateInfo>(_updateCache, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Gets update information for a specific model.
        /// </summary>
        public async Task<ModelUpdateInfo> GetUpdateInfoAsync(string modelId)
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                return !string.IsNullOrEmpty(modelId)
                       && _updateCache.TryGetValue(modelId, out ModelUpdateInfo info)
                    ? info
                    : null;
            }
        }

        public async Task<bool> HasUpdateAsync(string modelId)
        {
            ModelUpdateInfo info = await GetUpdateInfoAsync(modelId);
            return info?.hasUpdate ?? false;
        }

        public async Task<int> GetUpdateCountAsync()
        {
            await RefreshUpdateInfoAsync();
            lock (_cacheLock)
            {
                int count = 0;
                foreach (ModelUpdateInfo info in _updateCache.Values)
                {
                    if (info.hasUpdate)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Forces the next caller to refresh. An already running refresh is shared rather than duplicated.
        /// </summary>
        public async Task RefreshAllUpdatesAsync()
        {
            lock (_cacheLock)
            {
                _lastUpdateCheckUtc = DateTime.MinValue;
                _hasCompletedRefresh = false;
            }
            await RefreshUpdateInfoAsync();
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _updateCache.Clear();
                _lastUpdateCheckUtc = DateTime.MinValue;
                _hasCompletedRefresh = false;
            }
        }

        /// <summary>
        /// Returns a cached result when fresh and coalesces concurrent refresh requests.
        /// A completed refresh is valid even when zero models are installed; the previous
        /// implementation treated an empty cache as stale and rescanned once per model.
        /// </summary>
        private Task RefreshUpdateInfoAsync()
        {
            lock (_cacheLock)
            {
                bool cacheIsFresh = _hasCompletedRefresh
                                    && DateTime.UtcNow - _lastUpdateCheckUtc < _updateCheckInterval;
                if (cacheIsFresh)
                {
                    return Task.CompletedTask;
                }

                if (_refreshTask != null && !_refreshTask.IsCompleted)
                {
                    return _refreshTask;
                }

                _refreshTask = RefreshUpdateInfoCoreAsync();
                return _refreshTask;
            }
        }

        private async Task RefreshUpdateInfoCoreAsync()
        {
            try
            {
                List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)> scanResults =
                    await _service.ScanProjectForKnownModelsAsync();

                Dictionary<string, ModelUpdateInfo> newCache =
                    new Dictionary<string, ModelUpdateInfo>(StringComparer.OrdinalIgnoreCase);
                DateTime checkedAt = DateTime.UtcNow;

                for (int i = 0; i < scanResults.Count; i++)
                {
                    (ModelIndex.Entry entry, bool hasUpdate, string localVersion) = scanResults[i];
                    if (entry == null || string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(localVersion))
                    {
                        continue;
                    }

                    // Reading the latest model.json solely for a changelog is unnecessary
                    // when the installed model is already current.
                    string description = hasUpdate
                        ? await GetUpdateDescriptionAsync(entry.id, localVersion, entry.latestVersion)
                        : null;

                    newCache[entry.id] = new ModelUpdateInfo
                    {
                        modelId = entry.id,
                        modelName = entry.name,
                        localVersion = localVersion,
                        remoteVersion = entry.latestVersion,
                        hasUpdate = hasUpdate,
                        lastChecked = checkedAt,
                        updateDescription = description
                    };
                }

                lock (_cacheLock)
                {
                    _updateCache.Clear();
                    foreach (KeyValuePair<string, ModelUpdateInfo> pair in newCache)
                    {
                        _updateCache[pair.Key] = pair.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelUpdateDetector] Error refreshing update info: {ex.Message}");
            }
            finally
            {
                lock (_cacheLock)
                {
                    // Back off consistently after both success and failure.
                    _lastUpdateCheckUtc = DateTime.UtcNow;
                    _hasCompletedRefresh = true;
                    _refreshTask = null;
                }
            }
        }

        private async Task<string> GetUpdateDescriptionAsync(
            string modelId,
            string localVersion,
            string remoteVersion)
        {
            try
            {
                ModelMeta latestMeta = await _service.GetMetaAsync(modelId, remoteVersion);
                if (latestMeta?.changelog != null && latestMeta.changelog.Count > 0)
                {
                    ModelChangelogEntry latestEntry = null;
                    for (int i = 0; i < latestMeta.changelog.Count; i++)
                    {
                        ModelChangelogEntry candidate = latestMeta.changelog[i];
                        if (candidate != null
                            && (latestEntry == null || candidate.timestamp > latestEntry.timestamp))
                        {
                            latestEntry = candidate;
                        }
                    }

                    if (!string.IsNullOrEmpty(latestEntry?.summary))
                    {
                        return latestEntry.summary;
                    }
                }

                return BuildVersionDescription(localVersion, remoteVersion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ModelUpdateDetector] Error getting update description for {modelId}: {ex.Message}");
                return BuildVersionDescription(localVersion, remoteVersion);
            }
        }

        private static string BuildVersionDescription(string localVersion, string remoteVersion)
        {
            if (SemVer.TryParse(localVersion, out SemVer local)
                && SemVer.TryParse(remoteVersion, out SemVer remote))
            {
                if (remote.major > local.major)
                {
                    return $"Major update available: {localVersion} → {remoteVersion}";
                }
                if (remote.minor > local.minor)
                {
                    return $"Minor update available: {localVersion} → {remoteVersion}";
                }
                if (remote.patch > local.patch)
                {
                    return $"Patch update available: {localVersion} → {remoteVersion}";
                }
            }

            return $"Update available: {localVersion} → {remoteVersion}";
        }
    }
}
