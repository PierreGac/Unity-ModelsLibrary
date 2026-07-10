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
        /// Validates an install path for common format issues.
        /// </summary>
        /// <param name="installPath">Install path starting with Assets/.</param>
        /// <returns>List of validation error messages, empty if valid.</returns>
        public static List<string> ValidateInstallPath(string installPath)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(installPath))
            {
                errors.Add("Install path is required");
                return errors;
            }

            string trimmedPath = installPath.Trim();
            string sanitizedPath = SanitizePathSeparator(trimmedPath);

            if (!sanitizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Install path must start with 'Assets/'");
            }

            char[] invalidChars = Path.GetInvalidPathChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                char invalidChar = invalidChars[i];
                if (trimmedPath.Contains(invalidChar))
                {
                    errors.Add($"Install path contains invalid character: '{invalidChar}'");
                    break;
                }
            }

            if (sanitizedPath.Contains("..") || sanitizedPath.Contains("~"))
            {
                errors.Add("Install path cannot contain '..' or '~' (path traversal not allowed)");
            }

            if (sanitizedPath.EndsWith("/"))
            {
                errors.Add("Install path should not end with a slash");
            }

            const int MAX_INSTALL_PATH_LENGTH = 260;
            if (sanitizedPath.Length > MAX_INSTALL_PATH_LENGTH)
            {
                errors.Add($"Install path is too long (maximum {MAX_INSTALL_PATH_LENGTH} characters)");
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

        /// <summary>
        /// Normalizes a path by converting to absolute path, resolving relative components, and normalizing separators.
        /// This ensures consistent path handling across different operating systems and handles edge cases.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>Normalized absolute path</returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            try
            {
                // Get the full absolute path (resolves relative paths, .., etc.)
                string fullPath = Path.GetFullPath(path);

                // Normalize path separators to match the current OS
                fullPath = fullPath.Replace('/', Path.DirectorySeparatorChar);
                fullPath = fullPath.Replace('\\', Path.DirectorySeparatorChar);

                // Remove trailing separators (except for root paths like C:\)
                if (fullPath.Length > 1 && fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    // Don't remove trailing separator for root paths (C:\, \\server\share)
                    if (!IsRootPath(fullPath))
                    {
                        fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar);
                    }
                }

                return fullPath;
            }
            catch
            {
                // If path normalization fails (e.g., invalid characters), return the original path
                // This prevents exceptions from breaking the calling code
                return path;
            }
        }

        /// <summary>
        /// Checks if a path is a root path (e.g., C:\ or \\server\share).
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is a root path</returns>
        private static bool IsRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Check for Windows drive root (C:\, D:\, etc.)
            if (path.Length == 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == Path.DirectorySeparatorChar)
            {
                return true;
            }

            // Check for UNC root (\\server\share)
            if (path.StartsWith(@"\\") && path.IndexOf(Path.DirectorySeparatorChar, 2) > 0)
            {
                int secondSlash = path.IndexOf(Path.DirectorySeparatorChar, 2);
                // If there's no third slash, it's a root UNC path
                if (secondSlash == path.Length - 1 || path.IndexOf(Path.DirectorySeparatorChar, secondSlash + 1) < 0)
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================
        // CRITICAL SECURITY HELPERS (added in Phase 1 - CRIT-01/02/04 + HIGH-01)
        // =====================================================================
        //
        // These helpers prevent path-traversal attacks where untrusted strings
        // (modelId, version, relativePath from models_index.json or model.json)
        // flow into Path.Combine and escape the intended root directory.
        //
        // See audit findings CRIT-01, CRIT-02, CRIT-04, HIGH-01.
        // =====================================================================

        /// <summary>
        /// Validates that a string is safe to use as a single path segment
        /// (e.g., a modelId or version). Rejects anything containing path
        /// separators, parent-directory traversal, drive separators, or NUL.
        /// </summary>
        /// <param name="identifier">The identifier to validate (e.g., modelId, version).</param>
        /// <returns><c>true</c> if the identifier is safe; <c>false</c> otherwise.</returns>
        /// <remarks>
        /// SECURITY: This method is the primary defense against path traversal
        /// via untrusted modelId/version values. It MUST be called on every
        /// identifier that comes from models_index.json or model.json before
        /// it is used in any Path.Combine or filesystem operation.
        /// </remarks>
        public static bool IsSafeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            if (identifier.Length > 128)
            {
                return false;
            }

            // Reject any path separator, parent traversal, drive separator, or NUL
            foreach (char c in identifier)
            {
                if (c == '/' || c == '\\' || c == ':' || c == '\0')
                {
                    return false;
                }
            }

            if (identifier.Contains(".."))
            {
                return false;
            }

            // Reject reserved Windows device names (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
            string upper = identifier.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return false;
            }
            if (upper.Length >= 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")))
            {
                if (char.IsDigit(upper[3]) && (upper.Length == 4 || upper[4] == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Canonicalizes both paths and asserts that <paramref name="path"/>
        /// is located inside <paramref name="root"/>. Throws
        /// <see cref="InvalidOperationException"/> if the path escapes the root.
        /// </summary>
        /// <param name="path">The path to verify (absolute or relative to current directory).</param>
        /// <param name="root">The root directory that must contain <paramref name="path"/>.</param>
        /// <returns>The canonicalized absolute path of <paramref name="path"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="path"/> escapes <paramref name="root"/>.</exception>
        /// <remarks>
        /// SECURITY: This is the containment check that closes the path-traversal
        /// findings CRIT-01, CRIT-02, CRIT-04, and HIGH-01. Call it on every
        /// path computed from untrusted metadata immediately before any
        /// filesystem operation (Directory.CreateDirectory, File.WriteAllBytes,
        /// Directory.Delete, File.Copy, etc.).
        /// </remarks>
        public static string AssertInsideRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("AssertInsideRoot: path is null or empty.");
            }
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException("AssertInsideRoot: root is null or empty.");
            }

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);

            // Ensure the root ends with a separator so that
            // "/foo/bar" is not incorrectly considered inside "/foo/ba".
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            // Use Ordinal (case-sensitive) on Linux/macOS, OrdinalIgnoreCase on Windows.
            // For editor tools the simplest portable choice is OrdinalIgnoreCase,
            // which is correct on Windows and conservative on case-sensitive FS
            // (it may reject a path that would actually be safe, but never accept
            // an unsafe one). Callers who need case-sensitive behavior can call
            // the overload below.
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Also accept the case where path == root exactly (no trailing separator).
                if (!string.Equals(fullPath, fullRoot.TrimEnd(Path.DirectorySeparatorChar),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Refusing path outside allowed root.\nPath: {fullPath}\nRoot: {fullRoot}");
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Variant of <see cref="AssertInsideRoot(string, string)"/> that allows
        /// the caller to choose the comparison type.
        /// </summary>
        public static string AssertInsideRoot(string path, string root, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("AssertInsideRoot: path is null or empty.");
            }
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException("AssertInsideRoot: root is null or empty.");
            }

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), comparison))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            if (!fullPath.StartsWith(fullRoot, comparison)
                && !string.Equals(fullPath, fullRoot.TrimEnd(Path.DirectorySeparatorChar), comparison))
            {
                throw new InvalidOperationException(
                    $"Refusing path outside allowed root.\nPath: {fullPath}\nRoot: {fullRoot}");
            }

            return fullPath;
        }

        /// <summary>
        /// Validates that a relative path does not contain parent-directory
        /// traversal segments ("..") after normalization. Returns the
        /// normalized form, or throws if the path would escape its base.
        /// </summary>
        /// <param name="relativePath">The relative path to validate (e.g., from model.json).</param>
        /// <returns>The sanitized relative path with forward slashes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the relative path contains ".." segments.</exception>
        public static string ValidateRelativePathStrict(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException("Relative path is empty.");
            }

            string sanitized = SanitizePathSeparator(relativePath);

            // Reject any ".. segment" — splits on / and checks each segment.
            string[] segments = sanitized.Split('/');
            foreach (string segment in segments)
            {
                if (segment == "..")
                {
                    throw new InvalidOperationException(
                        $"Relative path contains a parent-directory traversal segment ('..'): '{relativePath}'");
                }
            }

            return sanitized;
        }
    }
}
