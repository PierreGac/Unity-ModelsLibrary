using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Constants for UI layout values used throughout the editor windows.
    /// Centralizes UI spacing, sizing, and color definitions to eliminate magic numbers.
    /// </summary>
    public static class UIConstants
    {
        /// <summary>Small spacing (5 pixels).</summary>
        public const float SPACING_SMALL = 5f;

        /// <summary>Standard spacing (8 pixels).</summary>
        public const float SPACING_STANDARD = 8f;

        /// <summary>Default spacing (10 pixels).</summary>
        public const float SPACING_DEFAULT = 10f;

        /// <summary>Large spacing (20 pixels).</summary>
        public const float SPACING_LARGE = 20f;

        /// <summary>Standard button height (26 pixels).</summary>
        public const float BUTTON_HEIGHT_STANDARD = 26f;

        /// <summary>Large button height (30 pixels).</summary>
        public const float BUTTON_HEIGHT_LARGE = 30f;

        /// <summary>Extra large button height (35 pixels).</summary>
        public const float BUTTON_HEIGHT_EXTRA_LARGE = 35f;

        /// <summary>Standard label width (60 pixels).</summary>
        public const float LABEL_WIDTH_STANDARD = 60f;

        /// <summary>Medium label width (80 pixels).</summary>
        public const float LABEL_WIDTH_MEDIUM = 80f;

        /// <summary>Large label width (120 pixels).</summary>
        public const float LABEL_WIDTH_LARGE = 120f;

        /// <summary>Standard button width (100 pixels).</summary>
        public const float BUTTON_WIDTH_STANDARD = 100f;

        /// <summary>Medium button width (200 pixels).</summary>
        public const float BUTTON_WIDTH_MEDIUM = 200f;

        /// <summary>Large button width (250 pixels).</summary>
        public const float BUTTON_WIDTH_LARGE = 250f;

        /// <summary>Standard window minimum width (500 pixels).</summary>
        public const float WINDOW_MIN_WIDTH = 500f;

        /// <summary>Standard window minimum height (300 pixels).</summary>
        public const float WINDOW_MIN_HEIGHT = 300f;

        /// <summary>Standard window maximum width (600 pixels).</summary>
        public const float WINDOW_MAX_WIDTH = 600f;

        /// <summary>Standard window maximum height (500 pixels).</summary>
        public const float WINDOW_MAX_HEIGHT = 500f;

        /// <summary>Preview minimum size (400 pixels).</summary>
        public const float PREVIEW_MIN_SIZE = 400f;

        // Color constants
        /// <summary>Blue color for retry buttons.</summary>
        public static readonly Color COLOR_BLUE = new Color(0.2f, 0.6f, 1f);

        /// <summary>Red color for delete buttons.</summary>
        public static readonly Color COLOR_RED = new Color(1f, 0.3f, 0.3f);

        /// <summary>Orange color for warnings.</summary>
        public static readonly Color COLOR_ORANGE = new Color(1f, 0.6f, 0f);

        /// <summary>Yellow color for warnings.</summary>
        public static readonly Color COLOR_YELLOW = new Color(1f, 0.8f, 0f);

        /// <summary>Green color for success/artist role.</summary>
        public static readonly Color COLOR_GREEN = new Color(0.4f, 0.8f, 0.4f);

        /// <summary>Light blue color for info.</summary>
        public static readonly Color COLOR_LIGHT_BLUE = new Color(0.5f, 0.5f, 1f);

        /// <summary>Blue-gray color for default role.</summary>
        public static readonly Color COLOR_BLUE_GRAY = new Color(0.6f, 0.6f, 0.8f);

        /// <summary>Error background red.</summary>
        public static readonly Color COLOR_ERROR_BACKGROUND = new Color(0.5f, 0f, 0f, 1f);

        /// <summary>Preview background gray.</summary>
        public static readonly Color COLOR_PREVIEW_BACKGROUND = new Color(0.2f, 0.2f, 0.2f, 1f);
    }
}

