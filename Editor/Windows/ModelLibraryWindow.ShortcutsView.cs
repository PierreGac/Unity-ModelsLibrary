using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelLibraryShortcutsWindow view implementation for ModelLibraryWindow.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        private const float __SHORTCUT_LABEL_WIDTH = 180f;
        private struct ShortcutEntry
        {
            public string Combination;
            public string Description;

            public ShortcutEntry(string combination, string description)
            {
                Combination = combination;
                Description = description;
            }
        }

        private struct ShortcutSection
        {
            public string Title;
            public ShortcutEntry[] Entries;

            public ShortcutSection(string title, ShortcutEntry[] entries)
            {
                Title = title;
                Entries = entries;
            }
        }

        private static readonly ShortcutSection[] __SHORTCUT_SECTIONS =
        {
            new ShortcutSection(
                "Global",
                new[]
                {
                    new ShortcutEntry("Ctrl+F / Cmd+F", "Focus the search field"),
                    new ShortcutEntry("Ctrl+, / Cmd+,", "Open Unified Settings"),
                    new ShortcutEntry("Ctrl+; / Cmd+;", "Open Unified Settings (alternate binding)"),
                    new ShortcutEntry("F5", "Refresh the repository index"),
                    new ShortcutEntry("Ctrl+Shift+L / Cmd+Shift+L", "Open keyboard shortcuts reference"),
                    new ShortcutEntry("Ctrl+B / Cmd+B", "Toggle bulk selection mode"),
                    new ShortcutEntry("Ctrl+Shift+T / Cmd+Shift+T", "Open bulk tag editor for selected models"),
                    new ShortcutEntry("Ctrl+I / Cmd+I", "Import the highlighted model"),
                    new ShortcutEntry("Ctrl+U / Cmd+U", "Update highlighted model (when update available)")
                }),
            new ShortcutSection(
                "Browser",
                new[]
                {
                    new ShortcutEntry("V", "Cycle view mode (List → Grid → Image-only)"),
                    new ShortcutEntry("Enter (search field)", "Save current search to history"),
                    new ShortcutEntry("Enter (navigation)", "Open details for the highlighted model"),
                    new ShortcutEntry("Arrow Keys", "Navigate list/grid results"),
                    new ShortcutEntry("Space", "Toggle favorite (or selection when bulk mode active)"),
                    new ShortcutEntry("Escape", "Clear search or exit selection mode"),
                    new ShortcutEntry("Shift+Click", "Add/remove models while bulk selection mode is active")
                }),
            new ShortcutSection(
                "Submission",
                new[]
                {
                    new ShortcutEntry("Ctrl+Enter / Cmd+Enter", "Submit model when the form is valid"),
                    new ShortcutEntry("Ctrl+Shift+S / Cmd+Shift+S", "Save submission draft"),
                    new ShortcutEntry("Tab / Shift+Tab", "Move between input fields and tabs")
                })
        };

        /// <summary>
        /// Scroll position for shortcuts view.
        /// </summary>
        private Vector2 _shortcutsScrollPosition;

        /// <summary>
        /// Draws the Shortcuts view.
        /// </summary>
        private void DrawShortcutsView()
        {
            UIStyles.DrawPageHeader("Keyboard Shortcuts", "Boost productivity with quick actions.");
            EditorGUILayout.HelpBox("Keep this window open while exploring the Model Library. Many shortcuts mirror familiar IDE bindings.", MessageType.Info);

            GUILayout.Space(UIConstants.SPACING_SMALL);
            _shortcutsScrollPosition = EditorGUILayout.BeginScrollView(_shortcutsScrollPosition);
            for (int i = 0; i < __SHORTCUT_SECTIONS.Length; i++)
            {
                DrawShortcutSection(__SHORTCUT_SECTIONS[i]);
                GUILayout.Space(UIConstants.SPACING_DEFAULT);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("Tip: You can also open the in-app help center for deep dives into workflows and troubleshooting.", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Help Center", GUILayout.Width(UIConstants.BUTTON_WIDTH_LARGE), GUILayout.Height(UIConstants.BUTTON_HEIGHT_STANDARD)))
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "helpSection", ModelLibraryHelpWindow.HelpSection.Shortcuts }
                };
                NavigateToView(ViewType.Help, parameters);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(UIConstants.SPACING_STANDARD);
        }

        private void DrawShortcutSection(ShortcutSection section)
        {
            UIStyles.DrawSectionHeader(section.Title);

            for (int i = 0; i < section.Entries.Length; i++)
            {
                ShortcutEntry entry = section.Entries[i];
                using (new EditorGUILayout.HorizontalScope(UIStyles.CardBox))
                {
                    GUILayout.Label(entry.Combination, UIStyles.MutedLabel, GUILayout.Width(__SHORTCUT_LABEL_WIDTH));
                    GUILayout.Label(entry.Description, EditorStyles.wordWrappedLabel);
                }
            }
        }
    }
}

