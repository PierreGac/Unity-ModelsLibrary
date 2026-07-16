using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEditor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for post-submit local install sync.
    /// </summary>
    public class LocalInstallSyncServiceTests
    {
        [Test]
        public void SubmittedAssetsMatchLocalInstall_AllPayloadGuidsContained()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "LocalInstallSync_" + System.Guid.NewGuid().ToString("N"));
            string installDir = Path.Combine(tempRoot, "Assets", "Models", "Ship");
            Directory.CreateDirectory(installDir);
            string assetFile = Path.Combine(installDir, "Ship.fbx");
            File.WriteAllText(assetFile, "dummy");

            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempRoot);
                AssetDatabase.Refresh();

                string assetPath = "Assets/Models/Ship/Ship.fbx";
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    Assert.Ignore("AssetDatabase did not register temp asset path; skipping GUID-based match test.");
                }

                ModelMeta local = new ModelMeta
                {
                    installPath = "Assets/Models/Ship",
                    assetGuids = new List<string> { guid, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
                };
                ModelMeta submitted = new ModelMeta
                {
                    installPath = "Assets/Models/Ship",
                    assetGuids = new List<string> { guid }
                };

                Assert.IsTrue(LocalInstallSyncService.SubmittedAssetsMatchLocalInstall(submitted, local));
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void SubmittedAssetsMatchLocalInstall_RejectsNewGuid()
        {
            ModelMeta local = new ModelMeta
            {
                installPath = "Assets/Models/Ship",
                assetGuids = new List<string> { "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" }
            };
            ModelMeta submitted = new ModelMeta
            {
                installPath = "Assets/Models/Ship",
                assetGuids = new List<string>
                {
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    "cccccccccccccccccccccccccccccccc"
                }
            };

            Assert.IsFalse(LocalInstallSyncService.SubmittedAssetsMatchLocalInstall(submitted, local));
        }

        [Test]
        public void SubmittedAssetsMatchLocalInstall_RejectsEmptySubmittedGuids()
        {
            ModelMeta local = new ModelMeta
            {
                installPath = "Assets/Models/Ship",
                assetGuids = new List<string> { "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" }
            };
            ModelMeta submitted = new ModelMeta
            {
                installPath = "Assets/Models/Ship",
                assetGuids = new List<string>()
            };

            Assert.IsFalse(LocalInstallSyncService.SubmittedAssetsMatchLocalInstall(submitted, local));
        }

        [Test]
        public void SubmittedAssetsMatchLocalInstall_RejectsGuidOutsideInstallPath()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "LocalInstallSync_" + System.Guid.NewGuid().ToString("N"));
            string installDir = Path.Combine(tempRoot, "Assets", "Models", "Ship");
            string otherDir = Path.Combine(tempRoot, "Assets", "Other");
            Directory.CreateDirectory(installDir);
            Directory.CreateDirectory(otherDir);
            File.WriteAllText(Path.Combine(otherDir, "Outside.fbx"), "dummy");

            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempRoot);
                AssetDatabase.Refresh();

                string outsidePath = "Assets/Other/Outside.fbx";
                string guid = AssetDatabase.AssetPathToGUID(outsidePath);
                if (string.IsNullOrEmpty(guid))
                {
                    Assert.Ignore("AssetDatabase did not register temp asset path; skipping outside-path GUID test.");
                }

                ModelMeta local = new ModelMeta
                {
                    installPath = "Assets/Models/Ship",
                    assetGuids = new List<string> { guid }
                };
                ModelMeta submitted = new ModelMeta
                {
                    installPath = "Assets/Models/Ship",
                    assetGuids = new List<string> { guid }
                };

                Assert.IsFalse(LocalInstallSyncService.SubmittedAssetsMatchLocalInstall(submitted, local));
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void TryFindLocalInstallMeta_FindsByModelId()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "LocalInstallSync_" + System.Guid.NewGuid().ToString("N"));
            string installPath = Path.Combine(tempRoot, "Assets", "Models", "Ship");
            Directory.CreateDirectory(installPath);

            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                string json =
                    "{\n" +
                    "  \"identity\": { \"id\": \"ship-id-123\", \"name\": \"Ship\" },\n" +
                    "  \"version\": \"1.2.0\",\n" +
                    "  \"installPath\": \"Assets/Models/Ship\"\n" +
                    "}";
                File.WriteAllText(Path.Combine(installPath, ".modelLibrary.meta.json"), json);

                ModelMeta found = LocalInstallSyncService.TryFindLocalInstallMeta("ship-id-123");

                Assert.IsNotNull(found);
                Assert.IsNotNull(found.identity);
                Assert.AreEqual("ship-id-123", found.identity.id);
                Assert.AreEqual("1.2.0", found.version);
                Assert.AreEqual("Assets/Models/Ship", found.installPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void TryFindLocalInstallMeta_ReturnsNullWhenMissing()
        {
            ModelMeta found = LocalInstallSyncService.TryFindLocalInstallMeta("nonexistent-model-id-zzzz");
            Assert.IsNull(found);
        }

        [Test]
        public void WriteLocalManifest_UpdatesVersionOnDisk()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "LocalInstallSync_" + System.Guid.NewGuid().ToString("N"));
            string installPath = Path.Combine(tempRoot, "Assets", "Models", "Ship");
            Directory.CreateDirectory(installPath);

            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "ship-id", name = "Ship" },
                    version = "2.0.0",
                    installPath = "Assets/Models/Ship",
                    description = "Synced"
                };

                LocalInstallSyncService.WriteLocalManifest(meta, "Assets/Models/Ship");

                string manifestPath = Path.Combine(installPath, ".modelLibrary.meta.json");
                Assert.IsTrue(File.Exists(manifestPath));

                string json = File.ReadAllText(manifestPath);
                ModelMeta loaded = JsonUtil.FromJson<ModelMeta>(json);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("2.0.0", loaded.version);
                Assert.AreEqual("Assets/Models/Ship", loaded.installPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCwd);
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void SyncAfterUpdateSubmitAsync_SkipsWhenNotInstalled()
        {
            ModelMeta submitted = new ModelMeta
            {
                identity = new ModelIdentity { id = "missing-id", name = "Missing" },
                version = "2.0.0",
                installPath = "Assets/Models/Missing",
                assetGuids = new List<string> { "dddddddddddddddddddddddddddddddd" }
            };

            System.Threading.Tasks.Task<LocalInstallSyncResult> task =
                LocalInstallSyncService.SyncAfterUpdateSubmitAsync(submitted, Path.GetTempPath(), "missing-id");
            task.Wait();
            LocalInstallSyncResult result = task.Result;

            Assert.IsFalse(result.Applied);
            Assert.AreEqual(LocalInstallSyncService.REASON_NOT_INSTALLED, result.Reason);
        }

        [Test]
        public void SyncBranch_UsesHasInstallPathChangedForSameAndDifferentPaths()
        {
            Assert.IsFalse(InstalledModelAssetCleaner.HasInstallPathChanged(
                "Assets/Models/Ship",
                "Assets/Models/Ship"));
            Assert.IsTrue(InstalledModelAssetCleaner.HasInstallPathChanged(
                "Assets/Models/Ship",
                "Assets/Art/Ship"));
        }
    }
}
