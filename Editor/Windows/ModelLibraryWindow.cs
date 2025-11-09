using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Main browser window for the Model Library system. Provides search, filtering, browsing, and model management features for repository content.
    /// </summary>
    public partial class ModelLibraryWindow : EditorWindow
    {
        /// <summary>
        /// Display mode options for the model browser.
        /// </summary>
        private enum ViewMode
        {
            /// <summary>Traditional list view with detailed information for each model.</summary>
            List,

            /// <summary>Grid view with thumbnail previews and compact information.</summary>
            Grid,

            /// <summary>Image-only view showing large thumbnails for visual browsing.</summary>
            ImageOnly
        }

        /// <summary>
        /// Opens the Model Library browser window from the Unity menu.
        /// </summary>
        [MenuItem("Tools/Model Library")]
        public static void Open()
        {
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            window.Show();
        }
    }
}

