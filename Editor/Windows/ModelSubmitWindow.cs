
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Wizard to submit a new model version based on current Project selection.
    /// </summary>
    public class ModelSubmitWindow : EditorWindow
    {
        private enum SubmitMode { New, Update }

        // UI Constants
        private const int __BUTTON_WIDTH_SMALL = 24;
        private const int __BUTTON_WIDTH_MEDIUM = 50;
        private const int __TEXT_AREA_HEIGHT_DESCRIPTION = 60;
        private const int __TEXT_AREA_HEIGHT_CHANGELOG = 40;

        // File Size Constants
        private const long __MAX_IMAGE_FILE_SIZE_BYTES = 50 * 1024 * 1024; // 50MB
        private const long __BYTES_PER_KILOBYTE = 1024;

        private SubmitMode _mode = SubmitMode.New;
        private ModelLibraryService _service;
        private readonly List<ModelIndex.Entry> _existingModels = new();
        private int _selectedModelIndex;
        private bool _isLoadingIndex;
        private bool _loadingBaseMeta;
        private bool _isSubmitting;
        private string _changeSummary = "Initial submission";
        private ModelMeta _latestSelectedMeta;

        private string _name = "New Model";
        private string _version = "1.0.0";
        private string _description = string.Empty;
        private string _installPath;
        private string _relativePath;
        private List<string> _imageAbsPaths = new();
        private List<string> _tags = new();
        private List<string> _projectTags = new();
        private string _newTag = string.Empty;
        private string _newProjectTag = string.Empty;
        private readonly IUserIdentityProvider _idProvider = new SimpleUserIdentityProvider();

        [MenuItem("Tools/Model Library/Submit Model")]
        public static void Open()
        {
            ModelSubmitWindow w = GetWindow<ModelSubmitWindow>("Submit Model");
            w.Show();
        }

        private void OnEnable()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);

            _service = new ModelLibraryService(repo);
            _installPath = DefaultInstallPath();
            _relativePath = GetDefaultRelativePath();
            _projectTags.Clear();
            _ = LoadIndexAsync();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Select your model assets in the Project window (FBX, materials, textures), then fill the form.", MessageType.Info);

            string[] modeLabels = { "New Model", "Update Existing" };
            int newModeIndex = GUILayout.Toolbar((int)_mode, modeLabels);
            if (newModeIndex != (int)_mode)
            {
                _mode = (SubmitMode)newModeIndex;
                OnModeChanged();
            }
            EditorGUILayout.Space();

            bool metadataReady = true;
            if (_mode == SubmitMode.Update)
            {
                metadataReady = DrawUpdateSelection();
            }
            else
            {
                _name = EditorGUILayout.TextField("Model Name", _name);
            }

            using (new EditorGUI.DisabledScope(_mode == SubmitMode.Update && !metadataReady))
            {
                _version = EditorGUILayout.TextField("Version (SemVer)", _version);
                _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(__TEXT_AREA_HEIGHT_DESCRIPTION));

                // Show validation errors in real-time
                List<string> uiValidationErrors = GetValidationErrors();
                if (uiValidationErrors.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Validation Issues:", MessageType.Warning);
                    foreach (string error in uiValidationErrors)
                    {
                        EditorGUILayout.HelpBox($"• {error}", MessageType.Warning);
                    }
                }

                EditorGUILayout.LabelField("Tags:", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newTag = EditorGUILayout.TextField("Add Tag", _newTag);
                    if (GUILayout.Button("Add", GUILayout.Width(__BUTTON_WIDTH_MEDIUM)) && !string.IsNullOrWhiteSpace(_newTag))
                    {
                        _tags.Add(_newTag.Trim());
                        _newTag = string.Empty;
                    }
                }

                for (int i = _tags.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("- " + _tags[i]);
                        if (GUILayout.Button("x", GUILayout.Width(__BUTTON_WIDTH_SMALL)))
                        {
                            _tags.RemoveAt(i);
                        }
                    }
                }

                _installPath = EditorGUILayout.TextField("Install Path", string.IsNullOrWhiteSpace(_installPath) ? DefaultInstallPath() : _installPath);

                // Relative Path with validation feedback
                DrawRelativePathField();

                EditorGUILayout.LabelField("Project Visibility", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newProjectTag = EditorGUILayout.TextField("Add Project", _newProjectTag);
                    if (GUILayout.Button("Add", GUILayout.Width(__BUTTON_WIDTH_MEDIUM)) && !string.IsNullOrWhiteSpace(_newProjectTag))
                    {
                        AddProjectTag(_newProjectTag);
                        _newProjectTag = string.Empty;
                    }
                }
                for (int i = _projectTags.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("- " + _projectTags[i]);
                        if (GUILayout.Button("x", GUILayout.Width(__BUTTON_WIDTH_SMALL)))
                        {
                            _projectTags.RemoveAt(i);
                        }
                    }
                }

                if (GUILayout.Button("Add Images..."))
                {
                    string chosen = EditorUtility.OpenFilePanelWithFilters("Choose images", Application.dataPath,
                        new string[] { "Image files", "png,jpg,jpeg,tga,psd", "All files", "*" });
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        if (IsValidImageFile(chosen))
                        {
                            if (!_imageAbsPaths.Contains(chosen))
                            {
                                _imageAbsPaths.Add(chosen);
                            }
                            else
                            {
                                Debug.LogWarning($"Image '{Path.GetFileName(chosen)}' is already selected.");
                            }
                        }
                        else
                        {
                            Debug.LogError($"Invalid image file: {Path.GetFileName(chosen)}");
                        }
                    }
                }
                if (_imageAbsPaths.Count > 0)
                {
                    EditorGUILayout.LabelField($"Selected Images ({_imageAbsPaths.Count}):", EditorStyles.boldLabel);
                    for (int i = _imageAbsPaths.Count - 1; i >= 0; i--)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string fileName = Path.GetFileName(_imageAbsPaths[i]);
                            string fileSize = GetFileSizeString(_imageAbsPaths[i]);
                            EditorGUILayout.LabelField($"• {fileName} ({fileSize})");
                            if (GUILayout.Button("x", GUILayout.Width(__BUTTON_WIDTH_SMALL)))
                            {
                                _imageAbsPaths.RemoveAt(i);
                            }
                        }
                    }
                }

                // Change Summary with validation feedback
                DrawChangelogField();
            }

            GUILayout.FlexibleSpace();

            // Enhanced submit button validation
            List<string> validationErrors = GetValidationErrors();
            bool hasValidationErrors = validationErrors.Count > 0;
            bool disableSubmit = _isSubmitting ||
                                (_mode == SubmitMode.Update && (!_existingModels.Any() || _isLoadingIndex || _loadingBaseMeta || !metadataReady)) ||
                                hasValidationErrors;

            using (new EditorGUI.DisabledScope(disableSubmit))
            {
                string buttonText = hasValidationErrors ? "Submit (Fix Issues First)" : "Submit";
                if (GUILayout.Button(buttonText))
                {
                    _ = Submit();
                }
            }
        }

        private void OnModeChanged()
        {
            if (_mode == SubmitMode.New)
            {
                if (string.IsNullOrWhiteSpace(_changeSummary))
                {
                    _changeSummary = "Initial submission";
                }
                _projectTags.Clear();
                _installPath = DefaultInstallPath();
                _relativePath = GetDefaultRelativePath();
            }
            else
            {
                _changeSummary = string.Empty;
                if (!_isLoadingIndex && _existingModels.Count > 0)
                {
                    _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1);
                    _ = LoadBaseMetaForSelection();
                }
            }
        }

        private async Task LoadIndexAsync()
        {
            _isLoadingIndex = true;
            try
            {
                ModelIndex index = await _service.GetIndexAsync();
                _existingModels.Clear();
                if (index?.entries != null)
                {
                    _existingModels.AddRange(index.entries.OrderBy(e => e.name));
                }

                if (_mode == SubmitMode.Update && _existingModels.Count > 0)
                {
                    _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1);
                    await LoadBaseMetaForSelection();
                }
                else
                {
                    _latestSelectedMeta = null;
                }
            }
            catch
            {
                _existingModels.Clear();
            }
            finally
            {
                _isLoadingIndex = false;
                Repaint();
            }
        }

        private bool DrawUpdateSelection()
        {
            if (_isLoadingIndex)
            {
                EditorGUILayout.HelpBox("Loading model catalog...", MessageType.Info);
                return false;
            }

            if (_existingModels.Count == 0)
            {
                EditorGUILayout.HelpBox("No existing models available to update.", MessageType.Warning);
                return false;
            }

            string[] options = _existingModels.Select(e => $"{e.name} (latest v{e.latestVersion})").ToArray();
            int newIndex = EditorGUILayout.Popup("Model", _selectedModelIndex, options);
            if (newIndex != _selectedModelIndex)
            {
                _selectedModelIndex = newIndex;
                _ = LoadBaseMetaForSelection();
            }

            ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
            EditorGUILayout.LabelField("Current Latest", entry.latestVersion);
            if (_loadingBaseMeta)
            {
                EditorGUILayout.LabelField("Loading metadata...", EditorStyles.miniLabel);
                return false;
            }

            _name = entry.name;
            return true;
        }

        private async Task LoadBaseMetaForSelection()
        {
            if (_existingModels.Count == 0)
            {
                _latestSelectedMeta = null;
                return;
            }

            ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
            _loadingBaseMeta = true;
            try
            {
                _latestSelectedMeta = await _service.GetMetaAsync(entry.id, entry.latestVersion);
                _name = entry.name;
                _description = _latestSelectedMeta?.description ?? string.Empty;
                _tags = new List<string>(_latestSelectedMeta?.tags?.values ?? new List<string>());
                _projectTags = new List<string>(_latestSelectedMeta?.projectTags ?? new List<string>());
                _installPath = _latestSelectedMeta?.installPath ?? DefaultInstallPath();
                _relativePath = _latestSelectedMeta?.relativePath ?? GetDefaultRelativePath();
                _version = SuggestNextVersion(entry.latestVersion);
            }
            catch
            {
                _latestSelectedMeta = null;
            }
            finally
            {
                _loadingBaseMeta = false;
                Repaint();
            }
        }

        private static string SuggestNextVersion(string current)
        {
            if (SemVer.TryParse(current, out SemVer parsed))
            {
                SemVer next = new SemVer(parsed.Major, parsed.Minor, parsed.Patch + 1);
                return next.ToString();
            }

            return current;
        }

        private async Task Submit()
        {
            if (_isSubmitting)
            {
                return;
            }

            // Final validation check (should not happen due to UI validation, but safety net)
            List<string> validationErrors = GetValidationErrors();
            if (validationErrors.Count > 0)
            {
                EditorUtility.DisplayDialog("Validation Error",
                    "Please fix the following issues before submitting:\n\n" + string.Join("\n", validationErrors), "OK");
                return;
            }

            string summary = string.IsNullOrWhiteSpace(_changeSummary)
                ? (_mode == SubmitMode.New ? "Initial submission" : "Updated assets")
                : _changeSummary.Trim();

            if (_mode == SubmitMode.Update && string.IsNullOrEmpty(summary))
            {
                EditorUtility.DisplayDialog("Missing Summary", "Please provide a changelog summary for the update.", "OK");
                return;
            }

            string identityId = null;
            string identityName = _name;
            string previousVersion = null;

            if (_mode == SubmitMode.Update)
            {
                ModelIndex.Entry entry = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
                identityId = entry.id;
                identityName = entry.name;
                previousVersion = entry.latestVersion;
            }

            string temp = null;
            _isSubmitting = true;
            string progressTitle = _mode == SubmitMode.Update ? "Submitting Update" : "Submitting Model";

            try
            {
                EditorUtility.DisplayProgressBar(progressTitle, "Preparing metadata...", 0.2f);
                ModelMeta meta = await ModelDeployer.BuildMetaFromSelectionAsync(identityName, identityId, _version, _description, _imageAbsPaths, _tags, _projectTags, _installPath, _relativePath, _idProvider);

                temp = Path.Combine(Path.GetTempPath(), "ModelSubmit_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                EditorUtility.DisplayProgressBar(progressTitle, "Copying files...", 0.5f);
                await ModelDeployer.MaterializeLocalVersionFolderAsync(meta, temp);

                foreach (string abs in _imageAbsPaths)
                {
                    try
                    {
                        string dst = Path.Combine(temp, "images", Path.GetFileName(abs));
                        File.Copy(abs, dst, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to copy image '{Path.GetFileName(abs)}': {ex.Message}");
                        throw new Exception($"Failed to copy image '{Path.GetFileName(abs)}': {ex.Message}");
                    }
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Uploading...", 0.8f);
                string remoteRel = await _service.SubmitNewVersionAsync(meta, temp, summary);
                await _service.RefreshIndexAsync();
                EditorUtility.DisplayDialog("Submitted", $"Uploaded to: {remoteRel}", "OK");

                if (_mode == SubmitMode.Update)
                {
                    _version = SuggestNextVersion(meta.version);
                    _changeSummary = string.Empty;
                    _ = LoadIndexAsync();
                }
                else
                {
                    _changeSummary = "Initial submission";
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Submission Failed", ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isSubmitting = false;
                if (!string.IsNullOrEmpty(temp))
                {
                    try { Directory.Delete(temp, true); } catch { }
                }
            }
        }

        private string DefaultInstallPath() => "Assets/Models/NewModel";

        private string GetDefaultRelativePath()
        {
            // Get the first selected asset's path as the default relative path
            string[] selected = Selection.assetGUIDs;
            if (selected != null && selected.Length > 0)
            {
                string firstAssetPath = AssetDatabase.GUIDToAssetPath(selected[0]);
                if (!string.IsNullOrEmpty(firstAssetPath))
                {
                    // Convert to relative path from Assets folder
                    if (firstAssetPath.StartsWith("Assets/"))
                    {
                        return firstAssetPath[7..]; // Remove "Assets/" prefix
                    }
                }
            }
            return "Models/NewModel";
        }

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
        /// Get validation errors for the current form state.
        /// </summary>
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

            // Validate changelog
            bool isUpdateMode = (_mode == SubmitMode.Update);
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

        private void AddProjectTag(string projectTag)
        {
            if (string.IsNullOrWhiteSpace(projectTag))
            {
                return;
            }

            string trimmed = projectTag.Trim();
            if (!_projectTags.Contains(trimmed))
            {
                _projectTags.Add(trimmed);
            }
        }

        /// <summary>
        /// Validates if the selected file is a valid image file.
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if the file is a valid image, false otherwise</returns>
        private static bool IsValidImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            // Check file extension
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };
            if (!validExtensions.Contains(extension))
            {
                return false;
            }

            // Check file size (max 50MB)
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > __MAX_IMAGE_FILE_SIZE_BYTES)
            {
                Debug.LogWarning($"Image file '{Path.GetFileName(filePath)}' is too large ({GetFileSizeString(filePath)}). Maximum size is 50MB.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a human-readable file size string.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Formatted file size string</returns>
        private static string GetFileSizeString(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return "Unknown";
            }


            long bytes = new FileInfo(filePath).Length;
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= __BYTES_PER_KILOBYTE && order < sizes.Length - 1)
            {
                order++;
                len /= __BYTES_PER_KILOBYTE;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Draws the relative path field with real-time validation feedback.
        /// </summary>
        private void DrawRelativePathField()
        {
            // Get validation errors for the current relative path
            List<string> pathErrors = PathUtils.ValidateRelativePath(_relativePath);
            bool hasPathErrors = pathErrors.Count > 0;

            // Set the text field color based on validation state
            Color originalColor = GUI.color;
            if (hasPathErrors)
            {
                GUI.color = Color.red;
            }

            // Draw the text field
            string newRelativePath = EditorGUILayout.TextField("Relative Path",
                string.IsNullOrWhiteSpace(_relativePath) ? GetDefaultRelativePath() : _relativePath);

            // Restore original color
            GUI.color = originalColor;

            // Update the field value
            if (newRelativePath != _relativePath)
            {
                _relativePath = newRelativePath;
            }

            // Show validation feedback
            if (hasPathErrors)
            {
                EditorGUILayout.Space(2);

                // Show error icon and message
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("⚠", "Path validation error"),
                        GUILayout.Width(20));

                    // Show the first error message
                    string firstError = pathErrors[0];
                    EditorGUILayout.LabelField(firstError, EditorStyles.helpBox);
                }

                // If there are multiple errors, show a tooltip with all errors
                if (pathErrors.Count > 1)
                {
                    EditorGUILayout.LabelField($"... and {pathErrors.Count - 1} more error(s)",
                        EditorStyles.miniLabel);
                }

                // Show helpful suggestions for common issues
                ShowPathValidationSuggestions(pathErrors);
            }
            else if (!string.IsNullOrWhiteSpace(_relativePath))
            {
                // Show success indicator for valid paths
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("✓", "Path is valid"),
                        GUILayout.Width(20));
                    EditorGUILayout.LabelField("Path is valid", EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>
        /// Shows helpful suggestions for common path validation errors.
        /// </summary>
        /// <param name="errors">List of validation errors</param>
        private static void ShowPathValidationSuggestions(List<string> errors)
        {
            bool showSuggestions = false;
            List<string> suggestions = new List<string>();

            foreach (string error in errors)
            {
                if (error.Contains("Materials"))
                {
                    suggestions.Add("• Use the parent folder instead (e.g., 'Models/Benne' instead of 'Models/Benne/Materials')");
                    showSuggestions = true;
                }
                else if (error.Contains("path traversal"))
                {
                    suggestions.Add("• Remove '..' and '~' characters from the path");
                    showSuggestions = true;
                }
                else if (error.Contains("reserved folder"))
                {
                    suggestions.Add("• Avoid using 'Editor', 'Resources', 'StreamingAssets', or 'Plugins' in the path");
                    showSuggestions = true;
                }
                else if (error.Contains("slash"))
                {
                    suggestions.Add("• Remove leading or trailing slashes from the path");
                    showSuggestions = true;
                }
            }

            if (showSuggestions)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Suggestions:", EditorStyles.boldLabel);
                foreach (string suggestion in suggestions)
                {
                    EditorGUILayout.LabelField(suggestion, EditorStyles.helpBox);
                }
            }
        }

        /// <summary>
        /// Draws the changelog field with real-time validation feedback.
        /// </summary>
        private void DrawChangelogField()
        {
            EditorGUILayout.LabelField("Change Summary", EditorStyles.boldLabel);

            // Get validation errors for the current changelog
            bool isUpdateMode = (_mode == SubmitMode.Update);
            List<string> changelogErrors = ChangelogValidator.ValidateChangelog(_changeSummary, isUpdateMode);
            bool hasChangelogErrors = changelogErrors.Count > 0;

            // Set the text area color based on validation state
            Color originalColor = GUI.color;
            if (hasChangelogErrors)
            {
                GUI.color = Color.red;
            }

            // Draw the text area
            string newChangeSummary = EditorGUILayout.TextArea(_changeSummary ?? string.Empty,
                GUILayout.MinHeight(__TEXT_AREA_HEIGHT_CHANGELOG));

            // Restore original color
            GUI.color = originalColor;

            // Update the field value
            if (newChangeSummary != _changeSummary)
            {
                _changeSummary = newChangeSummary;
            }

            // Show validation feedback
            if (hasChangelogErrors)
            {
                EditorGUILayout.Space(2);

                // Show error icon and message
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("⚠", "Changelog validation error"),
                        GUILayout.Width(20));

                    // Show the first error message
                    string firstError = changelogErrors[0];
                    EditorGUILayout.LabelField(firstError, EditorStyles.helpBox);
                }

                // If there are multiple errors, show a tooltip with all errors
                if (changelogErrors.Count > 1)
                {
                    EditorGUILayout.LabelField($"... and {changelogErrors.Count - 1} more error(s)",
                        EditorStyles.miniLabel);
                }

                // Show helpful suggestions for common issues
                ShowChangelogValidationSuggestions(changelogErrors);
            }
            else if (!string.IsNullOrWhiteSpace(_changeSummary))
            {
                // Show success indicator for valid changelogs
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("✓", "Changelog is valid"),
                        GUILayout.Width(20));
                    EditorGUILayout.LabelField("Changelog is valid", EditorStyles.miniLabel);
                }
            }

            // Show character count
            int currentLength = _changeSummary?.Length ?? 0;
            int maxLength = 1000; // From ChangelogValidator
            string lengthText = $"{currentLength}/{maxLength} characters";
            Color lengthColor = currentLength > maxLength ? Color.red :
                               currentLength < 10 ? Color.yellow : Color.green;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Character count:", EditorStyles.miniLabel);
                GUI.color = lengthColor;
                EditorGUILayout.LabelField(lengthText, EditorStyles.miniLabel);
                GUI.color = originalColor;
            }
        }

        // Changelog validation suggestion constants
        private const string __ERROR_TYPE_REQUIRED = "required";
        private const string __ERROR_TYPE_CHARACTERS = "characters";
        private const string __ERROR_TYPE_AT_LEAST = "at least";
        private const string __ERROR_TYPE_EXCEED = "exceed";
        private const string __ERROR_TYPE_PUNCTUATION = "punctuation";
        private const string __ERROR_TYPE_CAPITAL = "capital";
        private const string __ERROR_TYPE_MEANINGFUL = "meaningful";

        private const string __SUGGESTION_BULLET = "• ";
        private const string __SUGGESTIONS_LABEL = "Suggestions:";
        private const int __SUGGESTION_SPACING = 2;

        /// <summary>
        /// Shows helpful suggestions for common changelog validation errors.
        /// </summary>
        /// <param name="errors">List of validation errors</param>
        private static void ShowChangelogValidationSuggestions(List<string> errors)
        {
            bool showSuggestions = false;
            List<string> suggestions = new List<string>();

            for (int i = 0; i < errors.Count; i++)
            {
                string error = errors[i];
                if (error.Contains(__ERROR_TYPE_REQUIRED))
                {
                    suggestions.Add(__SUGGESTION_BULLET + "Provide a brief description of what changed");
                    suggestions.Add(__SUGGESTION_BULLET + "Include the reason for the update");
                    showSuggestions = true;
                }
                else if (error.Contains(__ERROR_TYPE_CHARACTERS))
                {
                    if (error.Contains(__ERROR_TYPE_AT_LEAST))
                    {
                        suggestions.Add(__SUGGESTION_BULLET + "Add more details about the changes made");
                        suggestions.Add(__SUGGESTION_BULLET + "Explain the impact or benefit of the update");
                    }
                    else if (error.Contains(__ERROR_TYPE_EXCEED))
                    {
                        suggestions.Add(__SUGGESTION_BULLET + "Consider shortening the description");
                        suggestions.Add(__SUGGESTION_BULLET + "Focus on the most important changes");
                    }
                    showSuggestions = true;
                }
                else if (error.Contains(__ERROR_TYPE_PUNCTUATION))
                {
                    suggestions.Add(__SUGGESTION_BULLET + "End the description with proper punctuation (. ! ?)");
                    showSuggestions = true;
                }
                else if (error.Contains(__ERROR_TYPE_CAPITAL))
                {
                    suggestions.Add(__SUGGESTION_BULLET + "Start the description with a capital letter");
                    showSuggestions = true;
                }
                else if (error.Contains(__ERROR_TYPE_MEANINGFUL))
                {
                    suggestions.Add(__SUGGESTION_BULLET + "Provide specific details about what was changed");
                    suggestions.Add(__SUGGESTION_BULLET + "Include version numbers or feature names");
                    showSuggestions = true;
                }
            }

            if (showSuggestions)
            {
                EditorGUILayout.Space(__SUGGESTION_SPACING);
                EditorGUILayout.LabelField(__SUGGESTIONS_LABEL, EditorStyles.boldLabel);
                for (int i = 0; i < suggestions.Count; i++)
                {
                    EditorGUILayout.LabelField(suggestions[i], EditorStyles.helpBox);
                }
            }
        }

    }
}


