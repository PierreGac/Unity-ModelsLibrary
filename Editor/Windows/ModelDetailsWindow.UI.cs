using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing main UI rendering methods for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        /// <summary>
        /// Word-wrapped text area style for automatic line wrapping.
        /// </summary>
        private static GUIStyle _wordWrappedTextAreaStyle;
        
        /// <summary>
        /// Gets or creates the word-wrapped text area style.
        /// </summary>
        private static GUIStyle _WordWrappedTextAreaStyle
        {
            get
            {
                if (_wordWrappedTextAreaStyle == null)
                {
                    _wordWrappedTextAreaStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true
                    };
                }
                return _wordWrappedTextAreaStyle;
            }
        }
        private void OnGUI()
        {
            // Handle keyboard shortcuts
            HandleKeyboardShortcuts();

            if (_meta == null) { GUILayout.Label("Loading meta..."); return; }

            EditorGUILayout.LabelField(_meta.identity.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"v{_meta.version} by {_meta.author}");

            // Delete version button (with safety checks)
            DrawDeleteVersionButton();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Description
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            SimpleUserIdentityProvider identityProviderForDesc = new SimpleUserIdentityProvider();
            bool isArtistForDesc = identityProviderForDesc.GetUserRole() == UserRole.Artist;
            bool isAdminForDesc = identityProviderForDesc.GetUserRole() == UserRole.Admin;
            using (new EditorGUI.DisabledScope(!isArtistForDesc && !isAdminForDesc))
            {
                // Constrain text area to available width and enable word wrapping for automatic line breaks
                Rect textAreaRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
                _editedDescription = EditorGUI.TextArea(textAreaRect, _editedDescription ?? string.Empty, _WordWrappedTextAreaStyle);
            }
            EditorGUILayout.Space();

            // Tags section with editing
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Tags:", EditorStyles.boldLabel);
                SimpleUserIdentityProvider identityProviderForTags = new SimpleUserIdentityProvider();
                bool isArtistForTags = identityProviderForTags.GetUserRole() == UserRole.Artist;

                if (isArtistForTags)
                {
                    string tagButtonLabel = _editingTags ? "Cancel" : "Edit";
                    if (GUILayout.Button(tagButtonLabel, GUILayout.Width(60)))
                    {
                        _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
                        _editingTags = !_editingTags;
                    }
                }
            }

            if (_editingTags)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newTag = EditorGUILayout.TextField("Add Tag", _newTag);
                    if (GUILayout.Button("Add", GUILayout.Width(50)) && !string.IsNullOrWhiteSpace(_newTag))
                    {
                        _editableTags.Add(_newTag.Trim());
                        _newTag = string.Empty;
                    }
                }
                for (int i = _editableTags.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"• {_editableTags[i]}");
                        if (GUILayout.Button("x", GUILayout.Width(24)))
                        {
                            _editableTags.RemoveAt(i);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(string.Join(", ", _meta.tags?.values.ToArray() ?? Array.Empty<string>()));
            }
            EditorGUILayout.Space();

            // Only show Save Metadata Changes button for Artists
            SimpleUserIdentityProvider identityProviderForSave = new SimpleUserIdentityProvider();
            bool isArtistForSave = identityProviderForSave.GetUserRole() == UserRole.Artist;

            if (isArtistForSave)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(_isSavingMetadata))
                    {
                        if (GUILayout.Button("Save Metadata Changes", GUILayout.Width(180)))
                        {
                            _ = SaveMetadataChangesAsync();
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Metadata editing is only available for Artists. Switch to Artist role in User Settings to edit metadata.", MessageType.Info);
            }
            EditorGUILayout.Space();

            // Model Structure
            _showStructure = EditorGUILayout.Foldout(_showStructure, "Model Structure", true);
            if (_showStructure)
            {
                _structureScroll = EditorGUILayout.BeginScrollView(_structureScroll, GUILayout.Height(150));
                DrawModelStructure();
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.Space();

            _showChangelog = EditorGUILayout.Foldout(_showChangelog, "Changelog", true);
            if (_showChangelog)
            {
                DrawChangelog();
            }
            EditorGUILayout.Space();

            // Notes section
            _showNotes = EditorGUILayout.Foldout(_showNotes, "Notes", true);
            if (_showNotes)
            {
                // Add new note
                EditorGUILayout.LabelField("Add Note:", EditorStyles.boldLabel);
                // Constrain text area to available width and enable word wrapping for automatic line breaks
                Rect textAreaRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
                _newNoteMessage = EditorGUI.TextArea(textAreaRect, _newNoteMessage, _WordWrappedTextAreaStyle);
                int currentIndex = Array.IndexOf(_noteTags, _newNoteTag);
                if (currentIndex == -1)
                {
                    currentIndex = 0; // Default to first tag if not found
                }

                int selectedIndex = EditorGUILayout.Popup("Tag", currentIndex, _noteTags);
                _newNoteTag = _noteTags[selectedIndex];

                if (GUILayout.Button("Submit Note") && !string.IsNullOrWhiteSpace(_newNoteMessage))
                {
                    _ = SubmitNote();
                }
                EditorGUILayout.Space();

                // Display existing notes
                if ((_meta.notes?.Count ?? 0) == 0)
                {
                    GUILayout.Label("(none)");
                }
                else
                {
                    foreach (ModelNote n in _meta.notes.OrderByDescending(n => n.createdTimeTicks))
                    {
                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            DateTime dateTime = new DateTime(n.createdTimeTicks);
                            EditorGUILayout.LabelField($"{n.author} — {dateTime.ToString(CultureInfo.CurrentCulture)} [{n.tag}]", EditorStyles.miniBoldLabel);
                            DrawMultilineText(n.message, EditorStyles.wordWrappedLabel);
                            if (!string.IsNullOrEmpty(n.context))
                            {
                                EditorGUILayout.LabelField("Context:", n.context);
                            }
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Compare Versions", GUILayout.Width(150)))
                    {
                        ModelVersionComparisonWindow.Open(_modelId, _version);
                    }

                    // Determine button state
                    bool isCurrentVersionInstalled = _isInstalled && string.Equals(_installedVersion, _version, StringComparison.OrdinalIgnoreCase);
                    bool shouldDisableButton = isCurrentVersionInstalled && !_hasUpdate;
                    string buttonLabel = _hasUpdate ? "Update" : "Import to Project";


                    using (new EditorGUI.DisabledScope(shouldDisableButton))
                    {
                        if (GUILayout.Button(buttonLabel, GUILayout.Width(160)))
                        {
                            _ = ImportToProject();
                        }
                    }


                    if (GUILayout.Button("3D Preview", GUILayout.Width(120)))
                    {
                        ModelPreview3DWindow.Open(_modelId, _version);
                    }
                }
            }
        }

        private void DrawModelStructure()
        {
            EditorGUILayout.LabelField("📁 Model Files", EditorStyles.boldLabel);
            foreach (string path in _meta.payloadRelativePaths)
            {
                string fileName = System.IO.Path.GetFileName(path);
                string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                string icon = ext switch
                {
                    FileExtensions.FBX or FileExtensions.OBJ => "🎯",
                    FileExtensions.PNG or FileExtensions.JPG or FileExtensions.JPEG or FileExtensions.TGA or FileExtensions.PSD => "🖼️",
                    FileExtensions.MAT => "🎨",
                    _ => "📄"
                };
                EditorGUILayout.LabelField($"  {icon} {fileName}");
            }

            if (_meta.dependencies?.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("📦 Dependencies", EditorStyles.boldLabel);
                foreach (string dep in _meta.dependencies)
                {
                    EditorGUILayout.LabelField($"  🔗 {dep}");
                }
            }
        }

        private void DrawChangelog()
        {
            if (_meta?.changelog == null || _meta.changelog.Count == 0)
            {
                GUILayout.Label("(none)");
                return;
            }

            foreach (ModelChangelogEntry entry in _meta.changelog.OrderByDescending(c => c.timestamp))
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    DateTime entryTimestampDate = new DateTime(entry.timestamp);
                    EditorGUILayout.LabelField($"{entry.version} - {entryTimestampDate.ToString(CultureInfo.CurrentCulture)}", EditorStyles.miniBoldLabel);
                    if (!string.IsNullOrEmpty(entry.author))
                    {
                        EditorGUILayout.LabelField(entry.author, EditorStyles.miniLabel);
                    }
                    string summary = string.IsNullOrEmpty(entry.summary) ? "(no summary)" : entry.summary;
                    DrawMultilineText(summary, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        /// <summary>
        /// Draws multiline text with proper newline handling in Unity Editor GUI.
        /// Unity's EditorGUILayout.LabelField doesn't handle \n characters properly,
        /// so this method splits the text and draws each line separately.
        /// </summary>
        /// <param name="text">The text to display, may contain newline characters</param>
        /// <param name="style">The GUIStyle to use for rendering</param>
        private static void DrawMultilineText(string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Split text by newlines and draw each line separately
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                EditorGUILayout.LabelField(line, style);
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts for the details window.
        /// F5: Refresh metadata
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            Event currentEvent = Event.current;

            // Only process key events
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            // F5: Refresh metadata
            if (currentEvent.keyCode == KeyCode.F5)
            {
                if (_service != null && _meta != null)
                {
                    _ = Load();
                    currentEvent.Use();
                }
            }
        }
    }
}

