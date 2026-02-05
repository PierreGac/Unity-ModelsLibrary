
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        public static async Task<string> ImportFromCacheAsync(string cacheVersionRoot, ModelMeta meta, bool cleanDestination = true, string overrideInstallPath = null, bool isUpdate = false, CancellationToken cancellationToken = default)
        {
            // Track imported files for rollback on cancellation
            List<string> importedFiles = new List<string>();
            List<string> importedDirectories = new List<string>();

            try
            {
                // Determine destination folder with validation and logging
                string destRel = ResolveDestinationPath(meta, overrideInstallPath);
                string destAbs = Path.GetFullPath(destRel);

                Debug.Log($"[ModelProjectImporter] Importing model '{(meta != null && meta.identity != null ? meta.identity.name : "Unknown")}' to path: {destRel}");

                cancellationToken.ThrowIfCancellationRequested();

                // Get existing GUIDs BEFORE import to avoid false positive conflicts
                HashSet<string> existingGuidsBeforeImport = new HashSet<string>();
                if (!isUpdate && meta != null && meta.assetGuids != null && meta.assetGuids.Count > 0)
                {
                    string[] allGuids = AssetDatabase.FindAssets(string.Empty);
                    existingGuidsBeforeImport = new HashSet<string>(allGuids);
                    Debug.Log($"[ModelProjectImporter] Found {existingGuidsBeforeImport.Count} existing GUIDs before import");
                }

                cancellationToken.ThrowIfCancellationRequested();

            // Ensure destination is a directory, not a file
            if (File.Exists(destAbs))
            {
                ErrorLogger.LogError("Invalid Destination Path", 
                    $"Destination path points to an existing file: {destAbs}. Converting to directory path.", 
                    ErrorHandler.ErrorCategory.FileSystem, null, $"Destination: {destAbs}");
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
                        importedFiles.Add(target);
                        string srcMeta = file + FileExtensions.META;
                        if (File.Exists(srcMeta))
                        {
                            string targetMeta = target + FileExtensions.META;
                            File.Copy(srcMeta, targetMeta, overwrite: true);
                            importedFiles.Add(targetMeta);
                        }
                        
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("Copy Payload File Failed", 
                            $"Failed to copy payload file {file} to {target}: {ex.Message}", 
                            ErrorHandler.CategorizeException(ex), ex, $"Source: {file}, Target: {target}");
                        throw;
                    }
                }
            }
            
            // Track destination directory
            if (!importedDirectories.Contains(destAbs))
            {
                importedDirectories.Add(destAbs);
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
                        importedFiles.Add(target);
                        string srcMeta = file + FileExtensions.META;
                        if (File.Exists(srcMeta))
                        {
                            string targetMeta = target + FileExtensions.META;
                            File.Copy(srcMeta, targetMeta, overwrite: true);
                            importedFiles.Add(targetMeta);
                        }
                        
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("Copy Dependency File Failed", 
                            $"Failed to copy dependency file {file} to {target}: {ex.Message}", 
                            ErrorHandler.CategorizeException(ex), ex, $"Source: {file}, Target: {target}");
                        throw;
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Persist manifest for local version tracking
            // Use dot prefix to hide from Unity Project window
            string manifestPath = Path.Combine(destAbs, ".modelLibrary.meta.json");
            File.WriteAllText(manifestPath, JsonUtil.ToJson(meta));
            importedFiles.Add(manifestPath);

            cancellationToken.ThrowIfCancellationRequested();

            // Refresh to register new files
            AssetDatabase.Refresh();

            // Get the relative path for Unity (convert absolute to relative)
            // File is already created with dot prefix, so it's already hidden
            string manifestRelativePath = destRel + "/.modelLibrary.meta.json";
            manifestRelativePath = PathUtils.SanitizePathSeparator(manifestRelativePath);

            Debug.Log($"[ModelProjectImporter] Created hidden manifest file: {manifestRelativePath}");

            cancellationToken.ThrowIfCancellationRequested();

            // Check for GUID conflicts after import (only for new imports, not updates)
            if (!isUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckForGuidConflictsAsync(meta, destAbs, existingGuidsBeforeImport, cancellationToken);
            }
            else
            {
                // For updates, log that GUID conflicts are expected and normal
                Debug.Log($"[ModelProjectImporter] Model update completed for '{meta.identity.name}'. GUID conflicts are expected and normal for updates.");
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                            catch (Exception ex)
                            {
                                // Keep default if API mismatch - log but don't fail
                                Debug.LogWarning($"[ModelProjectImporter] Failed to set asset labels: {ex.Message}");
                            }
                        }
                    }
                }
            }

            return await Task.FromResult(destRel);
            }
            catch (OperationCanceledException)
            {
                // Rollback: Delete all imported files and directories
                Debug.LogWarning($"[ModelProjectImporter] Import cancelled. Rolling back {importedFiles.Count} files...");
                RollbackImport(importedFiles, importedDirectories);
                throw;
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                // Rollback if cancellation was requested, even if exception is not OperationCanceledException
                Debug.LogWarning($"[ModelProjectImporter] Import cancelled (token cancelled). Rolling back {importedFiles.Count} files...");
                RollbackImport(importedFiles, importedDirectories);
                throw new OperationCanceledException("Import cancelled.", ex);
            }
        }

        /// <summary>
        /// Rolls back a cancelled import by deleting all imported files and directories.
        /// Deletes files and directories in reverse order to handle dependencies correctly.
        /// Logs warnings for any files that cannot be deleted but continues with the rollback.
        /// </summary>
        /// <param name="importedFiles">List of file paths that were imported and need to be deleted. Can be null or empty.</param>
        /// <param name="importedDirectories">List of directory paths that were created and need to be deleted. Can be null or empty.</param>
        private static void RollbackImport(List<string> importedFiles, List<string> importedDirectories)
        {
            // Delete files in reverse order
            for (int i = importedFiles.Count - 1; i >= 0; i--)
            {
                string file = importedFiles[i];
                try
                {
                    if (File.Exists(file))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModelProjectImporter] Failed to delete file during rollback: {file}, Error: {ex.Message}");
                }
            }

            // Delete directories in reverse order
            for (int i = importedDirectories.Count - 1; i >= 0; i--)
            {
                string dir = importedDirectories[i];
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModelProjectImporter] Failed to delete directory during rollback: {dir}, Error: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ModelProjectImporter] Rollback completed. Deleted {importedFiles.Count} files and {importedDirectories.Count} directories.");
        }

        /// <summary>
        /// Attempts to clean (delete) a directory or file at the specified path.
        /// Handles both file and directory deletion, with robust error handling.
        /// If initial deletion fails, attempts to clean individual files and subdirectories.
        /// </summary>
        /// <param name="path">Path to the file or directory to clean.</param>
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
                catch (Exception ex)
                {
                    // Best effort cleanup - will overwrite existing files during copy
                    ErrorLogger.LogError("Clean Destination Failed", 
                        $"Failed to clean destination: {path}", 
                        ErrorHandler.CategorizeException(ex), ex, $"Path: {path}");
                }
            }
        }

        /// <summary>
        /// Recursively copies a directory and all its contents to a destination directory.
        /// Preserves directory structure and overwrites existing files.
        /// </summary>
        /// <param name="srcDir">Source directory to copy from.</param>
        /// <param name="dstDir">Destination directory to copy to.</param>
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
        /// Resolves the destination path for model import with comprehensive validation and logging.
        /// Priority order: 1) Override path (if provided), 2) Meta relative path (if valid), 3) Fallback default path.
        /// Validates paths and handles file-to-directory conversion if needed.
        /// </summary>
        /// <param name="meta">Model metadata containing relative path information.</param>
        /// <param name="overrideInstallPath">Optional override path for installation (user-selected).</param>
        /// <returns>Resolved destination path relative to project root (e.g., "Assets/Models/ModelName").</returns>
        private static string ResolveDestinationPath(ModelMeta meta, string overrideInstallPath)
        {
            // Priority 1: Override path (highest priority)
            if (!string.IsNullOrEmpty(overrideInstallPath))
            {
                Debug.Log($"[ModelProjectImporter] Using override install path: {overrideInstallPath}");
                return PathUtils.SanitizePathSeparator(overrideInstallPath);
            }

            // Priority 2: Meta relative path (with validation)
            if (meta != null && !string.IsNullOrEmpty(meta.relativePath))
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
            string safeName = SanitizeFolderName(meta != null && meta.identity != null ? meta.identity.name : "UnknownModel");
            string fallbackPath = $"Assets/Models/{safeName}";
            Debug.Log($"[ModelProjectImporter] Using fallback path for model '{(meta != null && meta.identity != null ? meta.identity.name : "Unknown")}': {fallbackPath}");
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

        /// <summary>
        /// Checks for GUID conflicts between imported model assets and existing project assets.
        /// Uses pre-import GUID snapshot to avoid false positives from the import itself.
        /// Differentiates between same-model GUIDs (expected) and actual conflicts with other models.
        /// Shows a dialog to the user if conflicts are detected, offering to regenerate GUIDs.
        /// </summary>
        /// <param name="meta">Model metadata containing asset GUIDs to check.</param>
        /// <param name="destAbs">Absolute path to the destination directory where the model was imported.</param>
        /// <param name="existingGuidsBeforeImport">Set of GUIDs that existed before import (for accurate conflict detection).</param>
        /// <param name="cancellationToken">Cancellation token to check for cancellation requests.</param>
        private static async Task CheckForGuidConflictsAsync(ModelMeta meta, string destAbs, HashSet<string> existingGuidsBeforeImport = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (meta == null || meta.assetGuids == null || meta.assetGuids.Count == 0)
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
                    cancellationToken.ThrowIfCancellationRequested();
                    // Auto-keep existing GUIDs to preserve references (default behavior)
                    // This prevents losing references when importing models with conflicting GUIDs
                    await HandleGuidConflictsAsync(meta, conflictingGuids, destAbs, cancellationToken);
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

        /// <summary>
        /// Regenerates GUIDs for all .meta files in the imported model directory.
        /// This resolves GUID conflicts by assigning new unique identifiers to imported assets.
        /// Uses regex to replace GUID values in .meta files, then refreshes the AssetDatabase.
        /// </summary>
        /// <param name="destAbs">Absolute path to the directory containing the imported model.</param>
        private static async Task RegenerateGuidsForImportedModelAsync(string destAbs)
        {
            try
            {
                // Find all .meta files in the destination directory
                string[] metaFiles = Directory.GetFiles(destAbs, "*.meta", SearchOption.AllDirectories);

                for (int i = 0; i < metaFiles.Length; i++)
                {
                    string metaFile = metaFiles[i];
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

        /// <summary>
        /// Handles GUID conflicts by auto-keeping existing GUIDs to preserve references.
        /// Shows a dialog with options to regenerate GUIDs or keep existing ones.
        /// Default behavior is to keep existing GUIDs to prevent losing references.
        /// </summary>
        /// <param name="meta">Model metadata containing asset GUIDs.</param>
        /// <param name="conflictingGuids">List of GUIDs that conflict with existing assets.</param>
        /// <param name="destAbs">Absolute path to the destination directory.</param>
        /// <param name="cancellationToken">Cancellation token to check for cancellation requests.</param>
        private static async Task HandleGuidConflictsAsync(ModelMeta meta, List<string> conflictingGuids, string destAbs, CancellationToken cancellationToken = default)
        {
            string conflictMessage = $"GUID conflicts detected for model '{meta.identity.name}':\n\n";
            int shownCount = 0;
            for (int i = 0; i < conflictingGuids.Count && i < 5; i++)
            {
                string guid = conflictingGuids[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(assetPath);
                conflictMessage += $"• {fileName} ({guid})\n  → {assetPath}\n";
                shownCount++;
            }
            if (conflictingGuids.Count > 5)
            {
                conflictMessage += $"... and {conflictingGuids.Count - 5} more conflicts\n";
            }
            conflictMessage += "\nKeeping existing GUIDs to preserve references. This is the recommended option.";

            Debug.LogWarning($"[ModelProjectImporter] {conflictMessage}");

            // Show dialog with "Keep Existing" as the default (middle button)
            int choice = EditorUtility.DisplayDialogComplex(
                "GUID Conflicts Detected",
                conflictMessage + "\n\nHow would you like to resolve these conflicts?",
                "Regenerate GUIDs",
                "Keep Existing (Recommended)",
                "Cancel Import"
            );

            if (choice == 0) // Regenerate GUIDs
            {
                await RegenerateGuidsForImportedModelAsync(destAbs);
                Debug.Log($"[ModelProjectImporter] Regenerated GUIDs for {conflictingGuids.Count} conflicting assets.");
            }
            else if (choice == 1) // Keep Existing (Recommended)
            {
                // Auto-keep existing GUIDs - this preserves references
                Debug.Log($"[ModelProjectImporter] Preserving {conflictingGuids.Count} existing GUIDs to maintain references. This is the recommended approach.");
            }
            else // Cancel Import (choice == 2)
            {
                // Cancel the import - this will trigger rollback in the catch block
                Debug.LogWarning($"[ModelProjectImporter] Import cancelled by user due to GUID conflicts.");
                throw new OperationCanceledException("Import cancelled due to GUID conflicts.");
            }
        }
    }
}



