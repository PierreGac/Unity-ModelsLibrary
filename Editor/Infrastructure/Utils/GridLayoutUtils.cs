using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Viewport-aware layout math for the model browser grid and image-only views.
    /// Card footprint values must stay in sync with <see cref="UIStyles.CardBox"/> padding and margin.
    /// </summary>
    public static class GridLayoutUtils
    {
        /// <summary>
        /// Left + right padding of <see cref="UIStyles.CardBox"/> (<see cref="UIConstants.PADDING_LARGE"/> on each side).
        /// </summary>
        public const float CardBoxPaddingHorizontal = UIConstants.PADDING_LARGE * 2f;

        /// <summary>
        /// Left + right margin of <see cref="UIStyles.CardBox"/> (<see cref="UIConstants.PADDING_SMALL"/> on each side).
        /// </summary>
        public const float CardBoxMarginHorizontal = UIConstants.PADDING_SMALL * 2f;

        /// <summary>
        /// Width assigned to a grid card via <c>GUILayout.Width</c>, including CardBox padding
        /// so the thumbnail and actions fit without expanding the card.
        /// </summary>
        /// <param name="thumbnailSize">Thumbnail edge length in pixels.</param>
        /// <param name="innerPadding">Extra padding applied inside the card around the thumbnail.</param>
        /// <returns>Outer GUILayout width of one grid card.</returns>
        public static float GetGridCardOuterWidth(float thumbnailSize, float innerPadding)
        {
            float contentWidth = thumbnailSize + (innerPadding * 2f);
            return contentWidth + CardBoxPaddingHorizontal;
        }

        /// <summary>
        /// Full horizontal footprint of one grid card, including CardBox margin.
        /// </summary>
        /// <param name="thumbnailSize">Thumbnail edge length in pixels.</param>
        /// <param name="innerPadding">Extra padding applied inside the card around the thumbnail.</param>
        /// <returns>Width consumed by one card before inter-card spacing.</returns>
        public static float GetGridCardLayoutWidth(float thumbnailSize, float innerPadding)
        {
            return GetGridCardOuterWidth(thumbnailSize, innerPadding) + CardBoxMarginHorizontal;
        }

        /// <summary>
        /// Calculates how many fixed-width cards fit on one row, accounting for inter-card spacing.
        /// </summary>
        /// <param name="availableWidth">Usable viewport width for the row.</param>
        /// <param name="cardLayoutWidth">Outer horizontal footprint of one card (content + padding + margins).</param>
        /// <param name="interCardSpacing">Space inserted between adjacent cards.</param>
        /// <returns>Column count of at least 1.</returns>
        public static int CalculateColumnCount(float availableWidth, float cardLayoutWidth, float interCardSpacing)
        {
            if (availableWidth <= 0f || cardLayoutWidth <= 0f)
            {
                return 1;
            }

            float stride = cardLayoutWidth + Mathf.Max(0f, interCardSpacing);
            return Mathf.Max(1, Mathf.FloorToInt((availableWidth + Mathf.Max(0f, interCardSpacing)) / stride));
        }

        /// <summary>
        /// Total width consumed by a row of <paramref name="columns"/> cards plus inter-card spacing.
        /// </summary>
        /// <param name="columns">Number of cards on the row.</param>
        /// <param name="cardLayoutWidth">Outer horizontal footprint of one card.</param>
        /// <param name="interCardSpacing">Space inserted between adjacent cards.</param>
        /// <returns>Required row width; 0 when <paramref name="columns"/> is not positive.</returns>
        public static float GetRowWidth(int columns, float cardLayoutWidth, float interCardSpacing)
        {
            if (columns <= 0)
            {
                return 0f;
            }

            return (columns * cardLayoutWidth) + (Mathf.Max(0, columns - 1) * Mathf.Max(0f, interCardSpacing));
        }
    }
}
