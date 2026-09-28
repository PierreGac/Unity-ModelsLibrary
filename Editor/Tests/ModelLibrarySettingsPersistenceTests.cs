using System.IO;
using ModelLibrary.Editor.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Verifies that library settings live in project storage and survive a package asset reset.
    /// </summary>
    public class ModelLibrarySettingsPersistenceTests
    {
        private const string LEGACY_ASSET_PATH = "Assets/ModelLibrary/Resources/ModelLibrarySettings.asset";
        private const string SENTINEL_REPOSITORY_ROOT = "ProjectSettingsKeep_2C_sentinel";
        private const string SENTINEL_CACHE_ROOT = "Library/ModelLibraryCacheKeep_2C";

        /// <summary>
        /// A clean project copies the package asset once, then keeps later edits across reload and package replacement.
        /// </summary>
        [Test]
        public void ProjectSettings_SurviveRestartAndPackageReplacement()
        {
            string legacyAssetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "ModelLibrary", "Resources", "ModelLibrarySettings.asset"));
            string projectFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "ModelLibrarySettings.json"));
            bool existed = File.Exists(projectFile);
            string originalJson = existed ? File.ReadAllText(projectFile) : null;
            string legacyBefore = File.ReadAllText(legacyAssetPath);
            ModelLibrarySettings legacyAsset = AssetDatabase.LoadAssetAtPath<ModelLibrarySettings>(LEGACY_ASSET_PATH);

            try
            {
                if (existed)
                {
                    File.Delete(projectFile);
                }

                ModelLibrarySettings.ReloadProjectCopy();
                ModelLibrarySettings created = ModelLibrarySettings.GetOrCreate();

                Assert.IsTrue(File.Exists(projectFile));
                Assert.AreNotSame(legacyAsset, created);
                Assert.IsTrue(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(created)));
                Assert.AreEqual(legacyAsset.repositoryRoot, created.repositoryRoot);
                Assert.AreEqual(legacyAsset.localCacheRoot, created.localCacheRoot);

                created.repositoryKind = ModelLibrarySettings.RepositoryKind.Http;
                created.repositoryRoot = SENTINEL_REPOSITORY_ROOT;
                created.localCacheRoot = SENTINEL_CACHE_ROOT;
                created.SaveProjectCopy();

                ModelLibrarySettings.ReloadProjectCopy();
                ModelLibrarySettings restarted = ModelLibrarySettings.GetOrCreate();
                Assert.AreEqual(ModelLibrarySettings.RepositoryKind.Http, restarted.repositoryKind);
                Assert.AreEqual(SENTINEL_REPOSITORY_ROOT, restarted.repositoryRoot);
                Assert.AreEqual(SENTINEL_CACHE_ROOT, restarted.localCacheRoot);

                string projectJson = File.ReadAllText(projectFile);
                string legacyAfter = File.ReadAllText(legacyAssetPath);
                Assert.IsTrue(projectJson.Contains(SENTINEL_REPOSITORY_ROOT));
                Assert.AreEqual(legacyBefore, legacyAfter);
                Assert.IsFalse(legacyAfter.Contains(SENTINEL_REPOSITORY_ROOT));
                Assert.AreNotEqual(SENTINEL_REPOSITORY_ROOT, legacyAsset.repositoryRoot);
            }
            finally
            {
                ModelLibrarySettings.ReloadProjectCopy();
                if (existed)
                {
                    File.WriteAllText(projectFile, originalJson);
                }
                else if (File.Exists(projectFile))
                {
                    File.Delete(projectFile);
                }

                ModelLibrarySettings.GetOrCreate();
            }
        }
    }
}
