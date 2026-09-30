namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Decides how many toolbar rows the library browser needs for the current window width.
    /// </summary>
    internal static class LibraryToolbarLayout
    {
        /// <summary>Width at or above which every toolbar control fits on one row.</summary>
        internal const float SINGLE_ROW_MIN_WIDTH = 980f;

        /// <summary>Width at or above which search and commands fit on two rows.</summary>
        internal const float TWO_ROW_MIN_WIDTH = 640f;

        /// <summary>All controls share one toolbar row.</summary>
        internal const int SINGLE_ROW = 1;

        /// <summary>Search is on the first row and the remaining controls share the second.</summary>
        internal const int TWO_ROWS = 2;

        /// <summary>Search, commands, and view controls each have their own row.</summary>
        internal const int THREE_ROWS = 3;

        /// <summary>
        /// Returns how many toolbar rows fit the window without clipping the fixed controls.
        /// </summary>
        /// <param name="windowWidth">Current editor window width in pixels.</param>
        /// <returns>1, 2, or 3.</returns>
        internal static int GetRowCount(float windowWidth)
        {
            if (windowWidth >= SINGLE_ROW_MIN_WIDTH)
            {
                return SINGLE_ROW;
            }

            if (windowWidth >= TWO_ROW_MIN_WIDTH)
            {
                return TWO_ROWS;
            }

            return THREE_ROWS;
        }
    }
}
