using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests that browser grid column math stays within the viewport so rows do not overflow horizontally.
    /// </summary>
    public class GridLayoutUtilsTests
    {
        private const float ThumbnailSizeFromScreenshot = 81f;
        private const float InnerPadding = UIConstants.PADDING_SMALL;
        private const float InterCardSpacing = UIConstants.SPACING_STANDARD;
        private const float WideWindowWidth = 1800f;
        private const float BrowserChromeWidth = 42f;
        private const float LayoutEpsilon = 0.01f;

        /// <summary>
        /// Card GUILayout width must include CardBox left+right padding so the thumbnail fits inside the style.
        /// </summary>
        [Test]
        public void GetGridCardOuterWidth_IncludesCardBoxPadding()
        {
            const float thumbnailSize = 81f;
            float outerWidth = GridLayoutUtils.GetGridCardOuterWidth(thumbnailSize, InnerPadding);

            float expected = thumbnailSize + (InnerPadding * 2f) + GridLayoutUtils.CardBoxPaddingHorizontal;
            Assert.AreEqual(expected, outerWidth, LayoutEpsilon);
            Assert.Greater(outerWidth, thumbnailSize + (InnerPadding * 2f),
                "Outer width must be larger than content width; CardBox padding was previously omitted and caused overflow");
        }

        /// <summary>
        /// Layout footprint must include CardBox margin in addition to padding.
        /// </summary>
        [Test]
        public void GetGridCardLayoutWidth_IncludesPaddingAndMargin()
        {
            const float thumbnailSize = 128f;
            float layoutWidth = GridLayoutUtils.GetGridCardLayoutWidth(thumbnailSize, InnerPadding);
            float expected = GridLayoutUtils.GetGridCardOuterWidth(thumbnailSize, InnerPadding)
                + GridLayoutUtils.CardBoxMarginHorizontal;

            Assert.AreEqual(expected, layoutWidth, LayoutEpsilon);
        }

        /// <summary>
        /// Calculated columns must occupy no more than the available width; one extra column must not fit.
        /// </summary>
        [Test]
        public void CalculateColumnCount_FitsWithinAvailableWidth()
        {
            float[] thumbnailSizes = { 64f, 81f, 128f, 256f };
            float[] windowWidths = { 400f, 800f, 1200f, 1800f, 1920f };

            for (int sizeIndex = 0; sizeIndex < thumbnailSizes.Length; sizeIndex++)
            {
                float thumbnailSize = thumbnailSizes[sizeIndex];
                float cardLayoutWidth = GridLayoutUtils.GetGridCardLayoutWidth(thumbnailSize, InnerPadding);

                for (int widthIndex = 0; widthIndex < windowWidths.Length; widthIndex++)
                {
                    float availableWidth = windowWidths[widthIndex] - BrowserChromeWidth;
                    int columns = GridLayoutUtils.CalculateColumnCount(availableWidth, cardLayoutWidth, InterCardSpacing);
                    float usedWidth = GridLayoutUtils.GetRowWidth(columns, cardLayoutWidth, InterCardSpacing);

                    Assert.GreaterOrEqual(columns, 1, $"Should keep at least 1 column at width {windowWidths[widthIndex]}");
                    Assert.LessOrEqual(usedWidth, availableWidth + LayoutEpsilon,
                        $"Columns overflow viewport: thumbs={thumbnailSize}, window={windowWidths[widthIndex]}, columns={columns}, used={usedWidth}, available={availableWidth}");

                    float overflowWidth = GridLayoutUtils.GetRowWidth(columns + 1, cardLayoutWidth, InterCardSpacing);
                    Assert.Greater(overflowWidth, availableWidth,
                        $"Column count is too conservative: thumbs={thumbnailSize}, window={windowWidths[widthIndex]}, columns={columns}");
                }
            }
        }

        /// <summary>
        /// The 81px / ~1800px case from the overflowing grid screenshot must not pack 16 columns.
        /// </summary>
        [Test]
        public void CalculateColumnCount_DoesNotOverflowScreenshotViewport()
        {
            float cardLayoutWidth = GridLayoutUtils.GetGridCardLayoutWidth(ThumbnailSizeFromScreenshot, InnerPadding);
            float availableWidth = WideWindowWidth - BrowserChromeWidth;
            int columns = GridLayoutUtils.CalculateColumnCount(availableWidth, cardLayoutWidth, InterCardSpacing);
            float usedWidth = GridLayoutUtils.GetRowWidth(columns, cardLayoutWidth, InterCardSpacing);

            const int overflowingColumnCountFromScreenshot = 16;
            Assert.Less(columns, overflowingColumnCountFromScreenshot,
                "Old layout packed 16 columns that spilled past the viewport");
            Assert.LessOrEqual(usedWidth, availableWidth + LayoutEpsilon);
        }

        /// <summary>
        /// Degenerate widths still yield a usable single-column layout.
        /// </summary>
        [Test]
        public void CalculateColumnCount_ReturnsAtLeastOneColumn()
        {
            Assert.AreEqual(1, GridLayoutUtils.CalculateColumnCount(0f, 100f, InterCardSpacing));
            Assert.AreEqual(1, GridLayoutUtils.CalculateColumnCount(50f, 0f, InterCardSpacing));
            Assert.AreEqual(1, GridLayoutUtils.CalculateColumnCount(-10f, 80f, InterCardSpacing));
        }
    }
}
