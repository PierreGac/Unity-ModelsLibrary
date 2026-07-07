using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for scanning the Unity project to detect installed models and check for updates.
    /// Manifest files are scanned once per refresh. GUID-based detection is retained as a
    /// bounded-concurrency fallback for projects created before manifests were introduced.
    /// </summary>
    public class ModelScanService
    {
        private const int MAX_CONCURRENT_LEGACY_LOOKUPS = 4;

        private readonly ModelIndexService _indexService;
        private readonly ModelMetadataService _metadataService;

        public ModelScanService(ModelIndexService indexService, ModelMetadataService metadataService)
        {
            _indexService = indexService;
            _metadataService = metadataService;
        }

        /// <summary>
        /// Scans current project assets to detect installed model versions and available updates.
        /// </summary>
        public async Task<List<(ModelIndex.Entry entry, bool hasUpdate, string localVersion)>> ScanProjectForKnownModelsAsync()
        {
            ModelIndex index = await _indexService.GetIndexAsync();
            int entryCount = index?.entries?.Count ?? 0;
            List<(ModelIndex.Entry, bool, string)> results =
                new List<(ModelIndex.Entry, bool, string)>(entryCount);

            if (entryCount == 0)
            {
                return results;
            }

            // PERF: The old implementation enumerated and read every manifest once per
            // repository entry. With 60 models and 10 manifests that meant 600 reads.
            // Build a single modelId -> localVersion map instead.
            Dictionary<string, string> manifestVersions = await LoadManifestVersionsAsync();

            List<ModelIndex.Entry> unresolvedEntries = new List<ModelIndex.Entry>();
            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    continue;
                }

                if (!manifestVersions.ContainsKey(entry.id))
                {
                    unresolvedEntries.Add(entry);
                }
            }

            Dictionary<string, string> legacyVersions = null;
            if (unresolvedEntries.Count > 0 && _metadataService != null)
            {
                // AssetDatabase must be queried on Unity's main thread. It is deliberately
                // delayed until a legacy fallback is actually needed.
                string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                HashSet<string> projectGuids = new HashSet<string>(allGuids, StringComparer.OrdinalIgnoreCase);
                legacyVersions = await ResolveLegacyVersionsAsync(unresolvedEntries, projectGuids);
            }

            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    continue;
                }

                string localVersion = null;
                if (!manifestVersions.TryGetValue(entry.id, out localVersion) && legacyVersions != null)
                {
                    legacyVersions.TryGetValue(entry.id, out localVersion);
                }

                bool hasUpdate = IsUpdateAvailable(localVersion, entry.latestVersion);
                results.Add((entry, hasUpdate, localVersion));
            }

            return results;
        }

        /// <summary>
        /// Discovers and parses all current/legacy manifest files exactly once.
        /// If multiple manifests exist for a model, the highest semantic version wins.
        /// </summary>
        private static async Task<Dictionary<string, string>> LoadManifestVersionsAsync()
        {
            Dictionary<string, string> versionsByModel =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            List<string> manifestPaths = await ManifestDiscoveryUtility.DiscoverAllManifestFilesAsync("Assets");
            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = await File.ReadAllTextAsync(manifestPath);
                    ModelMeta meta = JsonUtil.FromJson<ModelMeta>(json);
                    string modelId = meta?.identity?.id;
                    string version = meta?.version;

                    if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
                    {
                        continue;
                    }

                    if (!versionsByModel.TryGetValue(modelId, out string existingVersion)
                        || IsVersionNewer(version, existingVersion))
                    {
                        versionsByModel[modelId] = version;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModelScanService] Failed to read manifest '{manifestPath}': {ex.Message}");
                }
            }

            return versionsByModel;
        }

        /// <summary>
        /// Resolves projects without manifests by comparing repository asset GUIDs with
        /// GUIDs present in the current project. Work is bounded to avoid flooding a LAN
        /// repository or creating hundreds of simultaneous UnityWebRequests.
        /// </summary>
        private async Task<Dictionary<string, string>> ResolveLegacyVersionsAsync(
            List<ModelIndex.Entry> entries,
            HashSet<string> projectGuids)
        {
            Dictionary<string, string> resolved =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (entries == null || entries.Count == 0 || projectGuids == null || projectGuids.Count == 0)
            {
                return resolved;
            }

            using (SemaphoreSlim gate = new SemaphoreSlim(MAX_CONCURRENT_LEGACY_LOOKUPS))
            {
                Task<LegacyResolution>[] tasks = new Task<LegacyResolution>[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    tasks[i] = ResolveLegacyVersionAsync(entries[i], projectGuids, gate);
                }

                LegacyResolution[] resolutions = await Task.WhenAll(tasks);
                for (int i = 0; i < resolutions.Length; i++)
                {
                    LegacyResolution resolution = resolutions[i];
                    if (!string.IsNullOrEmpty(resolution.ModelId)
                        && !string.IsNullOrEmpty(resolution.Version))
                    {
                        resolved[resolution.ModelId] = resolution.Version;
                    }
                }
            }

            return resolved;
        }

        private async Task<LegacyResolution> ResolveLegacyVersionAsync(
            ModelIndex.Entry entry,
            HashSet<string> projectGuids,
            SemaphoreSlim gate)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
            {
                return default;
            }

            await gate.WaitAsync();
            try
            {
                string version = await FindLocalVersionByGuidsAsync(entry, projectGuids);
                return new LegacyResolution(entry.id, version);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Finds a local model version by matching repository metadata GUIDs against project assets.
        /// This is only a compatibility fallback when no manifest identifies the model.
        /// </summary>
        private async Task<string> FindLocalVersionByGuidsAsync(
            ModelIndex.Entry entry,
            HashSet<string> projectGuids)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || _metadataService == null)
            {
                return null;
            }

            try
            {
                List<string> versions = await _indexService.GetAvailableVersionsAsync(entry.id);

                // Ensure the index's latest version is attempted first even when the optional
                // versions map is incomplete.
                if (!string.IsNullOrEmpty(entry.latestVersion))
                {
                    int latestIndex = -1;
                    for (int i = 0; i < versions.Count; i++)
                    {
                        if (string.Equals(versions[i], entry.latestVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            latestIndex = i;
                            break;
                        }
                    }

                    if (latestIndex > 0)
                    {
                        versions.RemoveAt(latestIndex);
                        versions.Insert(0, entry.latestVersion);
                    }
                    else if (latestIndex < 0)
                    {
                        versions.Insert(0, entry.latestVersion);
                    }
                }

                for (int i = 0; i < versions.Count; i++)
                {
                    string version = versions[i];
                    if (string.IsNullOrEmpty(version))
                    {
                        continue;
                    }

                    try
                    {
                        ModelMeta meta = await _metadataService.GetMetaAsync(entry.id, version);
                        if (ContainsProjectGuid(meta?.assetGuids, projectGuids))
                        {
                            return version;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        // Repository indexes can temporarily reference a missing version.
                        // Continue with the remaining versions without spamming the Console.
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[ModelScanService] Failed to load metadata for {entry.id} version {version}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ModelScanService] Error finding a legacy local version for {entry.id}: {ex.Message}");
            }

            return null;
        }

        private static bool ContainsProjectGuid(List<string> assetGuids, HashSet<string> projectGuids)
        {
            if (assetGuids == null || assetGuids.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < assetGuids.Count; i++)
            {
                string guid = assetGuids[i];
                if (!string.IsNullOrEmpty(guid) && projectGuids.Contains(guid))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUpdateAvailable(string localVersion, string remoteVersion)
        {
            return !string.IsNullOrEmpty(localVersion)
                   && SemVer.TryParse(localVersion, out SemVer local)
                   && SemVer.TryParse(remoteVersion, out SemVer remote)
                   && remote.CompareTo(local) > 0;
        }

        private static bool IsVersionNewer(string candidate, string current)
        {
            bool candidateParsed = SemVer.TryParse(candidate, out SemVer candidateVersion);
            bool currentParsed = SemVer.TryParse(current, out SemVer currentVersion);

            if (candidateParsed && currentParsed)
            {
                return candidateVersion.CompareTo(currentVersion) > 0;
            }

            if (candidateParsed != currentParsed)
            {
                return candidateParsed;
            }

            return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private readonly struct LegacyResolution
        {
            public string ModelId { get; }
            public string Version { get; }

            public LegacyResolution(string modelId, string version)
            {
                ModelId = modelId;
                Version = version;
            }
        }
    }
}
