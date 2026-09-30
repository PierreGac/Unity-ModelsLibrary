using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Experimental HTTP does not offer repository delete, and it is not a default repository choice.
    /// </summary>
    public class ExperimentalHttpActionTests
    {
        private const int FILE_SYSTEM_ONLY_COUNT = 1;
        private const int HTTP_LABEL_INDEX = 1;
        /// <summary>
        /// A filesystem repository offers delete. HTTP does not.
        /// </summary>
        [Test]
        public void RepositoryDelete_IsOfferedForFileSystemOnly()
        {
            Assert.IsTrue(RepositoryKindChooser.OffersRepositoryDelete(ModelLibrarySettings.RepositoryKind.FileSystem));
            Assert.IsFalse(RepositoryKindChooser.OffersRepositoryDelete(ModelLibrarySettings.RepositoryKind.Http));
        }

        /// <summary>
        /// HTTP appears only after the experimental option is enabled, or when it is already selected.
        /// </summary>
        [Test]
        public void HttpKind_IsHiddenUntilExperimentalOptionIsEnabled()
        {
            string[] hidden = RepositoryKindChooser.GetKindLabels(false);
            string[] shown = RepositoryKindChooser.GetKindLabels(true);

            Assert.AreEqual(FILE_SYSTEM_ONLY_COUNT, hidden.Length);
            Assert.AreEqual(RepositoryKindChooser.FILE_SYSTEM_LABEL, hidden[0]);
            Assert.IsFalse(RepositoryKindChooser.IsHttpSelectable(false, ModelLibrarySettings.RepositoryKind.FileSystem));
            Assert.IsTrue(RepositoryKindChooser.IsHttpSelectable(true, ModelLibrarySettings.RepositoryKind.FileSystem));
            Assert.IsTrue(RepositoryKindChooser.IsHttpSelectable(false, ModelLibrarySettings.RepositoryKind.Http));
            Assert.AreEqual(RepositoryKindChooser.HTTP_EXPERIMENTAL_LABEL, shown[HTTP_LABEL_INDEX]);
        }
    }
}
