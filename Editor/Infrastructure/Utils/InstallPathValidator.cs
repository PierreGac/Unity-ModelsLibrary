using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Editor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Validates install paths for model submission and import.
    /// Ensures install paths point to dedicated model folders, not shared containers.
    /// </summary>
    public static class InstallPathValidator
    {
        private const string NEW_MANIFEST_NAME = ".modelLibrary.meta.json";
        private const string OLD_MANIFEST_NAME = "modelLibrary.meta.json";

        /// <summary>
        /// Controls how strictly install paths are validated.
        /// </summary>
        public enum InstallPathValidationMode
        {
            /// <summary>Format and model-folder naming only; no on-disk folder inspection.</summary>
            Submission,

            /// <summary>Full validation including on-disk folder content checks used during import.</summary>
            Import
        }

        /// <summary>
        /// Result of install path validation including errors and a suggested path.
        /// </summary>
        public sealed class ValidationResult
        {
            /// <summary>True when the install path is safe for a new model submission.</summary>
            public bool IsValid { get; set; }

            /// <summary>Human-readable validation errors. Empty when <see cref="IsValid"/> is true.</summary>
            public List<string> Errors { get; set; } = new List<string>();

            /// <summary>Suggested install path in Assets/ form (e.g. Assets/MyFolder/my_model_name).</summary>
            public string SuggestedInstallPath { get; set; }
        }

        /// <summary>
        /// Validates that an install path is a dedicated model folder and not a shared container.
        /// </summary>
        /// <param name="installPath">Install path relative to the project (must start with Assets/).</param>
        /// <param name="modelName">Model display name used to build the suggested leaf folder.</param>
        /// <param name="allowExistingModelContent">When true, existing model content at the path is allowed (update flow).</param>
        /// <param name="mode">Validation strictness. Submission skips on-disk checks.</param>
        /// <returns>Validation result with errors and suggested path when invalid.</returns>
        public static ValidationResult Validate(
            string installPath,
            string modelName,
            bool allowExistingModelContent = false,
            InstallPathValidationMode mode = InstallPathValidationMode.Submission)
        {
            ValidationResult result = new ValidationResult();
            string normalizedPath = InstallPathUtils.NormalizeInstallPath(installPath);
            string sanitizedModelName = InstallPathUtils.SanitizeFolderName(modelName);

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                result.SuggestedInstallPath = InstallPathUtils.BuildInstallPath(sanitizedModelName);
                result.Errors.Add("Install path is required");
                return result;
            }

            result.SuggestedInstallPath = BuildSuggestedInstallPath(normalizedPath, modelName);

            List<string> formatErrors = PathUtils.ValidateInstallPath(normalizedPath);
            if (formatErrors.Count > 0)
            {
                result.Errors.AddRange(formatErrors);
                return result;
            }

            bool endsWithModelName = PathEndsWithFolderName(normalizedPath, sanitizedModelName);
            if (!endsWithModelName)
            {
                if (mode == InstallPathValidationMode.Import)
                {
                    string absolutePath = Path.GetFullPath(normalizedPath);
                    if (Directory.Exists(absolutePath))
                    {
                        if (allowExistingModelContent)
                        {
                            if (FolderContainsModelContent(absolutePath))
                            {
                                result.SuggestedInstallPath = normalizedPath;
                                result.IsValid = true;
                                return result;
                            }

                            string resolvedUpdatePath = TryResolveExistingModelInstallPath(
                                normalizedPath,
                                sanitizedModelName,
                                absolutePath);
                            if (!string.IsNullOrEmpty(resolvedUpdatePath))
                            {
                                result.SuggestedInstallPath = resolvedUpdatePath;
                                result.IsValid = true;
                                return result;
                            }
                        }
                        else
                        {
                            if (FolderContainsModelContent(absolutePath))
                            {
                                result.Errors.Add(
                                    $"Install path '{normalizedPath}' already contains model files. Use a dedicated subfolder per model.");
                            }

                            List<string> nestedModelFolders = GetNestedModelFolderNames(absolutePath);
                            if (nestedModelFolders.Count > 0)
                            {
                                result.Errors.Add(
                                    $"Install path '{normalizedPath}' is a container folder with nested models ({string.Join(", ", nestedModelFolders)}). " +
                                    "Use a dedicated subfolder for this model.");
                            }
                        }
                    }
                }

                if (!allowExistingModelContent && result.Errors.Count == 0)
                {
                    result.Errors.Add(
                        $"Install path must end with the model folder name '{sanitizedModelName}' (e.g. '{result.SuggestedInstallPath}').");
                }

                return FinalizeResult(result);
            }

            if (mode == InstallPathValidationMode.Submission)
            {
                result.IsValid = true;
                return result;
            }

            string existingAbsolutePath = Path.GetFullPath(normalizedPath);
            if (!Directory.Exists(existingAbsolutePath))
            {
                result.IsValid = true;
                return result;
            }

            if (!allowExistingModelContent)
            {
                if (FolderContainsModelContent(existingAbsolutePath))
                {
                    result.Errors.Add(
                        $"Install path '{normalizedPath}' already contains model files. Choose an empty folder or a new model subfolder.");
                    return FinalizeResult(result);
                }

                List<string> childModelFolders = GetNestedModelFolderNames(existingAbsolutePath);
                if (childModelFolders.Count > 0)
                {
                    result.Errors.Add(
                        $"Install path '{normalizedPath}' contains nested model folders ({string.Join(", ", childModelFolders)}). " +
                        "Install paths must point to a single model folder, not a container.");
                    return FinalizeResult(result);
                }
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// Returns true when the install path exists on disk and already contains model files or a manifest.
        /// </summary>
        /// <param name="installPath">Install path relative to the project (must start with Assets/).</param>
        /// <returns>True when the folder contains model content.</returns>
        public static bool PathContainsModelContent(string installPath)
        {
            string normalizedPath = InstallPathUtils.NormalizeInstallPath(installPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            string absolutePath = Path.GetFullPath(normalizedPath);
            return FolderContainsModelContent(absolutePath);
        }

        /// <summary>
        /// Builds a suggested install path using the parent of the current path and the model name.
        /// When the current path still uses the default placeholder folder name, the leaf segment
        /// is replaced instead of appending the new model name.
        /// </summary>
        /// <param name="installPath">Current install path.</param>
        /// <param name="modelName">Model display or folder name.</param>
        /// <returns>Suggested install path ending with the model folder name.</returns>
        public static string BuildSuggestedInstallPath(string installPath, string modelName)
        {
            string sanitizedModelName = InstallPathUtils.SanitizeFolderName(modelName);
            string normalizedPath = InstallPathUtils.NormalizeInstallPath(installPath) ?? InstallPathUtils.BuildInstallPath(sanitizedModelName);
            if (PathEndsWithFolderName(normalizedPath, sanitizedModelName))
            {
                return normalizedPath;
            }

            string lastSegment = GetLastPathSegment(normalizedPath);
            if (IsStaleModelFolderSegment(normalizedPath, lastSegment, sanitizedModelName))
            {
                string staleParentPath = GetParentPath(normalizedPath);
                if (string.IsNullOrEmpty(staleParentPath))
                {
                    staleParentPath = "Assets";
                }

                return PathUtils.SanitizePathSeparator($"{staleParentPath}/{sanitizedModelName}");
            }

            string parentPath = normalizedPath;
            if (FolderLooksLikeModelLeaf(normalizedPath))
            {
                parentPath = Path.GetDirectoryName(normalizedPath.Replace('/', Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "Assets/Models";
                }
                parentPath = PathUtils.SanitizePathSeparator(parentPath);
            }

            if (!parentPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                parentPath = $"Assets/{parentPath.TrimStart('/')}";
            }

            return PathUtils.SanitizePathSeparator($"{parentPath}/{sanitizedModelName}");
        }

        private static ValidationResult FinalizeResult(ValidationResult result)
        {
            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private static bool PathEndsWithFolderName(string installPath, string folderName)
        {
            if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            return string.Equals(GetLastPathSegment(installPath), folderName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLastPathSegment(string installPath)
        {
            string sanitized = PathUtils.SanitizePathSeparator(installPath).TrimEnd('/');
            int lastSlash = sanitized.LastIndexOf('/');
            return lastSlash >= 0 ? sanitized.Substring(lastSlash + 1) : sanitized;
        }

        private static string GetParentPath(string installPath)
        {
            string sanitized = PathUtils.SanitizePathSeparator(installPath).TrimEnd('/');
            int lastSlash = sanitized.LastIndexOf('/');
            if (lastSlash < 0)
            {
                return string.Empty;
            }

            return sanitized.Substring(0, lastSlash);
        }

        private static bool IsStaleModelFolderSegment(string normalizedPath, string lastSegment, string sanitizedModelName)
        {
            if (string.IsNullOrWhiteSpace(lastSegment) ||
                string.Equals(lastSegment, sanitizedModelName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string defaultModelFolderName = InstallPathUtils.SanitizeFolderName(StringConstants.DEFAULT_MODEL_NAME);
            if (string.Equals(lastSegment, defaultModelFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string defaultPathForSegment = InstallPathUtils.BuildInstallPath(lastSegment);
            return string.Equals(normalizedPath, defaultPathForSegment, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the install path for a model update when the stored path points at a parent
        /// folder that already contains the model as a nested install folder.
        /// </summary>
        /// <param name="normalizedPath">Normalized install path relative to Assets/.</param>
        /// <param name="sanitizedModelName">Sanitized model folder name.</param>
        /// <param name="absolutePath">Absolute path to the normalized install folder.</param>
        /// <returns>Resolved install path when the model folder exists on disk; otherwise null.</returns>
        private static string TryResolveExistingModelInstallPath(
            string normalizedPath,
            string sanitizedModelName,
            string absolutePath)
        {
            string nestedModelAbsolutePath = Path.Combine(absolutePath, sanitizedModelName);
            if (!Directory.Exists(nestedModelAbsolutePath) || !FolderContainsModelContent(nestedModelAbsolutePath))
            {
                return null;
            }

            return PathUtils.SanitizePathSeparator($"{normalizedPath}/{sanitizedModelName}");
        }

        private static bool FolderLooksLikeModelLeaf(string installPath)
        {
            string absolutePath = Path.GetFullPath(installPath);
            return Directory.Exists(absolutePath) && FolderContainsModelContent(absolutePath);
        }

        private static bool FolderContainsModelContent(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            if (File.Exists(Path.Combine(directoryPath, NEW_MANIFEST_NAME)) ||
                File.Exists(Path.Combine(directoryPath, OLD_MANIFEST_NAME)))
            {
                return true;
            }

            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (string.Equals(fileName, NEW_MANIFEST_NAME, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, OLD_MANIFEST_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension == FileExtensions.META)
                {
                    continue;
                }

                if (extension == FileExtensions.FBX || extension == FileExtensions.OBJ)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> GetNestedModelFolderNames(string directoryPath)
        {
            List<string> nestedModelFolders = new List<string>();
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return nestedModelFolders;
            }

            string[] subdirectories = Directory.GetDirectories(directoryPath);
            for (int i = 0; i < subdirectories.Length; i++)
            {
                if (FolderContainsModelContent(subdirectories[i]))
                {
                    nestedModelFolders.Add(Path.GetFileName(subdirectories[i]));
                }
            }

            return nestedModelFolders;
        }
    }
}
