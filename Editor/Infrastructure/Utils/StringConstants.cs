namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Constants for string length limits and common string values.
    /// Centralizes string-related constants to eliminate magic numbers.
    /// </summary>
    public static class StringConstants
    {
        /// <summary>Maximum length for string truncation in tooltips (50 characters).</summary>
        public const int MAX_TOOLTIP_PREVIEW_LENGTH = 50;

        /// <summary>Maximum path length (200 characters).</summary>
        public const int MAX_PATH_LENGTH = 200;

        /// <summary>Length of "Assets/" prefix (7 characters).</summary>
        public const int ASSETS_PREFIX_LENGTH = 7;

        /// <summary>Default version string.</summary>
        public const string DEFAULT_VERSION = "1.0.0";

        /// <summary>Default model name.</summary>
        public const string DEFAULT_MODEL_NAME = "New Model";

        /// <summary>Default install path.</summary>
        public const string DEFAULT_INSTALL_PATH = "Assets/Models/NewModel";

        /// <summary>Label for submitting a new repository version of an existing model.</summary>
        public const string NEW_VERSION_BUTTON_LABEL = "New Version";

        /// <summary>Tooltip for the New Version action (publish to repository, not local import).</summary>
        public const string NEW_VERSION_BUTTON_TOOLTIP = "Submit a new version of this model to the repository";

        /// <summary>Label for changing the model selected in Update Existing mode.</summary>
        public const string CHANGE_MODEL_BUTTON_LABEL = "Change";

        /// <summary>Label for choosing a model when Update Existing has no valid selection.</summary>
        public const string SELECT_MODEL_BUTTON_LABEL = "Select Model";
    }
}
