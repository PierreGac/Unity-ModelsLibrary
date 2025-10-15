
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Copies a cached model version into the project under Assets/Models/<ModelName>/
    /// Flattens payload and dependencies into the named folder, places images under an images/ subfolder.
    /// </summary>
    public static class ModelProjectImporter
    {
        public static async Task<string> ImportFromCacheAsync(string cacheVersionRoot, ModelMeta meta, bool cleanDestination = true, string overrideInstallPath = null, bool isUpdate = false)
        {
            // Determine destination folder with validation and logging
            string destRel = ResolveDestinationPath(meta, overrideInstallPath);
            string destAbs = Path.GetFullPath(destRel);

            Debug.Log($"[ModelProjectImporter] Importing model '{meta?.identity?.name}' to path: {destRel}");

            // Get existing GUIDs BEFORE import to avoid false positive conflicts
            HashSet<string> existingGuidsBeforeImport = new HashSet<string>();
            if (!isUpdate && meta?.assetGuids != null && meta.assetGuids.Count > 0)
            {
                string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                existingGuidsBeforeImport = new HashSet<string>(allGuids);
                Debug.Log($"[ModelProjectImporter] Found {existingGuidsBeforeImport.Count} existing GUIDs before import");
            }

            // Ensure destination is a directory, not a file
            if (File.Exists(destAbs))
            {
                Debug.LogWarning($"[ModelProjectImporter] Destination path points to an existing file: {destAbs}. Converting to directory path.");
                destAbs = Path.Combine(Path.GetDirectoryName(destAbs), Path.GetFileNameWithoutExtension(destAbs));
                destRel = PathUtils.SanitizePathSeparator(destAbs.Replace(Path.GetFullPath("Assets"), "Assets"));
            }

            if (cleanDestination && Directory.Exists(destAbs))
            {
                TryCleanDirectory(destAbs);
            }

            // Ensure the directory exists
            if (!Directory.Exists(destAbs))
            {
                Directory.CreateDirectory(destAbs);
            }

            // Copy payload files into root of model folder (flatten), and images under images/
            string payloadRoot = Path.Combine(cacheVersionRoot, "payload");
            string depsRoot = Path.Combine(payloadRoot, "deps");

            // Copy top-level payload files directly into destAbs (skip shaders). Copy .meta alongside when present
            if (Directory.Exists(payloadRoot))
            {
                foreach (string file in Directory.GetFiles(payloadRoot, "*", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    string target = Path.Combine(destAbs, fileName);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == FileExtensions.META) { continue; }
                    if (FileExtensions.IsNotAllowedFileExtension(ext))
                    {
                        continue;
                    }
                    try
                    {
                        // Ensure target directory exists
                        string targetDir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, target, overwrite: true);
                        string srcMeta = file + FileExtensions.META;
                        if (File.Exists(srcMeta))
                        {
                            File.Copy(srcMeta, target + FileExtensions.META, overwrite: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModelProjectImporter] Failed to copy payload file {file} to {target}: {ex.Message}");
                        throw;
                    }
                }
            }

            // Copy dependency files (any depth) directly into destAbs (flatten) and skip shaders. Copy .meta alongside when present
            if (Directory.Exists(depsRoot))
            {
                foreach (string file in Directory.GetFiles(depsRoot, "*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    string target = Path.Combine(destAbs, fileName);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == FileExtensions.META) { continue; }
                    if (FileExtensions.IsNotAllowedFileExtension(ext))
                    {
                        continue;
                    }
                    if (string.Equals(fileName, "auto_preview.png"))
                    {
                        continue;
                    }
                    try
                    {
                        // Ensure target directory exists
                        string targetDir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, target, overwrite: true);
                        string srcMeta = file + FileExtensions.META;
                        if (File.Exists(srcMeta))
                        {
                            File.Copy(srcMeta, target + FileExtensions.META, overwrite: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModelProjectImporter] Failed to copy dependency file {file} to {target}: {ex.Message}");
                        throw;
                    }
                }
            }

            // Persist manifest for local version tracking
            string manifestPath = Path.Combine(destAbs, "modelLibrary.meta.json");
            File.WriteAllText(manifestPath, JsonUtil.ToJson(meta));

            // Refresh to register new files
            AssetDatabase.Refresh();

            // Check for GUID conflicts after import (only for new imports, not updates)
            if (!isUpdate)
            {
                await CheckForGuidConflictsAsync(meta, destAbs, existingGuidsBeforeImport);
            }
            else
            {
                // For updates, log that GUID conflicts are expected and normal
                Debug.Log($"[ModelProjectImporter] Model update completed for '{meta.identity.name}'. GUID conflicts are expected and normal for updates.");
            }

            // Restore per-file model importer settings captured in meta (if present)
            foreach (string file in Directory.GetFiles(destAbs, "*", SearchOption.TopDirectoryOnly))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == FileExtensions.FBX || ext == FileExtensions.OBJ)
                {
                    string fileName = Path.GetFileName(file);
                    string payloadRel = $"payload/{fileName}";
                    bool hasMeta = File.Exists(file + FileExtensions.META);
                    if (!hasMeta && meta.modelImporters != null && meta.modelImporters.TryGetValue(payloadRel, out ModelImporterSettings settings) && settings != null)
                    {
                        string assetPath = PathUtils.SanitizePathSeparator(Path.Combine(destRel, fileName));
                        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                        if (importer != null)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(settings.materialImportMode) && Enum.TryParse(settings.materialImportMode, out ModelImporterMaterialImportMode mode))
                                {
                                    importer.materialImportMode = mode;
                                }
                                if (!string.IsNullOrEmpty(settings.materialSearch) && Enum.TryParse(settings.materialSearch, out ModelImporterMaterialSearch search))
                                {
                                    importer.materialSearch = search;
                                }
                                if (!string.IsNullOrEmpty(settings.materialName) && Enum.TryParse(settings.materialName, out ModelImporterMaterialName name))
                                {
                                    importer.materialName = name;
                                }
                                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                            }
                            catch { /* keep default if API mismatch */ }
                        }
                    }
                }
            }

            return await Task.FromResult(destRel);
        }

        private static void TryCleanDirectory(string path)
        {
            try
            {
                // Handle both files and directories
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                try
                {
                    // If it's a directory, try to clean individual files
                    if (Directory.Exists(path))
                    {
                        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }

                        foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                        {
                            Directory.Delete(dir, true);
                        }

                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                    // Best effort cleanup - will overwrite existing files during copy
                    Debug.LogWarning($"[ModelProjectImporter] Failed to clean destination: {path}");
                }
            }
        }

        private static void CopyDir(string srcDir, string dstDir)
        {
            if (!Directory.Exists(srcDir))
            {
                return;
            }

            Directory.CreateDirectory(dstDir);
            foreach (string file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                string rel = file[srcDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(dstDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, overwrite: true);
            }
        }

        /// <summary>
        /// Resolves the destination path for model import with validation and logging.
        /// </summary>
        /// <param name="meta">Model metadata containing relative path information</param>
        /// <param name="overrideInstallPath">Optional override path for installation</param>
        /// <returns>Resolved destination path relative to project root</returns>
        private static string ResolveDestinationPath(ModelMeta meta, string overrideInstallPath)
        {
            // Priority 1: Override path (highest priority)
            if (!string.IsNullOrEmpty(overrideInstallPath))
            {
                Debug.Log($"[ModelProjectImporter] Using override install path: {overrideInstallPath}");
                return PathUtils.SanitizePathSeparator(overrideInstallPath);
            }

            // Priority 2: Meta relative path (with validation)
            if (!string.IsNullOrEmpty(meta?.relativePath))
            {
                // Validate the relative path before using it
                List<string> pathErrors = PathUtils.ValidateRelativePath(meta.relativePath);
                if (pathErrors.Count > 0)
                {
                    Debug.LogWarning($"[ModelProjectImporter] Invalid relative path '{meta.relativePath}': {string.Join(", ", pathErrors)}. Using fallback path.");
                }
                else
                {
                    string resolvedPath = $"Assets/{meta.relativePath}";

                    // Ensure the path points to a directory, not a file
                    if (File.Exists(Path.GetFullPath(resolvedPath)))
                    {
                        Debug.LogWarning($"[ModelProjectImporter] Relative path points to a file: {meta.relativePath}. Converting to directory path.");
                        resolvedPath = Path.GetDirectoryName(resolvedPath);
                    }

                    Debug.Log($"[ModelProjectImporter] Using meta relative path: {resolvedPath}");
                    return PathUtils.SanitizePathSeparator(resolvedPath);
                }
            }

            // Priority 3: Fallback to safe default
            string safeName = SanitizeFolderName(meta?.identity?.name ?? "UnknownModel");
            string fallbackPath = $"Assets/Models/{safeName}";
            Debug.Log($"[ModelProjectImporter] Using fallback path for model '{meta?.identity?.name}': {fallbackPath}");
            return fallbackPath;
        }

        /// <summary>
        /// Sanitizes folder name by removing invalid characters.
        /// </summary>
        /// <param name="name">Original folder name</param>
        /// <returns>Sanitized folder name safe for file system</returns>
        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "UnknownModel";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
            return new string(result).Replace(' ', '_');
        }

        private static async Task CheckForGuidConflictsAsync(ModelMeta meta, string destAbs, HashSet<string> existingGuidsBeforeImport = null)
        {
            try
            {
                if (meta?.assetGuids == null || meta.assetGuids.Count == 0)
                {
                    return;
                }

                // Use pre-import GUIDs if provided, otherwise get current GUIDs (for backward compatibility)
                HashSet<string> existingGuids;
                if (existingGuidsBeforeImport != null)
                {
                    existingGuids = existingGuidsBeforeImport;
                    Debug.Log($"[ModelProjectImporter] Using pre-import GUIDs for conflict detection: {existingGuids.Count} GUIDs");
                }
                else
                {
                    string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                    existingGuids = new HashSet<string>(allGuids);
                    Debug.Log($"[ModelProjectImporter] Using current GUIDs for conflict detection: {existingGuids.Count} GUIDs");
                }

                List<string> conflictingGuids = new List<string>();
                List<string> sameModelGuids = new List<string>();

                foreach (string guid in meta.assetGuids)
                {
                    if (existingGuids.Contains(guid))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            // Check if the conflicting asset is from the same model (same directory)
                            if (assetPath.StartsWith(destAbs.Replace('\\', '/')))
                            {
                                sameModelGuids.Add(guid);
                            }
                            else
                            {
                                conflictingGuids.Add(guid);
                            }
                        }
                    }
                }

                // Only show conflict dialog for actual conflicts (different models), not same-model GUIDs
                if (conflictingGuids.Count > 0)
                {
                    string conflictMessage = $"GUID conflicts detected for model '{meta.identity.name}':\n";
                    foreach (string guid in conflictingGuids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        conflictMessage += $"• {guid} -> {assetPath}\n";
                    }
                    conflictMessage += "\nThis may cause issues with asset references. Consider regenerating GUIDs for the imported model.";

                    Debug.LogWarning($"[ModelProjectImporter] {conflictMessage}");

                    // Show a dialog to the user
                    bool regenerateGuids = EditorUtility.DisplayDialog(
                        "GUID Conflicts Detected",
                        conflictMessage + "\n\nWould you like to regenerate GUIDs for the imported model?",
                        "Regenerate GUIDs",
                        "Ignore"
                    );

                    if (regenerateGuids)
                    {
                        await RegenerateGuidsForImportedModelAsync(destAbs);
                    }
                }
                else if (sameModelGuids.Count > 0)
                {
                    // Same model GUIDs are expected and normal
                    Debug.Log($"[ModelProjectImporter] Found {sameModelGuids.Count} GUIDs from the same model - this is expected and normal.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelProjectImporter] Error checking for GUID conflicts: {ex.Message}");
            }
        }

        private static async Task RegenerateGuidsForImportedModelAsync(string destAbs)
        {
            try
            {
                // Find all .meta files in the destination directory
                string[] metaFiles = Directory.GetFiles(destAbs, "*.meta", SearchOption.AllDirectories);

                foreach (string metaFile in metaFiles)
                {
                    string content = await File.ReadAllTextAsync(metaFile);
                    if (content.Contains("guid:"))
                    {
                        // Generate new GUID
                        string newGuid = Guid.NewGuid().ToString("N");
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"guid: [a-f0-9]{32}", $"guid: {newGuid}");
                        await File.WriteAllTextAsync(metaFile, content);
                    }
                }

                // Refresh to apply new GUIDs
                AssetDatabase.Refresh();
                Debug.Log($"[ModelProjectImporter] Regenerated GUIDs for imported model in {destAbs}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelProjectImporter] Error regenerating GUIDs: {ex.Message}");
            }
        }
    }
}



