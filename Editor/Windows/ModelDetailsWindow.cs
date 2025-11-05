
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;
using System.Globalization;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Detailed view window for a specific model version.
    /// Displays comprehensive metadata including description, tags, structure, changelog, and notes.
    /// Allows Artists to edit metadata (description and tags) and delete versions.
    /// Allows all users to add feedback notes and import the model to their project.
    /// </summary>
    public class ModelDetailsWindow : EditorWindow
    {
        // Model Identification
        /// <summary>The unique identifier of the model being displayed.</summary>
        private string _modelId;
        /// <summary>The version of the model being displayed.</summary>
        private string _version;

        // Services
        /// <summary>Service instance for repository operations.</summary>
        private ModelLibraryService _service;

        // Metadata
        /// <summary>The loaded model metadata.</summary>
        private Data.ModelMeta _meta;
        /// <summary>Original metadata JSON for change detection when saving.</summary>
        private string _baselineMetaJson;
        /// <summary>Edited description text (may differ from _meta.description).</summary>
        private string _editedDescription;

        // UI State
        /// <summary>Scroll position for the main content area.</summary>
        private Vector2 _scroll;
        /// <summary>Scroll position for the model structure section.</summary>
        private Vector2 _structureScroll;
        /// <summary>Whether the structure section is expanded.</summary>
        private bool _showStructure = true;
        /// <summary>Whether the notes section is expanded.</summary>
        private bool _showNotes = true;
        /// <summary>Whether the changelog section is expanded.</summary>
        private bool _showChangelog = true;

        // Editing State
        /// <summary>Whether tag editing mode is active.</summary>
        private bool _editingTags = false;
        /// <summary>Flag indicating if metadata is currently being saved.</summary>
        private bool _isSavingMetadata = false;
        /// <summary>Editable list of tags (used during tag editing).</summary>
        private List<string> _editableTags = new();
        /// <summary>Text field for adding new tags.</summary>
        private string _newTag = string.Empty;

        // Notes
        /// <summary>Text field for the new note message.</summary>
        private string _newNoteMessage = string.Empty;
        /// <summary>Selected tag for the new note.</summary>
        private string _newNoteTag = "remarks";
        /// <summary>Available note tags for categorization.</summary>
        private readonly string[] _noteTags = { "bugfix", "improvements", "remarks", "question", "praise" };

        /// <summary>
        /// Opens the model details window for a specific model version.
        /// </summary>
        /// <param name="id">The unique identifier of the model.</param>
        /// <param name="version">The version of the model to display.</param>
        public static void Open(string id, string version)
        {
            ModelDetailsWindow w = GetWindow<ModelDetailsWindow>("Model Details");
            w._modelId = id; w._version = version; w.Init();
            w.Show();
        }

        /// <summary>
        /// Initializes the window by setting up services and loading metadata.
        /// </summary>
        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);
            _service = new ModelLibraryService(repo);
            _ = Load();
        }

        /// <summary>
        /// Asynchronously loads the model metadata and initializes the editing state.
        /// </summary>
        private async Task Load()
        {
            _meta = await _service.GetMetaAsync(_modelId, _version);
            _baselineMetaJson = JsonUtil.ToJson(_meta);
            _editedDescription = _meta.description;
            _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
            _editingTags = false;
            
            // Track view analytics
            AnalyticsService.RecordEvent("view", _modelId, _version, _meta.identity.name);
            
            Repaint();
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
                _editedDescription = EditorGUILayout.TextArea(_editedDescription ?? string.Empty, GUILayout.MinHeight(60));
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
                _newNoteMessage = EditorGUILayout.TextArea(_newNoteMessage, GUILayout.Height(60));
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
                    foreach (Data.ModelNote n in _meta.notes.OrderByDescending(n => n.createdTimeTicks))
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
                    if (GUILayout.Button("Import to Project", GUILayout.Width(160)))
                    {
                        _ = ImportToProject();
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

            foreach (Data.ModelChangelogEntry entry in _meta.changelog.OrderByDescending(c => c.timestamp))
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

        private async Task SaveMetadataChangesAsync()
        {
            if (_meta == null || string.IsNullOrEmpty(_baselineMetaJson))
            {
                return;
            }

            _meta.tags ??= new Data.Tags();

            if (_editingTags)
            {
                _meta.tags.values = new List<string>(_editableTags);
                _editingTags = false;
            }

            string trimmedDescription = string.IsNullOrEmpty(_editedDescription) ? string.Empty : _editedDescription.Trim();
            _meta.description = trimmedDescription;

            Data.ModelMeta before = JsonUtil.FromJson<Data.ModelMeta>(_baselineMetaJson);
            string summary = BuildMetadataChangeSummary(before, _meta);
            if (string.IsNullOrEmpty(summary))
            {
                EditorUtility.DisplayDialog("No Changes", "There are no metadata changes to save.", "OK");
                _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
                return;
            }

            try
            {
                _isSavingMetadata = true;
                string author = new SimpleUserIdentityProvider().GetUserName();
                await _service.PublishMetadataUpdateAsync(_meta, _version, summary, author);
                _version = _meta.version;
                await Load();
                EditorUtility.DisplayDialog("Metadata Updated", $"Published version {_version}.", "OK");
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowErrorWithRetry("Save Failed", $"Failed to save metadata: {ex.Message}",
                    async () => await SaveMeta(), ex);
                await Load();
            }
            finally
            {
                _isSavingMetadata = false;
            }
        }

        private static string BuildMetadataChangeSummary(Data.ModelMeta before, Data.ModelMeta after)
        {
            if (after == null)
            {
                return null;
            }

            List<string> parts = new List<string>();
            string beforeDescription = before?.description ?? string.Empty;
            string afterDescription = after.description ?? string.Empty;
            if (!string.Equals(beforeDescription.Trim(), afterDescription.Trim(), StringComparison.Ordinal))
            {
                parts.Add("Description updated");
            }

            HashSet<string> beforeTags = new HashSet<string>(before?.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> afterTags = new HashSet<string>(after.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            List<string> added = new List<string>();
            foreach (string tag in afterTags)
            {
                if (!beforeTags.Contains(tag))
                {
                    added.Add(tag);
                }
            }

            List<string> removed = new List<string>();
            foreach (string tag in beforeTags)
            {
                if (!afterTags.Contains(tag))
                {
                    removed.Add(tag);
                }
            }

            if (added.Count > 0 || removed.Count > 0)
            {
                string part = "Tags updated";
                if (added.Count > 0)
                {
                    part += $" (+{string.Join(", ", added)})";
                }
                if (removed.Count > 0)
                {
                    part += $" (-{string.Join(", ", removed)})";
                }
                parts.Add(part);
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }

        private async Task SaveMeta()
        {
            try
            {
                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(settings.repositoryRoot)
                    : new Repository.HttpRepository(settings.repositoryRoot);
                await repo.SaveMetaAsync(_modelId, _version, _meta);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Save Failed", ex.Message, "OK");
            }
        }

        private async Task SubmitNote()
        {
            try
            {
                Data.ModelNote note = new Data.ModelNote
                {
                    author = new Identity.SimpleUserIdentityProvider().GetUserName(),
                    message = _newNoteMessage,
                    createdTimeTicks = DateTime.Now.Ticks,
                    tag = _newNoteTag
                };

                _meta.notes.Add(note);
                await SaveMeta();
                _newNoteMessage = string.Empty;
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError("Note Submission Failed", $"Failed to add note: {ex.Message}", ex);
            }
        }

        private async Task ImportToProject()
        {
            try
            {
                titleContent.text = "Model Details - Importing...";
                EditorUtility.DisplayProgressBar("Importing Model", "Connecting to repository...", 0.1f);

                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(settings.repositoryRoot)
                    : new Repository.HttpRepository(settings.repositoryRoot);
                ModelLibraryService service = new ModelLibraryService(repo);

                EditorUtility.DisplayProgressBar("Importing Model", "Downloading model files...", 0.3f);
                (string cacheRoot, Data.ModelMeta meta) = await service.DownloadModelVersionAsync(_modelId, _version);

                EditorUtility.DisplayProgressBar("Importing Model", "Copying files to Assets folder...", 0.6f);
                await ModelProjectImporter.ImportFromCacheAsync(cacheRoot, meta, cleanDestination: true);

                EditorUtility.DisplayProgressBar("Importing Model", "Finalizing import...", 0.9f);
                await Task.Delay(100); // Brief pause for UI update

                EditorUtility.ClearProgressBar();
                
                // Track analytics
                AnalyticsService.RecordEvent("import", _modelId, _version, meta.identity.name);
                
                // Show completion dialog
                EditorUtility.DisplayDialog("Import Complete", $"Imported '{meta.identity.name}' v{meta.version} into Assets.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                ErrorHandler.ShowErrorWithRetry("Import Failed", $"Failed to import model: {ex.Message}",
                    async () => await ImportToProject(), ex);
            }
            finally
            {
                titleContent.text = "Model Details";
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
        /// Draws the delete version button with comprehensive safety checks.
        /// Only visible to Artists. Shows a warning dialog for all deletions,
        /// and an additional confirmation dialog if deleting the latest version.
        /// </summary>
        private void DrawDeleteVersionButton()
        {
            // Only show delete button for Artists
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                return; // Developers cannot delete versions
            }

            EditorGUILayout.Space(2);

            // Check if this is the latest version
            bool isLatestVersion = false;
            try
            {
                // Synchronously check if this is the latest version (we'll need to load index)
                // For now, we'll show the button but disable it if it's the latest
                // In a real implementation, we'd want to check this asynchronously
            }
            catch { }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Warning style for delete button
                Color originalColor = GUI.color;
                GUI.color = Color.red;

                EditorGUILayout.HelpBox("Deleting a version permanently removes it from the repository. This action cannot be undone.", MessageType.Warning);

                GUI.color = originalColor;

                if (GUILayout.Button("Delete This Version", GUILayout.Width(150), GUILayout.Height(25)))
                {
                    // Show confirmation dialog
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Delete Version",
                        $"Are you sure you want to delete version {_meta.version} of '{_meta.identity.name}'?\n\n" +
                        "This will permanently remove:\n" +
                        "• The version folder\n" +
                        "• All payload files\n" +
                        "• All images\n" +
                        "• The metadata\n\n" +
                        "This action cannot be undone!",
                        "Delete",
                        "Cancel");

                    if (confirmed)
                    {
                        // Double confirmation for latest version
                        ModelIndex index = _service.GetIndexAsync().GetAwaiter().GetResult();
                        ModelIndex.Entry entry = index?.entries?.FirstOrDefault(e => e.id == _modelId);
                        if (entry != null && entry.latestVersion == _version)
                        {
                            bool confirmLatest = EditorUtility.DisplayDialog(
                                "Delete Latest Version",
                                $"Warning: You are about to delete the LATEST version ({_version})!\n\n" +
                                "This will make the model appear outdated in the index.\n" +
                                "You should update the index manually after deletion.\n\n" +
                                "Are you absolutely sure?",
                                "Yes, Delete Anyway",
                                "Cancel");

                            if (!confirmLatest)
                            {
                                return;
                            }
                        }

                        // Perform deletion
                        _ = DeleteVersionAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously deletes the current model version from the repository.
        /// Removes all files, metadata, and images associated with the version.
        /// Closes the window after successful deletion since the version no longer exists.
        /// </summary>
        private async Task DeleteVersionAsync()
        {
            EditorUtility.DisplayProgressBar("Deleting Version", $"Deleting {_meta.identity.name} v{_meta.version}...", 0.5f);

            try
            {
                bool deleted = await _service.DeleteVersionAsync(_modelId, _version);

                EditorUtility.ClearProgressBar();

                if (deleted)
                {
                    EditorUtility.DisplayDialog("Deletion Successful",
                        $"Version {_version} of '{_meta.identity.name}' has been deleted from the repository.",
                        "OK");

                    // Close this window since the version no longer exists
                    Close();
                }
                else
                {
                    ErrorHandler.ShowError("Deletion Failed",
                        $"Failed to delete version {_version}. The version may not exist or there was an error accessing the repository.",
                        null);
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                ErrorHandler.ShowError("Deletion Error",
                    $"An error occurred while deleting the version: {ex.Message}",
                    ex);
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



