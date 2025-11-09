using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for controlling asset visibility in the Unity Project window.
    /// </summary>
    public static class AssetVisibilityUtility
    {
        /// <summary>
        /// Hides an asset from the Unity Project window by renaming it to start with a dot.
        /// Unity automatically hides files/folders that start with a dot from the Project window.
        /// Works with both absolute and relative paths (relative to Assets/).
        /// </summary>
        /// <param name="assetPath">Path to the asset (absolute or relative to Assets/).</param>
        public static void HideAssetFromProjectWindow(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            // Convert absolute path to relative if it's within Assets/
            string relativePath = assetPath;
            if (Path.IsPathRooted(assetPath))
            {
                // Normalize both paths for comparison
                string normalizedAssetPath = Path.GetFullPath(assetPath).Replace('\\', '/');
                string assetsPath = Path.GetFullPath("Assets").Replace('\\', '/');

                // Case-insensitive comparison on Windows
                if (normalizedAssetPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the relative portion
                    string relativePortion = normalizedAssetPath[assetsPath.Length..].TrimStart('/');
                    relativePath = "Assets/" + relativePortion;
                }
                else
                {
                    // Path is outside Assets/, can't hide it
                    Debug.LogWarning($"[AssetVisibilityUtility] Cannot hide asset outside Assets/: {assetPath}");
                    return;
                }
            }

            // Ensure path starts with Assets/ and normalize separators
            relativePath = relativePath.Replace('\\', '/');
            if (!relativePath.StartsWith("Assets/"))
            {
                Debug.LogWarning($"[AssetVisibilityUtility] Path does not start with Assets/: {relativePath}");
                return;
            }

            // Check if the file already starts with a dot (already hidden)
            string fileName = Path.GetFileName(relativePath);
            if (fileName.StartsWith("."))
            {
                Debug.Log($"[AssetVisibilityUtility] Asset {relativePath} is already hidden (starts with dot)");
                return;
            }

            // Get absolute paths for file operations
            string absolutePath = Path.GetFullPath(relativePath);
            string directory = Path.GetDirectoryName(absolutePath);
            string newFileName = "." + fileName;
            string newAbsolutePath = Path.Combine(directory, newFileName);
            string newRelativePath = Path.GetDirectoryName(relativePath) + "/" + newFileName;
            newRelativePath = newRelativePath.Replace('\\', '/');

            // Verify the original file exists
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning($"[AssetVisibilityUtility] File does not exist: {absolutePath}");
                return;
            }

            // Rename the file to start with a dot (Unity hides files starting with dot)
            try
            {
                // Move the file
                File.Move(absolutePath, newAbsolutePath);

                // Also move the meta file if it exists
                string metaPath = absolutePath + ".meta";
                string newMetaPath = newAbsolutePath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Move(metaPath, newMetaPath);
                }

                // Refresh AssetDatabase to pick up the rename
                AssetDatabase.Refresh();

                Debug.Log($"[AssetVisibilityUtility] Successfully hid asset by renaming: {relativePath} -> {newRelativePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetVisibilityUtility] Failed to rename file to hide it: {ex.Message}");
            }
        }
    }
}

