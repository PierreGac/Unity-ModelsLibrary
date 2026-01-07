using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Displays a side-by-side comparison of two model versions highlighting metadata differences.
    /// </summary>
    public class ModelVersionComparisonWindow : EditorWindow
    {
        private const float __ColumnWidth = 0.5f;

        private string _modelId;
        private string _initialRightVersion;
        private ModelLibraryService _service;

        private readonly List<string> _availableVersions = new List<string>();
        private string _leftVersion;
        private string _rightVersion;

        private ModelMeta _leftMeta;
        private ModelMeta _rightMeta;

        private ComparisonResult _comparison = new ComparisonResult();
        private readonly List<string> _diffSummary = new List<string>();

        private Vector2 _summaryScroll;
        private Vector2 _detailsScroll;

        private bool _isLoading;
        private string _errorMessage;

        private class ComparisonResult
        {
            public bool DescriptionChanged;
            public List<string> AddedTags = new List<string>();
            public List<string> RemovedTags = new List<string>();
            public List<string> AddedPayloads = new List<string>();
            public List<string> RemovedPayloads = new List<string>();
            public List<string> AddedDependencies = new List<string>();
            public List<string> RemovedDependencies = new List<string>();
            public List<string> AddedExtras = new List<string>();
            public List<string> RemovedExtras = new List<string>();
            public List<string> ChangedExtras = new List<string>();
            public List<ModelChangelogEntry> NewChangelogEntries = new List<ModelChangelogEntry>();
            public bool PreviewChanged;
            public bool VertexCountChanged;
            public bool TriangleCountChanged;
        }

        /// <summary>
        /// Opens the version comparison window.
        /// Now navigates to the VersionComparison view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        /// <param name="modelId">Model to compare.</param>
        /// <param name="preferredRightVersion">Optional version to pre-select as the newer side.</param>
        public static void Open(string modelId, string preferredRightVersion = null)
        {
            // Navigate to VersionComparison view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "modelId", modelId }
                };
                if (!string.IsNullOrEmpty(preferredRightVersion))
                {
                    parameters["preferredRightVersion"] = preferredRightVersion;
                }
                window.NavigateToView(ModelLibraryWindow.ViewType.VersionComparison, parameters);
                window.InitializeVersionComparisonState();
            }
        }

        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);

            _service = new ModelLibraryService(repo);
            _ = LoadVersionsAsync();
        }

        private async Task LoadVersionsAsync()
        {
            _isLoading = true;
            _errorMessage = null;
            Repaint();

            try
            {
                _availableVersions.Clear();
                List<string> versions = await _service.GetAvailableVersionsAsync(_modelId);

                if (versions.Count == 0)
                {
                    _errorMessage = "No other versions were found in the repository.";
                    return;
                }

                _availableVersions.AddRange(versions);

                _rightVersion = !string.IsNullOrEmpty(_initialRightVersion) && _availableVersions.Contains(_initialRightVersion)
                    ? _initialRightVersion
                    : _availableVersions[0];

                _leftVersion = ChooseInitialLeftVersion();

                await LoadSelectedVersionsAsync();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Failed to load versions: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private string ChooseInitialLeftVersion()
        {
            if (_availableVersions.Count <= 1)
            {
                return _availableVersions[0];
            }

            for (int i = 0; i < _availableVersions.Count; i++)
            {
                string candidate = _availableVersions[i];
                if (!string.Equals(candidate, _rightVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return _availableVersions[_availableVersions.Count - 1];
        }

        private async Task LoadSelectedVersionsAsync()
        {
            if (string.IsNullOrEmpty(_leftVersion) || string.IsNullOrEmpty(_rightVersion))
            {
                return;
            }

            _isLoading = true;
            _errorMessage = null;
            Repaint();

            try
            {
                Task<ModelMeta> leftTask = _service.GetMetaAsync(_modelId, _leftVersion);
                Task<ModelMeta> rightTask = _service.GetMetaAsync(_modelId, _rightVersion);
                await Task.WhenAll(leftTask, rightTask);

                _leftMeta = await leftTask;
                _rightMeta = await rightTask;

                BuildComparison();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Failed to load metadata: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_modelId))
            {
                EditorGUILayout.HelpBox("No model selected for comparison.", MessageType.Info);
                return;
            }

            if (_service == null && !_isLoading)
            {
                Init();
            }

            DrawVersionSelectors();

            if (_isLoading)
            {
                GUILayout.Space(10f);
                EditorGUILayout.LabelField("Loading version data...", EditorStyles.miniLabel);
                return;
            }

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUILayout.Space(10f);
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Warning);
            }

            if (_leftMeta == null || _rightMeta == null)
            {
                GUILayout.Space(10f);
                EditorGUILayout.HelpBox("Select two versions to compare.", MessageType.Info);
                return;
            }

            GUILayout.Space(6f);
            DrawDiffSummary();
            GUILayout.Space(8f);
            DrawComparisonColumns();
        }

        private void DrawVersionSelectors()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Base Version", GUILayout.Width(90f));
                int leftIndex = Math.Max(0, _availableVersions.IndexOf(_leftVersion));
                int newLeftIndex = EditorGUILayout.Popup(leftIndex, _availableVersions.ToArray());

                if (newLeftIndex != leftIndex)
                {
                    _leftVersion = _availableVersions[newLeftIndex];
                    _ = LoadSelectedVersionsAsync();
                }

                GUILayout.Space(12f);
                EditorGUILayout.LabelField("Compare With", GUILayout.Width(100f));
                int rightIndex = Math.Max(0, _availableVersions.IndexOf(_rightVersion));
                int newRightIndex = EditorGUILayout.Popup(rightIndex, _availableVersions.ToArray());

                if (newRightIndex != rightIndex)
                {
                    _rightVersion = _availableVersions[newRightIndex];
                    _ = LoadSelectedVersionsAsync();
                }

                GUILayout.Space(6f);
                using (new EditorGUI.DisabledScope(_leftVersion == null || _rightVersion == null || string.Equals(_leftVersion, _rightVersion, StringComparison.OrdinalIgnoreCase)))
                {
                    if (GUILayout.Button("Swap", GUILayout.Width(60f)))
                    {
                        string temp = _leftVersion;
                        _leftVersion = _rightVersion;
                        _rightVersion = temp;
                        _ = LoadSelectedVersionsAsync();
                    }
                }
            }
        }

        private void DrawDiffSummary()
        {
            EditorGUILayout.LabelField("Differences", EditorStyles.boldLabel);
            if (_diffSummary.Count == 0)
            {
                EditorGUILayout.HelpBox("No metadata differences detected between the selected versions.", MessageType.Info);
                return;
            }

            _summaryScroll = EditorGUILayout.BeginScrollView(_summaryScroll, GUILayout.Height(100f));
            for (int i = 0; i < _diffSummary.Count; i++)
            {
                EditorGUILayout.LabelField("• " + _diffSummary[i], EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawComparisonColumns()
        {
            EditorGUILayout.LabelField("Detailed Comparison", EditorStyles.boldLabel);
            GUILayout.Space(4f);

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            EditorGUILayout.BeginHorizontal();
            DrawMetadataColumn("Base", _leftVersion, _leftMeta, false);
            GUILayout.Space(12f);
            DrawMetadataColumn("Compare", _rightVersion, _rightMeta, true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMetadataColumn(string title, string version, ModelMeta meta, bool highlightChanges)
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Version {version}", EditorStyles.miniBoldLabel);

                DrawField("Author", string.IsNullOrEmpty(meta.author) ? "(unknown)" : meta.author, highlightChanges && !string.Equals(meta.author, _leftMeta?.author, StringComparison.Ordinal));

                DateTime created = meta.createdTimeTicks > 0 ? new DateTime(meta.createdTimeTicks) : DateTime.MinValue;
                DrawField("Created", created == DateTime.MinValue ? "(unknown)" : created.ToString(), false);

                DateTime updated = meta.updatedTimeTicks > 0 ? new DateTime(meta.updatedTimeTicks) : DateTime.MinValue;
                DrawField("Updated", updated == DateTime.MinValue ? "(unknown)" : updated.ToString(), false);

                DrawMultilineField("Description", string.IsNullOrEmpty(meta.description) ? "(none)" : meta.description,
                    highlightChanges && _comparison.DescriptionChanged);

                GUILayout.Space(6f);
                DrawTagSection(meta, highlightChanges);
                GUILayout.Space(6f);
                DrawPayloadSection(meta, highlightChanges);
                GUILayout.Space(6f);
                DrawDependencySection(meta, highlightChanges);
                GUILayout.Space(6f);
                DrawCountsSection(meta, highlightChanges);
                GUILayout.Space(6f);
                DrawExtrasSection(meta, highlightChanges);
                GUILayout.Space(6f);
                DrawChangelogSection(meta, highlightChanges);
            }
        }

        private void DrawField(string label, string value, bool highlight)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label + ":", GUILayout.Width(100f));
                DrawValueLabel(value, highlight);
            }
        }

        private void DrawMultilineField(string label, string value, bool highlight)
        {
            EditorGUILayout.LabelField(label + ":");
            DrawValueLabel(value, highlight, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawValueLabel(string value, bool highlight, GUIStyle style = null)
        {
            Color originalColor = GUI.color;
            if (highlight)
            {
                GUI.color = new Color(1f, 0.95f, 0.75f);
            }

            EditorGUILayout.LabelField(value, style ?? EditorStyles.label);
            GUI.color = originalColor;
        }

        private void DrawTagSection(ModelMeta meta, bool highlight)
        {
            EditorGUILayout.LabelField("Tags", EditorStyles.miniBoldLabel);
            if (meta.tags?.values == null || meta.tags.values.Count == 0)
            {
                DrawValueLabel("(none)", highlight && _comparison.RemovedTags.Count > 0);
                return;
            }

            string formatted = string.Join(", ", meta.tags.values);
            bool isChanged = highlight && (_comparison.AddedTags.Count > 0 || _comparison.RemovedTags.Count > 0);
            DrawValueLabel(formatted, isChanged, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPayloadSection(ModelMeta meta, bool highlight)
        {
            EditorGUILayout.LabelField("Payload Files", EditorStyles.miniBoldLabel);
            int count = meta.payloadRelativePaths?.Count ?? 0;
            bool changed = highlight && (_comparison.AddedPayloads.Count > 0 || _comparison.RemovedPayloads.Count > 0);
            DrawValueLabel($"{count} file(s)", changed);

            if (changed)
            {
                if (_comparison.AddedPayloads.Count > 0)
                {
                    DrawValueLabel("+ " + string.Join(", ", _comparison.AddedPayloads.Take(5)) + (_comparison.AddedPayloads.Count > 5 ? ", ..." : string.Empty), true, EditorStyles.wordWrappedMiniLabel);
                }
                if (_comparison.RemovedPayloads.Count > 0)
                {
                    DrawValueLabel("- " + string.Join(", ", _comparison.RemovedPayloads.Take(5)) + (_comparison.RemovedPayloads.Count > 5 ? ", ..." : string.Empty), true, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawDependencySection(ModelMeta meta, bool highlight)
        {
            EditorGUILayout.LabelField("Dependencies", EditorStyles.miniBoldLabel);
            int count = meta.dependencies?.Count ?? 0;
            bool changed = highlight && (_comparison.AddedDependencies.Count > 0 || _comparison.RemovedDependencies.Count > 0);
            DrawValueLabel($"{count} reference(s)", changed);

            if (changed)
            {
                if (_comparison.AddedDependencies.Count > 0)
                {
                    DrawValueLabel("+ " + string.Join(", ", _comparison.AddedDependencies.Take(5)) + (_comparison.AddedDependencies.Count > 5 ? ", ..." : string.Empty), true, EditorStyles.wordWrappedMiniLabel);
                }
                if (_comparison.RemovedDependencies.Count > 0)
                {
                    DrawValueLabel("- " + string.Join(", ", _comparison.RemovedDependencies.Take(5)) + (_comparison.RemovedDependencies.Count > 5 ? ", ..." : string.Empty), true, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawCountsSection(ModelMeta meta, bool highlight)
        {
            bool vertexHighlight = highlight && _comparison.VertexCountChanged;
            bool triangleHighlight = highlight && _comparison.TriangleCountChanged;
            DrawValueLabel($"Vertices: {meta.vertexCount}", vertexHighlight);
            DrawValueLabel($"Triangles: {meta.triangleCount}", triangleHighlight);
        }

        private void DrawExtrasSection(ModelMeta meta, bool highlight)
        {
            EditorGUILayout.LabelField("Extra Metadata", EditorStyles.miniBoldLabel);
            if (meta.extra == null || meta.extra.Count == 0)
            {
                DrawValueLabel("(none)", highlight && (_comparison.RemovedExtras.Count > 0 || _comparison.ChangedExtras.Count > 0));
                return;
            }

            bool changed = highlight && (_comparison.AddedExtras.Count > 0 || _comparison.RemovedExtras.Count > 0 || _comparison.ChangedExtras.Count > 0);
            foreach (KeyValuePair<string, string> pair in meta.extra.OrderBy(p => p.Key))
            {
                bool entryChanged = highlight && (_comparison.AddedExtras.Contains(pair.Key) || _comparison.RemovedExtras.Contains(pair.Key) || _comparison.ChangedExtras.Contains(pair.Key));
                DrawValueLabel($"{pair.Key}: {pair.Value}", entryChanged, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawChangelogSection(ModelMeta meta, bool highlight)
        {
            if (meta.changelog == null || meta.changelog.Count == 0)
            {
                EditorGUILayout.LabelField("Changelog", EditorStyles.miniBoldLabel);
                DrawValueLabel("(none)", false);
                return;
            }

            bool highlightEntries = highlight && _comparison.NewChangelogEntries.Count > 0;
            EditorGUILayout.LabelField("Changelog", EditorStyles.miniBoldLabel);

            IEnumerable<ModelChangelogEntry> ordered = meta.changelog.OrderByDescending(c => c.timestamp);
            foreach (ModelChangelogEntry entry in ordered.Take(4))
            {
                bool isNew = highlightEntries && _comparison.NewChangelogEntries.Any(e => e.version == entry.version && e.summary == entry.summary);
                string summary = string.IsNullOrEmpty(entry.summary) ? "(no summary)" : entry.summary;
                DrawValueLabel($"{entry.version}: {summary}", isNew, EditorStyles.wordWrappedMiniLabel);
            }

            if (meta.changelog.Count > 4)
            {
                DrawValueLabel($"... ({meta.changelog.Count - 4} more entries)", highlightEntries);
            }
        }

        private void BuildComparison()
        {
            _comparison = new ComparisonResult();
            _diffSummary.Clear();

            if (_leftMeta == null || _rightMeta == null)
            {
                return;
            }

            CompareDescription();
            CompareTags();
            ComparePayload();
            CompareDependencies();
            CompareCounts();
            CompareExtras();
            CompareChangelog();
            ComparePreview();

            if (_diffSummary.Count == 0)
            {
                _diffSummary.Add("No changes detected between the selected versions.");
            }
        }

        private void CompareDescription()
        {
            string leftDesc = (_leftMeta.description ?? string.Empty).Trim();
            string rightDesc = (_rightMeta.description ?? string.Empty).Trim();
            if (!string.Equals(leftDesc, rightDesc, StringComparison.Ordinal))
            {
                _comparison.DescriptionChanged = true;
                _diffSummary.Add("Description updated.");
            }
        }

        private void CompareTags()
        {
            HashSet<string> leftTags = new HashSet<string>(_leftMeta.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> rightTags = new HashSet<string>(_rightMeta.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            _comparison.AddedTags = rightTags.Except(leftTags, StringComparer.OrdinalIgnoreCase).ToList();
            _comparison.RemovedTags = leftTags.Except(rightTags, StringComparer.OrdinalIgnoreCase).ToList();

            if (_comparison.AddedTags.Count > 0)
            {
                _diffSummary.Add($"Tags added: {string.Join(", ", _comparison.AddedTags.Take(5))}{(_comparison.AddedTags.Count > 5 ? ", ..." : string.Empty)}.");
            }

            if (_comparison.RemovedTags.Count > 0)
            {
                _diffSummary.Add($"Tags removed: {string.Join(", ", _comparison.RemovedTags.Take(5))}{(_comparison.RemovedTags.Count > 5 ? ", ..." : string.Empty)}.");
            }
        }

        private void ComparePayload()
        {
            HashSet<string> leftPayload = new HashSet<string>(_leftMeta.payloadRelativePaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> rightPayload = new HashSet<string>(_rightMeta.payloadRelativePaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            _comparison.AddedPayloads = rightPayload.Except(leftPayload, StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
            _comparison.RemovedPayloads = leftPayload.Except(rightPayload, StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();

            if (_comparison.AddedPayloads.Count > 0)
            {
                _diffSummary.Add($"Payload files added: {_comparison.AddedPayloads.Count}.");
            }

            if (_comparison.RemovedPayloads.Count > 0)
            {
                _diffSummary.Add($"Payload files removed: {_comparison.RemovedPayloads.Count}.");
            }
        }

        private void CompareDependencies()
        {
            HashSet<string> leftDeps = new HashSet<string>(_leftMeta.dependencies ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> rightDeps = new HashSet<string>(_rightMeta.dependencies ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            _comparison.AddedDependencies = rightDeps.Except(leftDeps, StringComparer.OrdinalIgnoreCase).OrderBy(d => d).ToList();
            _comparison.RemovedDependencies = leftDeps.Except(rightDeps, StringComparer.OrdinalIgnoreCase).OrderBy(d => d).ToList();

            if (_comparison.AddedDependencies.Count > 0)
            {
                _diffSummary.Add($"Dependencies added: {_comparison.AddedDependencies.Count}.");
            }

            if (_comparison.RemovedDependencies.Count > 0)
            {
                _diffSummary.Add($"Dependencies removed: {_comparison.RemovedDependencies.Count}.");
            }
        }

        private void CompareCounts()
        {
            if (_leftMeta.vertexCount != _rightMeta.vertexCount)
            {
                _comparison.VertexCountChanged = true;
                _diffSummary.Add($"Vertex count changed ({_leftMeta.vertexCount} → {_rightMeta.vertexCount}).");
            }

            if (_leftMeta.triangleCount != _rightMeta.triangleCount)
            {
                _comparison.TriangleCountChanged = true;
                _diffSummary.Add($"Triangle count changed ({_leftMeta.triangleCount} → {_rightMeta.triangleCount}).");
            }
        }

        private void CompareExtras()
        {
            Dictionary<string, string> leftExtras = _leftMeta.extra ?? new Dictionary<string, string>();
            Dictionary<string, string> rightExtras = _rightMeta.extra ?? new Dictionary<string, string>();

            HashSet<string> leftKeys = new HashSet<string>(leftExtras.Keys, StringComparer.OrdinalIgnoreCase);
            HashSet<string> rightKeys = new HashSet<string>(rightExtras.Keys, StringComparer.OrdinalIgnoreCase);

            _comparison.AddedExtras = rightKeys.Except(leftKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
            _comparison.RemovedExtras = leftKeys.Except(rightKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();

            foreach (string key in leftKeys.Intersect(rightKeys, StringComparer.OrdinalIgnoreCase))
            {
                string leftValue = leftExtras[key];
                string rightValue = rightExtras[key];
                if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                {
                    _comparison.ChangedExtras.Add(key);
                }
            }

            if (_comparison.AddedExtras.Count > 0)
            {
                _diffSummary.Add($"Extra metadata added: {string.Join(", ", _comparison.AddedExtras.Take(5))}{(_comparison.AddedExtras.Count > 5 ? ", ..." : string.Empty)}.");
            }

            if (_comparison.RemovedExtras.Count > 0)
            {
                _diffSummary.Add($"Extra metadata removed: {string.Join(", ", _comparison.RemovedExtras.Take(5))}{(_comparison.RemovedExtras.Count > 5 ? ", ..." : string.Empty)}.");
            }

            if (_comparison.ChangedExtras.Count > 0)
            {
                _diffSummary.Add($"Extra metadata updated: {string.Join(", ", _comparison.ChangedExtras.Take(5))}{(_comparison.ChangedExtras.Count > 5 ? ", ..." : string.Empty)}.");
            }
        }

        private void CompareChangelog()
        {
            Dictionary<string, ModelChangelogEntry> leftEntries = (_leftMeta.changelog ?? new List<ModelChangelogEntry>()).ToDictionary(entry => entry.version ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ModelChangelogEntry> rightEntries = (_rightMeta.changelog ?? new List<ModelChangelogEntry>()).ToDictionary(entry => entry.version ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, ModelChangelogEntry> kvp in rightEntries)
            {
                if (!leftEntries.ContainsKey(kvp.Key))
                {
                    _comparison.NewChangelogEntries.Add(kvp.Value);
                }
            }

            if (_comparison.NewChangelogEntries.Count > 0)
            {
                _diffSummary.Add($"New changelog entries: {_comparison.NewChangelogEntries.Count}.");
            }
        }

        private void ComparePreview()
        {
            string leftPreview = _leftMeta.previewImagePath ?? string.Empty;
            string rightPreview = _rightMeta.previewImagePath ?? string.Empty;

            if (!string.Equals(leftPreview, rightPreview, StringComparison.OrdinalIgnoreCase))
            {
                _comparison.PreviewChanged = true;
                _diffSummary.Add("Preview image updated.");
            }
        }
    }
}
