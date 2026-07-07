using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages recently used models persistence for the Model Library browser.
    /// Tracks the most recently imported models and maintains a limited history.
    /// </summary>
    public class RecentlyUsedManager
    {
        private readonly List<string> _recentlyUsed = new List<string>();
        private readonly string _prefsKey;
        private readonly int _maxRecentlyUsed;

        /// <summary>
        /// Initializes a new instance of RecentlyUsedManager.
        /// </summary>
        /// <param name="prefsKey">EditorPrefs key for storing recently used models.</param>
        /// <param name="maxRecentlyUsed">Maximum number of recently used models to track.</param>
        public RecentlyUsedManager(string prefsKey, int maxRecentlyUsed)
        {
            _prefsKey = prefsKey;
            _maxRecentlyUsed = maxRecentlyUsed;
            LoadRecentlyUsed();
        }

        /// <summary>
        /// Gets the list of recently used model IDs (most recent first).
        /// </summary>
        public IReadOnlyList<string> RecentlyUsed => _recentlyUsed;

        /// <summary>
        /// Loads recently used models from EditorPrefs.
        /// </summary>
        /// <remarks>
        /// STABILITY (audit CRIT-10): see <see cref="FavoritesManager.LoadFavorites"/>
        /// for the rationale on the <c>StringArrayWrapper</c> approach.
        /// </remarks>
        public void LoadRecentlyUsed()
        {
            _recentlyUsed.Clear();
            string recentlyUsedJson = EditorPrefs.GetString(_prefsKey, "");
            if (string.IsNullOrWhiteSpace(recentlyUsedJson) || recentlyUsedJson == "[]")
            {
                return;
            }

            try
            {
                StringArrayWrapper wrapper = JsonUtility.FromJson<StringArrayWrapper>(recentlyUsedJson);
                if (wrapper?.values != null)
                {
                    _recentlyUsed.AddRange(wrapper.values);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RecentlyUsedManager] Failed to parse recently used list: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves recently used models to EditorPrefs.
        /// </summary>
        public void SaveRecentlyUsed()
        {
            try
            {
                StringArrayWrapper wrapper = new StringArrayWrapper { values = _recentlyUsed.ToArray() };
                string recentlyUsedJson = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(_prefsKey, recentlyUsedJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RecentlyUsedManager] Failed to save recently used list: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a model to the recently used list.
        /// </summary>
        /// <param name="modelId">The model ID to add.</param>
        public void AddToRecentlyUsed(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            // Remove if already exists
            _recentlyUsed.Remove(modelId);

            // Add to beginning
            _recentlyUsed.Insert(0, modelId);

            // Limit to max size
            if (_recentlyUsed.Count > _maxRecentlyUsed)
            {
                _recentlyUsed.RemoveRange(_maxRecentlyUsed, _recentlyUsed.Count - _maxRecentlyUsed);
            }

            SaveRecentlyUsed();
        }
    }
}
