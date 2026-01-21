using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Window for applying bulk tag operations (add/remove) across multiple models.
    /// </summary>
    public class ModelBulkTagWindow : EditorWindow
    {
        private const float __LIST_SCROLL_HEIGHT = 100f;
        private const float __BUTTON_APPLY_WIDTH = 120f;
        private const float __BUTTON_CANCEL_WIDTH = 100f;

        private readonly List<ModelIndex.Entry> _entries = new List<ModelIndex.Entry>();
        private ModelLibraryService _service;
        private Action _onCompleted;

        private string _tagsToAdd = string.Empty;
        private string _tagsToRemove = string.Empty;
        private bool _overwriteExistingCase;
        private bool _isProcessing;
        private Vector2 _listScroll;

        /// <summary>
        /// Opens the bulk tag editor window for the provided model entries.
        /// </summary>
        /// <param name="service">Model library service.</param>
        /// <param name="entries">Target models (latest versions are assumed).</param>
        /// <param name="onCompleted">Callback invoked after operations succeed or fail.</param>
        public static void Open(ModelLibraryService service, List<ModelIndex.Entry> entries, Action onCompleted)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (entries == null || entries.Count == 0) throw new ArgumentException("Entries required", nameof(entries));

            ModelBulkTagWindow window = CreateInstance<ModelBulkTagWindow>();
            window.titleContent = new GUIContent("Bulk Tag Editor");
            window.minSize = new Vector2(420f, 320f);
            window._service = service;
            window._entries.Clear();
            window._entries.AddRange(entries);
            window._onCompleted = onCompleted;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            UIStyles.DrawPageHeader("Bulk Tag Operations", "Apply tag changes across selected models.");
            EditorGUILayout.HelpBox("Add or remove tags on the latest version of each selected model. A new metadata-only version will be published for each model that changes.", MessageType.Info);

            GUILayout.Space(UIConstants.SPACING_SMALL);
            EditorGUILayout.LabelField($"Selected models: {_entries.Count}", UIStyles.MutedLabel);

            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(__LIST_SCROLL_HEIGHT));
                for (int i = 0; i < _entries.Count; i++)
                {
                    EditorGUILayout.LabelField("• " + _entries[i].name + " (latest v" + _entries[i].latestVersion + ")", UIStyles.MutedLabel);
                }
                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(UIConstants.SPACING_STANDARD);
            UIStyles.DrawSectionHeader("Tags to Add");
            _tagsToAdd = EditorGUILayout.TextField(_tagsToAdd);
            EditorGUILayout.HelpBox("Separate multiple tags with commas. Tags are case-insensitive.", MessageType.None);

            GUILayout.Space(UIConstants.SPACING_SMALL);
            UIStyles.DrawSectionHeader("Tags to Remove");
            _tagsToRemove = EditorGUILayout.TextField(_tagsToRemove);

            GUILayout.Space(UIConstants.SPACING_STANDARD);
            _overwriteExistingCase = EditorGUILayout.ToggleLeft("Normalize casing of all tags (Title Case)", _overwriteExistingCase);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_isProcessing))
                {
                    if (UIStyles.DrawPrimaryButton("Apply", GUILayout.Width(__BUTTON_APPLY_WIDTH), GUILayout.Height(UIConstants.BUTTON_HEIGHT_STANDARD)))
                    {
                        ApplyBulkTags();
                    }
                    GUILayout.Space(UIConstants.SPACING_STANDARD);
                    if (UIStyles.DrawSecondaryButton("Cancel", GUILayout.Width(__BUTTON_CANCEL_WIDTH), GUILayout.Height(UIConstants.BUTTON_HEIGHT_STANDARD)))
                    {
                        Close();
                    }
                }
            }

            GUILayout.Space(UIConstants.SPACING_SMALL);
        }

        private void ApplyBulkTags()
        {
            string[] add = ParseTags(_tagsToAdd);
            string[] remove = ParseTags(_tagsToRemove);

            if (add.Length == 0 && remove.Length == 0)
            {
                EditorUtility.DisplayDialog("Bulk Tags", "Enter at least one tag to add or remove.", "OK");
                return;
            }

            if (_isProcessing)
            {
                return;
            }

            _ = ApplyBulkTagsAsync(add, remove);
        }

        private async Task ApplyBulkTagsAsync(string[] addTags, string[] removeTags)
        {
            _isProcessing = true;
            try
            {
                SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                string author = identityProvider.GetUserName();

                EditorUtility.DisplayProgressBar("Bulk Tag Update", "Preparing updates...", 0f);

                int successCount = 0;
                int skippedCount = 0;
                List<string> failures = new List<string>();

                for (int i = 0; i < _entries.Count; i++)
                {
                    ModelIndex.Entry entry = _entries[i];
                    float progress = (float)i / _entries.Count;
                    EditorUtility.DisplayProgressBar("Bulk Tag Update", $"Updating {entry.name} ({i + 1}/{_entries.Count})...", progress);

                    try
                    {
                        ModelMeta baseMeta = await _service.GetMetaAsync(entry.id, entry.latestVersion);
                        ModelMeta updatedMeta = JsonUtil.FromJson<ModelMeta>(JsonUtil.ToJson(baseMeta));

                        if (updatedMeta.tags == null)
                        {
                            updatedMeta.tags = new Tags();
                        }

                        HashSet<string> tagSet = new HashSet<string>(updatedMeta.tags.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                        bool changed = false;

                        for (int a = 0; a < addTags.Length; a++)
                        {
                            string tag = NormalizeTag(addTags[a]);
                            if (tagSet.Add(tag))
                            {
                                changed = true;
                            }
                        }

                        for (int r = 0; r < removeTags.Length; r++)
                        {
                            string tag = NormalizeTag(removeTags[r]);
                            if (tagSet.Remove(tag))
                            {
                                changed = true;
                            }
                        }

                        if (_overwriteExistingCase && tagSet.Count > 0)
                        {
                            List<string> normalized = tagSet.Select(NormalizeTag).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            tagSet = new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase);
                            changed = true;
                        }

                        if (!changed)
                        {
                            skippedCount++;
                            continue;
                        }

                        updatedMeta.tags.values = tagSet.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
                        string summary = BuildSummary(addTags, removeTags);
                        await _service.PublishMetadataUpdateAsync(updatedMeta, baseMeta.version, summary, author);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add(entry.name + ": " + ex.Message);
                    }
                }

                EditorUtility.ClearProgressBar();

                string message = $"Updated tags for {successCount} model(s).";
                if (skippedCount > 0)
                {
                    message += $"\n{skippedCount} model(s) already had the requested tags.";
                }
                if (failures.Count > 0)
                {
                    message += $"\nFailed: {failures.Count}. See console for details.";
                    foreach (string failure in failures)
                    {
                        Debug.LogError("[ModelLibrary] Bulk tag update failed: " + failure);
                    }
                }

                EditorUtility.DisplayDialog("Bulk Tag Update", message, "OK");
                _onCompleted?.Invoke();
                Close();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isProcessing = false;
            }
        }

        private static string[] ParseTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            return input
                .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeTag)
                .Where(tag => !string.IsNullOrEmpty(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return string.Empty;
            }

            tag = tag.Trim();
            if (tag.Length <= 1)
            {
                return tag.ToLowerInvariant();
            }

            return char.ToUpperInvariant(tag[0]) + tag.Substring(1).ToLowerInvariant();
        }

        private static string BuildSummary(string[] addTags, string[] removeTags)
        {
            List<string> parts = new List<string>();
            if (addTags.Length > 0)
            {
                parts.Add("+" + string.Join(",+", addTags));
            }
            if (removeTags.Length > 0)
            {
                parts.Add("-" + string.Join(",-", removeTags));
            }
            return parts.Count > 0 ? $"Bulk tag update ({string.Join(" ", parts)})" : "Bulk tag update";
        }
    }
}
