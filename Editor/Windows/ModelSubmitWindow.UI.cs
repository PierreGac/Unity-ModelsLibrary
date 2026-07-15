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
            TagBadgeDrawer.DrawTagEditorPanel(
                _tags,
                ref _newTag,
                _tagPickerState,
                _tagCacheManager.SortedTags,
                _isLoadingIndex,
                TagBadgeDrawer.GetWrapWidth(this),
                ref _tagDuplicateWarning,
                ref _showAdvancedTagOptions,
                SaveDraft);
        }

        /// <summary>
        /// Draws the Assets tab content (model assets and install path).
        /// </summary>
        private void DrawAssetsTab()
        {
            EditorGUILayout.LabelField("Model Assets", UIStyles.SectionHeader);
            EditorGUILayout.HelpBox(
                "These project assets will be packaged with your submission. Add FBX/OBJ meshes plus related materials and textures.",
                MessageType.Info);

            DrawAssetDropArea();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add from Project Selection", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    AddAssetsFromProjectSelection();
                }

                if (GUILayout.Button("Clear Assets", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE), GUILayout.Width(UIConstants.BUTTON_WIDTH_LARGE)))
                {
                    _selectedAssetGuids.Clear();
                    _selectedAssetGuidLookup.Clear();
                    _assetPickerObject = null;
                    SaveDraft();
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);

            using (new EditorGUILayout.HorizontalScope())
            {
                _assetPickerObject = EditorGUILayout.ObjectField(
                    new GUIContent("Add Asset", "Pick a project asset or folder to include in the submission"),
                    _assetPickerObject,
                    typeof(UnityEngine.Object),
                    false);

                if (GUILayout.Button("Add", GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM), GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    if (TryAddUnityObject(_assetPickerObject))
                    {
                        _assetPickerObject = null;
                    }
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);

            if (_selectedAssetGuids.Count > 0)
            {
                EditorGUILayout.LabelField($"Selected Assets ({_selectedAssetGuids.Count}):", EditorStyles.boldLabel);
                _assetListScrollPosition = EditorGUILayout.BeginScrollView(
                    _assetListScrollPosition,
                    GUILayout.Height(__ASSET_LIST_SCROLL_HEIGHT));

                for (int i = 0; i < _selectedAssetGuids.Count; i++)
                {
                    string guid = _selectedAssetGuids[i];
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    UnityEngine.Object asset = string.IsNullOrEmpty(assetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        Texture preview = asset != null
                            ? AssetPreview.GetMiniThumbnail(asset)
                            : null;

                        if (preview != null)
                        {
                            GUILayout.Label(preview, GUILayout.Width(32), GUILayout.Height(32));
                        }
                        else
                        {
                            EditorGUI.DrawRect(
                                GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32)),
                                new Color(0.2f, 0.2f, 0.2f, 1f));
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            string displayName = string.IsNullOrEmpty(assetPath)
                                ? "(missing asset)"
                                : Path.GetFileName(assetPath);
                            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(
                                $"{GetAssetTypeLabel(guid)} • {assetPath}",
                                EditorStyles.miniLabel);
                        }

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Remove", GUILayout.Width(70), GUILayout.Height(30)))
                        {
                            RemoveSelectedAssetAt(i);
                            break;
                        }
                    }

                    EditorGUILayout.Space(4);
                }

                EditorGUILayout.EndScrollView();

                if (!SelectedAssetsContainMesh())
                {
                    EditorGUILayout.HelpBox(
                        "Add at least one FBX or OBJ mesh file to the asset list.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No assets selected. Use Project selection, drag-and-drop, or the asset picker above.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

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
            InstallPathValidator.ValidationResult installPathValidation = GetInstallPathValidation(displayInstallPath);
            bool hasInstallPathErrors = !installPathValidation.IsValid;

            Color originalGuiColor = GUI.color;
            if (hasInstallPathErrors)
            {
                GUI.color = Color.red;
            }

            string newInstallPath = EditorGUILayout.TextField(new GUIContent("Install Path", 
                "The absolute path where the model will be installed in your Unity project (e.g., Assets/Models/MyModel)"), 
                displayInstallPath);

            GUI.color = originalGuiColor;
            
            // Always update the field value to ensure it's captured
            if (newInstallPath != displayInstallPath || string.IsNullOrWhiteSpace(_installPath))
            {
                _installPath = newInstallPath;
                SaveDraft(); // Auto-save when path changes
            }

            if (hasInstallPathErrors)
            {
                EditorGUILayout.Space(2);
                for (int i = 0; i < installPathValidation.Errors.Count; i++)
                {
                    EditorGUILayout.HelpBox(installPathValidation.Errors[i], MessageType.Error);
                }

                if (!string.IsNullOrWhiteSpace(installPathValidation.SuggestedInstallPath))
                {
                    EditorGUILayout.HelpBox(
                        $"Suggested install path: {installPathValidation.SuggestedInstallPath}",
                        MessageType.Info);

                    if (GUILayout.Button("Use Suggested Install Path", GUILayout.Width(UIConstants.BUTTON_WIDTH_LARGE)))
                    {
                        _installPath = installPathValidation.SuggestedInstallPath;
                        SaveDraft();
                    }
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            _showAdvancedPathOptions = EditorGUILayout.Foldout(_showAdvancedPathOptions, "Advanced Path Options", true);
            if (_showAdvancedPathOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Advanced path configuration options.", MessageType.None);

                if (GUILayout.Button("Reset to Default Path", GUILayout.Width(UIConstants.BUTTON_WIDTH_LARGE)))
                {
                    _installPath = DefaultInstallPath();
                    SaveDraft();
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Current Default Path:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(DefaultInstallPath(), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws a drag-and-drop area for project assets.
        /// </summary>
        private void DrawAssetDropArea()
        {
            Event currentEvent = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));

            bool isDragging = currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform;
            bool isHovering = dropArea.Contains(currentEvent.mousePosition);

            Color originalColor = GUI.color;
            if (isDragging && isHovering)
            {
                GUI.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            }

            GUI.Box(dropArea, string.Empty, EditorStyles.helpBox);
            GUI.color = originalColor;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                string dropText = isDragging && isHovering
                    ? "Drop project assets here"
                    : "Drag and drop project assets or folders here";
                EditorGUILayout.LabelField(dropText, EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }

            if (!isHovering)
            {
                return;
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                bool hasValidAssets = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0;
                DragAndDrop.visualMode = hasValidAssets
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                currentEvent.Use();

                int addedCount = 0;
                if (DragAndDrop.objectReferences != null)
                {
                    for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                    {
                        if (TryAddUnityObject(DragAndDrop.objectReferences[i]))
                        {
                            addedCount++;
                        }
                    }
                }

                if (addedCount > 0)
                {
                    SaveDraft();
                }
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


