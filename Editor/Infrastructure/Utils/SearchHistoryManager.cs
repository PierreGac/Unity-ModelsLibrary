using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages search history persistence and UI for the Model Library browser.
    /// Handles loading, saving, and displaying search history in a context menu.
    /// </summary>
    public class SearchHistoryManager
    {
        private readonly List<string> _searchHistory = new List<string>();
        private readonly string _prefsKey;
        private readonly int _maxHistory;

        /// <summary>
        /// Initializes a new instance of SearchHistoryManager.
        /// </summary>
        /// <param name="prefsKey">EditorPrefs key for storing search history.</param>
        /// <param name="maxHistory">Maximum number of search history entries to keep.</param>
        public SearchHistoryManager(string prefsKey, int maxHistory)
        {
            _prefsKey = prefsKey;
            _maxHistory = maxHistory;
            LoadSearchHistory();
        }

        /// <summary>
        /// Gets the current search history list.
        /// </summary>
        public IReadOnlyList<string> History => _searchHistory;

        /// <summary>
        /// Loads search history from EditorPrefs.
        /// </summary>
        public void LoadSearchHistory()
        {
            _searchHistory.Clear();
            string historyJson = EditorPrefs.GetString(_prefsKey, "[]");
            try
            {
                string[] history = JsonUtility.FromJson<string[]>(historyJson);
                if (history != null)
                {
                    _searchHistory.AddRange(history);
                }
            }
            catch
            {
                // If parsing fails, start with empty history
            }
        }

        /// <summary>
        /// Saves search history to EditorPrefs.
        /// </summary>
        public void SaveSearchHistory()
        {
            try
            {
                string historyJson = JsonUtility.ToJson(_searchHistory.ToArray());
                EditorPrefs.SetString(_prefsKey, historyJson);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Adds a search term to history if it's not already there.
        /// Maintains maximum history size.
        /// </summary>
        /// <param name="searchTerm">The search term to add to history.</param>
        public void AddToSearchHistory(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return;
            }

            string trimmed = searchTerm.Trim();

            // Remove if already exists
            _searchHistory.RemoveAll(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));

            // Add to beginning
            _searchHistory.Insert(0, trimmed);

            // Limit to max size
            if (_searchHistory.Count > _maxHistory)
            {
                _searchHistory.RemoveRange(_maxHistory, _searchHistory.Count - _maxHistory);
            }

            SaveSearchHistory();
        }

        /// <summary>
        /// Shows a context menu with search history.
        /// </summary>
        /// <param name="onItemSelected">Callback invoked when a history item is selected. Parameter is the selected search term.</param>
        /// <param name="onClear">Callback invoked when clear history is selected.</param>
        public void ShowSearchHistoryMenu(Action<string> onItemSelected, Action onClear)
        {
            GenericMenu menu = new GenericMenu();

            if (_searchHistory.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No search history"));
            }
            else
            {
                for (int i = 0; i < _searchHistory.Count; i++)
                {
                    string item = _searchHistory[i]; // Capture for closure
                    menu.AddItem(new GUIContent(item), false, () => onItemSelected?.Invoke(item));
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clear History"), false, () =>
                {
                    _searchHistory.Clear();
                    SaveSearchHistory();
                    onClear?.Invoke();
                });
            }

            menu.ShowAsContext();
        }
    }
}

