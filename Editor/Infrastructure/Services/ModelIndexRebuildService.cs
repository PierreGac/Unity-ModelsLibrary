using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Rebuilds models_index.json by scanning modelId/version folders and loading model.json metadata.
    /// </summary>
    public class ModelIndexRebuildService
    {
        private const string MODELS_INDEX_FILE_NAME = "models_index.json";
        private const string BACKUP_FILE_PREFIX = "models_index.json.bak-";

        /// <summary>
        /// Scans the repository and returns a preview report without writing the index.
        /// </summary>
        /// <param name="repo">File system repository to scan.</param>
        /// <returns>Rebuild report with discovered counts and any warnings.</returns>
        public async Task<ModelIndexRebuildReport> PreviewAsync(FileSystemRepository repo)
        {
            ModelIndexRebuildReport report = new ModelIndexRebuildReport { isPreview = true };
            await ScanAndBuildIndexAsync(repo, report, writeIndex: false, createBackup: false);
            return report;
        }

        /// <summary>
        /// Scans the repository, optionally backs up the existing index, and writes a new models_index.json.
        /// </summary>
        /// <param name="repo">File system repository to scan.</param>
        /// <param name="createBackup">When true, copies the existing index before overwrite.</param>
        /// <returns>Rebuild report including backup path when applicable.</returns>
        public async Task<ModelIndexRebuildReport> RebuildAsync(FileSystemRepository repo, bool createBackup)
        {
            ModelIndexRebuildReport report = new ModelIndexRebuildReport { isPreview = false };
            await ScanAndBuildIndexAsync(repo, report, writeIndex: true, createBackup: createBackup);
            return report;
        }

        private static async Task ScanAndBuildIndexAsync(
            FileSystemRepository repo,
            ModelIndexRebuildReport report,
            bool writeIndex,
            bool createBackup)
        {
            if (repo == null)
            {
                report.errors.Add("Repository is null.");
                report.success = false;
                return;
            }

            string root = repo.Root?.Trim();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                report.errors.Add($"Repository root is missing or inaccessible: {root ?? "(null)"}");
                report.success = false;
                return;
            }

            try
            {
                ModelIndex existingIndex = await repo.LoadIndexAsync();
                report.previousIndexEntryCount = existingIndex?.entries != null ? existingIndex.entries.Count : 0;

                List<ModelIndex.Entry> entries = await BuildEntriesFromRepositoryAsync(repo, root, report);
                report.discoveredModelFolderCount = entries.Count;
                report.indexEntryCount = entries.Count;

                if (!writeIndex)
                {
                    report.success = report.errors.Count == 0;
                    return;
                }

                string indexPath = Path.Combine(root, MODELS_INDEX_FILE_NAME);
                if (createBackup && File.Exists(indexPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    string backupPath = Path.Combine(root, BACKUP_FILE_PREFIX + timestamp);
                    File.Copy(indexPath, backupPath, overwrite: false);
                    report.backupPath = backupPath;
                }

                ModelIndex newIndex = new ModelIndex { entries = entries };
                await repo.SaveIndexAsync(newIndex);
                report.success = report.errors.Count == 0;
            }
            catch (Exception ex)
            {
                report.errors.Add($"Rebuild failed: {ex.Message}");
                report.success = false;
                Debug.LogError($"[ModelIndexRebuildService] Rebuild failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static async Task<List<ModelIndex.Entry>> BuildEntriesFromRepositoryAsync(
            FileSystemRepository repo,
            string root,
            ModelIndexRebuildReport report)
        {
            List<ModelIndex.Entry> entries = new List<ModelIndex.Entry>();
            string[] modelDirs = Directory.GetDirectories(root);

            for (int i = 0; i < modelDirs.Length; i++)
            {
                string modelDir = modelDirs[i];
                string folderName = Path.GetFileName(modelDir);
                if (string.IsNullOrEmpty(folderName))
                {
                    continue;
                }

                if (string.Equals(folderName, MODELS_INDEX_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ModelMeta latestMeta = await FindLatestMetaInModelFolderAsync(repo, folderName, modelDir, report);
                if (latestMeta == null)
                {
                    report.skippedFolders.Add(folderName);
                    continue;
                }

                if (latestMeta.identity != null
                    && !string.IsNullOrWhiteSpace(latestMeta.identity.id)
                    && !string.Equals(latestMeta.identity.id, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    report.warnings.Add(
                        $"Folder '{folderName}' has model.json identity id '{latestMeta.identity.id}'; index uses metadata identity.");
                }

                try
                {
                    entries.Add(ModelIndexEntryFactory.FromMeta(latestMeta));
                }
                catch (Exception ex)
                {
                    report.errors.Add($"Failed to build index entry for folder '{folderName}': {ex.Message}");
                }
            }

            return entries;
        }

        private static async Task<ModelMeta> FindLatestMetaInModelFolderAsync(
            FileSystemRepository repo,
            string folderName,
            string modelDirPath,
            ModelIndexRebuildReport report)
        {
            string[] versionDirs = Directory.GetDirectories(modelDirPath);
            ModelMeta bestMeta = null;
            string bestVersion = null;

            for (int v = 0; v < versionDirs.Length; v++)
            {
                string versionDir = versionDirs[v];
                string version = Path.GetFileName(versionDir);
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                string metaPath = Path.Combine(versionDir, ModelMeta.MODEL_JSON);
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                try
                {
                    ModelMeta meta = await repo.LoadMetaAsync(folderName, version);
                    if (meta == null || meta.identity == null || string.IsNullOrWhiteSpace(meta.identity.id))
                    {
                        report.warnings.Add($"Skipped {folderName}/{version}: missing or invalid identity in model.json.");
                        continue;
                    }

                    if (IsNewerVersion(version, bestVersion))
                    {
                        bestVersion = version;
                        bestMeta = meta;
                    }
                }
                catch (Exception ex)
                {
                    report.errors.Add($"Failed to load {folderName}/{version}/model.json: {ex.Message}");
                }
            }

            return bestMeta;
        }

        private static bool IsNewerVersion(string candidate, string currentBest)
        {
            if (string.IsNullOrEmpty(currentBest))
            {
                return true;
            }

            bool candidateParsed = SemVer.TryParse(candidate, out SemVer candidateSemVer);
            bool currentParsed = SemVer.TryParse(currentBest, out SemVer currentSemVer);

            if (candidateParsed && currentParsed)
            {
                return candidateSemVer.CompareTo(currentSemVer) > 0;
            }

            if (candidateParsed)
            {
                return true;
            }

            if (currentParsed)
            {
                return false;
            }

            return string.Compare(candidate, currentBest, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
