using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for path-related operations and validation.
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Sanitizes path separators by converting backslashes to forward slashes and removing double slashes.
        /// </summary>
        /// <param name="path">The path to sanitize</param>
        /// <returns>Path with forward slashes as separators and no double slashes</returns>
        public static string SanitizePathSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Replace backslashes with forward slashes
            string sanitized = path.Replace('\\', '/');
            
            // Remove double slashes (but preserve the first slash if path starts with /)
            while (sanitized.Contains("//"))
            {
                sanitized = sanitized.Replace("//", "/");
            }
            
            return sanitized;
        }

        /// <summary>
        /// Validates the relative path for common issues and constraints.
        /// </summary>
        /// <param name="relativePath">The relative path to validate</param>
        /// <returns>List of validation error messages, empty if valid</returns>
        public static List<string> ValidateRelativePath(string relativePath)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                errors.Add("Relative path is required");
                return errors;
            }

            string trimmedPath = relativePath.Trim();

            // Check for empty path
            if (string.IsNullOrEmpty(trimmedPath))
            {
                errors.Add("Relative path cannot be empty");
                return errors;
            }

            // Check for invalid characters FIRST (before calling Path methods)
            char[] invalidChars = Path.GetInvalidPathChars();
            foreach (char invalidChar in invalidChars)
            {
                if (trimmedPath.Contains(invalidChar))
                {
                    errors.Add($"Relative path contains invalid character: '{invalidChar}'");
                    break; // Only report the first invalid character
                }
            }

            // If we found invalid characters, return early to avoid Path method exceptions
            if (errors.Count > 0)
            {
                return errors;
            }

            // Check for path traversal attempts
            if (trimmedPath.Contains("..") || trimmedPath.Contains("~"))
            {
                errors.Add("Relative path cannot contain '..' or '~' (path traversal not allowed)");
            }

            // Check for absolute path indicators
            if (Path.IsPathRooted(trimmedPath))
            {
                errors.Add("Relative path cannot be an absolute path");
            }

            // Check for Materials folder restriction
            if (IsMaterialsFolderPath(trimmedPath))
            {
                errors.Add("Relative path cannot end with 'Materials' folder. Use the parent folder instead (e.g., 'Models/Benne' instead of 'Models/Benne/Materials')");
            }

            // Check for reserved folder names
            List<string> reservedFolders = new List<string> { "Editor", "Resources", "StreamingAssets", "Plugins" };
            string[] pathSegments = trimmedPath.Split('/', '\\');
            foreach (string segment in pathSegments)
            {
                if (reservedFolders.Any(folder => string.Equals(folder, segment, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Relative path cannot contain reserved folder name: '{segment}'");
                    break;
                }
            }

            // Check for path length (reasonable limit)
            const int maxPathLength = 200;
            if (trimmedPath.Length > maxPathLength)
            {
                errors.Add($"Relative path is too long (maximum {maxPathLength} characters)");
            }

            // Check for leading/trailing slashes
            if (trimmedPath.StartsWith("/") || trimmedPath.StartsWith("\\"))
            {
                errors.Add("Relative path should not start with a slash");
            }

            if (trimmedPath.EndsWith("/") || trimmedPath.EndsWith("\\"))
            {
                errors.Add("Relative path should not end with a slash");
            }

            return errors;
        }

        /// <summary>
        /// Checks if the path ends with a Materials folder.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path ends with Materials folder, false otherwise</returns>
        public static bool IsMaterialsFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Normalize path separators
            string normalizedPath = SanitizePathSeparator(path);

            // Remove trailing slash if present
            if (normalizedPath.EndsWith("/"))
            {
                normalizedPath = normalizedPath.TrimEnd('/');
            }

            // Check if path ends with "Materials" (case-insensitive)
            return normalizedPath.EndsWith("/Materials", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedPath, "Materials", StringComparison.OrdinalIgnoreCase);
        }
    }
}
