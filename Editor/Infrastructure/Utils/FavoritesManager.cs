using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages favorites persistence for the Model Library browser.
    /// Handles loading, saving, and toggling favorite status of models.
    /// </summary>
    public class FavoritesManager
    {
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _prefsKey;

        /// <summary>
        /// Initializes a new instance of FavoritesManager.
        /// </summary>
        /// <param name="prefsKey">EditorPrefs key for storing favorites.</param>
        public FavoritesManager(string prefsKey)
        {
            _prefsKey = prefsKey;
            LoadFavorites();
        }

        /// <summary>
        /// Gets the set of favorite model IDs.
        /// </summary>
        public IReadOnlyCollection<string> Favorites => _favorites;

        /// <summary>
        /// Checks if a model is favorited.
        /// </summary>
        /// <param name="modelId">The model ID to check.</param>
        /// <returns>True if the model is favorited, false otherwise.</returns>
        public bool IsFavorite(string modelId) => _favorites.Contains(modelId);

        /// <summary>
        /// Loads favorites from EditorPrefs.
        /// </summary>
        /// <remarks>
        /// STABILITY (audit CRIT-10): Unity's <c>JsonUtility</c> cannot
        /// serialize/deserialize a top-level <c>string[]</c>. The previous
        /// implementation called <c>JsonUtility.FromJson&lt;string[]&gt;</c>
        /// which silently returned null, causing favorites to be lost on
        /// every editor restart. We now wrap the array in a
        /// <c>[Serializable]</c> wrapper that <c>JsonUtility</c> supports.
        /// </remarks>
        public void LoadFavorites()
        {
            _favorites.Clear();
            string favoritesJson = EditorPrefs.GetString(_prefsKey, "");
            if (string.IsNullOrWhiteSpace(favoritesJson) || favoritesJson == "[]")
            {
                return;
            }

            try
            {
                // Try the new wrapper format first.
                StringArrayWrapper wrapper = JsonUtility.FromJson<StringArrayWrapper>(favoritesJson);
                if (wrapper?.values != null)
                {
                    for (int i = 0; i < wrapper.values.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(wrapper.values[i]))
                        {
                            _favorites.Add(wrapper.values[i]);
                        }
                    }
                    return;
                }

                // Fallback: legacy newline-delimited format (for data written
                // before this fix). The previous JsonUtility call wrote "[]"
                // so this is only relevant if a future regression breaks the
                // wrapper format.
                if (favoritesJson.Contains("\n"))
                {
                    string[] parts = favoritesJson.Split('\n');
                    foreach (string p in parts)
                    {
                        if (!string.IsNullOrWhiteSpace(p)) _favorites.Add(p.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                // If parsing fails, start with empty favorites but log so the
                // user knows their data was lost (audit LOW-07/INFO-07).
                Debug.LogWarning($"[FavoritesManager] Failed to parse favorites from EditorPrefs: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves favorites to EditorPrefs.
        /// </summary>
        public void SaveFavorites()
        {
            try
            {
                StringArrayWrapper wrapper = new StringArrayWrapper { values = _favorites.ToArray() };
                string favoritesJson = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(_prefsKey, favoritesJson);
            }
            catch (Exception ex)
            {
                // Log instead of silently swallowing (audit INFO-07).
                Debug.LogWarning($"[FavoritesManager] Failed to save favorites: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the favorite status of a model.
        /// </summary>
        /// <param name="modelId">The model ID to toggle.</param>
        /// <returns>True if the model is now favorited, false if it was removed from favorites.</returns>
        public bool ToggleFavorite(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return false;
            }

            bool isFavorite = _favorites.Contains(modelId);
            if (isFavorite)
            {
                _favorites.Remove(modelId);
            }
            else
            {
                _favorites.Add(modelId);
            }

            SaveFavorites();
            return !isFavorite;
        }
    }
}
