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
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            bool canSubmit = identityProvider.GetUserRole() == UserRole.Artist;
            string secondaryLabel = canSubmit ? "Submit Model" : null;
            Action secondaryAction = canSubmit ? onSubmitModel : null;

            UIStyles.DrawEmptyState(title, message, "Refresh", onRefresh, secondaryLabel, secondaryAction);
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
        /// Draws a filter summary showing total and filtered model counts with colored badges for active filters.
        /// </summary>
        /// <param name="totalCount">Total number of models in repository.</param>
        /// <param name="filteredCount">Number of models matching current filters.</param>
        /// <param name="searchQuery">Current search query (null or empty if none).</param>
        /// <param name="selectedTags">Set of selected tags.</param>
        /// <param name="onClearFilters">Callback invoked when Clear Filters button is clicked.</param>
        public static void DrawFilterSummary(int totalCount, int filteredCount, string searchQuery, HashSet<string> selectedTags, Action onClearFilters)
        {
            bool hasActiveFilters = !string.IsNullOrWhiteSpace(searchQuery) || (selectedTags != null && selectedTags.Count > 0);

            using (new EditorGUILayout.HorizontalScope(UIStyles.CardBox))
            {
                GUILayout.Label($"{filteredCount} of {totalCount} models", UIStyles.SectionHeader);
                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Search badge with color
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    GUIStyle searchBadgeStyle = new GUIStyle(UIStyles.TagPill);
                    searchBadgeStyle.normal.textColor = UIConstants.COLOR_LIGHT_BLUE;
                    
                    string searchText = searchQuery.Trim();
                    if (searchText.Length > 30)
                    {
                        searchText = searchText.Substring(0, 27) + "...";
                    }
                    
                    Color originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(UIConstants.COLOR_LIGHT_BLUE.r, UIConstants.COLOR_LIGHT_BLUE.g, UIConstants.COLOR_LIGHT_BLUE.b, UIConstants.BADGE_BACKGROUND_ALPHA);
                    GUILayout.Label($"🔍 {searchText}", searchBadgeStyle);
                    GUI.backgroundColor = originalColor;
                    GUILayout.Space(UIConstants.SPACING_SMALL);
                }

                // Tags badge with color
                if (selectedTags != null && selectedTags.Count > 0)
                {
                    GUIStyle tagBadgeStyle = new GUIStyle(UIStyles.TagPill);
                    tagBadgeStyle.normal.textColor = UIConstants.COLOR_GREEN;
                    
                    string tagPreview = string.Join(", ", selectedTags.Take(2));
                    if (selectedTags.Count > 2)
                    {
                        tagPreview += $" (+{selectedTags.Count - 2})";
                    }
                    
                    Color originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(UIConstants.COLOR_GREEN.r, UIConstants.COLOR_GREEN.g, UIConstants.COLOR_GREEN.b, UIConstants.BADGE_BACKGROUND_ALPHA);
                    GUILayout.Label($"🏷️ {tagPreview}", tagBadgeStyle);
                    GUI.backgroundColor = originalColor;
                    GUILayout.Space(UIConstants.SPACING_SMALL);
                }

                GUIContent filterHelpContent = EditorGUIUtility.IconContent("_Help");
                if (filterHelpContent == null || filterHelpContent.image == null)
                {
                    filterHelpContent = new GUIContent("Help", "Learn about filtering, tags, and presets");
                }
                else
                {
                    filterHelpContent.tooltip = "Learn about filtering, tags, and presets";
                }
                float helpWidth = filterHelpContent.image != null ? UIConstants.BUTTON_HEIGHT_STANDARD : UIConstants.BUTTON_WIDTH_STANDARD;
                if (GUILayout.Button(filterHelpContent, EditorStyles.miniButton, GUILayout.Width(helpWidth)))
                {
                    ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Filtering);
                }

                GUILayout.Space(UIConstants.SPACING_SMALL);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!hasActiveFilters))
                {
                    if (GUILayout.Button("Clear Filters", GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM)))
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
        /// <param name="notesTooltip">Optional tooltip text for notes badge showing note preview.</param>
        public static void DrawNotificationBadges(bool hasNotes, bool hasUpdate, string notesTooltip = null)
        {
            // Draw notes badge if model has notes
            if (hasNotes)
            {
                // Create a more prominent style for notes badge
                GUIStyle notesBadgeStyle = new GUIStyle(GUI.skin.label);
                notesBadgeStyle.normal.textColor = new Color(0.3f, 0.7f, 1f); // Light blue
                notesBadgeStyle.fontStyle = FontStyle.Bold;
                
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
                    notesIcon.tooltip = !string.IsNullOrEmpty(notesTooltip) ? notesTooltip : "This model has feedback notes - Click to view";
                    // Make icon larger and more visible
                    GUILayout.Label(notesIcon, GUILayout.Width(20), GUILayout.Height(20));
                }
                else
                {
                    // Fallback to emoji if icon not available - make it larger and colored
                    Color originalColor = GUI.color;
                    GUI.color = new Color(0.3f, 0.7f, 1f); // Light blue
                    string tooltip = !string.IsNullOrEmpty(notesTooltip) ? notesTooltip : "This model has feedback notes - Click to view";
                    GUIContent notesContent = new GUIContent("💬", tooltip);
                    GUILayout.Label(notesContent, notesBadgeStyle, GUILayout.Width(25), GUILayout.Height(20));
                    GUI.color = originalColor;
                }
            }

            // Draw update badge if model has updates available
            if (hasUpdate)
            {
                // Create a more prominent style for update badge
                GUIStyle updateBadgeStyle = new GUIStyle(GUI.skin.label);
                updateBadgeStyle.normal.textColor = Color.yellow;
                updateBadgeStyle.fontStyle = FontStyle.Bold;
                
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
                    updateIcon.tooltip = "Update available - Click to view details and update";
                    // Make icon larger and more visible
                    GUILayout.Label(updateIcon, GUILayout.Width(20), GUILayout.Height(20));
                }
                else
                {
                    // Fallback to emoji if icon not available - make it larger and colored
                    Color originalColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label("🔄", updateBadgeStyle, GUILayout.Width(20), GUILayout.Height(20));
                    GUI.color = originalColor;
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
                GUILayout.Label("💬", GUILayout.Width(19), GUILayout.Height(14));
            }
            if (hasUpdate)
            {
                // Make update badge more prominent in grid view
                GUIStyle updateStyle = new GUIStyle(GUI.skin.label);
                updateStyle.normal.textColor = Color.yellow;
                Color originalColor = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label("🔄", updateStyle, GUILayout.Width(16), GUILayout.Height(16));
                GUI.color = originalColor;
            }
        }
    }
}

