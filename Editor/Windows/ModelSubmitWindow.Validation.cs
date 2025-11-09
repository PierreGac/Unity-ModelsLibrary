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
        private bool ModelNameExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return _existingModels.Any(entry => entry != null && string.Equals(entry.name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if a model with the same name and version already exists.
        /// </summary>
        private bool ModelVersionExists(string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            return _existingModels.Any(entry =>
                entry != null &&
                string.Equals(entry.name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.latestVersion, version.Trim(), StringComparison.OrdinalIgnoreCase));
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

            // Validate changelog - simple check
            if (string.IsNullOrEmpty(_changeSummary))
            {
                errors.Add("Changelog is required");
            }

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


