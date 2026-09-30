using ModelLibrary.Editor.Settings;
using UnityEditor;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Chooses which repository types and repository actions the editor offers.
    /// HTTP stays behind an explicit experimental option, and repository delete stays on the filesystem.
    /// </summary>
    internal static class RepositoryKindChooser
    {
        /// <summary>Toggle that reveals the experimental HTTP repository type.</summary>
        internal const string EXPERIMENTAL_HTTP_TOGGLE_LABEL = "Enable experimental HTTP repository";

        /// <summary>Filesystem choice in the repository popup.</summary>
        internal const string FILE_SYSTEM_LABEL = "File System";

        /// <summary>HTTP choice in the repository popup.</summary>
        internal const string HTTP_EXPERIMENTAL_LABEL = "HTTP (Experimental)";

        /// <summary>Shown when repository delete buttons are omitted.</summary>
        internal const string REPOSITORY_DELETE_UNAVAILABLE_MESSAGE = "Repository delete is not available for the experimental HTTP repository.";

        internal const string FILE_SYSTEM_ROOT_TOOLTIP = "Absolute path or UNC path to the repository (for example, C:\\Models or \\\\server\\Models).";
        internal const string HTTP_ROOT_TOOLTIP = "Experimental. Base URL for the HTTP repository (for example, https://api.example.com/models). Delete and index rebuild are not available.";

        private const string FILE_SYSTEM_EXAMPLE = "File System: C:\\Models or \\\\server\\share\\ModelLibrary";
        private const string HTTP_EXAMPLE = "HTTP (Experimental): https://models.example.com/api";
        private const string EXAMPLES_PREFIX = "Examples:\n• ";
        private const int HTTP_POPUP_INDEX = 1;

        private static readonly string[] __FileSystemOnlyLabels = { FILE_SYSTEM_LABEL };
        private static readonly string[] __ExperimentalLabels = { FILE_SYSTEM_LABEL, HTTP_EXPERIMENTAL_LABEL };

        /// <summary>
        /// Repository delete is offered for a filesystem repository.
        /// </summary>
        /// <param name="kind">Selected repository type.</param>
        /// <returns>True when delete version and delete model may be shown.</returns>
        internal static bool OffersRepositoryDelete(ModelLibrarySettings.RepositoryKind kind)
        {
            return kind == ModelLibrarySettings.RepositoryKind.FileSystem;
        }

        /// <summary>
        /// HTTP remains listed when the experimental option is on, or the current selection is already HTTP.
        /// </summary>
        /// <param name="experimentalHttpEnabled">True after the user enables the experimental option.</param>
        /// <param name="currentKind">Kind currently shown in the form.</param>
        /// <returns>True when the popup includes HTTP.</returns>
        internal static bool IsHttpSelectable(bool experimentalHttpEnabled, ModelLibrarySettings.RepositoryKind currentKind)
        {
            return experimentalHttpEnabled || currentKind == ModelLibrarySettings.RepositoryKind.Http;
        }

        /// <summary>
        /// Labels for the repository popup.
        /// </summary>
        /// <param name="httpSelectable">True when HTTP may be chosen.</param>
        /// <returns>Filesystem only, or filesystem plus experimental HTTP.</returns>
        internal static string[] GetKindLabels(bool httpSelectable)
        {
            return httpSelectable ? __ExperimentalLabels : __FileSystemOnlyLabels;
        }

        /// <summary>
        /// Draws the experimental toggle.
        /// </summary>
        /// <param name="enabled">Current toggle value.</param>
        /// <returns>The edited toggle value.</returns>
        internal static bool DrawExperimentalHttpToggle(bool enabled)
        {
            return EditorGUILayout.ToggleLeft(EXPERIMENTAL_HTTP_TOGGLE_LABEL, enabled);
        }

        /// <summary>
        /// Draws the repository type popup.
        /// </summary>
        /// <param name="label">Popup label.</param>
        /// <param name="currentKind">Kind currently shown.</param>
        /// <param name="experimentalHttpEnabled">True when the experimental option is on.</param>
        /// <returns>The selected kind.</returns>
        internal static ModelLibrarySettings.RepositoryKind DrawKindPopup(
            string label,
            ModelLibrarySettings.RepositoryKind currentKind,
            bool experimentalHttpEnabled)
        {
            bool httpSelectable = IsHttpSelectable(experimentalHttpEnabled, currentKind);
            string[] labels = GetKindLabels(httpSelectable);
            int index = GetKindIndex(currentKind, httpSelectable);
            int selected = EditorGUILayout.Popup(label, index, labels);
            return GetKindAtIndex(selected, httpSelectable);
        }

        /// <summary>
        /// Example text for the connection step.
        /// </summary>
        /// <param name="httpSelectable">True when the HTTP example should be included.</param>
        /// <returns>Filesystem example, plus the HTTP example when it is selectable.</returns>
        internal static string GetConnectionExamples(bool httpSelectable)
        {
            if (!httpSelectable)
            {
                return EXAMPLES_PREFIX + FILE_SYSTEM_EXAMPLE;
            }

            return EXAMPLES_PREFIX + FILE_SYSTEM_EXAMPLE + "\n• " + HTTP_EXAMPLE;
        }

        private static int GetKindIndex(ModelLibrarySettings.RepositoryKind kind, bool httpSelectable)
        {
            if (httpSelectable && kind == ModelLibrarySettings.RepositoryKind.Http)
            {
                return HTTP_POPUP_INDEX;
            }

            return 0;
        }

        private static ModelLibrarySettings.RepositoryKind GetKindAtIndex(int index, bool httpSelectable)
        {
            if (httpSelectable && index == HTTP_POPUP_INDEX)
            {
                return ModelLibrarySettings.RepositoryKind.Http;
            }

            return ModelLibrarySettings.RepositoryKind.FileSystem;
        }
    }
}
