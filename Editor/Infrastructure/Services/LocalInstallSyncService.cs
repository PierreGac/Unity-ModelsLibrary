using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Result of a post-submit local install sync attempt.
    /// </summary>
    public sealed class LocalInstallSyncResult
    {
        /// <summary>
        /// True when the local install was updated (manifest sync or path migration).
        /// </summary>
        public bool Applied { get; set; }

        /// <summary>
        /// Machine-readable reason for the outcome.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Creates a result that did not modify the local install.
        /// </summary>
        /// <param name="reason">Skip reason.</param>
        /// <returns>Result with <see cref="Applied"/> false.</returns>
        public static LocalInstallSyncResult Skipped(string reason)
        {
            return new LocalInstallSyncResult { Applied = false, Reason = reason };
        }

        /// <summary>
        /// Creates a result that applied a local sync.
        /// </summary>
        /// <param name="reason">Applied reason.</param>
        /// <returns>Result with <see cref="Applied"/> true.</returns>
        public static LocalInstallSyncResult Succeeded(string reason)
        {
            return new LocalInstallSyncResult { Applied = true, Reason = reason };
        }
    }

    /// <summary>
    /// Syncs the local install after an update submit in the authoring project.
    /// Manifest-only when installPath is unchanged; path migration when it changed.
    /// </summary>
    public static class LocalInstallSyncService
    {
        private const string NEW_MANIFEST_NAME = ".modelLibrary.meta.json";

        /// <summary>Local install was not found in the project.</summary>
        public const string REASON_NOT_INSTALLED = "NotInstalled";

        /// <summary>Submitted asset GUIDs do not match the local install.</summary>
        public const string REASON_GUID_MISMATCH = "GuidMismatch";

        /// <summary>Invalid arguments were provided.</summary>
        public const string REASON_INVALID_ARGS = "InvalidArgs";

        /// <summary>Manifest was rewritten at the existing install path.</summary>
        public const string REASON_MANIFEST_SYNCED = "ManifestSynced";

        /// <summary>Install was migrated to a new install path.</summary>
        public const string REASON_PATH_MIGRATED = "PathMigrated";

        /// <summary>Path migration failed.</summary>
        public const string REASON_PATH_MIGRATION_FAILED = "PathMigrationFailed";

        /// <summary>
        /// Finds the locally installed model metadata for a model ID by scanning manifests.
        /// </summary>
        /// <param name="modelId">Model identity ID.</param>
        /// <returns>Local <see cref="ModelMeta"/>, or null when not found.</returns>
        public static ModelMeta TryFindLocalInstallMeta(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return null;
            }

            List<string> manifestPaths = ManifestDiscoveryUtility.DiscoverAllManifestFiles("Assets");
            ModelMeta bestMatch = null;
            SemVer bestVersion = default;
            bool hasBestVersion = false;

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    ModelMeta meta = JsonUtil.FromJson<ModelMeta>(json);
                    if (meta?.identity == null || string.IsNullOrEmpty(meta.identity.id))
                    {
                        continue;
                    }

                    if (!string.Equals(meta.identity.id, modelId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string folderPath = Path.GetDirectoryName(manifestPath);
                    string normalizedFolder = InstallPathUtils.NormalizeInstallPath(
                        PathUtils.SanitizePathSeparator(folderPath));
                    if (string.IsNullOrWhiteSpace(meta.installPath) && !string.IsNullOrWhiteSpace(normalizedFolder))
                    {
                        meta.installPath = normalizedFolder;
                    }

                    if (bestMatch == null)
                    {
                        bestMatch = meta;
                        hasBestVersion = SemVer.TryParse(meta.version, out bestVersion);
                        continue;
                    }

                    bool preferThis = PreferManifestCandidate(meta, bestMatch, hasBestVersion, bestVersion);
                    if (preferThis)
                    {
                        bestMatch = meta;
                        hasBestVersion = SemVer.TryParse(meta.version, out bestVersion);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LocalInstallSyncService] Failed to read manifest '{manifestPath}': {ex.Message}");
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Returns true when submitted payload GUIDs are contained in the local install
        /// and resolve to paths under the local install folder.
        /// </summary>
        /// <param name="submitted">Submitted model metadata.</param>
        /// <param name="local">Locally installed model metadata.</param>
        /// <returns>True when the submitted assets match the local install.</returns>
        public static bool SubmittedAssetsMatchLocalInstall(ModelMeta submitted, ModelMeta local)
        {
            if (submitted?.assetGuids == null || submitted.assetGuids.Count == 0)
            {
                return false;
            }

            if (local?.assetGuids == null || local.assetGuids.Count == 0)
            {
                return false;
            }

            string localInstallPath = InstallPathUtils.NormalizeInstallPath(local.installPath);
            if (string.IsNullOrWhiteSpace(localInstallPath))
            {
                return false;
            }

            HashSet<string> localGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < local.assetGuids.Count; i++)
            {
                string guid = local.assetGuids[i];
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    localGuids.Add(guid);
                }
            }

            for (int i = 0; i < submitted.assetGuids.Count; i++)
            {
                string guid = submitted.assetGuids[i];
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                if (!localGuids.Contains(guid))
                {
                    return false;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return false;
                }

                string sanitizedPath = PathUtils.SanitizePathSeparator(assetPath);
                if (!InstalledModelAssetCleaner.IsPathUnderInstallFolder(sanitizedPath, localInstallPath))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Syncs the local install after a successful update submit.
        /// </summary>
        /// <param name="submittedMeta">Metadata that was just submitted.</param>
        /// <param name="materializedVersionRoot">Temp folder containing the materialized version payload.</param>
        /// <param name="modelId">Model identity ID.</param>
        /// <returns>Sync result describing whether local install was updated.</returns>
        public static async Task<LocalInstallSyncResult> SyncAfterUpdateSubmitAsync(
            ModelMeta submittedMeta,
            string materializedVersionRoot,
            string modelId)
        {
            if (submittedMeta == null || string.IsNullOrWhiteSpace(modelId))
            {
                return LocalInstallSyncResult.Skipped(REASON_INVALID_ARGS);
            }

            ModelMeta localMeta = TryFindLocalInstallMeta(modelId);
            if (localMeta == null)
            {
                return LocalInstallSyncResult.Skipped(REASON_NOT_INSTALLED);
            }

            if (!SubmittedAssetsMatchLocalInstall(submittedMeta, localMeta))
            {
                return LocalInstallSyncResult.Skipped(REASON_GUID_MISMATCH);
            }

            string newPath = InstallPathUtils.NormalizeInstallPath(submittedMeta.installPath);
            string oldPath = InstallPathUtils.NormalizeInstallPath(localMeta.installPath);
            if (string.IsNullOrWhiteSpace(newPath))
            {
                return LocalInstallSyncResult.Skipped(REASON_INVALID_ARGS);
            }

            bool pathChanged = InstalledModelAssetCleaner.HasInstallPathChanged(oldPath, newPath);
            if (!pathChanged)
            {
                WriteLocalManifest(submittedMeta, newPath);
                Debug.Log($"[LocalInstallSyncService] Manifest synced at '{newPath}' to version {submittedMeta.version}.");
                return LocalInstallSyncResult.Succeeded(REASON_MANIFEST_SYNCED);
            }

            if (string.IsNullOrWhiteSpace(materializedVersionRoot) || !Directory.Exists(materializedVersionRoot))
            {
                Debug.LogWarning("[LocalInstallSyncService] Path migration skipped: materialized version root is missing.");
                return LocalInstallSyncResult.Skipped(REASON_PATH_MIGRATION_FAILED);
            }

            try
            {
                await ModelProjectImporter.ImportFromCacheAsync(
                    materializedVersionRoot,
                    submittedMeta,
                    cleanDestination: false,
                    overrideInstallPath: newPath,
                    isUpdate: true);

                InstalledModelAssetCleaner.DeleteAssetsFromManifest(localMeta);
                Debug.Log($"[LocalInstallSyncService] Path migrated from '{oldPath}' to '{newPath}' for version {submittedMeta.version}.");
                return LocalInstallSyncResult.Succeeded(REASON_PATH_MIGRATED);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalInstallSyncService] Path migration failed: {ex.Message}");
                return LocalInstallSyncResult.Skipped(REASON_PATH_MIGRATION_FAILED);
            }
        }

        /// <summary>
        /// Writes the model metadata as a local install manifest at the install path.
        /// </summary>
        /// <param name="meta">Metadata to write.</param>
        /// <param name="installPath">Project-relative install folder.</param>
        public static void WriteLocalManifest(ModelMeta meta, string installPath)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            string normalized = InstallPathUtils.NormalizeInstallPath(installPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Install path is required.", nameof(installPath));
            }

            string absoluteDir = Path.GetFullPath(normalized);
            if (!Directory.Exists(absoluteDir))
            {
                Directory.CreateDirectory(absoluteDir);
            }

            meta.installPath = normalized;
            string manifestPath = Path.Combine(absoluteDir, NEW_MANIFEST_NAME);
            File.WriteAllText(manifestPath, JsonUtil.ToJson(meta));
            AssetDatabase.Refresh();
        }

        private static bool PreferManifestCandidate(
            ModelMeta candidate,
            ModelMeta currentBest,
            bool hasBestVersion,
            SemVer bestVersion)
        {
            bool candidateHasGuids = candidate.assetGuids != null && candidate.assetGuids.Count > 0;
            bool bestHasGuids = currentBest.assetGuids != null && currentBest.assetGuids.Count > 0;
            if (candidateHasGuids && !bestHasGuids)
            {
                return true;
            }

            if (!candidateHasGuids && bestHasGuids)
            {
                return false;
            }

            if (SemVer.TryParse(candidate.version, out SemVer candidateVersion))
            {
                if (!hasBestVersion)
                {
                    return true;
                }

                return candidateVersion.CompareTo(bestVersion) > 0;
            }

            return false;
        }
    }
}
