using System.IO;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for resolving Unity Editor-specific paths.
    /// Provides convenient access to project root and library paths.
    /// </summary>
    public static class EditorPaths
    {
        /// <summary>
        /// Gets the Unity project root directory (the folder containing Assets, Library, etc.).
        /// Calculated by removing "/Assets" from Application.dataPath.
        /// </summary>
        public static string projectRoot => Application.dataPath[..^"/Assets".Length];

        /// <summary>
        /// Combines the project root with a subdirectory path and sanitizes the result.
        /// Useful for creating paths in the Library folder or other project directories outside Assets.
        /// </summary>
        /// <param name="sub">Subdirectory path relative to project root.</param>
        /// <returns>Combined and sanitized absolute path.</returns>
        public static string LibraryPath(string sub) => PathUtils.SanitizePathSeparator(Path.Combine(projectRoot, sub));
    }
}



