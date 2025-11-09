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
        private List<ErrorLogEntry> _allEntries = new List<ErrorLogEntry>();
        private List<ErrorLogEntry> _filteredEntries = new List<ErrorLogEntry>();
        private Vector2 _scrollPosition = Vector2.zero;
        private ErrorHandler.ErrorCategory _filterCategory = ErrorHandler.ErrorCategory.Unknown;
        private string _searchText = "";
        private bool _autoRefresh = true;
        private DateTime _lastRefresh = DateTime.MinValue;
        private const float REFRESH_INTERVAL_SECONDS = 2f;

        public static void Open()
        {
            ErrorLogViewerWindow window = GetWindow<ErrorLogViewerWindow>("Model Library Error Log");
            window.minSize = new Vector2(800, 400);
            window.Show();
        }

        private void OnEnable() => RefreshLog();

        private void OnGUI()
        {
            // Auto-refresh if enabled
            if (_autoRefresh && (DateTime.Now - _lastRefresh).TotalSeconds > REFRESH_INTERVAL_SECONDS)
            {
                RefreshLog();
            }

            DrawToolbar();
            EditorGUILayout.Space(5);
            DrawFilterBar();
            EditorGUILayout.Space(5);
            DrawLogEntries();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshLog();
                }

                GUILayout.Space(10);

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-refresh", EditorStyles.toolbarButton, GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Log", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Clear Error Log", 
                        "Are you sure you want to clear all error log entries? This cannot be undone.", 
                        "Clear", "Cancel"))
                    {
                        ErrorLogger.ClearLog();
                        RefreshLog();
                    }
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Clear Suppressions", EditorStyles.toolbarButton, GUILayout.Width(120)))
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

                GUILayout.Space(10);

                if (GUILayout.Button("Open Log File", EditorStyles.toolbarButton, GUILayout.Width(100)))
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

                GUILayout.Label($"Entries: {_filteredEntries.Count}", EditorStyles.miniLabel, GUILayout.Width(80));
            }
        }

        private void DrawFilterBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Filter:", EditorStyles.miniLabel, GUILayout.Width(40));

                // Category filter
                GUILayout.Label("Category:", EditorStyles.miniLabel, GUILayout.Width(60));
                ErrorHandler.ErrorCategory newCategory = (ErrorHandler.ErrorCategory)EditorGUILayout.EnumPopup(
                    _filterCategory, EditorStyles.miniButton, GUILayout.Width(120));
                if (newCategory != _filterCategory)
                {
                    _filterCategory = newCategory;
                    ApplyFilters();
                }

                GUILayout.Space(10);

                // Search filter
                GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(50));
                string newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (newSearch != _searchText)
                {
                    _searchText = newSearch;
                    ApplyFilters();
                }

                GUILayout.FlexibleSpace();

                // Clear filters button
                if (GUILayout.Button("Clear Filters", EditorStyles.miniButton, GUILayout.Width(90)))
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

            foreach (ErrorLogEntry entry in _filteredEntries)
            {
                DrawLogEntry(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLogEntry(ErrorLogEntry entry)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Header with timestamp and category
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
                    headerStyle.fontSize = 11;
                    GUILayout.Label($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}", headerStyle, GUILayout.Width(150));

                    // Category badge
                    Color categoryColor = GetCategoryColor(entry.Category);
                    Color originalColor = GUI.color;
                    GUI.color = categoryColor;
                    GUILayout.Label($"[{entry.Category}]", EditorStyles.miniLabel, GUILayout.Width(100));
                    GUI.color = originalColor;

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(2);

                // Title
                GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
                titleStyle.fontStyle = FontStyle.Bold;
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
                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Context:", EditorStyles.miniLabel, GUILayout.Width(60));
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

