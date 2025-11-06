using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Static helper class for drawing UI components in the Model Library browser window.
    /// Provides reusable UI drawing methods that take parameters instead of accessing instance fields.
    /// </summary>
    public static class ModelLibraryUIDrawer
    {
        /// <summary>
        /// Filter mode for displaying models.
        /// </summary>
        public enum FilterMode
        {
            /// <summary>Show all models.</summary>
            All,
            /// <summary>Show only favorite models.</summary>
            Favorites,
            /// <summary>Show only recently used models.</summary>
            Recent
        }

        /// <summary>
        /// Draws an empty state message with helpful guidance.
        /// Used when there are no models to display or when loading.
        /// </summary>
        /// <param name="title">Title of the empty state.</param>
        /// <param name="message">Helpful message explaining the situation and what to do.</param>
        /// <param name="onRefresh">Callback invoked when Refresh button is clicked.</param>
        /// <param name="onSubmitModel">Callback invoked when Submit Model button is clicked (only shown for Artists).</param>
        public static void DrawEmptyState(string title, string message, Action onRefresh, Action onSubmitModel)
        {
            EditorGUILayout.Space(20);

            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.FlexibleSpace();

                // Title
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Height(24));
                EditorGUILayout.Space(10);

                // Message
                EditorGUILayout.HelpBox(message, MessageType.Info);
                EditorGUILayout.Space(10);

                // Quick action buttons
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Refresh", GUILayout.Width(100), GUILayout.Height(30)))
                    {
                        onRefresh?.Invoke();
                    }

                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        if (GUILayout.Button("Submit Model", GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            onSubmitModel?.Invoke();
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>
        /// Draws filter mode tabs (All/Favorites/Recent).
        /// </summary>
        /// <param name="currentMode">The current filter mode.</param>
        /// <param name="favoritesCount">Number of favorite models.</param>
        /// <param name="recentCount">Number of recently used models.</param>
        /// <param name="onModeChanged">Callback invoked when filter mode changes. Parameter is the new mode.</param>
        /// <returns>The new filter mode (may be same as current if unchanged).</returns>
        public static FilterMode DrawFilterModeTabs(FilterMode currentMode, int favoritesCount, int recentCount, Action<FilterMode> onModeChanged)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] modeLabels = { "All", $"Favorites ({favoritesCount})", $"Recent ({recentCount})" };
                int newMode = GUILayout.Toolbar((int)currentMode, modeLabels, EditorStyles.toolbarButton);
                if (newMode != (int)currentMode)
                {
                    FilterMode newFilterMode = (FilterMode)newMode;
                    onModeChanged?.Invoke(newFilterMode);
                    return newFilterMode;
                }
            }
            EditorGUILayout.Space(5);
            return currentMode;
        }

        /// <summary>
        /// Draws a filter summary showing total and filtered model counts.
        /// </summary>
        /// <param name="totalCount">Total number of models in repository.</param>
        /// <param name="filteredCount">Number of models matching current filters.</param>
        /// <param name="searchQuery">Current search query (null or empty if none).</param>
        /// <param name="selectedTags">Set of selected tags.</param>
        /// <param name="onClearFilters">Callback invoked when Clear Filters button is clicked.</param>
        public static void DrawFilterSummary(int totalCount, int filteredCount, string searchQuery, HashSet<string> selectedTags, Action onClearFilters)
        {
            bool hasActiveFilters = !string.IsNullOrWhiteSpace(searchQuery) || (selectedTags != null && selectedTags.Count > 0);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{filteredCount} of {totalCount} models", EditorStyles.boldLabel);

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    GUILayout.Label($"Search: \"{searchQuery.Trim()}\"", EditorStyles.miniLabel);
                }

                if (selectedTags != null && selectedTags.Count > 0)
                {
                    string tagPreview = string.Join(", ", selectedTags.Take(3));
                    if (selectedTags.Count > 3)
                    {
                        tagPreview += $" (+{selectedTags.Count - 3})";
                    }
                    GUILayout.Label($"Tags: {tagPreview}", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!hasActiveFilters))
                {
                    if (GUILayout.Button("Clear Filters", GUILayout.Width(110)))
                    {
                        onClearFilters?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Draws notification badges for notes and updates in list view.
        /// </summary>
        /// <param name="hasNotes">Whether the model has feedback notes.</param>
        /// <param name="hasUpdate">Whether the model has updates available.</param>
        public static void DrawNotificationBadges(bool hasNotes, bool hasUpdate)
        {
            // Draw notes badge if model has notes
            if (hasNotes)
            {
                // Try multiple icon names for better compatibility
                GUIContent notesIcon = EditorGUIUtility.IconContent("console.infoicon");
                if (notesIcon == null || notesIcon.image == null)
                {
                    notesIcon = EditorGUIUtility.IconContent("d_console.infoicon");
                }
                if (notesIcon == null || notesIcon.image == null)
                {
                    notesIcon = EditorGUIUtility.IconContent("_Help");
                }

                if (notesIcon != null && notesIcon.image != null)
                {
                    notesIcon.tooltip = "This model has feedback notes";
                    GUILayout.Label(notesIcon, GUILayout.Width(16), GUILayout.Height(16));
                }
                else
                {
                    // Fallback to emoji if icon not available
                    GUILayout.Label("📝", GUILayout.Width(16), GUILayout.Height(16));
                }
            }

            // Draw update badge if model has updates available
            if (hasUpdate)
            {
                // Try multiple icon names for better compatibility
                GUIContent updateIcon = EditorGUIUtility.IconContent("d_Refresh");
                if (updateIcon == null || updateIcon.image == null)
                {
                    updateIcon = EditorGUIUtility.IconContent("Refresh");
                }
                if (updateIcon == null || updateIcon.image == null)
                {
                    updateIcon = EditorGUIUtility.IconContent("TreeEditor.Refresh");
                }

                if (updateIcon != null && updateIcon.image != null)
                {
                    updateIcon.tooltip = "Update available";
                    GUILayout.Label(updateIcon, GUILayout.Width(16), GUILayout.Height(16));
                }
                else
                {
                    // Fallback to emoji if icon not available
                    GUILayout.Label("🔄", GUILayout.Width(16), GUILayout.Height(16));
                }
            }

            // Add spacing if any badges were shown
            if (hasNotes || hasUpdate)
            {
                GUILayout.Space(4);
            }
        }

        /// <summary>
        /// Draws compact notification badges for grid view.
        /// </summary>
        /// <param name="hasNotes">Whether the model has feedback notes.</param>
        /// <param name="hasUpdate">Whether the model has updates available.</param>
        public static void DrawCompactNotificationBadges(bool hasNotes, bool hasUpdate)
        {
            if (hasNotes)
            {
                GUILayout.Label("📝", GUILayout.Width(12), GUILayout.Height(12));
            }
            if (hasUpdate)
            {
                GUILayout.Label("🔄", GUILayout.Width(12), GUILayout.Height(12));
            }
        }
    }
}

