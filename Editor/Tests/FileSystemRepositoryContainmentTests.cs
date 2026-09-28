using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Verifies filesystem repository operations stay inside the configured root.
    /// </summary>
    public class FileSystemRepositoryContainmentTests
    {
        private const string SAFE_MODEL_ID = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
        private const string SAFE_VERSION = "1.0.0";
        private const string SENTINEL_FILE_NAME = "sentinel.txt";
        private const string SENTINEL_TEXT = "keep";
        private const string LOCAL_FILE_NAME = "source.obj";
        private const string PAYLOAD_RELATIVE_PATH = SAFE_MODEL_ID + "/" + SAFE_VERSION + "/payload/hero.obj";

        private string _tempRoot;
        private string _repositoryRoot;
        private string _outsideDirectory;
        private string _sentinelPath;
        private string _localSourcePath;
        private FileSystemRepository _repository;

        /// <summary>
        /// Creates a repository root and a sibling sentinel that must survive every rejected call.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ModelLibraryContainment_" + Guid.NewGuid().ToString("N"));
            _repositoryRoot = Path.Combine(_tempRoot, "repo");
            _outsideDirectory = Path.Combine(_tempRoot, "outside");
            Directory.CreateDirectory(_repositoryRoot);
            Directory.CreateDirectory(_outsideDirectory);
            _sentinelPath = Path.Combine(_outsideDirectory, SENTINEL_FILE_NAME);
            File.WriteAllText(_sentinelPath, SENTINEL_TEXT);
            _localSourcePath = Path.Combine(_tempRoot, LOCAL_FILE_NAME);
            File.WriteAllText(_localSourcePath, "v 0 0 0\n");
            _repository = new FileSystemRepository(_repositoryRoot);
        }

        /// <summary>
        /// Removes the temporary repository and the sibling sentinel directory.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        /// <summary>
        /// Rejects unsafe model ids and versions for load, save, and delete.
        /// </summary>
        [Test]
        public async Task IdentifierOperations_RejectUnsafeIds_AndLeaveOutsideUntouched()
        {
            string[] unsafeIdentifiers = new string[]
            {
                "..",
                "foo/bar",
                "foo\\bar",
                "CON",
                "COM1",
                _outsideDirectory
            };

            ModelMeta meta = CreateMeta();
            for (int i = 0; i < unsafeIdentifiers.Length; i++)
            {
                string bad = unsafeIdentifiers[i];
                await AssertRejects(async () => await _repository.LoadMetaAsync(bad, SAFE_VERSION));
                await AssertRejects(async () => await _repository.SaveMetaAsync(bad, SAFE_VERSION, meta));
                await AssertRejects(async () => await _repository.DeleteVersionAsync(bad, SAFE_VERSION));
                await AssertRejects(async () => await _repository.DeleteModelAsync(bad));
                await AssertRejects(async () => await _repository.LoadMetaAsync(SAFE_MODEL_ID, bad));
                await AssertRejects(async () => await _repository.SaveMetaAsync(SAFE_MODEL_ID, bad, meta));
                await AssertRejects(async () => await _repository.DeleteVersionAsync(SAFE_MODEL_ID, bad));
            }

            ModelLibraryService service = new ModelLibraryService(_repository);
            await AssertRejects(async () => await service.GetMetaAsync("..", SAFE_VERSION));
            await AssertRejects(async () => await service.DeleteVersionAsync("..", SAFE_VERSION));
            await AssertRejects(async () => await service.DeleteModelAsync(".."));
            await AssertRejects(async () => await service.GetAvailableVersionsAsync("CON"));
            ModelMeta unsafeSubmit = CreateMeta();
            unsafeSubmit.version = "..";
            await AssertRejects(async () => await service.SubmitNewVersionAsync(unsafeSubmit, _repositoryRoot));

            AssertSentinelIntact();
        }

        /// <summary>
        /// Rejects traversal, rooted, device-name, and absolute paths for directory and file operations.
        /// </summary>
        [Test]
        public async Task RelativeOperations_RejectEscapingPaths_AndLeaveOutsideUntouched()
        {
            string[] unsafePaths = new string[]
            {
                "..",
                "../outside",
                "payload/../../outside",
                "CON",
                "COM1",
                _outsideDirectory
            };

            string downloadDestination = Path.Combine(_tempRoot, "download.obj");
            for (int i = 0; i < unsafePaths.Length; i++)
            {
                string bad = unsafePaths[i];
                await AssertRejects(async () => await _repository.ListFilesAsync(bad));
                await AssertRejects(async () => await _repository.DirectoryExistsAsync(bad));
                await AssertRejects(async () => await _repository.EnsureDirectoryAsync(bad));
                await AssertRejects(async () => await _repository.UploadFileAsync(bad, _localSourcePath));
                await AssertRejects(async () => await _repository.DownloadFileAsync(bad, downloadDestination));
            }

            AssertSentinelIntact();
            Assert.IsFalse(File.Exists(downloadDestination), "A rejected download must not create a destination file.");
        }

        /// <summary>
        /// Saves, lists, uploads, downloads, and deletes a safe model inside the repository root.
        /// </summary>
        [Test]
        public async Task SafeOperations_StayInsideRepositoryRoot()
        {
            ModelMeta meta = CreateMeta();
            await _repository.SaveMetaAsync(SAFE_MODEL_ID, SAFE_VERSION, meta);
            string savedMeta = Path.Combine(_repositoryRoot, SAFE_MODEL_ID, SAFE_VERSION, ModelMeta.MODEL_JSON);
            Assert.IsTrue(File.Exists(savedMeta));
            PathUtils.AssertInsideRoot(savedMeta, _repositoryRoot);

            await _repository.UploadFileAsync(PAYLOAD_RELATIVE_PATH, _localSourcePath);
            string uploaded = Path.Combine(_repositoryRoot, SAFE_MODEL_ID, SAFE_VERSION, "payload", "hero.obj");
            Assert.IsTrue(File.Exists(uploaded));
            PathUtils.AssertInsideRoot(uploaded, _repositoryRoot);

            List<string> listed = await _repository.ListFilesAsync(SAFE_MODEL_ID);
            Assert.Greater(listed.Count, 0);
            for (int i = 0; i < listed.Count; i++)
            {
                string fullPath = Path.GetFullPath(Path.Combine(_repositoryRoot, listed[i]));
                PathUtils.AssertInsideRoot(fullPath, _repositoryRoot);
            }

            string downloadDestination = Path.Combine(_tempRoot, "downloaded.obj");
            await _repository.DownloadFileAsync(PAYLOAD_RELATIVE_PATH, downloadDestination);
            Assert.IsTrue(File.Exists(downloadDestination));

            bool deletedVersion = await _repository.DeleteVersionAsync(SAFE_MODEL_ID, SAFE_VERSION);
            Assert.IsTrue(deletedVersion);
            Assert.IsFalse(Directory.Exists(Path.Combine(_repositoryRoot, SAFE_MODEL_ID, SAFE_VERSION)));
            AssertSentinelIntact();
        }

        private static ModelMeta CreateMeta()
        {
            return new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = SAFE_MODEL_ID,
                    name = "Containment"
                },
                version = SAFE_VERSION
            };
        }

        private static async Task AssertRejects(Func<Task> operation)
        {
            bool rejected = false;
            try
            {
                await operation();
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected, "Unsafe repository path was accepted.");
        }

        private void AssertSentinelIntact()
        {
            Assert.IsTrue(File.Exists(_sentinelPath), "Sentinel outside the repository was deleted.");
            Assert.AreEqual(SENTINEL_TEXT, File.ReadAllText(_sentinelPath));
        }
    }
}
