using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Shared IMGUI drawing for emoji tag badge display and tag picker editing.
    /// Used by ModelSubmitWindow and ModelDetailsWindow.
    /// </summary>
    public static class TagBadgeDrawer
    {
        private const float VERTICAL_SCROLLBAR_WIDTH = 18f;
        private const float LAYOUT_EDGE_PADDING = UIConstants.PADDING_LARGE * 2f;
        private const int ADD_TAG_BUTTON_WIDTH = 50;

        private static bool _stylesInitialized;
        private static GUIStyle _readOnlyTagBadgeStyle;
        private static GUIStyle _existingTagBadgeStyle;
        private static GUIStyle _existingTagBadgeAddedStyle;
        private static GUIStyle _selectedTagBadgeStyle;

        /// <summary>
        /// Initializes cached GUIStyles for tag badge buttons once.
        /// Creating GUIStyles during OnGUI causes severe hover/repaint slowdowns.
        /// </summary>
        public static void EnsureStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            _stylesInitialized = true;

            _readOnlyTagBadgeStyle = new GUIStyle(UIStyles.TagPill)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
            _readOnlyTagBadgeStyle.normal.textColor = UIConstants.COLOR_LIGHT_BLUE;

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
        /// Resolves the width available for wrapping tag badge rows.
        /// </summary>
        /// <param name="host">The editor window hosting the tag UI.</param>
        /// <returns>Available width in pixels.</returns>
        public static float GetWrapWidth(EditorWindow host)
        {
            float availableWidth = GetHostEditorWindowWidth(host);
            availableWidth -= VERTICAL_SCROLLBAR_WIDTH + LAYOUT_EDGE_PADDING;
            return Mathf.Max(1f, availableWidth);
        }

        /// <summary>
        /// Draws non-interactive emoji tag badges for read-only display.
        /// </summary>
        /// <param name="tags">Tags to display.</param>
        /// <param name="wrapWidth">Width available for wrapping rows.</param>
        public static void DrawReadOnlyBadges(IReadOnlyList<string> tags, float wrapWidth)
        {
            if (tags == null || tags.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                return;
            }

            EnsureStyles();
            DrawBadgeRow(
                tags,
                _readOnlyTagBadgeStyle,
                UIConstants.COLOR_LIGHT_BLUE,
                wrapWidth,
                null);
        }

        /// <summary>
        /// Draws the full tag editor panel: add field, selected tags, catalog picker, and optional clear-all.
        /// </summary>
        /// <param name="selectedTags">Mutable list of tags on the model.</param>
        /// <param name="newTagInput">Text field for adding a custom tag.</param>
        /// <param name="pickerState">Cached picker state for catalog layout.</param>
        /// <param name="catalogTags">Sorted catalog tags from the model index.</param>
        /// <param name="isLoadingCatalog">Whether the catalog is still loading.</param>
        /// <param name="wrapWidth">Width available for wrapping badge rows.</param>
        /// <param name="duplicateWarning">Warning message when a duplicate tag add is attempted.</param>
        /// <param name="showAdvancedOptions">Whether the advanced options foldout is expanded.</param>
        /// <param name="onTagsChanged">Callback invoked when tags are modified.</param>
        public static void DrawTagEditorPanel(
            List<string> selectedTags,
            ref string newTagInput,
            TagPickerState pickerState,
            IReadOnlyList<string> catalogTags,
            bool isLoadingCatalog,
            float wrapWidth,
            ref string duplicateWarning,
            ref bool showAdvancedOptions,
            Action onTagsChanged)
        {
            string previousNewTag = newTagInput;
            using (new EditorGUILayout.HorizontalScope())
            {
                newTagInput = EditorGUILayout.TextField("Add Tag", newTagInput);
                if (GUILayout.Button("Add", GUILayout.Width(ADD_TAG_BUTTON_WIDTH)) && !string.IsNullOrWhiteSpace(newTagInput))
                {
                    if (TryAddTag(selectedTags, newTagInput, ref duplicateWarning, pickerState, onTagsChanged))
                    {
                        newTagInput = string.Empty;
                    }
                }
            }

            if (!string.Equals(previousNewTag, newTagInput, StringComparison.Ordinal))
            {
                duplicateWarning = null;
            }

            if (!string.IsNullOrEmpty(duplicateWarning))
            {
                EditorGUILayout.HelpBox(duplicateWarning, MessageType.Warning);
            }

            DrawSelectedTagsEditor(selectedTags, pickerState, wrapWidth, onTagsChanged);
            DrawExistingTagsPicker(catalogTags, selectedTags, pickerState, wrapWidth, isLoadingCatalog, onTagsChanged);

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Tag Options", true);
            if (showAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Tag management options for power users.", MessageType.None);
                if (GUILayout.Button("Clear All Tags", GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM)))
                {
                    if (EditorUtility.DisplayDialog("Clear All Tags", "Are you sure you want to remove all tags?", "Yes", "No"))
                    {
                        selectedTags.Clear();
                        pickerState.MarkTagsDirty();
                        duplicateWarning = null;
                        onTagsChanged?.Invoke();
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws selected tags with click-to-remove badges.
        /// </summary>
        /// <param name="selectedTags">Mutable list of selected tags.</param>
        /// <param name="pickerState">Cached picker state.</param>
        /// <param name="wrapWidth">Width available for wrapping rows.</param>
        /// <param name="onTagsChanged">Callback invoked when a tag is removed.</param>
        public static void DrawSelectedTagsEditor(
            List<string> selectedTags,
            TagPickerState pickerState,
            float wrapWidth,
            Action onTagsChanged)
        {
            if (selectedTags == null || selectedTags.Count == 0)
            {
                EditorGUILayout.HelpBox("No tags added yet. Pick from existing tags below or type a new one.", MessageType.Info);
                return;
            }

            EnsureStyles();

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            EditorGUILayout.LabelField("Selected Tags", EditorStyles.miniBoldLabel);

            int removeIndex = -1;
            DrawBadgeRow(
                selectedTags,
                _selectedTagBadgeStyle,
                UIConstants.COLOR_GREEN,
                wrapWidth,
                (int index) => removeIndex = index);

            if (removeIndex >= 0)
            {
                selectedTags.RemoveAt(removeIndex);
                pickerState.MarkTagsDirty();
                onTagsChanged?.Invoke();
            }
            else
            {
                EditorGUILayout.LabelField("Click a selected tag to remove it.", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Draws clickable badges for tags already used in the model catalog.
        /// </summary>
        /// <param name="catalogTags">Sorted catalog tags.</param>
        /// <param name="selectedTags">Currently selected tags on the model.</param>
        /// <param name="pickerState">Cached picker state.</param>
        /// <param name="wrapWidth">Width available for wrapping rows.</param>
        /// <param name="isLoadingCatalog">Whether the catalog is still loading.</param>
        /// <param name="onTagsChanged">Callback invoked when a catalog tag is added.</param>
        public static void DrawExistingTagsPicker(
            IReadOnlyList<string> catalogTags,
            List<string> selectedTags,
            TagPickerState pickerState,
            float wrapWidth,
            bool isLoadingCatalog,
            Action onTagsChanged)
        {
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            EditorGUILayout.LabelField("Existing Tags", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Click a tag to add it to this model.", EditorStyles.miniLabel);

            if (isLoadingCatalog)
            {
                EditorGUILayout.LabelField("Loading tags from catalog...", EditorStyles.miniLabel);
                return;
            }

            if (catalogTags == null || catalogTags.Count == 0)
            {
                EditorGUILayout.HelpBox("No tags found in the catalog yet. Add a new tag above to create one.", MessageType.Info);
                return;
            }

            EnsureStyles();

            string tagToAdd = null;
            Vector2 scrollPosition = pickerState.ExistingTagsScroll;
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                GUILayout.MaxHeight(UIConstants.EXISTING_TAGS_SCROLL_HEIGHT));
            pickerState.ExistingTagsScroll = scrollPosition;

            IReadOnlyList<TagPickerState.CatalogTagBadgeEntry> cache = pickerState.GetCatalogBadgeCache(
                catalogTags,
                selectedTags,
                _existingTagBadgeAddedStyle,
                _existingTagBadgeStyle);

            float currentLineWidth = 0f;
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < cache.Count; i++)
            {
                TagPickerState.CatalogTagBadgeEntry entry = cache[i];
                GUIStyle badgeStyle = entry.AlreadyAdded ? _existingTagBadgeAddedStyle : _existingTagBadgeStyle;
                Color badgeColor = entry.AlreadyAdded ? UIConstants.COLOR_GREEN : UIConstants.COLOR_LIGHT_BLUE;

                if (ShouldWrapTagBadgeRow(currentLineWidth, entry.ButtonWidth, wrapWidth))
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
                string ignoredWarning = null;
                TryAddTag(selectedTags, tagToAdd, ref ignoredWarning, pickerState, onTagsChanged);
            }
        }

        /// <summary>
        /// Attempts to add a tag and updates picker state on success.
        /// </summary>
        /// <param name="selectedTags">Mutable tag list.</param>
        /// <param name="tag">Tag to add.</param>
        /// <param name="duplicateWarning">Warning message output on failure.</param>
        /// <param name="pickerState">Picker state to mark dirty on success.</param>
        /// <param name="onTagsChanged">Callback on success.</param>
        /// <returns>True when the tag was added.</returns>
        public static bool TryAddTag(
            List<string> selectedTags,
            string tag,
            ref string duplicateWarning,
            TagPickerState pickerState,
            Action onTagsChanged)
        {
            if (TagUtils.TryAddTag(selectedTags, tag, out string errorMessage))
            {
                duplicateWarning = null;
                pickerState.MarkTagsDirty();
                onTagsChanged?.Invoke();
                return true;
            }

            duplicateWarning = errorMessage;
            return false;
        }

        private static float GetHostEditorWindowWidth(EditorWindow host)
        {
            if (host != null && host.position.width > 1f)
            {
                return host.position.width;
            }

            ModelLibraryWindow libraryWindow = EditorWindow.GetWindow<ModelLibraryWindow>(false, null, false);
            if (libraryWindow != null && libraryWindow.position.width > 1f)
            {
                return libraryWindow.position.width;
            }

            return EditorGUIUtility.currentViewWidth;
        }

        private static void DrawBadgeRow(
            IReadOnlyList<string> tags,
            GUIStyle badgeStyle,
            Color badgeColor,
            float wrapWidth,
            Action<int> onBadgeClicked)
        {
            float currentLineWidth = 0f;
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < tags.Count; i++)
            {
                string label = TagUtils.FormatBadgeLabel(tags[i]);
                float buttonWidth = badgeStyle.CalcSize(new GUIContent(label)).x + UIConstants.TAG_BADGE_HORIZONTAL_PADDING;

                if (ShouldWrapTagBadgeRow(currentLineWidth, buttonWidth, wrapWidth))
                {
                    BeginNextTagBadgeRow(ref currentLineWidth);
                }

                if (onBadgeClicked != null)
                {
                    if (DrawSingleTagBadgeButton(label, badgeStyle, badgeColor, buttonWidth))
                    {
                        onBadgeClicked.Invoke(i);
                    }
                }
                else
                {
                    DrawReadOnlyTagBadge(label, badgeStyle, badgeColor, buttonWidth);
                }

                currentLineWidth += buttonWidth;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawReadOnlyTagBadge(string label, GUIStyle badgeStyle, Color badgeColor, float buttonWidth)
        {
            Color originalBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(
                badgeColor.r,
                badgeColor.g,
                badgeColor.b,
                UIConstants.BADGE_BACKGROUND_ALPHA);

            GUILayout.Label(label, badgeStyle, GUILayout.Width(buttonWidth));

            GUI.backgroundColor = originalBackground;
        }

        private static bool ShouldWrapTagBadgeRow(float currentLineWidth, float nextButtonWidth, float availableWidth)
        {
            if (currentLineWidth <= 0f)
            {
                return false;
            }

            return currentLineWidth + UIConstants.TAG_BADGE_HORIZONTAL_PADDING + nextButtonWidth > availableWidth;
        }

        private static void BeginNextTagBadgeRow(ref float currentLineWidth)
        {
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            currentLineWidth = 0f;
        }

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
    }
}
