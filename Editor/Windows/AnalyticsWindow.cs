using System;
using System.Collections.Generic;
using System.Linq;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Analytics dashboard window for viewing model usage statistics and reports.
    /// Displays import counts, view counts, most popular models, and usage trends.
    /// </summary>
    public class AnalyticsWindow : EditorWindow
    {
        private const float __TOOLBAR_BUTTON_WIDTH_SMALL = 80f;
        private const float __TOOLBAR_BUTTON_WIDTH_MEDIUM = 100f;
        private const float __TOOLBAR_BUTTON_WIDTH_LARGE = 120f;
        private const float __TOOLBAR_LABEL_WIDTH = 80f;
        private const float __RANK_LABEL_WIDTH = 30f;
        private const float __EVENT_LABEL_WIDTH = 150f;
        private const float __EVENT_VALUE_WIDTH = 100f;
        private const float __EVENT_PERCENT_WIDTH = 60f;
        private const float __EVENT_VIEW_WIDTH = 150f;
        private const float __EVENT_DATE_WIDTH = 200f;
        /// <summary>Scroll position for the analytics content.</summary>
        private Vector2 _scrollPosition;
        /// <summary>Currently selected tab in the analytics view.</summary>
        private int _selectedTab = 0;
        /// <summary>Time range filter for analytics.</summary>
        private TimeRange _timeRange = TimeRange.Last30Days;
        /// <summary>Cached most imported models list.</summary>
        private List<ModelUsageStats> _mostImported;
        /// <summary>Cached most viewed models list.</summary>
        private List<ModelUsageStats> _mostViewed;
        /// <summary>Cached event counts by type.</summary>
        private Dictionary<string, int> _eventCounts;

        /// <summary>
        /// Time range options for filtering analytics.
        /// </summary>
        private enum TimeRange
        {
            Last7Days,
            Last30Days,
            Last90Days,
            AllTime
        }

        /// <summary>
        /// Opens the analytics window.
        /// Now navigates to the Analytics view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            // Check if user has access (Admin or Artist)
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            UserRole role = identityProvider.GetUserRole();
            
            if (role != UserRole.Admin && role != UserRole.Artist)
            {
                EditorUtility.DisplayDialog("Access Denied", 
                    "Analytics access is restricted to Administrators and Artists.", 
                    "OK");
                return;
            }

            // Navigate to Analytics view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.Analytics);
            }
        }

        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// </summary>
        private void OnEnable() => RefreshAnalytics();

        /// <summary>
        /// Refreshes the analytics data from the analytics service.
        /// </summary>
        private void RefreshAnalytics()
        {
            _mostImported = AnalyticsService.GetMostImportedModels(10);
            _mostViewed = AnalyticsService.GetMostViewedModels(10);
            _eventCounts = AnalyticsService.GetEventCountsByType();
        }

        /// <summary>
        /// Unity GUI method called to draw the window.
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            UIStyles.DrawPageHeader("Analytics", "Usage insights for imports, views, and activity.");

            // Toolbar
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_SMALL)))
                {
                    RefreshAnalytics();
                }

                GUILayout.FlexibleSpace();

                // Time range filter
                EditorGUILayout.LabelField("Time Range:", GUILayout.Width(__TOOLBAR_LABEL_WIDTH));
                _timeRange = (TimeRange)EditorGUILayout.EnumPopup(_timeRange, UIStyles.ToolbarPopup, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_LARGE));

                if (GUILayout.Button("Clear Data", UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BUTTON_WIDTH_MEDIUM)))
                {
                    if (EditorUtility.DisplayDialog("Clear Analytics", 
                        "Are you sure you want to clear all analytics data? This cannot be undone.", 
                        "Clear", "Cancel"))
                    {
                        AnalyticsService.ClearAnalytics();
                        RefreshAnalytics();
                    }
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);

            // Tab bar
            string[] tabs = { "Overview", "Most Imported", "Most Viewed", "Event Types" };
            _selectedTab = GUILayout.Toolbar(_selectedTab, tabs);

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Tab content
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case 0:
                    DrawOverview();
                    break;
                case 1:
                    DrawMostImported();
                    break;
                case 2:
                    DrawMostViewed();
                    break;
                case 3:
                    DrawEventTypes();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the overview tab with summary statistics.
        /// </summary>
        private void DrawOverview()
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                UIStyles.DrawSectionHeader("Summary Statistics");

                // Total events
                int totalEvents = _eventCounts.Values.Sum();
                EditorGUILayout.LabelField($"Total Events: {totalEvents:N0}");

                // Event breakdown
                if (_eventCounts.Count > 0)
                {
                    EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
                    UIStyles.DrawSectionHeader("Events by Type");
                    foreach (KeyValuePair<string, int> kvp in _eventCounts.OrderByDescending(k => k.Value))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(__EVENT_LABEL_WIDTH));
                            EditorGUILayout.LabelField($"{kvp.Value:N0}", GUILayout.Width(__EVENT_VALUE_WIDTH));
                            EditorGUILayout.LabelField($"({(kvp.Value * 100f / totalEvents):F1}%)", GUILayout.Width(__EVENT_PERCENT_WIDTH));
                        }
                    }
                }

                // Top models summary
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
                UIStyles.DrawSectionHeader("Top Models");

                if (_mostImported.Count > 0)
                {
                    EditorGUILayout.LabelField($"Most Imported: {_mostImported[0].modelId} ({_mostImported[0].importCount} imports)");
                }

                if (_mostViewed.Count > 0)
                {
                    EditorGUILayout.LabelField($"Most Viewed: {_mostViewed[0].modelId} ({_mostViewed[0].viewCount} views)");
                }
            }
        }

        /// <summary>
        /// Draws the most imported models tab.
        /// </summary>
        private void DrawMostImported()
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                UIStyles.DrawSectionHeader("Most Imported Models");

                if (_mostImported == null || _mostImported.Count == 0)
                {
                    EditorGUILayout.HelpBox("No import data available.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < _mostImported.Count; i++)
                {
                    ModelUsageStats stats = _mostImported[i];
                    using (EditorGUILayout.VerticalScope entryScope = UIStyles.BeginCard())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(__RANK_LABEL_WIDTH));
                            EditorGUILayout.LabelField(stats.modelId, UIStyles.SectionHeader);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField($"{stats.importCount} imports", GUILayout.Width(__EVENT_VALUE_WIDTH));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Views: {stats.viewCount}", GUILayout.Width(__EVENT_VIEW_WIDTH));
                            EditorGUILayout.LabelField($"Last accessed: {stats.lastAccessed:yyyy-MM-dd HH:mm}", GUILayout.Width(__EVENT_DATE_WIDTH));
                        }
                    }
                    EditorGUILayout.Space(UIConstants.SPACING_SMALL);
                }
            }
        }

        /// <summary>
        /// Draws the most viewed models tab.
        /// </summary>
        private void DrawMostViewed()
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                UIStyles.DrawSectionHeader("Most Viewed Models");

                if (_mostViewed == null || _mostViewed.Count == 0)
                {
                    EditorGUILayout.HelpBox("No view data available.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < _mostViewed.Count; i++)
                {
                    ModelUsageStats stats = _mostViewed[i];
                    using (EditorGUILayout.VerticalScope entryScope = UIStyles.BeginCard())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(__RANK_LABEL_WIDTH));
                            EditorGUILayout.LabelField(stats.modelId, UIStyles.SectionHeader);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField($"{stats.viewCount} views", GUILayout.Width(__EVENT_VALUE_WIDTH));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Imports: {stats.importCount}", GUILayout.Width(__EVENT_VIEW_WIDTH));
                            EditorGUILayout.LabelField($"Last accessed: {stats.lastAccessed:yyyy-MM-dd HH:mm}", GUILayout.Width(__EVENT_DATE_WIDTH));
                        }
                    }
                    EditorGUILayout.Space(UIConstants.SPACING_SMALL);
                }
            }
        }

        /// <summary>
        /// Draws the event types tab with event distribution.
        /// </summary>
        private void DrawEventTypes()
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                UIStyles.DrawSectionHeader("Event Types Distribution");

                if (_eventCounts == null || _eventCounts.Count == 0)
                {
                    EditorGUILayout.HelpBox("No event data available.", MessageType.Info);
                    return;
                }

                int totalEvents = _eventCounts.Values.Sum();

                foreach (KeyValuePair<string, int> kvp in _eventCounts.OrderByDescending(k => k.Value))
                {
                    using (EditorGUILayout.VerticalScope entryScope = UIStyles.BeginCard())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(kvp.Key, UIStyles.SectionHeader, GUILayout.Width(__EVENT_LABEL_WIDTH));
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField($"{kvp.Value:N0}", GUILayout.Width(__EVENT_VALUE_WIDTH));
                            EditorGUILayout.LabelField($"({(kvp.Value * 100f / totalEvents):F1}%)", GUILayout.Width(__EVENT_PERCENT_WIDTH));
                        }

                        // Simple progress bar
                        Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(UIConstants.SPACING_DEFAULT));
                        float progress = totalEvents > 0 ? (float)kvp.Value / totalEvents : 0f;
                        EditorGUI.ProgressBar(progressRect, progress, string.Empty);
                    }
                    EditorGUILayout.Space(UIConstants.SPACING_SMALL);
                }
            }
        }
    }
}

