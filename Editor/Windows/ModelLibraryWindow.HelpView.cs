using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelLibraryHelpWindow view implementation for ModelLibraryWindow.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        private static readonly string[] __HELP_TAB_LABELS =
        {
            "Overview",
            "Searching",
            "Filtering",
            "Importing",
            "Submission",
            "Shortcuts",
            "Troubleshooting"
        };

        /// <summary>
        /// Initializes help state when navigating to the Help view.
        /// </summary>
        public void InitializeHelpState()
        {
            // Check if a specific section was requested via parameters
            if (HasViewParameter("helpSection"))
            {
                object sectionObj = GetViewParameter<object>("helpSection");
                if (sectionObj is ModelLibraryHelpWindow.HelpSection)
                {
                    _helpSelectedSection = (ModelLibraryHelpWindow.HelpSection)sectionObj;
                    _helpScrollPosition = Vector2.zero;
                }
            }
            else
            {
                _helpSelectedSection = ModelLibraryHelpWindow.HelpSection.Overview;
                _helpScrollPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// Draws the Help view with all help content.
        /// </summary>
        private void DrawHelpView()
        {
            GUILayout.Space(6f);
            DrawHelpSectionToolbar();
            GUILayout.Space(8f);

            _helpScrollPosition = EditorGUILayout.BeginScrollView(_helpScrollPosition);

            switch (_helpSelectedSection)
            {
                case ModelLibraryHelpWindow.HelpSection.Overview:
                    DrawHelpOverviewSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Searching:
                    DrawHelpSearchingSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Filtering:
                    DrawHelpFilteringSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Importing:
                    DrawHelpImportingSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Submission:
                    DrawHelpSubmissionSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Shortcuts:
                    DrawHelpShortcutsSection();
                    break;
                case ModelLibraryHelpWindow.HelpSection.Troubleshooting:
                    DrawHelpTroubleshootingSection();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHelpSectionToolbar()
        {
            int currentIndex = (int)_helpSelectedSection;
            int newIndex = GUILayout.Toolbar(currentIndex, __HELP_TAB_LABELS);
            if (newIndex != currentIndex)
            {
                _helpSelectedSection = (ModelLibraryHelpWindow.HelpSection)newIndex;
                _helpScrollPosition = Vector2.zero;
            }
        }

        private void DrawHelpSectionHeader(string title)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                wordWrap = true
            };
            EditorGUILayout.LabelField(title, headerStyle);
            GUILayout.Space(6f);
        }

        private void DrawHelpParagraph(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            GUILayout.Space(4f);
        }

        private void DrawHelpBulletedList(string[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                EditorGUILayout.LabelField("• " + items[i], EditorStyles.wordWrappedLabel);
            }
            GUILayout.Space(6f);
        }

        private void DrawHelpOverviewSection()
        {
            DrawHelpSectionHeader("Model Library Overview");
            DrawHelpParagraph("The Model Library lets you browse, search, and import curated models into your project without leaving the Unity Editor.");
            DrawHelpParagraph("Use the toolbar to search, switch view modes, open settings, or trigger bulk operations. Visual indicators show updates, notes, favorites, and installation status at a glance.");
            DrawHelpParagraph("A status banner appears when operations complete (imports, downloads, submissions) to keep feedback lightweight and non-blocking.");
        }

        private void DrawHelpSearchingSection()
        {
            DrawHelpSectionHeader("Searching");
            DrawHelpParagraph("The search field matches across model names, descriptions, tags, and notes. Press Enter to store the query in your history.");
            DrawHelpParagraph("Advanced Operators:");
            DrawHelpBulletedList(new string[]
            {
                "Use \" AND \" (with spaces) to require all terms: character AND human",
                "Use \" OR \" (with spaces) for either term: character OR human",
                "Combine operators: character AND (human OR robot)",
                "Prefix-specific search terms: has:update, has:notes"
            });
            DrawHelpParagraph("Keyboard shortcuts: Ctrl+F / Cmd+F focuses the search field, Enter saves to history, and Escape clears the current query.");
        }

        private void DrawHelpFilteringSection()
        {
            DrawHelpSectionHeader("Filtering and Tags");
            DrawHelpParagraph("Toggle between Filter Mode tabs to refine by tags, favorites, recent models, or update availability. Saved presets capture both search queries and tag selections.");
            DrawHelpBulletedList(new string[]
            {
                "Use the tag sidebar to combine multiple tags and see usage counts.",
                "Create saved presets for common workflows (e.g., \"Environment Favorites\").",
                "Clear Filters resets search text, tag selections, and preset filters.",
                "Tooltips on filter chips explain exactly what is currently applied."
            });
        }

        private void DrawHelpImportingSection()
        {
            DrawHelpSectionHeader("Importing and Updating Models");
            DrawHelpParagraph("Select a model card to inspect metadata, then choose Import or Update. The system caches downloads, relinks textures, and keeps an import history for quick undo.");
            DrawHelpBulletedList(new string[]
            {
                "Use Actions ▾ → Check Updates to refresh update indicators without a full repository reload.",
                "Bulk ▾ provides multi-selection import/update options when selection mode is enabled.",
                "During import, you can cancel the process. Completed imports appear in history for quick undo.",
                "If a GUID conflict occurs, the resolver dialog explains each option with recommended actions."
            });
            DrawHelpParagraph("Troubleshooting: If an import fails, the new error dialog highlights retry steps and you can review detailed logs via Tools → Model Library → Error Log Viewer.");
        }

        private void DrawHelpSubmissionSection()
        {
            DrawHelpSectionHeader("Submitting Models");
            DrawHelpParagraph("Artists can open the submission form from Actions ▾ → Submit Model or via Project view context menus. The form auto-saves drafts and provides validation hints inline.");
            DrawHelpBulletedList(new string[]
            {
                "Tabs organize the workflow: Basic Info, Assets, Images, and Advanced (changelog).",
                "Switch between New Model and Update Existing using the toggle at the top of the form.",
                "Drag and drop preview images directly into the Images tab, where size and format checks run automatically.",
                "Use Save Draft to pause your work; you can clear the draft when finished or if requirements change."
            });
            DrawHelpParagraph("Tip: Hover over the inline help icons to learn about naming rules, versioning (SemVer), and path selection best practices.");
        }

        private void DrawHelpShortcutsSection()
        {
            DrawHelpSectionHeader("Keyboard Shortcuts");
            DrawHelpBulletedList(new string[]
            {
                "Ctrl+F / Cmd+F – Focus the search field.",
                "Ctrl+, / Cmd+, – Open Unified Settings.",
                "F5 – Refresh repository index when the window is focused.",
                "V – Cycle through List, Grid, and Image-only view modes.",
                "Arrow keys – Navigate search history once the dropdown is open.",
                "Shift+Click – Add or remove items while bulk selection mode is enabled."
            });
        }

        private void DrawHelpTroubleshootingSection()
        {
            DrawHelpSectionHeader("Troubleshooting");
            DrawHelpParagraph("If something goes wrong, the error dialogs include a prominent Retry button, guidance, and a \"Don't show again\" option for repetitive issues.");
            DrawHelpBulletedList(new string[]
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

