using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Submissions must contain one file the primary-model policy accepts.
    /// </summary>
    public class PrimaryModelSubmissionTests
    {
        private const string VERSION = "1.0.0";
        private const string MODEL_NAME = "Widget";
        private const string PRIMARY_FILE_STEM = "Hero";
        private const string IMAGE_FILE_NAME = "preview.png";
        private const string DISALLOWED_FILE_NAME = "Script.cs";
        private const string ACCEPTED_FOLDER_NAME = "Accepted";
        private const string IMAGE_FOLDER_NAME = "Images";
        private const string AUTHOR_NAME = "tester";

        [Test]
        public async Task MetadataOnly_IsRejected()
        {
            string repoRoot = CreateTempDirectory();
            string versionRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(versionRoot, ModelMeta.MODEL_JSON), "{}");
                await AssertSubmissionRejected(repoRoot, versionRoot, "meta-only-model");
            }
            finally
            {
                DeleteDirectory(repoRoot);
                DeleteDirectory(versionRoot);
            }
        }

        [Test]
        public async Task ImageOnly_IsRejected()
        {
            string samplePath = "Models/Preview" + FileExtensions.PNG;
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath(samplePath));

            string repoRoot = CreateTempDirectory();
            string versionRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(versionRoot, IMAGE_FILE_NAME), "image");
                await AssertSubmissionRejected(repoRoot, versionRoot, "image-only-model");
            }
            finally
            {
                DeleteDirectory(repoRoot);
                DeleteDirectory(versionRoot);
            }
        }

        [Test]
        public async Task DisallowedOnly_IsRejected()
        {
            string samplePath = "Models/Script" + FileExtensions.CS;
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath(samplePath));

            string repoRoot = CreateTempDirectory();
            string versionRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(versionRoot, DISALLOWED_FILE_NAME), "class Script {}");
                await AssertSubmissionRejected(repoRoot, versionRoot, "disallowed-only-model");
            }
            finally
            {
                DeleteDirectory(repoRoot);
                DeleteDirectory(versionRoot);
            }
        }

        [Test]
        public async Task PolicyAcceptedModel_Submits()
        {
            string acceptedExtension = RequirePolicyAcceptedExtension();
            string repoRoot = CreateTempDirectory();
            string versionRoot = CreateTempDirectory();
            string scanRoot = CreateTempDirectory();
            try
            {
                string fileName = PRIMARY_FILE_STEM + acceptedExtension;
                File.WriteAllText(Path.Combine(versionRoot, fileName), "mesh");

                string acceptedFolder = Path.Combine(scanRoot, ACCEPTED_FOLDER_NAME);
                string imageFolder = Path.Combine(scanRoot, IMAGE_FOLDER_NAME);
                Directory.CreateDirectory(acceptedFolder);
                Directory.CreateDirectory(imageFolder);
                File.WriteAllText(Path.Combine(acceptedFolder, fileName), "mesh");
                File.WriteAllText(Path.Combine(imageFolder, IMAGE_FILE_NAME), "image");

                List<BatchUploadService.BatchUploadItem> scanned = BatchUploadService.ScanDirectoryForModels(scanRoot);
                Assert.AreEqual(1, scanned.Count);
                Assert.AreEqual(ACCEPTED_FOLDER_NAME, scanned[0].modelName);

                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(repoRoot));
                ModelMeta meta = CreateMeta("accepted-model");
                string remotePath = await service.SubmitNewVersionAsync(meta, versionRoot);

                string uploadedPath = Path.Combine(repoRoot, remotePath.Replace('/', Path.DirectorySeparatorChar), fileName);
                Assert.IsTrue(File.Exists(uploadedPath));

                ModelLibraryService reopened = new ModelLibraryService(new FileSystemRepository(repoRoot));
                ModelIndex index = await reopened.GetIndexAsync();
                Assert.IsNotNull(FindEntry(index, "accepted-model"));

                BatchUploadService batch = new BatchUploadService(service, new TestIdentity());
                BatchUploadService.BatchUploadResult batchResult = await batch.UploadBatchAsync(scanned);
                Assert.AreEqual(1, batchResult.successfulUploads.Count);
                Assert.AreEqual(0, batchResult.failedUploads.Count);
            }
            finally
            {
                DeleteDirectory(repoRoot);
                DeleteDirectory(versionRoot);
                DeleteDirectory(scanRoot);
            }
        }

        [Test]
        public async Task BatchImageOnly_IsRejected()
        {
            string repoRoot = CreateTempDirectory();
            string imageFolder = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(imageFolder, IMAGE_FILE_NAME), "image");
                BatchUploadService.BatchUploadItem item = new BatchUploadService.BatchUploadItem
                {
                    folderPath = imageFolder,
                    modelName = IMAGE_FOLDER_NAME,
                    version = VERSION,
                    selected = true
                };
                List<BatchUploadService.BatchUploadItem> items = new List<BatchUploadService.BatchUploadItem>();
                items.Add(item);

                LogAssert.Expect(
                    LogType.Error,
                    "[BatchUploadService] Failed to upload " + IMAGE_FOLDER_NAME + ": " + AssetDependencyResolver.PRIMARY_MODEL_REQUIRED_MESSAGE);

                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(repoRoot));
                BatchUploadService batch = new BatchUploadService(service, new TestIdentity());
                BatchUploadService.BatchUploadResult result = await batch.UploadBatchAsync(items);

                Assert.AreEqual(0, result.successfulUploads.Count);
                Assert.AreEqual(1, result.failedUploads.Count);
                Assert.AreEqual(AssetDependencyResolver.PRIMARY_MODEL_REQUIRED_MESSAGE, result.failedUploads[0].errorMessage);
                Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "models_index.json")));
            }
            finally
            {
                DeleteDirectory(repoRoot);
                DeleteDirectory(imageFolder);
            }
        }

        private static async Task AssertSubmissionRejected(string repoRoot, string versionRoot, string modelId)
        {
            ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(repoRoot));
            InvalidOperationException thrown = null;
            try
            {
                await service.SubmitNewVersionAsync(CreateMeta(modelId), versionRoot);
            }
            catch (InvalidOperationException ex)
            {
                thrown = ex;
            }

            Assert.IsNotNull(thrown);
            Assert.AreEqual(AssetDependencyResolver.PRIMARY_MODEL_REQUIRED_MESSAGE, thrown.Message);
            Assert.IsFalse(Directory.Exists(Path.Combine(repoRoot, modelId)));
        }

        private static string RequirePolicyAcceptedExtension()
        {
            string[] candidates = new string[]
            {
                FileExtensions.FBX,
                FileExtensions.OBJ,
                FileExtensions.PNG,
                FileExtensions.MAT,
                FileExtensions.PREFAB,
                FileExtensions.CS
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string samplePath = "Models/Primary" + candidates[i];
                if (AssetDependencyResolver.IsMeshAssetPath(samplePath))
                {
                    return candidates[i];
                }
            }

            Assert.Fail("The primary-model policy did not accept any probed extension.");
            return string.Empty;
        }

        private static ModelMeta CreateMeta(string modelId)
        {
            ModelMeta meta = new ModelMeta();
            meta.identity = new ModelIdentity();
            meta.identity.id = modelId;
            meta.identity.name = MODEL_NAME;
            meta.version = VERSION;
            return meta;
        }

        private static ModelIndex.Entry FindEntry(ModelIndex index, string modelId)
        {
            if (index == null || index.entries == null)
            {
                return null;
            }

            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry != null && string.Equals(entry.id, modelId, System.StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "PrimarySubmit_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private sealed class TestIdentity : IUserIdentityProvider
        {
            public string GetUserName()
            {
                return AUTHOR_NAME;
            }

            public void SetUserName(string name)
            {
            }

            public UserRole GetUserRole()
            {
                return UserRole.Artist;
            }

            public void SetUserRole(UserRole role)
            {
            }
        }
    }
}
