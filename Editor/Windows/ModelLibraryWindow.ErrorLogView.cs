using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ErrorLogViewerWindow view implementation for ModelLibraryWindow.
    /// Handles the error log viewer for reviewing recent errors and clearing suppressions.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Hidden ErrorLogViewerWindow instance for reuse.
        /// This instance is created once and reused to maintain log state and filters.
        /// </summary>
        private ErrorLogViewerWindow _errorLogInstance;

        /// <summary>
        /// Initializes error log state when navigating to the ErrorLog view.
        /// Creates the hidden ErrorLogViewerWindow instance if needed.
        /// </summary>
        public void InitializeErrorLogState()
        {
            // Create instance if needed - will be initialized by DrawEditorWindowView
            if (_errorLogInstance == null)
            {
                _errorLogInstance = CreateInstance<ErrorLogViewerWindow>();
            }
        }

        /// <summary>
        /// Draws the ErrorLog view.
        /// Uses a hidden ErrorLogViewerWindow instance to render error log entries and filters.
        /// </summary>
        private void DrawErrorLogView()
        {
            // Use helper method to render the EditorWindow instance
            DrawEditorWindowView(ref _errorLogInstance);
        }
    }
}

