
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
        public static async Task<string> ImportFromCacheAsync(string cacheVersionRoot, ModelMeta meta, bool cleanDestination = true, string overrideInstallPath = null)
        {
            // Determine destination folder with validation and logging
            string destRel = ResolveDestinationPath(meta, overrideInstallPath);
            string destAbs = Path.GetFullPath(destRel);

            Debug.Log($"[ModelProjectImporter] Importing model '{meta?.identity?.name}' to path: {destRel}");

            if (cleanDestination && Directory.Exists(destAbs))
            {
                TryCleanDirectory(destAbs);
            }
            Directory.CreateDirectory(destAbs);

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
                    File.Copy(file, target, overwrite: true);
                    string srcMeta = file + FileExtensions.META;
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, target + FileExtensions.META, overwrite: true);
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
                    File.Copy(file, target, overwrite: true);
                    string srcMeta = file + FileExtensions.META;
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, target + FileExtensions.META, overwrite: true);
                    }
                }
            }

            // Persist manifest for local version tracking
            string manifestPath = Path.Combine(destAbs, "modelLibrary.meta.json");
            File.WriteAllText(manifestPath, JsonUtil.ToJson(meta));

            // Refresh to register new files
            AssetDatabase.Refresh();

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
                                if (!string.IsNullOrEmpty(settings.materialImportMode) && System.Enum.TryParse(settings.materialImportMode, out ModelImporterMaterialImportMode mode))
                                {
                                    importer.materialImportMode = mode;
                                }
                                if (!string.IsNullOrEmpty(settings.materialSearch) && System.Enum.TryParse(settings.materialSearch, out ModelImporterMaterialSearch search))
                                {
                                    importer.materialSearch = search;
                                }
                                if (!string.IsNullOrEmpty(settings.materialName) && System.Enum.TryParse(settings.materialName, out ModelImporterMaterialName name))
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
                Directory.Delete(path, true);
            }
            catch
            {
                try
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
                catch
                {
                    // give up, will overwrite existing files during copy
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
    }
}



