using System;
using System.Collections.Generic;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing validation logic for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Checks if a model with the given name already exists in the repository.
        /// Performs case-insensitive comparison and trims whitespace.
        /// </summary>
        /// <param name="name">The model name to check (will be trimmed before comparison).</param>
        /// <returns>True if a model with this name exists, false otherwise.</returns>
        private bool ModelNameExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string trimmedName = name.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                return false;
            }

            return _existingModels.Any(entry => 
                entry != null && 
                !string.IsNullOrEmpty(entry.name) &&
                string.Equals(entry.name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a model with the same name and version already exists.
        /// Performs case-insensitive comparison and trims whitespace for both name and version.
        /// </summary>
        /// <param name="name">The model name to check (will be trimmed before comparison).</param>
        /// <param name="version">The version string to check (will be trimmed before comparison).</param>
        /// <returns>True if a model with this name and version exists, false otherwise.</returns>
        private bool ModelVersionExists(string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            string trimmedName = name.Trim();
            string trimmedVersion = version.Trim();

            if (string.IsNullOrEmpty(trimmedName) || string.IsNullOrEmpty(trimmedVersion))
            {
                return false;
            }

            return _existingModels.Any(entry =>
                entry != null &&
                !string.IsNullOrEmpty(entry.name) &&
                !string.IsNullOrEmpty(entry.latestVersion) &&
                string.Equals(entry.name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.latestVersion.Trim(), trimmedVersion, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates the current form state and returns a list of validation errors.
        /// Checks model name, version, path validity, changelog requirements, and duplicate detection.
        /// </summary>
        /// <returns>List of validation error messages. Empty list indicates valid form state.</returns>
        private List<string> GetValidationErrors()
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_name))
            {
                errors.Add("Model name is required");
            }
            else if (string.IsNullOrWhiteSpace(_version))
            {
                errors.Add("Version is required");
            }

            // Validate relative path
            List<string> pathErrors = PathUtils.ValidateRelativePath(_relativePath);
            errors.AddRange(pathErrors);

            // Validate changelog using comprehensive validator
            bool isUpdateMode = _mode == SubmitMode.Update;
            List<string> changelogErrors = ChangelogValidator.ValidateChangelog(_changeSummary, isUpdateMode);
            errors.AddRange(changelogErrors);

            if (_mode == SubmitMode.New)
            {
                // Check for duplicate model name
                if (ModelNameExists(_name))
                {
                    errors.Add($"Model '{_name}' already exists. Switch to 'Update Existing' to submit a new version.");
                }

                // Check for duplicate version (even for new models, in case of exact name match)
                if (ModelVersionExists(_name, _version))
                {
                    errors.Add($"Version {_version} already exists for model '{_name}'");
                }
            }
            else if (_mode == SubmitMode.Update)
            {
                if (_existingModels.Count == 0)
                {
                    errors.Add("No existing models available to update");
                }
                else
                {
                    ModelIndex.Entry selectedModel = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];

                    // Check for duplicate version
                    if (string.Equals(selectedModel.latestVersion, _version, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Version {_version} already exists for model '{selectedModel.name}'");
                    }
                    else if (SemVer.TryParse(selectedModel.latestVersion, out SemVer prev) && SemVer.TryParse(_version, out SemVer next))
                    {
                        if (next.CompareTo(prev) <= 0)
                        {
                            errors.Add($"New version must be greater than {selectedModel.latestVersion}");
                        }
                    }
                }
            }

            return errors;
        }

        private bool ShouldDisableSubmit(List<string> validationErrors, bool metadataReady)
        {
            if (_isSubmitting)
            {
                return true;
            }

            if (_mode == SubmitMode.Update && (!_existingModels.Any() || _isLoadingIndex || _loadingBaseMeta || !metadataReady))
            {
                return true;
            }

            return validationErrors.Count > 0;
        }
    }
}


