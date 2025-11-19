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
    }
}

