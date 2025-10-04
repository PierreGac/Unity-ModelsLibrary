
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
                _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));
                
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
                    if (GUILayout.Button("Add", GUILayout.Width(50)) && !string.IsNullOrWhiteSpace(_newTag))
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
                        if (GUILayout.Button("x", GUILayout.Width(24)))
                        {
                            _tags.RemoveAt(i);
                        }
                    }
                }

                _installPath = EditorGUILayout.TextField("Install Path", string.IsNullOrWhiteSpace(_installPath) ? DefaultInstallPath() : _installPath);
                _relativePath = EditorGUILayout.TextField("Relative Path", string.IsNullOrWhiteSpace(_relativePath) ? GetDefaultRelativePath() : _relativePath);

                EditorGUILayout.LabelField("Project Visibility", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newProjectTag = EditorGUILayout.TextField("Add Project", _newProjectTag);
                    if (GUILayout.Button("Add", GUILayout.Width(50)) && !string.IsNullOrWhiteSpace(_newProjectTag))
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
                        if (GUILayout.Button("x", GUILayout.Width(24)))
                        {
                            _projectTags.RemoveAt(i);
                        }
                    }
                }

                if (GUILayout.Button("Add Images..."))
                {
                    string chosen = EditorUtility.OpenFilePanel("Choose images", Application.dataPath, "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        _imageAbsPaths.Add(chosen);
                    }
                }
                for (int i = _imageAbsPaths.Count - 1; i >= 0; i--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(Path.GetFileName(_imageAbsPaths[i]));
                        if (GUILayout.Button("x", GUILayout.Width(24)))
                        {
                            _imageAbsPaths.RemoveAt(i);
                        }
                    }
                }

                EditorGUILayout.LabelField("Change Summary", EditorStyles.boldLabel);
                _changeSummary = EditorGUILayout.TextArea(_changeSummary ?? string.Empty, GUILayout.MinHeight(40));
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
                    string dst = Path.Combine(temp, "images", Path.GetFileName(abs));
                    File.Copy(abs, dst, overwrite: true);
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
            else if (_mode == SubmitMode.New)
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
    }
}


