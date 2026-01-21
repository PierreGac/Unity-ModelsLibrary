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

        /// <summary>Extra small spacing (2 pixels).</summary>
        public const float SPACING_EXTRA_SMALL = 2f;

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

        /// <summary>Alpha used for subtle badge backgrounds.</summary>
        public const float BADGE_BACKGROUND_ALPHA = 0.2f;

        /// <summary>Thumbnail placeholder background color.</summary>
        public static readonly Color COLOR_THUMBNAIL_PLACEHOLDER_BG = new Color(0.25f, 0.25f, 0.3f, 1f);

        /// <summary>Thumbnail placeholder border color.</summary>
        public static readonly Color COLOR_THUMBNAIL_PLACEHOLDER_BORDER = new Color(0.4f, 0.4f, 0.45f, 1f);

        /// <summary>Thumbnail placeholder text color.</summary>
        public static readonly Color COLOR_THUMBNAIL_PLACEHOLDER_TEXT = new Color(0.5f, 0.5f, 0.6f, 1f);

        /// <summary>Thumbnail placeholder loading text color.</summary>
        public static readonly Color COLOR_THUMBNAIL_PLACEHOLDER_TEXT_LOADING = new Color(0.7f, 0.7f, 0.8f, 1f);

        /// <summary>Selection/highlight background color for list/grid cards.</summary>
        public static readonly Color COLOR_SELECTION_BACKGROUND = new Color(0.2f, 0.4f, 0.6f, 0.35f);

        /// <summary>Selection outline color for image-only cards.</summary>
        public static readonly Color COLOR_SELECTION_OUTLINE = new Color(0.25f, 0.45f, 0.7f, 0.3f);

        /// <summary>Installed status badge color (green).</summary>
        public static readonly Color COLOR_STATUS_INSTALLED = new Color(0.1f, 0.7f, 0.2f);

        /// <summary>Installed status badge background color.</summary>
        public static readonly Color COLOR_STATUS_INSTALLED_BG = new Color(0.2f, 0.8f, 0.3f, 0.3f);

        /// <summary>Update available status badge color (yellow).</summary>
        public static readonly Color COLOR_STATUS_UPDATE = new Color(1f, 0.8f, 0f);

        /// <summary>Update available status badge background color.</summary>
        public static readonly Color COLOR_STATUS_UPDATE_BG = new Color(1f, 0.8f, 0f, 0.3f);

        /// <summary>Unknown version status badge color (gray).</summary>
        public static readonly Color COLOR_STATUS_UNKNOWN = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>Unknown version status badge background color.</summary>
        public static readonly Color COLOR_STATUS_UNKNOWN_BG = new Color(0.6f, 0.6f, 0.6f, 0.2f);

        /// <summary>Search history button active color (light blue).</summary>
        public static readonly Color COLOR_SEARCH_HISTORY_ACTIVE = new Color(0.7f, 0.9f, 1f);

        /// <summary>Bulk selection mode active color (orange).</summary>
        public static readonly Color COLOR_BULK_SELECTION_ACTIVE = new Color(1f, 0.7f, 0.3f);

        // Button color constants
        /// <summary>Primary button background color (blue).</summary>
        public static readonly Color COLOR_BUTTON_PRIMARY_BG = new Color(0.2f, 0.6f, 1f);

        /// <summary>Primary button hover background color (lighter blue).</summary>
        public static readonly Color COLOR_BUTTON_PRIMARY_BG_HOVER = new Color(0.3f, 0.7f, 1f);

        /// <summary>Primary button active background color (darker blue).</summary>
        public static readonly Color COLOR_BUTTON_PRIMARY_BG_ACTIVE = new Color(0.15f, 0.5f, 0.9f);

        /// <summary>Primary button text color (white).</summary>
        public static readonly Color COLOR_BUTTON_PRIMARY_TEXT = Color.white;

        /// <summary>Secondary button background color (gray).</summary>
        public static readonly Color COLOR_BUTTON_SECONDARY_BG = new Color(0.4f, 0.4f, 0.4f);

        /// <summary>Secondary button hover background color (lighter gray).</summary>
        public static readonly Color COLOR_BUTTON_SECONDARY_BG_HOVER = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>Secondary button active background color (darker gray).</summary>
        public static readonly Color COLOR_BUTTON_SECONDARY_BG_ACTIVE = new Color(0.3f, 0.3f, 0.3f);

        /// <summary>Secondary button text color (white).</summary>
        public static readonly Color COLOR_BUTTON_SECONDARY_TEXT = Color.white;

        /// <summary>Success button background color (green).</summary>
        public static readonly Color COLOR_BUTTON_SUCCESS_BG = new Color(0.2f, 0.7f, 0.3f);

        /// <summary>Success button hover background color (lighter green).</summary>
        public static readonly Color COLOR_BUTTON_SUCCESS_BG_HOVER = new Color(0.3f, 0.8f, 0.4f);

        /// <summary>Danger button background color (red).</summary>
        public static readonly Color COLOR_BUTTON_DANGER_BG = new Color(0.8f, 0.2f, 0.2f);

        /// <summary>Danger button hover background color (lighter red).</summary>
        public static readonly Color COLOR_BUTTON_DANGER_BG_HOVER = new Color(0.9f, 0.3f, 0.3f);

        /// <summary>Extra small padding (2 pixels).</summary>
        public const int PADDING_EXTRA_SMALL = 2;

        /// <summary>Small padding (4 pixels).</summary>
        public const int PADDING_SMALL = 4;

        /// <summary>Standard padding (6 pixels).</summary>
        public const int PADDING_STANDARD = 6;

        /// <summary>Large padding (10 pixels).</summary>
        public const int PADDING_LARGE = 10;

        /// <summary>Title font size.</summary>
        public const int FONT_SIZE_TITLE = 14;

        /// <summary>Section font size.</summary>
        public const int FONT_SIZE_SECTION = 12;

        /// <summary>Muted label font size.</summary>
        public const int FONT_SIZE_MUTED = 11;
    }
}

