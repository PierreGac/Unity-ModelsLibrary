using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages filter preset persistence and UI for the Model Library browser.
    /// Handles loading, saving, and applying filter presets.
    /// </summary>
    public class FilterPresetManager
    {
        private readonly List<FilterPreset> _filterPresets = new List<FilterPreset>();
        private readonly string _prefsKey;

        /// <summary>
        /// Initializes a new instance of FilterPresetManager.
        /// </summary>
        /// <param name="prefsKey">EditorPrefs key for storing filter presets.</param>
        public FilterPresetManager(string prefsKey)
        {
            _prefsKey = prefsKey;
            LoadFilterPresets();
        }

        /// <summary>
        /// Gets the list of saved filter presets.
        /// </summary>
        public IReadOnlyList<FilterPreset> Presets => _filterPresets;

        /// <summary>
        /// Loads filter presets from EditorPrefs.
        /// </summary>
        public void LoadFilterPresets()
        {
            _filterPresets.Clear();
            string presetsJson = EditorPrefs.GetString(_prefsKey, "[]");
            try
            {
                FilterPreset[] presets = JsonUtility.FromJson<FilterPreset[]>(presetsJson);
                if (presets != null)
                {
                    _filterPresets.AddRange(presets);
                }
            }
            catch
            {
                // If parsing fails, start with empty presets
            }
        }

        /// <summary>
        /// Saves filter presets to EditorPrefs.
        /// </summary>
        public void SaveFilterPresets()
        {
            try
            {
                string presetsJson = JsonUtility.ToJson(_filterPresets.ToArray());
                EditorPrefs.SetString(_prefsKey, presetsJson);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Shows a context menu with filter presets.
        /// </summary>
        /// <param name="onPresetSelected">Callback invoked when a preset is selected. Parameters are search query and selected tags.</param>
        /// <param name="onManagePresets">Callback invoked when manage presets is selected.</param>
        public void ShowFilterPresetsMenu(Action<string, HashSet<string>> onPresetSelected, Action onManagePresets)
        {
            GenericMenu menu = new GenericMenu();

            if (_filterPresets.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No presets saved"));
            }
            else
            {
                for (int i = 0; i < _filterPresets.Count; i++)
                {
                    FilterPreset preset = _filterPresets[i];
                    string presetName = preset.name;
                    FilterPreset presetCopy = preset; // Capture for closure
                    menu.AddItem(new GUIContent(presetName), false, () =>
                    {
                        HashSet<string> tags = presetCopy.selectedTags != null ? new HashSet<string>(presetCopy.selectedTags, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        onPresetSelected?.Invoke(presetCopy.searchQuery ?? string.Empty, tags);
                    });
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Manage Presets..."), false, () => onManagePresets?.Invoke());
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Shows a dialog to save the current filter state as a preset.
        /// </summary>
        /// <param name="currentSearch">The current search query.</param>
        /// <param name="currentTags">The currently selected tags.</param>
        public void ShowSavePresetDialog(string currentSearch, HashSet<string> currentTags)
        {
            string presetName = EditorInputDialog.Show("Save Filter Preset", "Enter a name for this preset:", "My Preset");
            if (!string.IsNullOrWhiteSpace(presetName))
            {
                FilterPreset preset = new FilterPreset
                {
                    name = presetName.Trim(),
                    searchQuery = currentSearch,
                    selectedTags = new List<string>(currentTags)
                };

                // Remove existing preset with same name
                _filterPresets.RemoveAll(p => string.Equals(p.name, preset.name, StringComparison.OrdinalIgnoreCase));

                // Add new preset
                _filterPresets.Add(preset);
                SaveFilterPresets();
            }
        }

        /// <summary>
        /// Shows a dialog to manage (rename/delete) filter presets.
        /// </summary>
        public void ShowManagePresetsDialog()
        {
            // Simple dialog using EditorUtility
            string message = "Filter Presets:\n\n";
            if (_filterPresets.Count == 0)
            {
                message += "No presets saved.";
            }
            else
            {
                for (int i = 0; i < _filterPresets.Count; i++)
                {
                    FilterPreset preset = _filterPresets[i];
                    message += $"{i + 1}. {preset.name}\n";
                    if (!string.IsNullOrEmpty(preset.searchQuery))
                    {
                        message += $"   Search: {preset.searchQuery}\n";
                    }
                    if (preset.selectedTags != null && preset.selectedTags.Count > 0)
                    {
                        message += $"   Tags: {string.Join(", ", preset.selectedTags)}\n";
                    }
                    message += "\n";
                }
            }

            EditorUtility.DisplayDialog("Filter Presets", message, "OK");
        }
    }
}

