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
    /// After a version delete, a reopened index and the metadata details would load match the folders that remain.
    /// </summary>
    public class VersionDeleteIndexTests
    {
        private const string INDEX_FILE_NAME = "models_index.json";
        private const string MODEL_ID = "widget-model";
        private const string OTHER_ID = "other-model";
        private const string OLDER_VERSION = "1.0.0";
        private const string NEWER_VERSION = "2.0.0";
        private const string OLDER_NAME = "Widget Old";
        private const string NEWER_NAME = "Widget New";
        private const string OTHER_NAME = "Other";

        [Test]
        public async Task DeleteNonLatest_KeepsHighestVersion()
        {
            string tempRoot = CreateRepository(true);
            try
            {
                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(tempRoot));
                bool deleted = await service.DeleteVersionAsync(MODEL_ID, OLDER_VERSION);

                Assert.IsTrue(deleted);
                ModelLibraryService reopened = new ModelLibraryService(new FileSystemRepository(tempRoot));
                ModelIndex index = await reopened.GetIndexAsync();
                ModelIndex.Entry entry = Find(index, MODEL_ID);
                Assert.IsNotNull(entry);
                Assert.AreEqual(NEWER_VERSION, entry.latestVersion);
                Assert.AreEqual(NEWER_NAME, entry.name);
                Assert.IsNotNull(Find(index, OTHER_ID));

                List<string> versions = await reopened.GetAvailableVersionsAsync(MODEL_ID);
                Assert.AreEqual(1, versions.Count);
                Assert.AreEqual(NEWER_VERSION, versions[0]);
                ModelMeta details = await reopened.GetMetaAsync(MODEL_ID, versions[0]);
                Assert.AreEqual(NEWER_NAME, details.identity.name);
                Assert.IsFalse(File.Exists(VersionMetaPath(tempRoot, MODEL_ID, OLDER_VERSION)));
            }
            finally
            {
                DeleteRepository(tempRoot);
            }
        }

        [Test]
        public async Task DeleteLatest_PointsAtHighestRemaining()
        {
            string tempRoot = CreateRepository(true);
            try
            {
                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(tempRoot));
                bool deleted = await service.DeleteVersionAsync(MODEL_ID, NEWER_VERSION);

                Assert.IsTrue(deleted);
                ModelLibraryService reopened = new ModelLibraryService(new FileSystemRepository(tempRoot));
                ModelIndex index = await reopened.GetIndexAsync();
                ModelIndex.Entry entry = Find(index, MODEL_ID);
                Assert.IsNotNull(entry);
                Assert.AreEqual(OLDER_VERSION, entry.latestVersion);
                Assert.AreEqual(OLDER_NAME, entry.name);

                List<string> versions = await reopened.GetAvailableVersionsAsync(MODEL_ID);
                Assert.AreEqual(1, versions.Count);
                Assert.AreEqual(OLDER_VERSION, versions[0]);
                ModelMeta details = await reopened.GetMetaAsync(MODEL_ID, entry.latestVersion);
                Assert.AreEqual(OLDER_NAME, details.identity.name);
                Assert.AreEqual(OLDER_VERSION, details.version);
            }
            finally
            {
                DeleteRepository(tempRoot);
            }
        }

        [Test]
        public async Task DeleteSoleVersion_RemovesModelFromIndex()
        {
            string tempRoot = CreateRepository(false);
            try
            {
                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(tempRoot));
                bool deleted = await service.DeleteVersionAsync(MODEL_ID, OLDER_VERSION);

                Assert.IsTrue(deleted);
                ModelLibraryService reopened = new ModelLibraryService(new FileSystemRepository(tempRoot));
                ModelIndex index = await reopened.GetIndexAsync();
                Assert.IsNull(Find(index, MODEL_ID));
                Assert.IsNotNull(Find(index, OTHER_ID));
                Assert.AreEqual(OTHER_NAME, Find(index, OTHER_ID).name);

                List<string> versions = await reopened.GetAvailableVersionsAsync(MODEL_ID);
                Assert.AreEqual(0, versions.Count);
                Assert.IsFalse(File.Exists(VersionMetaPath(tempRoot, MODEL_ID, OLDER_VERSION)));
            }
            finally
            {
                DeleteRepository(tempRoot);
            }
        }

        private static string CreateRepository(bool includeNewerVersion)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "VersionDelete_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            WriteVersion(tempRoot, MODEL_ID, OLDER_VERSION, OLDER_NAME);
            if (includeNewerVersion)
            {
                WriteVersion(tempRoot, MODEL_ID, NEWER_VERSION, NEWER_NAME);
            }

            ModelIndex index = new ModelIndex();
            index.entries.Add(new ModelIndex.Entry
            {
                id = MODEL_ID,
                name = includeNewerVersion ? NEWER_NAME : OLDER_NAME,
                latestVersion = includeNewerVersion ? NEWER_VERSION : OLDER_VERSION
            });
            index.entries.Add(new ModelIndex.Entry
            {
                id = OTHER_ID,
                name = OTHER_NAME,
                latestVersion = OLDER_VERSION
            });
            List<string> known = new List<string>();
            known.Add(OLDER_VERSION);
            if (includeNewerVersion)
            {
                known.Add(NEWER_VERSION);
            }

            index.versions[MODEL_ID] = known;
            File.WriteAllText(Path.Combine(tempRoot, INDEX_FILE_NAME), JsonUtil.ToJson(index));
            return tempRoot;
        }

        private static void WriteVersion(string root, string modelId, string version, string name)
        {
            string directory = Path.Combine(root, modelId, version);
            Directory.CreateDirectory(directory);
            ModelMeta meta = new ModelMeta();
            meta.identity = new ModelIdentity();
            meta.identity.id = modelId;
            meta.identity.name = name;
            meta.version = version;
            meta.description = name;
            File.WriteAllText(Path.Combine(directory, ModelMeta.MODEL_JSON), JsonUtil.ToJson(meta));
        }

        private static string VersionMetaPath(string root, string modelId, string version)
        {
            return Path.Combine(root, modelId, version, ModelMeta.MODEL_JSON);
        }

        private static ModelIndex.Entry Find(ModelIndex index, string id)
        {
            if (index == null || index.entries == null)
            {
                return null;
            }

            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry != null && string.Equals(entry.id, id, System.StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static void DeleteRepository(string tempRoot)
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
