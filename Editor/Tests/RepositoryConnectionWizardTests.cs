using System.IO;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEditor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// The setup wizard rejects a missing filesystem folder unless the user chooses save-without-testing.
    /// </summary>
    public class RepositoryConnectionWizardTests
    {
        private const string MISSING_FOLDER_NAME = "ModelLibraryMissingRepo6E";
        private const string VALID_HTTP_URL = "https://models.example.com/api";
        private const string INVALID_HTTP_URL = "not-a-url";

        private ModelLibrarySettings.RepositoryKind _savedKind;
        private string _savedRoot;
        private string _missingRoot;

        /// <summary>
        /// Remembers the project repository and picks a folder that does not exist.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            _savedKind = settings.repositoryKind;
            _savedRoot = settings.repositoryRoot;
            _missingRoot = Path.Combine(Path.GetTempPath(), MISSING_FOLDER_NAME);
            if (Directory.Exists(_missingRoot))
            {
                Directory.Delete(_missingRoot, true);
            }
        }

        /// <summary>
        /// Restores the project repository.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryKind = _savedKind;
            settings.repositoryRoot = _savedRoot;
            settings.SaveProjectCopy();
        }

        /// <summary>
        /// The default connection path does not save a missing directory.
        /// </summary>
        [Test]
        public void MissingDirectory_IsRejectedOnDefaultPath()
        {
            string message;
            MessageType messageType;
            bool saved = RepositoryConnectionValidator.TrySaveRepository(
                ModelLibrarySettings.RepositoryKind.FileSystem,
                _missingRoot,
                false,
                out message,
                out messageType);

            Assert.IsFalse(saved);
            Assert.AreEqual(MessageType.Error, messageType);
            Assert.IsFalse(string.IsNullOrEmpty(message));
            Assert.AreEqual(_savedRoot, ModelLibrarySettings.GetOrCreate().repositoryRoot);
        }

        /// <summary>
        /// Choosing save-without-testing stores the missing directory.
        /// </summary>
        [Test]
        public void SaveWithoutTesting_StoresMissingDirectory()
        {
            string message;
            MessageType messageType;
            bool saved = RepositoryConnectionValidator.TrySaveRepository(
                ModelLibrarySettings.RepositoryKind.FileSystem,
                _missingRoot,
                true,
                out message,
                out messageType);

            Assert.IsTrue(saved);
            Assert.AreEqual(RepositoryConnectionValidator.SAVE_WITHOUT_TESTING_LABEL, "Save without testing");
            Assert.AreEqual(MessageType.Warning, messageType);
            Assert.AreEqual(_missingRoot, ModelLibrarySettings.GetOrCreate().repositoryRoot);
        }

        /// <summary>
        /// HTTP acceptance is URL shape only.
        /// </summary>
        [Test]
        public void HttpRepository_AcceptsUrlShapeOnly()
        {
            string message;
            MessageType messageType;
            bool valid = RepositoryConnectionValidator.TryValidate(
                ModelLibrarySettings.RepositoryKind.Http,
                VALID_HTTP_URL,
                false,
                out message,
                out messageType);
            bool invalid = RepositoryConnectionValidator.TryValidate(
                ModelLibrarySettings.RepositoryKind.Http,
                INVALID_HTTP_URL,
                false,
                out message,
                out messageType);

            Assert.IsTrue(valid);
            Assert.IsFalse(invalid);
            Assert.AreEqual(MessageType.Error, messageType);
        }
    }
}
