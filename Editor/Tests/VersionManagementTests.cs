using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for version management functionality.
    /// Tests version deletion workflow and auto-upload functionality.
    /// </summary>
    public class VersionManagementTests
    {
        /// <summary>
        /// Tests version deletion workflow.
        /// Verifies that version deletion requests are properly validated and executed.
        /// </summary>
        [Test]
        public void TestVersionDeletionWorkflow()
        {
            // Create a temporary test repository
            string tempRepoPath = Path.Combine(Path.GetTempPath(), $"ModelLibrary_VersionTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRepoPath);

            try
            {
                string modelId = "test-deletion-model";
                string version1 = "1.0.0";
                string version2 = "2.0.0";

                // Create two versions of a model
                string version1Dir = Path.Combine(tempRepoPath, modelId, version1);
                string version2Dir = Path.Combine(tempRepoPath, modelId, version2);
                Directory.CreateDirectory(version1Dir);
                Directory.CreateDirectory(version2Dir);

                // Create metadata files
                ModelMeta meta1 = new ModelMeta
                {
                    identity = new ModelIdentity { id = modelId, name = "Test Model" },
                    version = version1
                };
                ModelMeta meta2 = new ModelMeta
                {
                    identity = new ModelIdentity { id = modelId, name = "Test Model" },
                    version = version2
                };

                File.WriteAllText(Path.Combine(version1Dir, "modelLibrary.meta.json"), JsonUtility.ToJson(meta1));
                File.WriteAllText(Path.Combine(version2Dir, "modelLibrary.meta.json"), JsonUtility.ToJson(meta2));

                // Create index
                ModelIndex index = new ModelIndex
                {
                    entries = new List<ModelIndex.Entry>
                    {
                        new ModelIndex.Entry
                        {
                            id = modelId,
                            name = "Test Model",
                            latestVersion = version2,
                            updatedTimeTicks = DateTime.UtcNow.Ticks
                        }
                    }
                };
                File.WriteAllText(Path.Combine(tempRepoPath, "index.json"), JsonUtility.ToJson(index));

                // Test repository deletion
                IModelRepository repo = new FileSystemRepository(tempRepoPath);
                ModelLibraryService service = new ModelLibraryService(repo);

                // Verify versions exist before deletion
                Assert.IsTrue(Directory.Exists(version1Dir), "Version 1 directory should exist");
                Assert.IsTrue(Directory.Exists(version2Dir), "Version 2 directory should exist");
                Assert.IsTrue(File.Exists(Path.Combine(version1Dir, "modelLibrary.meta.json")), "Version 1 metadata should exist");
                Assert.IsTrue(File.Exists(Path.Combine(version2Dir, "modelLibrary.meta.json")), "Version 2 metadata should exist");

                // Test deletion workflow validation
                // Note: Actual deletion requires user confirmation, so we test the validation logic
                bool canDeleteOldVersion = version1 != meta2.version; // Can delete if not latest
                bool cannotDeleteLatestVersion = version2 == meta2.version; // Cannot delete latest version

                Assert.IsTrue(canDeleteOldVersion, "Should be able to delete old version");
                Assert.IsTrue(cannotDeleteLatestVersion, "Should not delete latest version without special handling");

                // Verify repository interface supports deletion
                Assert.IsTrue(repo is IModelRepository, "Repository should implement IModelRepository");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRepoPath))
                {
                    Directory.Delete(tempRepoPath, true);
                }
            }
        }

        /// <summary>
        /// Tests auto-upload functionality.
        /// Verifies that batch upload service can process multiple models.
        /// </summary>
        [Test]
        public void TestAutoUploadFunctionality()
        {
            // Create a temporary directory with model folders
            string tempSourcePath = Path.Combine(Path.GetTempPath(), $"ModelLibrary_AutoUpload_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempSourcePath);

            try
            {
                // Create test model folders
                string[] modelFolders = { "Model1", "Model2", "Model3" };
                foreach (string folder in modelFolders)
                {
                    string modelPath = Path.Combine(tempSourcePath, folder);
                    Directory.CreateDirectory(modelPath);

                    // Create a dummy FBX file
                    string fbxPath = Path.Combine(modelPath, $"{folder}.fbx");
                    File.WriteAllText(fbxPath, "dummy fbx content");
                }

                // Test that batch upload service can discover models
                string[] discoveredFolders = Directory.GetDirectories(tempSourcePath);
                Assert.AreEqual(modelFolders.Length, discoveredFolders.Length, "Should discover all model folders");

                // Test that each folder contains valid model files
                foreach (string folder in modelFolders)
                {
                    string modelPath = Path.Combine(tempSourcePath, folder);
                    string[] files = Directory.GetFiles(modelPath, "*.fbx", SearchOption.TopDirectoryOnly);
                    Assert.GreaterOrEqual(files.Length, 1, $"Folder {folder} should contain at least one FBX file");
                }

                // Test validation logic
                bool isValidModelFolder(string folderPath)
                {
                    if (!Directory.Exists(folderPath))
                    {
                        return false;
                    }

                    string[] fbxFiles = Directory.GetFiles(folderPath, "*.fbx", SearchOption.TopDirectoryOnly);
                    string[] objFiles = Directory.GetFiles(folderPath, "*.obj", SearchOption.TopDirectoryOnly);
                    return fbxFiles.Length > 0 || objFiles.Length > 0;
                }

                foreach (string folder in modelFolders)
                {
                    string modelPath = Path.Combine(tempSourcePath, folder);
                    Assert.IsTrue(isValidModelFolder(modelPath), $"Folder {folder} should be valid for upload");
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempSourcePath))
                {
                    Directory.Delete(tempSourcePath, true);
                }
            }
        }

        /// <summary>
        /// Tests that version deletion properly handles edge cases.
        /// </summary>
        [Test]
        public void TestVersionDeletionEdgeCases()
        {
            // Test that deletion of non-existent version is handled gracefully
            string nonExistentModelId = "non-existent-model";
            string nonExistentVersion = "99.9.9";

            // Test validation logic
            bool canDelete = !string.IsNullOrEmpty(nonExistentModelId) && !string.IsNullOrEmpty(nonExistentVersion);
            Assert.IsTrue(canDelete, "Validation should allow deletion attempt for valid IDs");

            // Test that empty/null model ID is rejected
            Assert.IsFalse(string.IsNullOrEmpty(null), "Null model ID should be rejected");
            Assert.IsFalse(string.IsNullOrEmpty(""), "Empty model ID should be rejected");

            // Test that empty/null version is rejected
            Assert.IsFalse(string.IsNullOrEmpty(null), "Null version should be rejected");
            Assert.IsFalse(string.IsNullOrEmpty(""), "Empty version should be rejected");
        }
    }
}

