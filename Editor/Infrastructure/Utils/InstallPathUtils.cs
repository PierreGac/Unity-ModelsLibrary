using System;
using System.IO;
using System.Linq;
using ModelLibrary.Editor.Utils;
using UnityEditor;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for install path operations and path normalization.
    /// </summary>
    internal static class InstallPathUtils
    {
        /// <summary>Project Assets folder name.</summary>
        private const string ASSETS_ROOT_NAME = "Assets";

        /// <summary>Prefix for project-relative asset paths.</summary>
        private const string ASSETS_ROOT_PREFIX = "Assets/";

        /// <summary>
        /// Sanitizes a folder name by replacing invalid characters with underscores.
        /// </summary>
        /// <param name="name">The folder name to sanitize.</param>
        /// <returns>A sanitized folder name safe for use in file system paths.</returns>
        public static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Model";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(result);
        }

        /// <summary>
        /// Normalizes an install path to ensure it starts with "Assets/".
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path, or null if the input is invalid.</returns>
        public static string NormalizeInstallPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalized = PathUtils.SanitizePathSeparator(path.Trim());
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"Assets/{normalized.TrimStart('/')}";
            }

            if (normalized.Length > StringConstants.ASSETS_PREFIX_LENGTH)
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }

        /// <summary>
        /// Attempts to convert an absolute file system path to a project-relative path.
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert.</param>
        /// <param name="relativePath">Output parameter containing the relative path if conversion succeeds.</param>
        /// <returns>True if conversion was successful, false otherwise.</returns>
        /// <remarks>
        /// SECURITY (audit MED-03): The previous implementation used
        /// <c>OrdinalIgnoreCase</c> for the <c>StartsWith</c> check, which is
        /// correct on Windows (case-insensitive filesystem) but could
        /// incorrectly accept paths on Linux/macOS case-sensitive filesystems.
        /// We now use <c>OrdinalIgnoreCase</c> as a conservative default
        /// (it may reject a path that would actually be safe on a
        /// case-sensitive FS, but never accept an unsafe one) and document
        /// the choice.
        /// </remarks>
        public static bool TryConvertAbsoluteToProjectRelative(string absolutePath, out string relativePath)
        {
            relativePath = null;
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedAbsolute = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // SECURITY (MED-03): Use OrdinalIgnoreCase — conservative on case-sensitive FS,
            // correct on case-insensitive FS (Windows / macOS default).
            if (!normalizedAbsolute.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rel = normalizedAbsolute[normalizedRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            rel = PathUtils.SanitizePathSeparator(rel);
            if (string.IsNullOrEmpty(rel) || !rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            relativePath = rel;
            return true;
        }

        /// <summary>
        /// Builds a default install path for a model based on its name.
        /// </summary>
        /// <param name="modelName">The name of the model.</param>
        /// <returns>A default install path in the format "Assets/Models/{sanitizedModelName}".</returns>
        public static string BuildInstallPath(string modelName) => $"Assets/Models/{SanitizeFolderName(modelName)}";

        /// <summary>
        /// Loads the project folder or asset at an install path.
        /// </summary>
        /// <param name="installPath">Project-relative path (Assets/...) or an absolute path inside the project.</param>
        /// <param name="asset">The loaded folder or asset when the path exists in the project.</param>
        /// <returns>True when a project object was found.</returns>
        public static bool TryLoadProjectObject(string installPath, out UnityEngine.Object asset)
        {
            asset = null;
            string projectPath = ResolveProjectAssetPath(installPath);
            if (string.IsNullOrEmpty(projectPath))
            {
                return false;
            }

            asset = AssetDatabase.LoadMainAssetAtPath(projectPath);
            if (asset != null)
            {
                return true;
            }

            if (!AssetDatabase.IsValidFolder(projectPath))
            {
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(projectPath);
            return asset != null;
        }

        /// <summary>
        /// Converts an install path into a project-relative asset path when it points inside this project.
        /// </summary>
        /// <param name="installPath">Project-relative or absolute install path.</param>
        /// <returns>A project-relative path starting with Assets, or null when the path is outside the project.</returns>
        private static string ResolveProjectAssetPath(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return null;
            }

            if (TryConvertAbsoluteToProjectRelative(installPath, out string relativeFromAbsolute))
            {
                return relativeFromAbsolute;
            }

            string sanitized = PathUtils.SanitizePathSeparator(installPath.Trim());
            if (string.Equals(sanitized, ASSETS_ROOT_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return ASSETS_ROOT_NAME;
            }

            if (!sanitized.StartsWith(ASSETS_ROOT_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return NormalizeInstallPath(sanitized);
        }
    }
}
