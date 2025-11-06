using System.IO;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Helper class for install path operations that require Unity Editor dialogs.
    /// Handles determining default install paths and prompting users for custom paths.
    /// </summary>
    public class InstallPathHelper
    {
        /// <summary>
        /// Determines the install path for a model based on its metadata.
        /// Tries multiple sources in order: relativePath, installPath, or default path.
        /// </summary>
        /// <param name="meta">The model metadata containing path information.</param>
        /// <returns>The determined install path, normalized to start with "Assets/".</returns>
        public string DetermineInstallPath(ModelMeta meta)
        {
            string modelName = meta?.identity?.name ?? "Model";
            string candidate;

            // First try the relative path from meta
            if (!string.IsNullOrWhiteSpace(meta?.relativePath))
            {
                candidate = $"Assets/{meta.relativePath}";
            }
            // Then try the install path from meta
            else if (!string.IsNullOrWhiteSpace(meta?.installPath))
            {
                candidate = meta.installPath;
            }
            // Finally fall back to default
            else
            {
                candidate = InstallPathUtils.BuildInstallPath(modelName);
            }

            return InstallPathUtils.NormalizeInstallPath(candidate) ?? InstallPathUtils.BuildInstallPath(modelName);
        }

        /// <summary>
        /// Prompts the user to select an install folder using a folder picker dialog.
        /// </summary>
        /// <param name="defaultInstallPath">The default path to show in the folder picker.</param>
        /// <returns>The selected relative path, or null if cancelled or invalid.</returns>
        public string PromptForInstallPath(string defaultInstallPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string initialAbsolute = Path.Combine(projectRoot, defaultInstallPath.Replace('/', Path.DirectorySeparatorChar));
            string selected = EditorUtility.OpenFolderPanel("Choose install folder", initialAbsolute, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return null;
            }

            if (!InstallPathUtils.TryConvertAbsoluteToProjectRelative(selected, out string relative))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside this Unity project.", "OK");
                return null;
            }

            return InstallPathUtils.NormalizeInstallPath(relative);
        }
    }
}

