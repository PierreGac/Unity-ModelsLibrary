using System;
using System.Collections.Generic;
using ModelLibrary.Data;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing draft save/load functionality for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Saves the current form state as a draft to EditorPrefs.
        /// </summary>
        private void SaveDraft()
        {
            try
            {
                DraftData draft = new DraftData
                {
                    name = _name,
                    version = _version,
                    description = _description,
                    tags = new List<string>(_tags),
                    installPath = _installPath,
                    relativePath = _relativePath,
                    imagePaths = new List<string>(_imageAbsPaths),
                    changeSummary = _changeSummary,
                    mode = _mode
                };

                string draftJson = JsonUtility.ToJson(draft);
                EditorPrefs.SetString(__DRAFT_PREF_KEY, draftJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save draft: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a saved draft from EditorPrefs and populates the form.
        /// </summary>
        private void LoadDraft()
        {
            try
            {
                string draftJson = EditorPrefs.GetString(__DRAFT_PREF_KEY, string.Empty);
                if (string.IsNullOrEmpty(draftJson))
                {
                    return;
                }

                DraftData draft = JsonUtility.FromJson<DraftData>(draftJson);
                if (draft != null)
                {
                    _name = draft.name ?? "New Model";
                    _version = draft.version ?? "1.0.0";
                    _description = draft.description ?? string.Empty;
                    _tags = draft.tags ?? new List<string>();
                    _installPath = draft.installPath ?? DefaultInstallPath();
                    _relativePath = draft.relativePath ?? GetDefaultRelativePath();
                    _imageAbsPaths = draft.imagePaths ?? new List<string>();
                    _changeSummary = draft.changeSummary ?? "Initial submission";
                    _mode = draft.mode;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load draft: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the saved draft.
        /// </summary>
        private void ClearDraft() => EditorPrefs.DeleteKey(__DRAFT_PREF_KEY);

        /// <summary>
        /// Data class for storing draft submission form state.
        /// </summary>
        [System.Serializable]
        private class DraftData
        {
            public string name;
            public string version;
            public string description;
            public List<string> tags;
            public string installPath;
            public string relativePath;
            public List<string> imagePaths;
            public string changeSummary;
            public SubmitMode mode;
        }
    }
}


