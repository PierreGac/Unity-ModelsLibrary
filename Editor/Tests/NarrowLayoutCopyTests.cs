using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Destructive copy, empty details labels, and narrow toolbar rows.
    /// </summary>
    public class NarrowLayoutCopyTests
    {
        private const float WIDE_WINDOW_WIDTH = 1200f;
        private const float MEDIUM_WINDOW_WIDTH = 700f;
        private const float NARROW_WINDOW_WIDTH = 400f;
        private const string MODEL_NAME = "Crate";
        private const string MODEL_VERSION = "1.2.0";
        private const string NONE_LABEL = "(none)";

        /// <summary>
        /// Repository deletion says imported copies stay. Project removal is a separate local action.
        /// </summary>
        [Test]
        public void RepositoryDelete_StatesThatLocalInstallsRemain()
        {
            string deleteVersion = DestructiveActionCopy.BuildDeleteVersionMessage(MODEL_NAME, MODEL_VERSION);
            string deleteModel = DestructiveActionCopy.BuildDeleteModelMessage(MODEL_NAME);
            string removeLocal = DestructiveActionCopy.BuildRemoveFromProjectMessage(MODEL_NAME);

            StringAssert.Contains(DestructiveActionCopy.LOCAL_INSTALLS_REMAIN, deleteVersion);
            StringAssert.Contains(DestructiveActionCopy.REMOVE_FROM_PROJECT_IS_SEPARATE, deleteVersion);
            StringAssert.Contains(DestructiveActionCopy.LOCAL_INSTALLS_REMAIN, deleteModel);
            StringAssert.Contains(DestructiveActionCopy.REMOVE_FROM_PROJECT_SCOPE, removeLocal);
            Assert.AreNotEqual(deleteVersion, removeLocal);
            Assert.IsFalse(string.IsNullOrEmpty(DestructiveActionCopy.DELETE_VERSION_BUTTON_TOOLTIP));
            Assert.IsFalse(string.IsNullOrEmpty(DestructiveActionCopy.DELETE_MODEL_BUTTON_TOOLTIP));
            Assert.IsFalse(string.IsNullOrEmpty(DestructiveActionCopy.REMOVE_FROM_PROJECT_BUTTON_TOOLTIP));
        }

        /// <summary>
        /// Critical empty details use a sentence instead of (none).
        /// </summary>
        [Test]
        public void CriticalEmptyStates_DoNotUseNoneLabel()
        {
            Assert.AreNotEqual(NONE_LABEL, StringConstants.EMPTY_TAGS_LABEL);
            Assert.AreNotEqual(NONE_LABEL, StringConstants.EMPTY_NOTES_LABEL);
            Assert.AreNotEqual(NONE_LABEL, StringConstants.EMPTY_CHANGELOG_LABEL);
            Assert.IsFalse(string.IsNullOrEmpty(StringConstants.EMPTY_TAGS_LABEL));
            Assert.IsFalse(string.IsNullOrEmpty(StringConstants.EMPTY_NOTES_LABEL));
            Assert.IsFalse(string.IsNullOrEmpty(StringConstants.EMPTY_CHANGELOG_LABEL));
        }

        /// <summary>
        /// A narrow window gets extra toolbar rows so the fixed controls are not clipped on one line.
        /// </summary>
        [Test]
        public void NarrowWindow_UsesExtraToolbarRows()
        {
            Assert.AreEqual(LibraryToolbarLayout.SINGLE_ROW, LibraryToolbarLayout.GetRowCount(WIDE_WINDOW_WIDTH));
            Assert.AreEqual(LibraryToolbarLayout.TWO_ROWS, LibraryToolbarLayout.GetRowCount(MEDIUM_WINDOW_WIDTH));
            Assert.AreEqual(LibraryToolbarLayout.THREE_ROWS, LibraryToolbarLayout.GetRowCount(NARROW_WINDOW_WIDTH));
        }
    }
}
