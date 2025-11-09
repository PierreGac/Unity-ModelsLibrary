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
        public void LoadFavorites()
        {
            _favorites.Clear();
            string favoritesJson = EditorPrefs.GetString(_prefsKey, "[]");
            try
            {
                string[] favorites = JsonUtility.FromJson<string[]>(favoritesJson);
                if (favorites != null)
                {
                    for (int i = 0; i < favorites.Length; i++)
                    {
                        _favorites.Add(favorites[i]);
                    }
                }
            }
            catch
            {
                // If parsing fails, start with empty favorites
            }
        }

        /// <summary>
        /// Saves favorites to EditorPrefs.
        /// </summary>
        public void SaveFavorites()
        {
            try
            {
                string favoritesJson = JsonUtility.ToJson(_favorites.ToArray());
                EditorPrefs.SetString(_prefsKey, favoritesJson);
            }
            catch
            {
                // Ignore save errors
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

