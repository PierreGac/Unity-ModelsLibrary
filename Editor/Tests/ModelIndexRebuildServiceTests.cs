using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for rebuilding models_index.json from repository folder structure.
    /// </summary>
    public class ModelIndexRebuildServiceTests
    {
        private const string MODELS_INDEX_FILE_NAME = "models_index.json";

        [Test]
        public void TestModelIndexEntryFactory_FromMeta_MapsFields()
        {
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = "abc123", name = "Test Model" },
                version = "2.0.0",
                description = "Description",
                tags = new Tags { values = new List<string> { "tag1" } },
                updatedTimeTicks = 100,
                uploadTimeTicks = 200
            };

            ModelIndex.Entry entry = ModelIndexEntryFactory.FromMeta(meta);

            Assert.AreEqual("abc123", entry.id);
            Assert.AreEqual("Test Model", entry.name);
            Assert.AreEqual("2.0.0", entry.latestVersion);
            Assert.AreEqual("Description", entry.description);
            Assert.AreEqual(100, entry.updatedTimeTicks);
            Assert.AreEqual(200, entry.releaseTimeTicks);
            Assert.AreEqual(1, entry.tags.Count);
            Assert.AreEqual("tag1", entry.tags[0]);
        }

        [Test]
        public async Task TestRebuild_PicksHighestSemVer()
        {
            string tempRepoPath = CreateTempRepoPath();
            try
            {
                string modelId = "model-semver";
                WriteModelMeta(tempRepoPath, modelId, "1.0.0", "Model v1");
                WriteModelMeta(tempRepoPath, modelId, "1.1.0", "Model v1.1");

                ModelIndexRebuildService rebuildService = new ModelIndexRebuildService();
                FileSystemRepository repo = new FileSystemRepository(tempRepoPath);
                ModelIndexRebuildReport report = await rebuildService.RebuildAsync(repo, createBackup: false);

                Assert.IsTrue(report.success);
                Assert.AreEqual(1, report.indexEntryCount);

                ModelIndex index = await repo.LoadIndexAsync();
                Assert.AreEqual(1, index.entries.Count);
                Assert.AreEqual("1.1.0", index.entries[0].latestVersion);
                Assert.AreEqual("Model v1.1", index.entries[0].description);
            }
            finally
            {
                DeleteTempRepo(tempRepoPath);
            }
        }

        [Test]
        public async Task TestRebuild_RestoresFromTruncatedIndex()
        {
            string tempRepoPath = CreateTempRepoPath();
            try
            {
                WriteModelMeta(tempRepoPath, "model-a", "1.0.0", "A");
                WriteModelMeta(tempRepoPath, "model-b", "1.0.0", "B");
                WriteModelMeta(tempRepoPath, "model-c", "1.0.0", "C");

                ModelIndex truncated = new ModelIndex
                {
                    entries = new List<ModelIndex.Entry>
                    {
                        new ModelIndex.Entry { id = "only-one", name = "Only", latestVersion = "1.0.0" }
                    }
                };
                File.WriteAllText(
                    Path.Combine(tempRepoPath, MODELS_INDEX_FILE_NAME),
                    JsonUtility.ToJson(truncated));

                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(tempRepoPath));
                ModelIndexRebuildReport report = await service.RebuildIndexFromRepositoryAsync(createBackup: false);

                Assert.IsTrue(report.success);
                Assert.AreEqual(3, report.indexEntryCount);
                Assert.AreEqual(1, report.previousIndexEntryCount);

                ModelIndex index = await service.GetIndexAsync();
                Assert.AreEqual(3, index.entries.Count);
            }
            finally
            {
                DeleteTempRepo(tempRepoPath);
            }
        }

        [Test]
        public async Task TestRebuild_SkipsFolderWithoutModelJson()
        {
            string tempRepoPath = CreateTempRepoPath();
            try
            {
                WriteModelMeta(tempRepoPath, "valid-model", "1.0.0", "Valid");
                string emptyFolder = Path.Combine(tempRepoPath, "empty-folder");
                Directory.CreateDirectory(emptyFolder);
                Directory.CreateDirectory(Path.Combine(emptyFolder, "1.0.0"));

                ModelIndexRebuildService rebuildService = new ModelIndexRebuildService();
                FileSystemRepository repo = new FileSystemRepository(tempRepoPath);
                ModelIndexRebuildReport report = await rebuildService.PreviewAsync(repo);

                Assert.AreEqual(1, report.indexEntryCount);
                Assert.AreEqual(1, report.skippedFolders.Count);
                Assert.AreEqual("empty-folder", report.skippedFolders[0]);
            }
            finally
            {
                DeleteTempRepo(tempRepoPath);
            }
        }

        [Test]
        public async Task TestRebuild_CreatesBackupWhenIndexExists()
        {
            string tempRepoPath = CreateTempRepoPath();
            try
            {
                WriteModelMeta(tempRepoPath, "model-a", "1.0.0", "A");
                string indexPath = Path.Combine(tempRepoPath, MODELS_INDEX_FILE_NAME);
                File.WriteAllText(indexPath, JsonUtility.ToJson(new ModelIndex { entries = new List<ModelIndex.Entry>() }));

                ModelIndexRebuildService rebuildService = new ModelIndexRebuildService();
                FileSystemRepository repo = new FileSystemRepository(tempRepoPath);
                ModelIndexRebuildReport report = await rebuildService.RebuildAsync(repo, createBackup: true);

                Assert.IsTrue(report.success);
                Assert.IsFalse(string.IsNullOrEmpty(report.backupPath));
                Assert.IsTrue(File.Exists(report.backupPath));
            }
            finally
            {
                DeleteTempRepo(tempRepoPath);
            }
        }

        [Test]
        public async Task TestRebuild_ContinuesWhenOneModelJsonInvalid()
        {
            string tempRepoPath = CreateTempRepoPath();
            try
            {
                WriteModelMeta(tempRepoPath, "good-model", "1.0.0", "Good");
                string badMetaDir = Path.Combine(tempRepoPath, "bad-model", "1.0.0");
                Directory.CreateDirectory(badMetaDir);
                File.WriteAllText(Path.Combine(badMetaDir, ModelMeta.MODEL_JSON), "{ not valid json");

                ModelIndexRebuildService rebuildService = new ModelIndexRebuildService();
                FileSystemRepository repo = new FileSystemRepository(tempRepoPath);
                ModelIndexRebuildReport report = await rebuildService.RebuildAsync(repo, createBackup: false);

                Assert.AreEqual(1, report.indexEntryCount);
                Assert.AreEqual(1, report.skippedFolders.Count);
                Assert.AreEqual("bad-model", report.skippedFolders[0]);

                ModelIndex index = await repo.LoadIndexAsync();
                Assert.AreEqual(1, index.entries.Count);
                Assert.AreEqual("good-model", index.entries[0].id);
            }
            finally
            {
                DeleteTempRepo(tempRepoPath);
            }
        }

        [Test]
        public async Task TestModelLibraryService_RebuildRequiresFileSystemRepository()
        {
            ModelLibraryService httpService = new ModelLibraryService(new HttpRepository("https://example.com"));
            try
            {
                await httpService.PreviewIndexRebuildAsync();
                Assert.Fail("Expected InvalidOperationException for HTTP repository.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains("file system", ex.Message);
            }
        }

        private static string CreateTempRepoPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"IndexRebuildTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempRepo(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void WriteModelMeta(string repoRoot, string modelId, string version, string description)
        {
            string versionDir = Path.Combine(repoRoot, modelId, version);
            Directory.CreateDirectory(versionDir);

            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = modelId, name = $"Name {modelId}" },
                version = version,
                description = description,
                author = "Test",
                updatedTimeTicks = DateTime.UtcNow.Ticks,
                createdTimeTicks = DateTime.UtcNow.Ticks
            };

            File.WriteAllText(Path.Combine(versionDir, ModelMeta.MODEL_JSON), JsonUtility.ToJson(meta));
        }
    }
}
