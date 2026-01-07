using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelSubmitWindow view implementation for ModelLibraryWindow.
    /// Handles the submission form view for creating new models or updating existing ones.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Initializes submit state when navigating to the Submit view.
        /// Creates and initializes the hidden ModelSubmitWindow instance.
        /// The instance field is defined in ModelLibraryWindow.State.cs.
        /// </summary>
        public void InitializeSubmitState()
        {
            // Create instance if needed - will be initialized by DrawEditorWindowView
            if (_submitWindowInstance == null)
            {
                _submitWindowInstance = CreateInstance<ModelSubmitWindow>();
            }
        }

        /// <summary>
        /// Draws the Submit view.
        /// Uses a hidden ModelSubmitWindow instance to render the full submission form.
        /// </summary>
        private void DrawSubmitView()
        {
            // Use helper method to render the EditorWindow instance
            DrawEditorWindowView(ref _submitWindowInstance);
        }
    }
}

