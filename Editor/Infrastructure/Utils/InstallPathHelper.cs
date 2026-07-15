using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
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
        /// Uses installPath from meta, or falls back to a default path built from the model name.
        /// </summary>
        /// <param name="meta">The model metadata containing path information.</param>
        /// <returns>The determined install path, normalized to start with "Assets/".</returns>
        public string DetermineInstallPath(ModelMeta meta)
        {
            string modelName = meta?.identity?.name ?? "Model";
            string candidate;

            if (!string.IsNullOrWhiteSpace(meta?.installPath))
            {
                candidate = meta.installPath;
            }
            else
            {
                candidate = InstallPathUtils.BuildInstallPath(modelName);
            }

            return InstallPathUtils.NormalizeInstallPath(candidate) ?? InstallPathUtils.BuildInstallPath(modelName);
        }

        /// <summary>
        /// Resolves an install path for import using on-disk validation.
        /// Prompts the user to choose a folder when the stored path fails validation.
        /// </summary>
        /// <param name="meta">Model metadata containing the stored install path.</param>
        /// <param name="allowExistingModelContent">When true, existing model content at the path is allowed (update flow).</param>
        /// <returns>The resolved install path, or null if the user cancelled.</returns>
        public string ResolveInstallPathForImport(ModelMeta meta, bool allowExistingModelContent)
        {
            string modelName = meta?.identity?.name ?? "Model";
            string storedInstallPath = DetermineInstallPath(meta);
            InstallPathValidator.ValidationResult storedValidation = InstallPathValidator.Validate(
                storedInstallPath,
                modelName,
                allowExistingModelContent,
                InstallPathValidator.InstallPathValidationMode.Import);

            if (storedValidation.IsValid)
            {
                return !string.IsNullOrWhiteSpace(storedValidation.SuggestedInstallPath)
                    ? storedValidation.SuggestedInstallPath
                    : storedInstallPath;
            }

            ShowInvalidInstallPathDialog(storedInstallPath, storedValidation.Errors, storedValidation.SuggestedInstallPath, true);
            string promptDefault = !string.IsNullOrWhiteSpace(storedValidation.SuggestedInstallPath)
                ? storedValidation.SuggestedInstallPath
                : storedInstallPath;

            return PromptForInstallPathWithValidation(promptDefault, modelName, allowExistingModelContent);
        }

        /// <summary>
        /// Prompts the user to select an install folder and validates it for import.
        /// </summary>
        /// <param name="defaultInstallPath">The default path to show in the folder picker.</param>
        /// <param name="modelName">Model display name used for validation.</param>
        /// <param name="allowExistingModelContent">When true, existing model content at the path is allowed (update flow).</param>
        /// <returns>The selected relative path, or null if cancelled or invalid after repeated attempts.</returns>
        public string PromptForInstallPathWithValidation(
            string defaultInstallPath,
            string modelName,
            bool allowExistingModelContent)
        {
            string promptDefault = defaultInstallPath;

            while (true)
            {
                string selected = PromptForInstallPath(promptDefault);
                if (string.IsNullOrEmpty(selected))
                {
                    return null;
                }

                InstallPathValidator.ValidationResult validation = InstallPathValidator.Validate(
                    selected,
                    modelName,
                    allowExistingModelContent,
                    InstallPathValidator.InstallPathValidationMode.Import);

                if (validation.IsValid)
                {
                    return selected;
                }

                ShowInvalidInstallPathDialog(selected, validation.Errors, validation.SuggestedInstallPath, false);
                promptDefault = !string.IsNullOrWhiteSpace(validation.SuggestedInstallPath)
                    ? validation.SuggestedInstallPath
                    : selected;
            }
        }

        /// <summary>
        /// Prompts the user to select an install folder using a folder picker dialog.
        /// </summary>
        /// <param name="defaultInstallPath">The default path to show in the folder picker.</param>
        /// <returns>The selected relative path, or null if cancelled or invalid.</returns>
        public string PromptForInstallPath(string defaultInstallPath)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            string initialAbsolute = System.IO.Path.Combine(projectRoot, defaultInstallPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
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

        private static void ShowInvalidInstallPathDialog(
            string installPath,
            List<string> errors,
            string suggestedInstallPath,
            bool isStoredPath)
        {
            string title = isStoredPath ? "Invalid Stored Install Path" : "Invalid Install Path";
            string message = isStoredPath
                ? $"The stored install path '{installPath}' cannot be used:"
                : $"The selected install path '{installPath}' cannot be used:";

            if (errors != null && errors.Count > 0)
            {
                message = $"{message}\n\n{string.Join("\n", errors)}";
            }

            if (!string.IsNullOrWhiteSpace(suggestedInstallPath))
            {
                message = $"{message}\n\nSuggested path: {suggestedInstallPath}";
            }

            message = $"{message}\n\nPlease choose a folder for this import.";
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
