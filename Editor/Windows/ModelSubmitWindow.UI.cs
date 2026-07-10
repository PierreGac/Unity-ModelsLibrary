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

        private static bool _tagBadgeStylesInitialized;
        private static GUIStyle _existingTagBadgeStyle;
        private static GUIStyle _existingTagBadgeAddedStyle;
        private static GUIStyle _selectedTagBadgeStyle;

        /// <summary>
        /// Initializes cached GUIStyles for tag badge buttons once.
        /// Creating GUIStyles during OnGUI causes severe hover/repaint slowdowns.
        /// </summary>
        private static void EnsureTagBadgeStyles()
        {
            if (_tagBadgeStylesInitialized)
            {
                return;
            }

            _tagBadgeStylesInitialized = true;

            _existingTagBadgeStyle = new GUIStyle(UIStyles.TagPill)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
            _existingTagBadgeStyle.normal.textColor = UIConstants.COLOR_LIGHT_BLUE;
            _existingTagBadgeStyle.hover.textColor = Color.white;
            _existingTagBadgeStyle.active.textColor = Color.white;

            _existingTagBadgeAddedStyle = new GUIStyle(UIStyles.TagPill)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _existingTagBadgeAddedStyle.normal.textColor = UIConstants.COLOR_GREEN;
            _existingTagBadgeAddedStyle.hover.textColor = UIConstants.COLOR_GREEN;
            _existingTagBadgeAddedStyle.active.textColor = UIConstants.COLOR_GREEN;

            _selectedTagBadgeStyle = new GUIStyle(UIStyles.TagPill)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _selectedTagBadgeStyle.normal.textColor = UIConstants.COLOR_GREEN;
            _selectedTagBadgeStyle.hover.textColor = UIConstants.COLOR_RED;
            _selectedTagBadgeStyle.active.textColor = UIConstants.COLOR_RED;
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
                    TryAddTagToModel(_newTag);
                    _newTag = string.Empty;
                }
            }

            DrawSelectedTagsList();
            DrawExistingTagsPicker();

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
                        MarkTagsDirty();
                        SaveDraft();
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws the tags currently assigned to the model being submitted.
        /// </summary>
        private void DrawSelectedTagsList()
        {
            if (_tags.Count == 0)
            {
                EditorGUILayout.HelpBox("No tags added yet. Pick from existing tags below or type a new one.", MessageType.Info);
                return;
            }

            EnsureTagBadgeStyles();

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            EditorGUILayout.LabelField("Selected Tags", EditorStyles.miniBoldLabel);

            int removeIndex = -1;
            DrawTagBadgeButtons(
                _tags,
                _selectedTagBadgeStyle,
                UIConstants.COLOR_GREEN,
                (int index) => removeIndex = index);

            if (removeIndex >= 0)
            {
                _tags.RemoveAt(removeIndex);
                MarkTagsDirty();
                SaveDraft();
            }
            else
            {
                EditorGUILayout.LabelField("Click a selected tag to remove it.", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Returns the viewport width available for wrapping tag badge rows.
        /// Must be called before entering a scroll view; inside a scroll view the layout
        /// width is unbounded and wrapping would never occur.
        /// </summary>
        private float GetTagBadgeWrapWidth()
        {
            const float VERTICAL_SCROLLBAR_WIDTH = 18f;
            const float LAYOUT_EDGE_PADDING = UIConstants.PADDING_LARGE * 2f;

            float availableWidth = GetHostEditorWindowWidth();
            availableWidth -= VERTICAL_SCROLLBAR_WIDTH + LAYOUT_EDGE_PADDING;
            return Mathf.Max(1f, availableWidth);
        }

        /// <summary>
        /// Resolves the width of the editor window hosting this UI.
        /// When rendered inside ModelLibraryWindow the hidden instance position is unset,
        /// so the parent library window position is used instead.
        /// </summary>
        private float GetHostEditorWindowWidth()
        {
            if (position.width > 1f)
            {
                return position.width;
            }

            ModelLibraryWindow libraryWindow = GetWindow<ModelLibraryWindow>(false, null, false);
            if (libraryWindow != null && libraryWindow.position.width > 1f)
            {
                return libraryWindow.position.width;
            }

            return EditorGUIUtility.currentViewWidth;
        }

        /// <summary>
        /// Returns whether the next badge should start a new row.
        /// </summary>
        /// <param name="currentLineWidth">Width already used on the current row.</param>
        /// <param name="nextButtonWidth">Width of the next badge button.</param>
        /// <param name="availableWidth">Total width available for one row.</param>
        private static bool ShouldWrapTagBadgeRow(float currentLineWidth, float nextButtonWidth, float availableWidth)
        {
            if (currentLineWidth <= 0f)
            {
                return false;
            }

            return currentLineWidth + __TAG_BADGE_HORIZONTAL_PADDING + nextButtonWidth > availableWidth;
        }

        /// <summary>
        /// Ends the current badge row and starts a new one.
        /// </summary>
        /// <param name="currentLineWidth">Current row width tracker to reset.</param>
        private static void BeginNextTagBadgeRow(ref float currentLineWidth)
        {
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            currentLineWidth = 0f;
        }

        /// <summary>
        /// Draws clickable badges for tags already used in the model catalog.
        /// </summary>
        private void DrawExistingTagsPicker()
        {
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            EditorGUILayout.LabelField("Existing Tags", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Click a tag to add it to this model.", EditorStyles.miniLabel);

            if (_isLoadingIndex)
            {
                EditorGUILayout.LabelField("Loading tags from catalog...", EditorStyles.miniLabel);
                return;
            }

            if (_tagCacheManager.SortedTags.Count == 0)
            {
                EditorGUILayout.HelpBox("No tags found in the catalog yet. Add a new tag above to create one.", MessageType.Info);
                return;
            }

            EnsureTagBadgeStyles();

            string tagToAdd = null;
            float availableWidth = GetTagBadgeWrapWidth();
            _existingTagsScroll = EditorGUILayout.BeginScrollView(
                _existingTagsScroll,
                GUILayout.MaxHeight(__EXISTING_TAGS_SCROLL_HEIGHT));

            EnsureCatalogTagBadgeCache();

            float currentLineWidth = 0f;
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _catalogTagBadgeCache.Count; i++)
            {
                CatalogTagBadgeEntry entry = _catalogTagBadgeCache[i];
                GUIStyle badgeStyle = entry.AlreadyAdded ? _existingTagBadgeAddedStyle : _existingTagBadgeStyle;
                Color badgeColor = entry.AlreadyAdded ? UIConstants.COLOR_GREEN : UIConstants.COLOR_LIGHT_BLUE;

                if (ShouldWrapTagBadgeRow(currentLineWidth, entry.ButtonWidth, availableWidth))
                {
                    BeginNextTagBadgeRow(ref currentLineWidth);
                }

                using (new EditorGUI.DisabledScope(entry.AlreadyAdded))
                {
                    if (DrawSingleTagBadgeButton(entry.Label, badgeStyle, badgeColor, entry.ButtonWidth) && !entry.AlreadyAdded)
                    {
                        tagToAdd = entry.Tag;
                    }
                }

                currentLineWidth += entry.ButtonWidth;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(tagToAdd))
            {
                TryAddTagToModel(tagToAdd);
            }
        }

        /// <summary>
        /// Draws a wrapping row of tag badge buttons from a string list.
        /// </summary>
        private void DrawTagBadgeButtons(
            IReadOnlyList<string> tags,
            GUIStyle badgeStyle,
            Color badgeColor,
            Action<int> onBadgeClicked)
        {
            float currentLineWidth = 0f;
            float availableWidth = GetTagBadgeWrapWidth();
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < tags.Count; i++)
            {
                string label = $"{__TAG_BADGE_EMOJI} {tags[i]}";
                float buttonWidth = badgeStyle.CalcSize(new GUIContent(label)).x + __TAG_BADGE_HORIZONTAL_PADDING;

                if (ShouldWrapTagBadgeRow(currentLineWidth, buttonWidth, availableWidth))
                {
                    BeginNextTagBadgeRow(ref currentLineWidth);
                }

                if (DrawSingleTagBadgeButton(label, badgeStyle, badgeColor, buttonWidth))
                {
                    onBadgeClicked?.Invoke(i);
                }

                currentLineWidth += buttonWidth;
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws one tag badge button with a tinted background.
        /// </summary>
        private static bool DrawSingleTagBadgeButton(string label, GUIStyle badgeStyle, Color badgeColor, float buttonWidth = 0f)
        {
            Color originalBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(
                badgeColor.r,
                badgeColor.g,
                badgeColor.b,
                UIConstants.BADGE_BACKGROUND_ALPHA);

            bool clicked;
            if (buttonWidth > 0f)
            {
                clicked = GUILayout.Button(label, badgeStyle, GUILayout.Width(buttonWidth));
            }
            else
            {
                clicked = GUILayout.Button(label, badgeStyle, GUILayout.ExpandWidth(false));
            }

            GUI.backgroundColor = originalBackground;
            return clicked;
        }

        /// <summary>
        /// Marks selected-tag caches dirty after the tag list changes.
        /// </summary>
        private void MarkTagsDirty()
        {
            _tagsRevision++;
        }

        /// <summary>
        /// Marks catalog-tag layout caches dirty after the catalog tag list changes.
        /// </summary>
        private void MarkCatalogTagsDirty()
        {
            _catalogTagsRevision++;
        }

        /// <summary>
        /// Rebuilds the case-insensitive lookup for selected tags when needed.
        /// </summary>
        private void EnsureSelectedTagsLookup()
        {
            if (_selectedTagsLookupRevision == _tagsRevision)
            {
                return;
            }

            _selectedTagsLookupRevision = _tagsRevision;
            _selectedTagsLookup.Clear();
            for (int i = 0; i < _tags.Count; i++)
            {
                string tag = _tags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    _selectedTagsLookup.Add(tag.Trim());
                }
            }
        }

        /// <summary>
        /// Rebuilds precomputed catalog tag badge layout data when tags or selection change.
        /// </summary>
        private void EnsureCatalogTagBadgeCache()
        {
            int revisionKey = (_catalogTagsRevision * 397) ^ _tagsRevision;
            if (_catalogTagBadgeCacheRevision == revisionKey)
            {
                return;
            }

            _catalogTagBadgeCacheRevision = revisionKey;
            _catalogTagBadgeCache.Clear();
            EnsureSelectedTagsLookup();

            for (int i = 0; i < _tagCacheManager.SortedTags.Count; i++)
            {
                string tag = _tagCacheManager.SortedTags[i];
                bool alreadyAdded = _selectedTagsLookup.Contains(tag.Trim());
                GUIStyle badgeStyle = alreadyAdded ? _existingTagBadgeAddedStyle : _existingTagBadgeStyle;
                string label = string.Concat(__TAG_BADGE_EMOJI, " ", tag);
                float buttonWidth = badgeStyle.CalcSize(new GUIContent(label)).x + __TAG_BADGE_HORIZONTAL_PADDING;

                CatalogTagBadgeEntry entry = new CatalogTagBadgeEntry
                {
                    Tag = tag,
                    Label = label,
                    ButtonWidth = buttonWidth,
                    AlreadyAdded = alreadyAdded
                };
                _catalogTagBadgeCache.Add(entry);
            }
        }

        /// <summary>
        /// Adds a tag to the submission list if it is not already present.
        /// </summary>
        /// <param name="tag">Tag value to add.</param>
        private void TryAddTagToModel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || IsTagAlreadyOnModel(tag))
            {
                return;
            }

            _tags.Add(tag.Trim());
            MarkTagsDirty();
            SaveDraft();
        }

        /// <summary>
        /// Checks whether a tag is already on the model submission list.
        /// </summary>
        /// <param name="tag">Tag value to check.</param>
        /// <returns>True when the tag is already selected.</returns>
        private bool IsTagAlreadyOnModel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            EnsureSelectedTagsLookup();
            return _selectedTagsLookup.Contains(tag.Trim());
        }

        /// <summary>
        /// Draws the Assets tab content (install path).
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


