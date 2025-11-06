using System;
using System.IO;
using System.Linq;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for install path operations and path normalization.
    /// </summary>
    public static class InstallPathUtils
    {
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
            return normalized;
        }

        /// <summary>
        /// Attempts to convert an absolute file system path to a project-relative path.
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert.</param>
        /// <param name="relativePath">Output parameter containing the relative path if conversion succeeds.</param>
        /// <returns>True if conversion was successful, false otherwise.</returns>
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
        public static string BuildInstallPath(string modelName)
        {
            return $"Assets/Models/{SanitizeFolderName(modelName)}";
        }
    }
}

