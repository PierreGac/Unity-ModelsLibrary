using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing AnalyticsWindow view implementation for ModelLibraryWindow.
    /// Handles the analytics view for viewing model usage statistics and reports.
    /// Only accessible to Administrators and Artists.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Hidden AnalyticsWindow instance for reuse.
        /// This instance is created once and reused to maintain analytics state.
        /// </summary>
        private AnalyticsWindow _analyticsInstance;

        /// <summary>
        /// Initializes analytics state when navigating to the Analytics view.
        /// Creates the hidden AnalyticsWindow instance if needed.
        /// </summary>
        public void InitializeAnalyticsState()
        {
            // Create instance if needed - will be initialized by DrawEditorWindowView
            if (_analyticsInstance == null)
            {
                _analyticsInstance = CreateInstance<AnalyticsWindow>();
            }
        }

        /// <summary>
        /// Draws the Analytics view.
        /// Uses a hidden AnalyticsWindow instance to render analytics data and statistics.
        /// </summary>
        private void DrawAnalyticsView()
        {
            // Use helper method to render the EditorWindow instance
            DrawEditorWindowView(ref _analyticsInstance);
        }
    }
}

