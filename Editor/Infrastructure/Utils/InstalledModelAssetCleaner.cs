using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Deletes project assets that belong to a previously installed model version,
    /// using only paths and GUIDs listed in that version's local manifest.
    /// Does not recursively wipe the install folder.
    /// </summary>
    public static class InstalledModelAssetCleaner
    {
        private const string NEW_MANIFEST_NAME = ".modelLibrary.meta.json";
        private const string OLD_MANIFEST_NAME = "modelLibrary.meta.json";

        /// <summary>
        /// Deletes assets listed in <paramref name="oldLocalMeta"/> under its <see cref="ModelMeta.installPath"/>.
        /// Only deletes paths that resolve under the old install folder; unrelated files are left alone.
        /// </summary>
        /// <param name="oldLocalMeta">Locally installed model metadata (source of truth for what to delete).</param>
        /// <returns>Number of assets successfully deleted via <see cref="AssetDatabase.DeleteAsset"/>.</returns>
        public static int DeleteAssetsFromManifest(ModelMeta oldLocalMeta)
        {
            if (oldLocalMeta == null)
            {
                return 0;
            }

            string installPath = InstallPathUtils.NormalizeInstallPath(oldLocalMeta.installPath);
            if (string.IsNullOrWhiteSpace(installPath))
            {
                Debug.LogWarning("[InstalledModelAssetCleaner] Cannot delete assets: old manifest has no installPath.");
                return 0;
            }

            HashSet<string> pathsToDelete = CollectProjectPathsToDelete(oldLocalMeta, installPath);
            int deletedCount = 0;

            List<string> orderedPaths = new List<string>(pathsToDelete);
            orderedPaths.Sort(ComparePathsDeepestFirst);

            for (int i = 0; i < orderedPaths.Count; i++)
            {
                string assetPath = orderedPaths[i];
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                if (!AssetExistsAtPath(assetPath))
                {
                    continue;
                }

                try
                {
                    if (AssetDatabase.DeleteAsset(assetPath))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[InstalledModelAssetCleaner] Failed to delete asset: {assetPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InstalledModelAssetCleaner] Exception deleting '{assetPath}': {ex.Message}");
                }
            }

            TryRemoveEmptyInstallFolder(installPath);

            Debug.Log($"[InstalledModelAssetCleaner] Deleted {deletedCount} asset(s) from old install path '{installPath}'.");
            return deletedCount;
        }

        /// <summary>
        /// Loads the local install manifest from an install folder, if present.
        /// </summary>
        /// <param name="installPath">Project-relative install path (e.g. Assets/Models/Ship).</param>
        /// <returns>Parsed <see cref="ModelMeta"/>, or null when no readable manifest exists.</returns>
        public static ModelMeta TryLoadLocalManifest(string installPath)
        {
            string normalized = InstallPathUtils.NormalizeInstallPath(installPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            string newManifestPath = Path.Combine(normalized, NEW_MANIFEST_NAME);
            string oldManifestPath = Path.Combine(normalized, OLD_MANIFEST_NAME);
            string manifestPath = File.Exists(newManifestPath)
                ? newManifestPath
                : (File.Exists(oldManifestPath) ? oldManifestPath : null);

            if (manifestPath == null)
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                ModelMeta meta = JsonUtil.FromJson<ModelMeta>(json);
                if (meta != null && string.IsNullOrWhiteSpace(meta.installPath))
                {
                    meta.installPath = normalized;
                }

                return meta;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InstalledModelAssetCleaner] Failed to load local manifest at '{manifestPath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns true when two install paths refer to different folders after normalization.
        /// </summary>
        /// <param name="oldInstallPath">Previous install path.</param>
        /// <param name="newInstallPath">New install path.</param>
        /// <returns>True when both paths are non-empty and differ (case-insensitive).</returns>
        public static bool HasInstallPathChanged(string oldInstallPath, string newInstallPath)
        {
            string oldNormalized = InstallPathUtils.NormalizeInstallPath(oldInstallPath);
            string newNormalized = InstallPathUtils.NormalizeInstallPath(newInstallPath);
            if (string.IsNullOrWhiteSpace(oldNormalized) || string.IsNullOrWhiteSpace(newNormalized))
            {
                return false;
            }

            return !string.Equals(oldNormalized, newNormalized, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Collects project-relative asset paths that should be deleted for the given local manifest.
        /// Exposed for unit testing without requiring AssetDatabase deletes.
        /// </summary>
        /// <param name="oldLocalMeta">Old local model metadata.</param>
        /// <param name="installPath">Normalized old install path.</param>
        /// <returns>Set of project-relative paths under the install folder.</returns>
        public static HashSet<string> CollectProjectPathsToDelete(ModelMeta oldLocalMeta, string installPath)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (oldLocalMeta == null || string.IsNullOrWhiteSpace(installPath))
            {
                return paths;
            }

            string normalizedInstall = InstallPathUtils.NormalizeInstallPath(installPath) ?? installPath;

            if (oldLocalMeta.payloadRelativePaths != null)
            {
                for (int i = 0; i < oldLocalMeta.payloadRelativePaths.Count; i++)
                {
                    string payloadRel = oldLocalMeta.payloadRelativePaths[i];
                    if (string.IsNullOrWhiteSpace(payloadRel))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(payloadRel);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    TryAddPathUnderInstall(paths, normalizedInstall, PathUtils.SanitizePathSeparator($"{normalizedInstall}/{fileName}"));
                }
            }

            if (oldLocalMeta.imageRelativePaths != null)
            {
                for (int i = 0; i < oldLocalMeta.imageRelativePaths.Count; i++)
                {
                    string imageRel = oldLocalMeta.imageRelativePaths[i];
                    if (string.IsNullOrWhiteSpace(imageRel))
                    {
                        continue;
                    }

                    string sanitizedImageRel = PathUtils.SanitizePathSeparator(imageRel).TrimStart('/');
                    TryAddPathUnderInstall(paths, normalizedInstall, PathUtils.SanitizePathSeparator($"{normalizedInstall}/{sanitizedImageRel}"));
                }
            }

            AddGuidPathsUnderInstall(paths, normalizedInstall, oldLocalMeta.assetGuids);
            AddGuidPathsUnderInstall(paths, normalizedInstall, oldLocalMeta.dependencies);

            TryAddPathUnderInstall(paths, normalizedInstall, PathUtils.SanitizePathSeparator($"{normalizedInstall}/{NEW_MANIFEST_NAME}"));
            TryAddPathUnderInstall(paths, normalizedInstall, PathUtils.SanitizePathSeparator($"{normalizedInstall}/{OLD_MANIFEST_NAME}"));

            return paths;
        }

        private static void AddGuidPathsUnderInstall(HashSet<string> paths, string installPath, List<string> guids)
        {
            if (guids == null || guids.Count == 0)
            {
                return;
            }

            for (int i = 0; i < guids.Count; i++)
            {
                string guid = guids[i];
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                TryAddPathUnderInstall(paths, installPath, PathUtils.SanitizePathSeparator(assetPath));
            }
        }

        private static void TryAddPathUnderInstall(HashSet<string> paths, string installPath, string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return;
            }

            string sanitized = PathUtils.SanitizePathSeparator(candidatePath);
            if (!IsPathUnderInstallFolder(sanitized, installPath))
            {
                return;
            }

            paths.Add(sanitized);
        }

        private static bool IsPathUnderInstallFolder(string assetPath, string installPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(installPath))
            {
                return false;
            }

            string normalizedAsset = PathUtils.SanitizePathSeparator(assetPath);
            string normalizedInstall = PathUtils.SanitizePathSeparator(installPath).TrimEnd('/');

            if (string.Equals(normalizedAsset, normalizedInstall, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix = normalizedInstall + "/";
            return normalizedAsset.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AssetExistsAtPath(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                return true;
            }

            string absolutePath = Path.GetFullPath(assetPath);
            return File.Exists(absolutePath) || Directory.Exists(absolutePath);
        }

        private static int ComparePathsDeepestFirst(string left, string right)
        {
            int leftDepth = CountPathSegments(left);
            int rightDepth = CountPathSegments(right);
            int depthCompare = rightDepth.CompareTo(leftDepth);
            if (depthCompare != 0)
            {
                return depthCompare;
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountPathSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            int count = 0;
            string sanitized = PathUtils.SanitizePathSeparator(path).Trim('/');
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (sanitized[i] == '/')
                {
                    count++;
                }
            }

            return count + 1;
        }

        private static void TryRemoveEmptyInstallFolder(string installPath)
        {
            string absolutePath = Path.GetFullPath(installPath);
            if (!Directory.Exists(absolutePath))
            {
                return;
            }

            string[] remainingFiles = Directory.GetFiles(absolutePath, "*", SearchOption.AllDirectories);
            string[] remainingDirs = Directory.GetDirectories(absolutePath, "*", SearchOption.AllDirectories);
            if (remainingFiles.Length > 0 || remainingDirs.Length > 0)
            {
                return;
            }

            try
            {
                if (AssetDatabase.DeleteAsset(installPath))
                {
                    Debug.Log($"[InstalledModelAssetCleaner] Removed empty install folder '{installPath}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InstalledModelAssetCleaner] Failed to remove empty folder '{installPath}': {ex.Message}");
            }
        }
    }
}
