using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Dedicated reference window listing keyboard shortcuts and productivity tips for the Model Library.
    /// </summary>
    public class ModelLibraryShortcutsWindow : EditorWindow
    {
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

        private static readonly ShortcutSection[] __Sections =
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

        private Vector2 _scrollPosition;

        /// <summary>
        /// Opens the shortcuts window.
        /// </summary>
        public static void Open()
        {
            ModelLibraryShortcutsWindow window = GetWindow<ModelLibraryShortcutsWindow>("Model Library Shortcuts");
            window.minSize = new Vector2(480f, 360f);
            window.maxSize = new Vector2(620f, 520f);
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Keep this window open while exploring the Model Library. Many shortcuts mirror familiar IDE bindings.", MessageType.Info);

            GUILayout.Space(4f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < __Sections.Length; i++)
            {
                DrawSection(__Sections[i]);
                GUILayout.Space(10f);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("Tip: You can also open the in-app help center for deep dives into workflows and troubleshooting.", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Help Center", GUILayout.Width(160f), GUILayout.Height(24f)))
            {
                ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Shortcuts);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private void DrawSection(ShortcutSection section)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            EditorGUILayout.LabelField(section.Title, headerStyle);

            for (int i = 0; i < section.Entries.Length; i++)
            {
                ShortcutEntry entry = section.Entries[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(entry.Combination, EditorStyles.miniBoldLabel, GUILayout.Width(180f));
                    GUILayout.Label(entry.Description, EditorStyles.wordWrappedLabel);
                }
            }
        }
    }
}
