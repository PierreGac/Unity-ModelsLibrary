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
    /// Optimistic index saves: a later edit reloads when another revision landed first.
    /// </summary>
    public class ModelIndexConcurrencyTests
    {
        private const string INDEX_FILE_NAME = "models_index.json";
        private const string MODEL_A = "model-a";
        private const string MODEL_B = "model-b";
        private const string MODEL_C = "model-c";
        private const string NAME_A = "Alpha";
        private const string NAME_B = "Beta";
        private const string NAME_C = "Gamma";
        private const string VERSION = "1.0.0";
        private const long INITIAL_BLOCKER_REVISION = 100;
        private const int EXPECTED_REVISION_AFTER_ONE_SAVE = 1;
        private const int CONFLICTING_REVISION = 2;
        private const int EXPECTED_REVISION_AFTER_CONFLICT = 3;
        private const int MINIMUM_SAVE_ATTEMPTS_AFTER_CONFLICT = 2;
        private const string REVISION_FIELD = "\"revision\"";

        [Test]
        public async Task LegacyIndex_GainsRevisionAndKeepsExistingEntry()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "IndexConcurrency_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string indexPath = Path.Combine(tempRoot, INDEX_FILE_NAME);

            try
            {
                string json = "{\n  \"entries\": [\n    {\n      \"id\": \"" + MODEL_A + "\",\n      \"name\": \"" + NAME_A + "\",\n      \"latestVersion\": \"" + VERSION + "\"\n    }\n  ]\n}";
                Assert.IsFalse(json.Contains(REVISION_FIELD));
                File.WriteAllText(indexPath, json);

                ModelIndexService service = new ModelIndexService(new FileSystemRepository(tempRoot));
                await service.UpdateIndexWithLatestMetaAsync(Meta(MODEL_B, NAME_B));

                ModelIndex saved = JsonUtil.FromJson<ModelIndex>(File.ReadAllText(indexPath));
                Assert.AreEqual(EXPECTED_REVISION_AFTER_ONE_SAVE, saved.revision);
                Assert.IsTrue(Contains(saved, MODEL_A));
                Assert.IsTrue(Contains(saved, MODEL_B));
            }
            finally
            {
                ModelIndexService.BeforeIndexSaveAttempt = null;
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public async Task Save_ReloadsWhenAnotherRevisionLandedFirst()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "IndexConcurrency_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string indexPath = Path.Combine(tempRoot, INDEX_FILE_NAME);
            int saveAttempts = 0;

            try
            {
                ModelIndexService service = new ModelIndexService(new FileSystemRepository(tempRoot));
                await service.UpdateIndexWithLatestMetaAsync(Meta(MODEL_A, NAME_A));

                ModelIndexService.BeforeIndexSaveAttempt = () =>
                {
                    saveAttempts++;
                    if (saveAttempts != 1)
                    {
                        return;
                    }

                    ModelIndex conflict = new ModelIndex();
                    conflict.revision = CONFLICTING_REVISION;
                    conflict.entries.Add(Entry(MODEL_A, NAME_A));
                    conflict.entries.Add(Entry(MODEL_C, NAME_C));
                    File.WriteAllText(indexPath, JsonUtil.ToJson(conflict));
                };

                await service.UpdateIndexWithLatestMetaAsync(Meta(MODEL_B, NAME_B));

                ModelIndex saved = JsonUtil.FromJson<ModelIndex>(File.ReadAllText(indexPath));
                Assert.AreEqual(EXPECTED_REVISION_AFTER_CONFLICT, saved.revision);
                Assert.IsTrue(Contains(saved, MODEL_A));
                Assert.IsTrue(Contains(saved, MODEL_B));
                Assert.IsTrue(Contains(saved, MODEL_C));
                Assert.GreaterOrEqual(saveAttempts, MINIMUM_SAVE_ATTEMPTS_AFTER_CONFLICT);
            }
            finally
            {
                ModelIndexService.BeforeIndexSaveAttempt = null;
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public async Task Save_ThrowsWhenTheRevisionKeepsChanging()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "IndexConcurrency_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string indexPath = Path.Combine(tempRoot, INDEX_FILE_NAME);
            long blockerRevision = INITIAL_BLOCKER_REVISION;

            try
            {
                ModelIndexService service = new ModelIndexService(new FileSystemRepository(tempRoot));
                await service.UpdateIndexWithLatestMetaAsync(Meta(MODEL_A, NAME_A));

                ModelIndexService.BeforeIndexSaveAttempt = () =>
                {
                    blockerRevision++;
                    ModelIndex blocker = new ModelIndex();
                    blocker.revision = blockerRevision;
                    blocker.entries.Add(Entry(MODEL_A, NAME_A));
                    File.WriteAllText(indexPath, JsonUtil.ToJson(blocker));
                };

                IOException thrown = null;
                try
                {
                    await service.UpdateIndexWithLatestMetaAsync(Meta(MODEL_B, NAME_B));
                }
                catch (IOException ex)
                {
                    thrown = ex;
                }

                Assert.IsNotNull(thrown);
                ModelIndex saved = JsonUtil.FromJson<ModelIndex>(File.ReadAllText(indexPath));
                Assert.IsTrue(Contains(saved, MODEL_A));
                Assert.IsFalse(Contains(saved, MODEL_B));
            }
            finally
            {
                ModelIndexService.BeforeIndexSaveAttempt = null;
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static ModelMeta Meta(string id, string name)
        {
            ModelMeta meta = new ModelMeta();
            meta.identity = new ModelIdentity();
            meta.identity.id = id;
            meta.identity.name = name;
            meta.version = VERSION;
            return meta;
        }

        private static ModelIndex.Entry Entry(string id, string name)
        {
            return new ModelIndex.Entry
            {
                id = id,
                name = name,
                latestVersion = VERSION
            };
        }

        private static bool Contains(ModelIndex index, string id)
        {
            if (index == null || index.entries == null)
            {
                return false;
            }

            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry != null && string.Equals(entry.id, id, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
