
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using Identity = ModelLibrary.Editor.Identity;
using UnityEditor;
using UnityEngine;
using System.Globalization;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Shows a model version's metadata, structure, tags and notes; allows import and editing.
    /// </summary>
    public class ModelDetailsWindow : EditorWindow
    {
        private string _modelId;
        private string _version;
        private ModelLibraryService _service;
        private Data.ModelMeta _meta;
        private string _baselineMetaJson;
        private string _editedDescription;
        private Vector2 _scroll;
        private Vector2 _structureScroll;
        private bool _showStructure = true;
        private bool _showNotes = true;
        private bool _showChangelog = true;
        private bool _editingTags = false;
        private bool _isSavingMetadata = false;
        private List<string> _editableTags = new();
        private string _newTag = string.Empty;
        private string _newNoteMessage = string.Empty;
        private string _newNoteTag = "remarks";
        private readonly string[] _noteTags = { "bugfix", "improvements", "remarks", "question", "praise" };

        public static void Open(string id, string version)
        {
            ModelDetailsWindow w = GetWindow<ModelDetailsWindow>("Model Details");
            w._modelId = id; w._version = version; w.Init();
            w.Show();
        }

        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);
            _service = new ModelLibraryService(repo);
            _ = Load();
        }

        private async Task Load()
        {
            _meta = await _service.GetMetaAsync(_modelId, _version);
            _baselineMetaJson = JsonUtil.ToJson(_meta);
            _editedDescription = _meta.description;
            _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
            _editingTags = false;
            Repaint();
        }

        private void OnGUI()
        {
            if (_meta == null) { GUILayout.Label("Loading meta..."); return; }

            EditorGUILayout.LabelField(_meta.identity.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"v{_meta.version} by {_meta.author}");
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Description
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            _editedDescription = EditorGUILayout.TextArea(_editedDescription ?? string.Empty, GUILayout.MinHeight(60));
            EditorGUILayout.Space();

            // Tags section with editing
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Tags:", EditorStyles.boldLabel);
                string tagButtonLabel = _editingTags ? "Cancel" : "Edit";
                if (GUILayout.Button(tagButtonLabel, GUILayout.Width(60)))
                {
                    _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
                    _editingTags = !_editingTags;
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
                            EditorGUILayout.LabelField(n.message, EditorStyles.wordWrappedLabel);
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
                if (GUILayout.Button("Import to Project", GUILayout.Width(160)))
                {
                    _ = ImportToProject();
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
                    EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
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
                string author = new Identity.SimpleUserIdentityProvider().GetUserName();
                await _service.PublishMetadataUpdateAsync(_meta, _version, summary, author);
                _version = _meta.version;
                await Load();
                EditorUtility.DisplayDialog("Metadata Updated", $"Published version {_version}.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Save Failed", ex.Message, "OK");
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
                EditorUtility.DisplayDialog("Note Submission Failed", ex.Message, "OK");
            }
        }

        private async Task ImportToProject()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Importing Model", "Downloading and importing...", 0.1f);
                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(settings.repositoryRoot)
                    : new Repository.HttpRepository(settings.repositoryRoot);
                ModelLibraryService service = new ModelLibraryService(repo);
                (string cacheRoot, Data.ModelMeta meta) = await service.DownloadModelVersionAsync(_modelId, _version);
                EditorUtility.DisplayProgressBar("Importing Model", "Copying to Assets...", 0.5f);
                await ModelProjectImporter.ImportFromCacheAsync(cacheRoot, meta, cleanDestination: true);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Import Complete", $"Imported '{meta.identity.name}' v{meta.version} into Assets.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
            }
        }
    }
}



