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
    /// Partial class containing all UI rendering methods for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Word-wrapped text area style for automatic line wrapping.
        /// </summary>
        private static GUIStyle _wordWrappedTextAreaStyle;

        /// <summary>
        /// Gets or creates the word-wrapped text area style.
        /// </summary>
        private static GUIStyle GetWordWrappedTextAreaStyle()
        {
            _wordWrappedTextAreaStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };
            return _wordWrappedTextAreaStyle;
        }
        /// <summary>
        /// Draws the Basic Info tab content (name, version, description, tags).
        /// </summary>
        private void DrawBasicInfoTab()
        {
            // Auto-save draft when fields change
            string previousName = _name;
            string previousVersion = _version;
            string previousDescription = _description;

            if (_mode == SubmitMode.New)
            {
                DrawNameField();
            }
            else
            {
                EditorGUILayout.LabelField("Model Name", UIStyles.SectionHeader);
                EditorGUILayout.LabelField(_name, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            }

            DrawVersionField();

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            EditorGUILayout.LabelField("Description", UIStyles.SectionHeader);
            // Constrain text area to available width and enable word wrapping for automatic line breaks
            Rect textAreaRect = GUILayoutUtility.GetRect(0, __TEXT_AREA_HEIGHT_DESCRIPTION, GUILayout.ExpandWidth(true));
            _description = EditorGUI.TextArea(textAreaRect, _description, GetWordWrappedTextAreaStyle());

            // Auto-save draft if fields changed
            if (previousName != _name || previousVersion != _version || previousDescription != _description)
            {
                SaveDraft();
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            EditorGUILayout.LabelField("Tags", UIStyles.SectionHeader);
            using (new EditorGUILayout.HorizontalScope())
            {
                _newTag = EditorGUILayout.TextField("Add Tag", _newTag);
                if (GUILayout.Button("Add", GUILayout.Width(__BUTTON_WIDTH_MEDIUM)) && !string.IsNullOrWhiteSpace(_newTag))
                {
                    _tags.Add(_newTag.Trim());
                    _newTag = string.Empty;
                    SaveDraft(); // Auto-save when tags change
                }
            }

            // Show tags list
            if (_tags.Count > 0)
            {
                EditorGUILayout.Space(UIConstants.SPACING_SMALL);
                for (int i = _tags.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("- " + _tags[i]);
                        if (GUILayout.Button("x", GUILayout.Width(__BUTTON_WIDTH_SMALL)))
                        {
                            _tags.RemoveAt(i);
                            SaveDraft(); // Auto-save when tags change
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No tags added. Tags help categorize and find your model.", MessageType.Info);
            }

            // Advanced tag options (progressive disclosure)
            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            _showAdvancedTagOptions = EditorGUILayout.Foldout(_showAdvancedTagOptions, "Advanced Tag Options", true);
            if (_showAdvancedTagOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Tag management options for power users.", MessageType.None);
                if (GUILayout.Button("Clear All Tags", GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM)))
                {
                    if (EditorUtility.DisplayDialog("Clear All Tags", "Are you sure you want to remove all tags?", "Yes", "No"))
                    {
                        _tags.Clear();
                        SaveDraft();
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws the Assets tab content (install path, relative path).
        /// </summary>
        private void DrawAssetsTab()
        {
            EditorGUILayout.LabelField(new GUIContent("Install Path", 
                "The absolute path where the model will be installed in YOUR Unity project.\n" +
                "Example: Assets/Models/MyModel\n\n" +
                "This is where users will find the model after importing it into their project."), UIStyles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Install Path: Where the model will be placed in YOUR Unity project.\n" +
                "This is the full path starting with 'Assets/' that users will see in their Project window.\n" +
                "Example: Assets/Models/MyModel", 
                MessageType.Info);
            string displayInstallPath = string.IsNullOrWhiteSpace(_installPath) ? DefaultInstallPath() : _installPath;
            string newInstallPath = EditorGUILayout.TextField(new GUIContent("Install Path", 
                "The absolute path where the model will be installed in your Unity project (e.g., Assets/Models/MyModel)"), 
                displayInstallPath);
            
            // Always update the field value to ensure it's captured
            if (newInstallPath != displayInstallPath || string.IsNullOrWhiteSpace(_installPath))
            {
                _installPath = newInstallPath;
                SaveDraft(); // Auto-save when path changes
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Relative Path with validation feedback
            EditorGUILayout.LabelField(new GUIContent("Relative Path", 
                "The path relative to the Assets folder where model files are stored in the repository.\n" +
                "Example: Models/MyModel\n\n" +
                "This is used for organizing files in the repository, not in your project."), UIStyles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Relative Path: How files are organized in the REPOSITORY (not in your project).\n" +
                "This is the path relative to the Assets folder, used for repository file organization.\n" +
                "Example: Models/MyModel\n\n" +
                "Note: This is different from Install Path - Install Path is for YOUR project, Relative Path is for the repository.", 
                MessageType.Info);
            DrawRelativePathField();

            // Advanced path options (progressive disclosure)
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            _showAdvancedPathOptions = EditorGUILayout.Foldout(_showAdvancedPathOptions, "Advanced Path Options", true);
            if (_showAdvancedPathOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Advanced path configuration options.", MessageType.None);

                if (GUILayout.Button("Reset to Default Path", GUILayout.Width(UIConstants.BUTTON_WIDTH_LARGE)))
                {
                    _installPath = DefaultInstallPath();
                    _relativePath = string.Empty;
                    SaveDraft();
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Current Default Path:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(DefaultInstallPath(), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws the Images tab content (preview images upload and management).
        /// </summary>
        private void DrawImagesTab()
        {
            EditorGUILayout.LabelField("Preview Images", UIStyles.SectionHeader);
            EditorGUILayout.HelpBox("Add preview images to showcase your model. Supported formats: PNG, JPG, TGA, PSD. Maximum file size: 50MB per image.", MessageType.Info);

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);

            // Drag and drop area for images
            DrawImageDropArea();

            if (GUILayout.Button("Add Images...", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
            {
                string chosen = EditorUtility.OpenFilePanelWithFilters("Choose images", Application.dataPath,
                    new string[] { "Image files", "png,jpg,jpeg,tga,psd", "All files", "*" });
                if (!string.IsNullOrEmpty(chosen))
                {
                    AddImageFile(chosen);
                }
            }

            EditorGUILayout.Space(10);

            if (_imageAbsPaths.Count > 0)
            {
                EditorGUILayout.LabelField($"Selected Images ({_imageAbsPaths.Count}):", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                for (int i = _imageAbsPaths.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        // Show image preview if possible
                        Texture2D preview = LoadImagePreview(_imageAbsPaths[i]);
                        if (preview != null)
                        {
                            GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
                        }
                        else
                        {
                            EditorGUI.DrawRect(GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64)), new Color(0.2f, 0.2f, 0.2f, 1f));
                            GUILayout.Label("No Preview", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(64), GUILayout.Height(64));
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            string fileName = Path.GetFileName(_imageAbsPaths[i]);
                            string fileSize = GetFileSizeString(_imageAbsPaths[i]);
                            EditorGUILayout.LabelField(fileName, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(fileSize, EditorStyles.miniLabel);
                        }

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Remove", GUILayout.Width(70), GUILayout.Height(30)))
                        {
                            _imageAbsPaths.RemoveAt(i);
                            SaveDraft(); // Auto-save when images change
                        }
                    }
                    EditorGUILayout.Space(4);
                }
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No images selected. Drag and drop image files here or click 'Add Images...' to browse.", MessageType.Info);
            }
        }

        /// <summary>
        /// Draws the Advanced tab content (changelog).
        /// </summary>
        private void DrawAdvancedTab()
        {
            EditorGUILayout.LabelField("Changelog", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Describe what changed in this version. This is required for updates and helps users understand what's new.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Change Summary with validation feedback
            DrawChangelogField();
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
            string displayRelativePath = string.IsNullOrWhiteSpace(_relativePath) ? GetDefaultRelativePath() : _relativePath;
            string newRelativePath = EditorGUILayout.TextField("Relative Path", displayRelativePath);

            // Restore original color
            GUI.color = originalColor;

            // Always update the field value to ensure it's captured
            if (newRelativePath != displayRelativePath || string.IsNullOrWhiteSpace(_relativePath))
            {
                _relativePath = newRelativePath;
                SaveDraft(); // Auto-save when path changes
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
        /// Draws the model name field with real-time validation feedback.
        /// </summary>
        private void DrawNameField()
        {
            bool hasError = string.IsNullOrWhiteSpace(_name);
            if (_mode == SubmitMode.New && !string.IsNullOrWhiteSpace(_name) && ModelNameExists(_name))
            {
                hasError = true;
            }

            Color originalColor = GUI.color;
            if (hasError && !string.IsNullOrWhiteSpace(_name))
            {
                GUI.color = Color.red;
            }

            _name = EditorGUILayout.TextField("Model Name", _name);
            GUI.color = originalColor;

            if (hasError)
            {
                EditorGUILayout.Space(2);
                if (string.IsNullOrWhiteSpace(_name))
                {
                    EditorGUILayout.HelpBox("Model name is required", MessageType.Error);
                }
                else if (_mode == SubmitMode.New && ModelNameExists(_name))
                {
                    EditorGUILayout.HelpBox($"Model '{_name}' already exists. Switch to 'Update Existing' to submit a new version.", MessageType.Warning);
                }
            }
        }

        /// <summary>
        /// Draws the version field with real-time validation feedback.
        /// </summary>
        private void DrawVersionField()
        {
            bool hasError = string.IsNullOrWhiteSpace(_version);
            string versionError = null;

            if (!hasError)
            {
                // Validate SemVer format
                if (!SemVer.TryParse(_version, out SemVer parsed))
                {
                    hasError = true;
                    versionError = "Version must be in SemVer format (e.g., 1.0.0)";
                }
                else if (_mode == SubmitMode.Update && _existingModels.Count > 0)
                {
                    ModelIndex.Entry selectedModel = _existingModels[Mathf.Clamp(_selectedModelIndex, 0, _existingModels.Count - 1)];
                    if (string.Equals(selectedModel.latestVersion, _version, StringComparison.OrdinalIgnoreCase))
                    {
                        hasError = true;
                        versionError = $"Version {_version} already exists for this model";
                    }
                    else if (SemVer.TryParse(selectedModel.latestVersion, out SemVer prev) && parsed.CompareTo(prev) <= 0)
                    {
                        hasError = true;
                        versionError = $"New version must be greater than {selectedModel.latestVersion}";
                    }
                }
                else if (_mode == SubmitMode.New && !string.IsNullOrWhiteSpace(_name) && ModelVersionExists(_name, _version))
                {
                    hasError = true;
                    versionError = $"Version {_version} already exists for model '{_name}'";
                }
            }

            Color originalColor = GUI.color;
            if (hasError && !string.IsNullOrWhiteSpace(_version))
            {
                GUI.color = Color.red;
            }

            _version = EditorGUILayout.TextField("Version (SemVer)", _version);
            GUI.color = originalColor;

            if (hasError)
            {
                EditorGUILayout.Space(2);
                if (string.IsNullOrWhiteSpace(_version))
                {
                    EditorGUILayout.HelpBox("Version is required", MessageType.Error);
                }
                else if (!string.IsNullOrEmpty(versionError))
                {
                    EditorGUILayout.HelpBox(versionError, MessageType.Error);
                }
            }
            else if (!string.IsNullOrWhiteSpace(_version))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("✓ Version format is valid", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Shows a non-intrusive notification message in the window.
        /// </summary>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message.</param>
        private void ShowNotification(string title, string message)
        {
            _notificationMessage = $"{title}: {message}";
            _notificationTime = DateTime.Now;
            Repaint();
        }

        /// <summary>
        /// Draws the notification message if one is active.
        /// </summary>
        private void DrawNotification()
        {
            if (string.IsNullOrEmpty(_notificationMessage))
            {
                return;
            }

            // Check if notification should expire
            if ((DateTime.Now - _notificationTime) > _notificationDuration)
            {
                _notificationMessage = null;
                return;
            }

            // Draw notification at top of window
            Rect notificationRect = new Rect(0, 0, position.width, 30);
            GUI.Box(notificationRect, "", EditorStyles.helpBox);

            Color originalColor = GUI.color;
            GUI.color = new Color(0.3f, 0.8f, 0.3f, 0.9f); // Green tint

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField(_notificationMessage, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    _notificationMessage = null;
                }
                GUILayout.Space(10);
            }

            GUI.color = originalColor;
        }

        /// <summary>
        /// Draws the changelog field with comprehensive validation feedback.
        /// </summary>
        private void DrawChangelogField()
        {
            EditorGUILayout.LabelField("Change Summary", EditorStyles.boldLabel);

            // Set a stable control name BEFORE drawing - Unity uses this to track focus
            GUI.SetNextControlName(__CHANGELOG_CONTROL_NAME);

            // Perform comprehensive validation
            bool isUpdateMode = _mode == SubmitMode.Update;
            List<string> validationErrors = ChangelogValidator.ValidateChangelog(_changeSummary, isUpdateMode);
            bool hasErrors = validationErrors != null && validationErrors.Count > 0;

            Color originalColor = GUI.color;
            if (hasErrors)
            {
                GUI.color = Color.red;
            }

            // Draw the text area
            // Constrain text area to available width and enable word wrapping for automatic line breaks
            Rect textAreaRect = GUILayoutUtility.GetRect(0, __TEXT_AREA_HEIGHT_CHANGELOG, GUILayout.ExpandWidth(true));
            string newChangeSummary = EditorGUI.TextArea(textAreaRect, _changeSummary ?? string.Empty, GetWordWrappedTextAreaStyle());
            GUI.color = originalColor;

            // Update the change summary when text changes
            if (newChangeSummary != _changeSummary)
            {
                _changeSummary = newChangeSummary;
                Repaint(); // Trigger repaint to update validation feedback
            }

            // Show validation errors
            if (hasErrors)
            {
                EditorGUILayout.Space(2);
                string errorMessage = string.Join("\n• ", validationErrors);
                EditorGUILayout.HelpBox($"Validation errors:\n• {errorMessage}", MessageType.Error);
            }
            else if (!string.IsNullOrWhiteSpace(_changeSummary))
            {
                // Show validation suggestions if changelog exists but could be improved
                List<string> suggestions = ChangelogValidator.GetValidationSuggestions(_changeSummary);
                if (suggestions != null && suggestions.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    string suggestionText = string.Join("\n", suggestions);
                    EditorGUILayout.HelpBox($"Suggestions:\n{suggestionText}", MessageType.Info);
                }
            }
            else if (isUpdateMode)
            {
                // For updates, changelog is required
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("Changelog is required for updates. Please describe what changed in this version.", MessageType.Warning);
            }
        }
    }
}


