using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
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
        /// Results are cached until submission inputs change so hover repaints stay fast.
        /// </summary>
        /// <returns>List of validation error messages. Empty list indicates valid form state.</returns>
        private List<string> GetValidationErrors()
        {
            string cacheKey = BuildValidationCacheKey();
            if (cacheKey == _validationCacheKey)
            {
                return _cachedValidationErrors;
            }

            _validationCacheKey = cacheKey;
            _cachedValidationErrors.Clear();
            BuildValidationErrors(_cachedValidationErrors);
            return _cachedValidationErrors;
        }

        /// <summary>
        /// Builds a cache key from all inputs that affect form validation.
        /// </summary>
        /// <returns>Stable cache key string.</returns>
        private string BuildValidationCacheKey()
        {
            string selectedModelVersion = string.Empty;
            if (_mode == SubmitMode.Update && _existingModels.Count > 0)
            {
                int selectedIndex = Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1);
                ModelIndex.Entry selectedModel = _existingModels[selectedIndex];
                selectedModelVersion = selectedModel?.latestVersion ?? string.Empty;
            }

            return string.Concat(
                ((int)_mode).ToString(), "|",
                _name ?? string.Empty, "|",
                _version ?? string.Empty, "|",
                _installPath ?? string.Empty, "|",
                _changeSummary ?? string.Empty, "|",
                _selectedModelIndex.ToString(), "|",
                _existingModels.Count.ToString(), "|",
                selectedModelVersion, "|",
                _isLoadingIndex.ToString(), "|",
                _loadingBaseMeta.ToString(), "|",
                _latestSelectedMeta?.installPath ?? string.Empty, "|",
                BuildSelectedAssetGuidsCacheKey());
        }

        /// <summary>
        /// Validates the install path and caches the result until inputs change.
        /// </summary>
        /// <param name="installPathToValidate">Install path to validate.</param>
        /// <returns>Cached install path validation result.</returns>
        private InstallPathValidator.ValidationResult GetInstallPathValidation(string installPathToValidate)
        {
            bool allowExistingModelContent = false;
            if (_mode == SubmitMode.Update && _latestSelectedMeta != null && !string.IsNullOrWhiteSpace(_latestSelectedMeta.installPath))
            {
                string normalizedCurrent = InstallPathUtils.NormalizeInstallPath(installPathToValidate);
                string normalizedExisting = InstallPathUtils.NormalizeInstallPath(_latestSelectedMeta.installPath);
                allowExistingModelContent = string.Equals(normalizedCurrent, normalizedExisting, StringComparison.OrdinalIgnoreCase);
            }

            string cacheKey = string.Concat(
                installPathToValidate ?? string.Empty, "|",
                _name ?? string.Empty, "|",
                allowExistingModelContent.ToString());

            if (cacheKey == _installPathValidationCacheKey)
            {
                return _cachedInstallPathValidation;
            }

            _installPathValidationCacheKey = cacheKey;
            _cachedInstallPathValidation = InstallPathValidator.Validate(
                installPathToValidate,
                _name,
                allowExistingModelContent,
                InstallPathValidator.InstallPathValidationMode.Submission);
            return _cachedInstallPathValidation;
        }

        /// <summary>
        /// Populates a validation error list from the current form state.
        /// </summary>
        /// <param name="errors">List to populate with validation errors.</param>
        private void BuildValidationErrors(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                errors.Add("Model name is required");
            }
            else if (string.IsNullOrWhiteSpace(_version))
            {
                errors.Add("Version is required");
            }

            string installPathToValidate = string.IsNullOrWhiteSpace(_installPath) ? DefaultInstallPath() : _installPath.Trim();
            InstallPathValidator.ValidationResult installPathResult = GetInstallPathValidation(installPathToValidate);
            if (!installPathResult.IsValid)
            {
                for (int i = 0; i < installPathResult.Errors.Count; i++)
                {
                    errors.Add(installPathResult.Errors[i]);
                }

                if (!string.IsNullOrWhiteSpace(installPathResult.SuggestedInstallPath))
                {
                    errors.Add($"Suggested install path: {installPathResult.SuggestedInstallPath}");
                }
            }

            bool isUpdateMode = _mode == SubmitMode.Update;
            List<string> changelogErrors = ChangelogValidator.ValidateChangelog(_changeSummary, isUpdateMode);
            errors.AddRange(changelogErrors);

            if (!SelectedAssetsContainMesh())
            {
                errors.Add("Please add at least one FBX or OBJ mesh file to the asset list");
            }

            if (_mode == SubmitMode.New)
            {
                if (ModelNameExists(_name))
                {
                    errors.Add($"Model '{_name}' already exists. Switch to 'Update Existing' to submit a new version.");
                }

                if (ModelVersionExists(_name, _version))
                {
                    errors.Add($"Version {_version} already exists for model '{_name}'");
                }
            }
            else if (_mode == SubmitMode.Update)
            {
                if (_isLoadingIndex)
                {
                    errors.Add("Model catalog is still loading. Please wait.");
                }
                else if (_loadingBaseMeta)
                {
                    errors.Add("Model metadata is still loading. Please wait.");
                }
                else if (_existingModels.Count == 0)
                {
                    errors.Add("No existing models available to update");
                }
                else
                {
                    ModelIndex.Entry selectedModel = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];

                    if (selectedModel == null)
                    {
                        errors.Add("Selected model entry is invalid");
                        return;
                    }

                    if (string.IsNullOrEmpty(selectedModel.latestVersion))
                    {
                        errors.Add("Selected model has no version information");
                    }
                    else if (string.Equals(selectedModel.latestVersion, _version, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Version {_version} already exists for model '{selectedModel.name ?? "Unknown"}'");
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
        }
    }
}


