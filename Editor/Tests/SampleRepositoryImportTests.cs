using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// The Asset Store sample repository can be browsed and its cube imported.
    /// </summary>
    public class SampleRepositoryImportTests
    {
        private const string MODEL_ID = "sample-cube";
        private const string MODEL_VERSION = "1.0.0";
        private const string MODEL_NAME = "Sample Cube";
        private const string CUBE_FILE_NAME = "Cube.obj";
        private const string INSTALL_RELATIVE_PATH = "Assets/__SampleCubeImport";

        /// <summary>
        /// Downloads the sample cube and copies it into the project.
        /// </summary>
        [Test]
        public async Task SampleRepository_ImportsCube()
        {
            string packageRoot = Path.Combine(Application.dataPath, "ModelLibrary");
            string repositoryRoot = Path.Combine(packageRoot, "Samples~", "Example", "Repository");
            string destination = Path.GetFullPath(INSTALL_RELATIVE_PATH);
            string cacheRoot = null;

            try
            {
                ModelLibraryService service = new ModelLibraryService(new FileSystemRepository(repositoryRoot));
                ModelIndex index = await service.GetIndexAsync();
                ModelIndex.Entry entry = FindEntry(index, MODEL_ID);
                Assert.IsNotNull(entry);
                Assert.AreEqual(MODEL_NAME, entry.name);
                Assert.AreEqual(MODEL_VERSION, entry.latestVersion);

                (string versionRoot, ModelMeta meta) downloaded = await service.DownloadModelVersionAsync(MODEL_ID, MODEL_VERSION);
                cacheRoot = downloaded.versionRoot;
                Assert.AreEqual(MODEL_NAME, downloaded.meta.identity.name);
                Assert.IsTrue(File.Exists(Path.Combine(cacheRoot, "payload", CUBE_FILE_NAME)));

                string installPath = await ModelProjectImporter.ImportFromCacheAsync(
                    cacheRoot,
                    downloaded.meta,
                    true,
                    INSTALL_RELATIVE_PATH,
                    true);

                string importedCube = Path.Combine(Path.GetFullPath(installPath), CUBE_FILE_NAME);
                Assert.IsTrue(File.Exists(importedCube));
                string cubeText = File.ReadAllText(importedCube);
                Assert.IsTrue(cubeText.Contains("SampleCube"));
            }
            finally
            {
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }

                string destinationMeta = destination + ".meta";
                if (File.Exists(destinationMeta))
                {
                    File.Delete(destinationMeta);
                }

                if (!string.IsNullOrEmpty(cacheRoot) && Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, true);
                }

                AssetDatabase.Refresh();
            }
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
    }
}
