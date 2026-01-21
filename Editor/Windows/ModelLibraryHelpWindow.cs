using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Centralized in-app help window for the Model Library ecosystem.
    /// Provides contextual guidance for browsing, searching, importing, and submitting models.
    /// </summary>
    public class ModelLibraryHelpWindow : EditorWindow
    {
        /// <summary>
        /// Top-level help sections available in the window.
        /// </summary>
        public enum HelpSection
        {
            /// <summary>Overview of the Model Library browser.</summary>
            Overview,
            /// <summary>Searching guidance and advanced operators.</summary>
            Searching,
            /// <summary>Filtering, tags, and saved presets.</summary>
            Filtering,
            /// <summary>Importing models, updates, and troubleshooting.</summary>
            Importing,
            /// <summary>Submitting new models or updates.</summary>
            Submission,
            /// <summary>Keyboard shortcuts and productivity tips.</summary>
            Shortcuts,
            /// <summary>Common issues and troubleshooting advice.</summary>
            Troubleshooting
        }

        private static readonly string[] __TAB_LABELS =
        {
            "Overview",
            "Searching",
            "Filtering",
            "Importing",
            "Submission",
            "Shortcuts",
            "Troubleshooting"
        };

        private HelpSection _selectedSection = HelpSection.Overview;
        private Vector2 _scrollPosition = Vector2.zero;

        /// <summary>
        /// Opens the help window.
        /// Now navigates to the Help view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.Help);
            }
        }

        /// <summary>
        /// Opens the help window focusing on a specific section.
        /// Now navigates to the Help view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        /// <param name="section">The section to display.</param>
        public static void OpenToSection(HelpSection section)
        {
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "helpSection", section }
                };
                window.NavigateToView(ModelLibraryWindow.ViewType.Help, parameters);
            }
        }

        private void InitializeWindowBounds()
        {
            minSize = new Vector2(520f, 420f);
            maxSize = new Vector2(900f, 720f);
        }

        private void OnGUI()
        {
            UIStyles.DrawPageHeader("Help Center", "Guides, tips, and troubleshooting.");
            GUILayout.Space(UIConstants.SPACING_SMALL);
            DrawSectionToolbar();
            GUILayout.Space(UIConstants.SPACING_STANDARD);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedSection)
            {
                case HelpSection.Overview:
                    DrawOverviewSection();
                    break;
                case HelpSection.Searching:
                    DrawSearchingSection();
                    break;
                case HelpSection.Filtering:
                    DrawFilteringSection();
                    break;
                case HelpSection.Importing:
                    DrawImportingSection();
                    break;
                case HelpSection.Submission:
                    DrawSubmissionSection();
                    break;
                case HelpSection.Shortcuts:
                    DrawShortcutsSection();
                    break;
                case HelpSection.Troubleshooting:
                    DrawTroubleshootingSection();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSectionToolbar()
        {
            int currentIndex = (int)_selectedSection;
            int newIndex = GUILayout.Toolbar(currentIndex, __TAB_LABELS);
            if (newIndex != currentIndex)
            {
                _selectedSection = (HelpSection)newIndex;
                _scrollPosition = Vector2.zero;
            }
        }

        private void DrawSectionHeader(string title)
        {
            UIStyles.DrawSectionHeader(title);
        }

        private void DrawParagraph(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            GUILayout.Space(UIConstants.SPACING_SMALL);
        }

        private void DrawBulletedList(string[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                EditorGUILayout.LabelField("• " + items[i], EditorStyles.wordWrappedLabel);
            }
            GUILayout.Space(UIConstants.SPACING_STANDARD);
        }

        private void DrawOverviewSection()
        {
            DrawSectionHeader("Model Library Overview");
            DrawParagraph("The Model Library lets you browse, search, and import curated models into your project without leaving the Unity Editor.");
            DrawParagraph("Use the toolbar to search, switch view modes, open settings, or trigger bulk operations. Visual indicators show updates, notes, favorites, and installation status at a glance.");
            DrawParagraph("A status banner appears when operations complete (imports, downloads, submissions) to keep feedback lightweight and non-blocking.");
        }

        private void DrawSearchingSection()
        {
            DrawSectionHeader("Searching");
            DrawParagraph("The search field matches across model names, descriptions, tags, and notes. Press Enter to store the query in your history.");
            DrawParagraph("Advanced Operators:");
            DrawBulletedList(new string[]
            {
                "Use \" AND \" (with spaces) to require all terms: character AND human",
                "Use \" OR \" (with spaces) for either term: character OR human",
                "Combine operators: character AND (human OR robot)",
                "Prefix-specific search terms: has:update, has:notes"
            });
            DrawParagraph("Keyboard shortcuts: Ctrl+F / Cmd+F focuses the search field, Enter saves to history, and Escape clears the current query.");
        }

        private void DrawFilteringSection()
        {
            DrawSectionHeader("Filtering and Tags");
            DrawParagraph("Toggle between Filter Mode tabs to refine by tags, favorites, recent models, or update availability. Saved presets capture both search queries and tag selections.");
            DrawBulletedList(new string[]
            {
                "Use the tag sidebar to combine multiple tags and see usage counts.",
                "Create saved presets for common workflows (e.g., \"Environment Favorites\").",
                "Clear Filters resets search text, tag selections, and preset filters.",
                "Tooltips on filter chips explain exactly what is currently applied."
            });
        }

        private void DrawImportingSection()
        {
            DrawSectionHeader("Importing and Updating Models");
            DrawParagraph("Select a model card to inspect metadata, then choose Import or Update. The system caches downloads, relinks textures, and keeps an import history for quick undo.");
            DrawBulletedList(new string[]
            {
                "Use Actions ▾ → Check Updates to refresh update indicators without a full repository reload.",
                "Bulk ▾ provides multi-selection import/update options when selection mode is enabled.",
                "During import, you can cancel the process. Completed imports appear in history for quick undo.",
                "If a GUID conflict occurs, the resolver dialog explains each option with recommended actions."
            });
            DrawParagraph("Troubleshooting: If an import fails, the new error dialog highlights retry steps and you can review detailed logs via Tools → Model Library → Error Log Viewer.");
        }

        private void DrawSubmissionSection()
        {
            DrawSectionHeader("Submitting Models");
            DrawParagraph("Artists can open the submission form from Actions ▾ → Submit Model or via Project view context menus. The form auto-saves drafts and provides validation hints inline.");
            DrawBulletedList(new string[]
            {
                "Tabs organize the workflow: Basic Info, Assets, Images, and Advanced (changelog).",
                "Switch between New Model and Update Existing using the toggle at the top of the form.",
                "Drag and drop preview images directly into the Images tab, where size and format checks run automatically.",
                "Use Save Draft to pause your work; you can clear the draft when finished or if requirements change."
            });
            DrawParagraph("Tip: Hover over the inline help icons to learn about naming rules, versioning (SemVer), and path selection best practices.");
        }

        private void DrawShortcutsSection()
        {
            DrawSectionHeader("Keyboard Shortcuts");
            DrawBulletedList(new string[]
            {
                "Ctrl+F / Cmd+F – Focus the search field.",
                "Ctrl+, / Cmd+, – Open Unified Settings.",
                "F5 – Refresh repository index when the window is focused.",
                "V – Cycle through List, Grid, and Image-only view modes.",
                "Arrow keys – Navigate search history once the dropdown is open.",
                "Shift+Click – Add or remove items while bulk selection mode is enabled."
            });
        }

        private void DrawTroubleshootingSection()
        {
            DrawSectionHeader("Troubleshooting");
            DrawParagraph("If something goes wrong, the error dialogs include a prominent Retry button, guidance, and a \"Don't show again\" option for repetitive issues.");
            DrawBulletedList(new string[]
            {
                "Use Tools → Model Library → Error Log Viewer to inspect recent issues and clear suppressions.",
                "Connection tests are available in Unified Settings → Repository tab (includes animated status).",
                "If models fail to appear, refresh the index (F5) and ensure the repository root is correct.",
                "For import texture issues, open the Model Preview window; its console logs detail any relinking problems.",
                "Contact an administrator if your role lacks permissions for submission or deletion features."
            });
        }
    }
}
