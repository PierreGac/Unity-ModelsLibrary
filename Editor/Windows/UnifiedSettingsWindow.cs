using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Utility class for unified settings functionality.
    /// Provides access to settings tab enumeration and navigation to the settings view.
    /// The actual settings UI is implemented in ModelLibraryWindow.SettingsView.
    /// </summary>
    public static class UnifiedSettingsWindow
    {
        /// <summary>
        /// Tab selection for the settings view.
        /// </summary>
        public enum SettingsTab
        {
            /// <summary>User settings tab (name, role).</summary>
            User,
            /// <summary>Repository settings tab (repository type, URL/path, cache).</summary>
            Repository
        }

        /// <summary>
        /// Opens the unified settings view.
        /// Navigates to the Settings view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            ModelLibraryWindow window = EditorWindow.GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.Settings);
                window.InitializeSettingsState();
            }
        }
    }
}

