using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Window for viewing and managing error logs from the Model Library.
    /// </summary>
    public class ErrorLogViewerWindow : EditorWindow
    {
        private const float __TOOLBAR_BUTTON_WIDTH_SMALL = 80f;
        private const float __TOOLBAR_BUTTON_WIDTH_MEDIUM = 100f;
        private const float __TOOLBAR_BUTTON_WIDTH_LARGE = 120f;
        private const float __FILTER_LABEL_WIDTH_SMALL = 40f;
        private const float __FILTER_LABEL_WIDTH_MEDIUM = 60f;
        private const float __FILTER_LABEL_WIDTH_LARGE = 90f;
        private const float __FILTER_SEARCH_WIDTH = 200f;
        private const float __ENTRY_TIMESTAMP_WIDTH = 150f;
        private const float __ENTRY_CATEGORY_WIDTH = 100f;
        private const float __ENTRY_CONTEXT_LABEL_WIDTH = 60f;
        private List<ErrorLogEntry> _allEntries = new List<ErrorLogEntry>();
        private List<ErrorLogEntry> _filteredEntries = new List<ErrorLogEntry>();
        private Vector2 _scrollPosition = Vector2.zero;
        private ErrorHandler.ErrorCategory _filterCategory = ErrorHandler.ErrorCategory.Unknown;
        private string _searchText = "";
        private bool _autoRefresh = true;
        private DateTime _lastRefresh = DateTime.MinValue;
        private const float REFRESH_INTERVAL_SECONDS = 2f;

        /// <summary>
        /// Opens the error log viewer window.
        /// Now navigates to the ErrorLog view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            // Navigate to ErrorLog view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.ErrorLog);
            }
        }

        private void OnEnable() => RefreshLog();

        private void OnGUI()
        {
            // Auto-refresh if enabled
            if (_autoRefresh && (DateTime.Now - _lastRefresh).TotalSeconds > REFRESH_INTERVAL_SECONDS)
            {
                RefreshLog();
            }

            UIStyles.DrawPageHeader("Error Log", "Review recent issues and clear suppressions.");
            DrawToolbar();
            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            DrawFilterBar();
            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
            DrawLogEntries();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_SMALL)))
                {
                    RefreshLog();
                }

                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-refresh", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_MEDIUM));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Log", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_SMALL)))
                {
                    if (EditorUtility.DisplayDialog("Clear Error Log", 
                        "Are you sure you want to clear all error log entries? This cannot be undone.", 
                        "Clear", "Cancel"))
                    {
                        ErrorLogger.ClearLog();
                        RefreshLog();
                    }
                }

                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                if (GUILayout.Button("Clear Suppressions", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_LARGE)))
                {
                    if (EditorUtility.DisplayDialog("Clear Suppressed Errors", 
                        "This will re-enable all error dialogs that were previously suppressed. Continue?", 
                        "Clear", "Cancel"))
                    {
                        ErrorDialogWindow.ClearSuppressions();
                        EditorUtility.DisplayDialog("Suppressions Cleared", 
                            "All suppressed error dialogs have been re-enabled.", "OK");
                    }
                }

                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                if (GUILayout.Button("Open Log File", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_MEDIUM)))
                {
                    string logPath = ErrorLogger.GetLogFilePathForDisplay();
                    if (File.Exists(logPath))
                    {
                        EditorUtility.RevealInFinder(logPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Log File Not Found", 
                            $"The log file does not exist yet:\n{logPath}", "OK");
                    }
                }

                GUILayout.Label($"Entries: {_filteredEntries.Count}", UIStyles.MutedLabel, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_SMALL));
            }
        }

        private void DrawFilterBar()
        {
            using (new EditorGUILayout.HorizontalScope(UIStyles.CardBox))
            {
                GUILayout.Label("Filter:", UIStyles.MutedLabel, GUILayout.Width(__FILTER_LABEL_WIDTH_SMALL));

                // Category filter
                GUILayout.Label("Category:", UIStyles.MutedLabel, GUILayout.Width(__FILTER_LABEL_WIDTH_MEDIUM));
                ErrorHandler.ErrorCategory newCategory = (ErrorHandler.ErrorCategory)EditorGUILayout.EnumPopup(
                    _filterCategory, EditorStyles.miniButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_LARGE));
                if (newCategory != _filterCategory)
                {
                    _filterCategory = newCategory;
                    ApplyFilters();
                }

                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Search filter
                GUILayout.Label("Search:", UIStyles.MutedLabel, GUILayout.Width(__FILTER_LABEL_WIDTH_MEDIUM));
                string newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(__FILTER_SEARCH_WIDTH));
                if (newSearch != _searchText)
                {
                    _searchText = newSearch;
                    ApplyFilters();
                }

                GUILayout.FlexibleSpace();

                // Clear filters button
                if (GUILayout.Button("Clear Filters", EditorStyles.miniButton, GUILayout.Width(__FILTER_LABEL_WIDTH_LARGE)))
                {
                    _filterCategory = ErrorHandler.ErrorCategory.Unknown;
                    _searchText = "";
                    ApplyFilters();
                }
            }
        }

        private void DrawLogEntries()
        {
            if (_filteredEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _allEntries.Count == 0 
                        ? "No error log entries found. The log file may be empty or not exist yet." 
                        : "No entries match the current filters.",
                    MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < _filteredEntries.Count; i++)
            {
                ErrorLogEntry entry = _filteredEntries[i];
                DrawLogEntry(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLogEntry(ErrorLogEntry entry)
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                // Header with timestamp and category
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIStyle headerStyle = new GUIStyle(UIStyles.MutedLabel);
                    headerStyle.fontStyle = FontStyle.Bold;
                    GUILayout.Label($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}", headerStyle, GUILayout.Width(__ENTRY_TIMESTAMP_WIDTH));

                    // Category badge
                    Color categoryColor = GetCategoryColor(entry.Category);
                    Color originalColor = GUI.color;
                    GUI.color = categoryColor;
                    GUILayout.Label($"[{entry.Category}]", UIStyles.MutedLabel, GUILayout.Width(__ENTRY_CATEGORY_WIDTH));
                    GUI.color = originalColor;

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(UIConstants.SPACING_EXTRA_SMALL);

                // Title
                GUIStyle titleStyle = new GUIStyle(UIStyles.SectionHeader);
                titleStyle.wordWrap = true;
                EditorGUILayout.LabelField(entry.Title, titleStyle);

                // Message
                if (!string.IsNullOrEmpty(entry.Message))
                {
                    EditorGUILayout.LabelField(entry.Message, EditorStyles.wordWrappedLabel);
                }

                // Context
                if (!string.IsNullOrEmpty(entry.Context))
                {
                    EditorGUILayout.Space(UIConstants.SPACING_EXTRA_SMALL);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Context:", UIStyles.MutedLabel, GUILayout.Width(__ENTRY_CONTEXT_LABEL_WIDTH));
                        EditorGUILayout.LabelField(entry.Context, EditorStyles.wordWrappedMiniLabel);
                    }
                }

                // Exception details (collapsible)
                if (!string.IsNullOrEmpty(entry.ExceptionType))
                {
                    EditorGUILayout.Space(2);
                    string exceptionKey = $"exception_{entry.GetHashCode()}";
                    bool showException = EditorPrefs.GetBool(exceptionKey, false);
                    bool newShowException = EditorGUILayout.Foldout(showException, 
                        $"Exception: {entry.ExceptionType}", true);
                    if (newShowException != showException)
                    {
                        EditorPrefs.SetBool(exceptionKey, newShowException);
                    }

                    if (newShowException)
                    {
                        EditorGUI.indentLevel++;
                        
                        if (!string.IsNullOrEmpty(entry.ExceptionMessage))
                        {
                            EditorGUILayout.LabelField($"Message: {entry.ExceptionMessage}", EditorStyles.wordWrappedMiniLabel);
                        }

                        if (!string.IsNullOrEmpty(entry.StackTrace))
                        {
                            EditorGUILayout.Space(2);
                            EditorGUILayout.LabelField("Stack Trace:", EditorStyles.miniLabel);
                            EditorGUILayout.TextArea(entry.StackTrace, EditorStyles.wordWrappedMiniLabel, 
                                GUILayout.Height(100));
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space(3);
        }

        private Color GetCategoryColor(ErrorHandler.ErrorCategory category)
        {
            return category switch
            {
                ErrorHandler.ErrorCategory.Connection => new Color(1f, 0.6f, 0f), // Orange
                ErrorHandler.ErrorCategory.FileSystem => new Color(1f, 0.3f, 0.3f), // Red
                ErrorHandler.ErrorCategory.Validation => new Color(1f, 0.8f, 0f), // Yellow
                ErrorHandler.ErrorCategory.Permission => new Color(1f, 0.5f, 0.5f), // Light red
                ErrorHandler.ErrorCategory.Configuration => new Color(0.5f, 0.5f, 1f), // Light blue
                _ => Color.gray
            };
        }

        private void RefreshLog()
        {
            _allEntries = ErrorLogger.ReadLogEntries();
            _lastRefresh = DateTime.Now;
            ApplyFilters();
            Repaint();
        }

        private void ApplyFilters()
        {
            _filteredEntries = _allEntries.Where(entry =>
            {
                // Category filter
                if (_filterCategory != ErrorHandler.ErrorCategory.Unknown && entry.Category != _filterCategory)
                {
                    return false;
                }

                // Search filter
                if (!string.IsNullOrEmpty(_searchText))
                {
                    string searchLower = _searchText.ToLowerInvariant();
                    if (!entry.Title.ToLowerInvariant().Contains(searchLower) &&
                        !entry.Message.ToLowerInvariant().Contains(searchLower) &&
                        (string.IsNullOrEmpty(entry.Context) || !entry.Context.ToLowerInvariant().Contains(searchLower)) &&
                        (string.IsNullOrEmpty(entry.ExceptionType) || !entry.ExceptionType.ToLowerInvariant().Contains(searchLower)))
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();
        }
    }
}

