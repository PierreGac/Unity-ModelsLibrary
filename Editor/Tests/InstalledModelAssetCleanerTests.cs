using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for selective cleanup of old install-path assets during model updates.
    /// </summary>
    public class InstalledModelAssetCleanerTests
    {
        [Test]
        public void HasInstallPathChanged_ReturnsTrueWhenPathsDiffer()
        {
            Assert.IsTrue(InstalledModelAssetCleaner.HasInstallPathChanged(
                "Assets/Models/OldPath",
                "Assets/Art/NewPath"));
        }

        [Test]
        public void HasInstallPathChanged_ReturnsFalseWhenPathsMatch()
        {
            Assert.IsFalse(InstalledModelAssetCleaner.HasInstallPathChanged(
                "Assets/Models/SamePath/",
                "Assets/Models/SamePath"));
        }

        [Test]
        public void HasInstallPathChanged_ReturnsFalseWhenEitherPathMissing()
        {
            Assert.IsFalse(InstalledModelAssetCleaner.HasInstallPathChanged(null, "Assets/Models/New"));
            Assert.IsFalse(InstalledModelAssetCleaner.HasInstallPathChanged("Assets/Models/Old", null));
            Assert.IsFalse(InstalledModelAssetCleaner.HasInstallPathChanged("", "Assets/Models/New"));
        }

        [Test]
        public void CollectProjectPathsToDelete_MapsPayloadAndImagesUnderInstallPath()
        {
            ModelMeta oldMeta = new ModelMeta
            {
                installPath = "Assets/Models/OldShip",
                payloadRelativePaths = new List<string>
                {
                    "payload/OldShip.fbx",
                    "payload/Hull.mat"
                },
                imageRelativePaths = new List<string>
                {
                    "images/preview.png"
                }
            };

            HashSet<string> paths = InstalledModelAssetCleaner.CollectProjectPathsToDelete(
                oldMeta,
                "Assets/Models/OldShip");

            Assert.IsTrue(paths.Contains("Assets/Models/OldShip/OldShip.fbx"));
            Assert.IsTrue(paths.Contains("Assets/Models/OldShip/Hull.mat"));
            Assert.IsTrue(paths.Contains("Assets/Models/OldShip/images/preview.png"));
            Assert.IsTrue(paths.Contains("Assets/Models/OldShip/.modelLibrary.meta.json"));
            Assert.IsTrue(paths.Contains("Assets/Models/OldShip/modelLibrary.meta.json"));
        }

        [Test]
        public void CollectProjectPathsToDelete_DoesNotIncludeUnrelatedSiblingFiles()
        {
            ModelMeta oldMeta = new ModelMeta
            {
                installPath = "Assets/Art/Weapons",
                payloadRelativePaths = new List<string> { "payload/Sword.fbx" }
            };

            HashSet<string> paths = InstalledModelAssetCleaner.CollectProjectPathsToDelete(
                oldMeta,
                "Assets/Art/Weapons");

            Assert.IsTrue(paths.Contains("Assets/Art/Weapons/Sword.fbx"));
            Assert.IsFalse(paths.Contains("Assets/Art/Weapons/UnrelatedNotes.txt"));
            Assert.IsFalse(paths.Contains("Assets/Art/Other/Sword.fbx"));
        }

        [Test]
        public void TryLoadLocalManifest_ReadsInstallManifestFromFolder()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstalledModelAssetCleaner_" + System.Guid.NewGuid().ToString("N"));
            string installPath = Path.Combine(tempRoot, "Assets", "Models", "Ship");
            Directory.CreateDirectory(installPath);

            string previousCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                string json =
                    "{\n" +
                    "  \"identity\": { \"id\": \"ship-id\", \"name\": \"Ship\" },\n" +
                    "  \"version\": \"1.0.0\",\n" +
                    "  \"installPath\": \"Assets/Models/Ship\"\n" +
                    "}";
                File.WriteAllText(Path.Combine(installPath, ".modelLibrary.meta.json"), json);

                ModelMeta loaded = InstalledModelAssetCleaner.TryLoadLocalManifest("Assets/Models/Ship");

                Assert.IsNotNull(loaded);
                Assert.IsNotNull(loaded.identity);
                Assert.AreEqual("ship-id", loaded.identity.id);
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
    }
}
