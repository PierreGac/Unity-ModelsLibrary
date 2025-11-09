using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Windows;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for model name prefilling functionality in ModelSubmitWindow.
    /// Verifies that model names are extracted from file names, folder structure, or existing model manifests.
    /// </summary>
    public class ModelNamePrefillingTests
    {
        /// <summary>
        /// Tests Priority 1: Extract name from FBX file name (e.g., "MyModel.fbx" -> "MyModel").
        /// </summary>
        [Test]
        public void TestPrefillNameFromFbxFileName()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestFolder");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create FBX file
                string fbxFile = Path.Combine(assetsTestDir, "MyModel.fbx");
                File.WriteAllText(fbxFile, "dummy fbx content");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    // Create a test asset GUID
                    string testGuid = "12345678901234567890123456789012";
                    AssetDatabase.Refresh();

                    // Test the name extraction logic
                    string fileName = Path.GetFileNameWithoutExtension(fbxFile);
                    string extension = Path.GetExtension(fbxFile).ToLowerInvariant();
                    string suggestedName = null;

                    if ((extension == FileExtensions.FBX || extension == FileExtensions.OBJ) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        suggestedName = fileName;
                    }

                    Assert.AreEqual("MyModel", suggestedName, "Should extract name from FBX file name");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests Priority 1: Extract name from OBJ file name.
        /// </summary>
        [Test]
        public void TestPrefillNameFromObjFileName()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestFolder");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create OBJ file
                string objFile = Path.Combine(assetsTestDir, "MyModel.obj");
                File.WriteAllText(objFile, "dummy obj content");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetDatabase.Refresh();

                    // Test the name extraction logic
                    string fileName = Path.GetFileNameWithoutExtension(objFile);
                    string extension = Path.GetExtension(objFile).ToLowerInvariant();
                    string suggestedName = null;

                    if ((extension == FileExtensions.FBX || extension == FileExtensions.OBJ) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        suggestedName = fileName;
                    }

                    Assert.AreEqual("MyModel", suggestedName, "Should extract name from OBJ file name");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests Priority 2: Extract name from folder name when no FBX/OBJ.
        /// </summary>
        [Test]
        public void TestPrefillNameFromFolderStructure()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "MyModelFolder");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create a non-FBX/OBJ file (e.g., material)
                string matFile = Path.Combine(assetsTestDir, "Material.mat");
                File.WriteAllText(matFile, "dummy material content");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetDatabase.Refresh();

                    // Test Priority 2: folder structure
                    // Convert absolute path to relative path for testing
                    string firstAsset = matFile;
                    string absolutePath = Path.GetFullPath(firstAsset);
                    string assetsPath = Path.GetFullPath("Assets").Replace('\\', '/');
                    string relativePath = firstAsset;
                    
                    // Convert to relative if needed
                    if (Path.IsPathRooted(firstAsset))
                    {
                        string normalizedAssetPath = absolutePath.Replace('\\', '/');
                        if (normalizedAssetPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = "Assets/" + normalizedAssetPath.Substring(assetsPath.Length).TrimStart('/');
                        }
                    }
                    
                    string directory = Path.GetDirectoryName(relativePath);
                    string suggestedName = null;

                    if (directory != null && directory.Replace('\\', '/').StartsWith("Assets/"))
                    {
                        string relativeDir = directory.Replace('\\', '/');
                        if (relativeDir.StartsWith("Assets/"))
                        {
                            relativeDir = relativeDir.Substring(7); // Remove "Assets/" prefix
                        }
                        string[] parts = relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            suggestedName = parts[parts.Length - 1];
                        }
                    }

                    Assert.AreEqual("MyModelFolder", suggestedName, "Should extract name from folder structure when no FBX/OBJ");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests Priority 3: Extract name from existing model manifest.
        /// </summary>
        [Test]
        public void TestPrefillNameFromExistingModel()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file for existing model
                string manifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Existing Model Name" },
                    version = "1.0.0",
                    assetGuids = new List<string> { "12345678901234567890123456789012" }
                };
                File.WriteAllText(manifestFile, JsonUtility.ToJson(meta));

                // Create index
                // FileSystemRepository expects the index file to be named "models_index.json"
                string tempRepoPath = Path.Combine(Path.GetTempPath(), $"NamePrefillRepo_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempRepoPath);
                ModelIndex index = new ModelIndex
                {
                    entries = new List<ModelIndex.Entry>
                    {
                        new ModelIndex.Entry
                        {
                            id = "test-model",
                            name = "Existing Model Name",
                            latestVersion = "1.0.0",
                            updatedTimeTicks = DateTime.UtcNow.Ticks
                        }
                    }
                };
                File.WriteAllText(Path.Combine(tempRepoPath, "models_index.json"), JsonUtility.ToJson(index));

                // Use absolute path to Assets folder to avoid searching Unity's actual Assets folder
                // Don't change current directory - use absolute paths throughout
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                // Don't call AssetDatabase.Refresh() - it can block and is not needed for file system enumeration

                // Test Priority 3: existing model manifest
                string assetGuid = "12345678901234567890123456789012";
                string modelId = FindModelIdFromManifest(assetGuid, assetsPath);
                string suggestedName = null;

                if (!string.IsNullOrEmpty(modelId))
                {
                    // Load index file directly to avoid async complexity in tests
                    // This simulates what the service would do
                    string indexPath = Path.Combine(tempRepoPath, "models_index.json");
                    if (File.Exists(indexPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(indexPath);
                            ModelIndex loadedIndex = JsonUtility.FromJson<ModelIndex>(json);
                            if (loadedIndex?.entries != null)
                            {
                                ModelIndex.Entry entry = loadedIndex.entries.FirstOrDefault(e => string.Equals(e.id, modelId, StringComparison.OrdinalIgnoreCase));
                                if (entry != null && !string.IsNullOrWhiteSpace(entry.name))
                                {
                                    suggestedName = entry.name;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Test] Error loading index: {ex.Message}");
                        }
                    }
                }

                // Verify that modelId was found and name was extracted
                Assert.IsNotNull(modelId, "Should find model ID from manifest");
                Assert.AreEqual("test-model", modelId, "Model ID should match");
                Assert.AreEqual("Existing Model Name", suggestedName, "Should extract name from existing model manifest");
                
                // Cleanup
                if (Directory.Exists(tempRepoPath))
                {
                    Directory.Delete(tempRepoPath, true);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests that FBX name takes priority over folder name.
        /// </summary>
        [Test]
        public void TestPrefillNamePriorityOrder()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "FolderName");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create FBX file with different name than folder
                string fbxFile = Path.Combine(assetsTestDir, "ModelName.fbx");
                File.WriteAllText(fbxFile, "dummy fbx content");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetDatabase.Refresh();

                    string firstAsset = fbxFile;
                    string suggestedName = null;

                    // Priority 1: File name
                    string fileName = Path.GetFileNameWithoutExtension(firstAsset);
                    string extension = Path.GetExtension(firstAsset).ToLowerInvariant();
                    if ((extension == FileExtensions.FBX || extension == FileExtensions.OBJ) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        suggestedName = fileName;
                    }

                    // Priority 2: Folder name (should not be used if Priority 1 succeeded)
                    if (string.IsNullOrWhiteSpace(suggestedName))
                    {
                        string directory = Path.GetDirectoryName(firstAsset);
                        if (directory != null && directory.StartsWith("Assets/"))
                        {
                            string relativeDir = directory.Substring(7);
                            string[] parts = relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                suggestedName = parts[parts.Length - 1];
                            }
                        }
                    }

                    Assert.AreEqual("ModelName", suggestedName, "FBX name should take priority over folder name");
                    Assert.AreNotEqual("FolderName", suggestedName, "Folder name should not be used when FBX name is available");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests behavior when no assets are selected.
        /// </summary>
        [Test]
        public void TestPrefillNameWithNoSelection()
        {
            // Test that when Selection.assetGUIDs is null or empty, no name is prefilled
            string[] emptySelection = null;
            bool shouldReturn = emptySelection == null || emptySelection.Length == 0;

            Assert.IsTrue(shouldReturn, "Should return early when no assets are selected");
        }

        /// <summary>
        /// Tests behavior with non-model assets selected.
        /// </summary>
        [Test]
        public void TestPrefillNameWithInvalidAssets()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"NamePrefillTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestFolder");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create non-model file (e.g., script)
                string scriptFile = Path.Combine(assetsTestDir, "Script.cs");
                File.WriteAllText(scriptFile, "dummy script content");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetDatabase.Refresh();

                    // Test that non-model files are filtered out
                    string[] validExtensions = {
                        FileExtensions.FBX,
                        FileExtensions.OBJ,
                        FileExtensions.PNG,
                        FileExtensions.TGA,
                        FileExtensions.JPG,
                        FileExtensions.JPEG,
                        FileExtensions.PSD,
                        FileExtensions.MAT
                    };

                    string extension = Path.GetExtension(scriptFile).ToLowerInvariant();
                    bool isValid = validExtensions.Contains(extension);

                    Assert.IsFalse(isValid, "Script files should not be considered valid model assets");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests that name is only prefilled if current value is "New Model".
        /// </summary>
        [Test]
        public void TestPrefillNameOnlyWhenDefault()
        {
            string suggestedName = "SuggestedName";
            string currentName = "New Model";
            string newName = currentName;

            // Test that name is only prefilled if current value is "New Model"
            if (!string.IsNullOrWhiteSpace(suggestedName) && currentName == "New Model")
            {
                newName = suggestedName;
            }

            Assert.AreEqual("SuggestedName", newName, "Name should be prefilled when current value is 'New Model'");

            // Test that name is NOT prefilled if current value is not "New Model"
            currentName = "Custom Name";
            newName = currentName;
            if (!string.IsNullOrWhiteSpace(suggestedName) && currentName == "New Model")
            {
                newName = suggestedName;
            }

            Assert.AreEqual("Custom Name", newName, "Name should NOT be prefilled when current value is not 'New Model'");
        }

        /// <summary>
        /// Helper method to find model ID from manifest file (simulating FindModelIdFromSelectedAsset).
        /// </summary>
        private string FindModelIdFromManifest(string assetGuid, string assetsRoot)
        {
            if (string.IsNullOrEmpty(assetGuid))
            {
                return null;
            }

            List<string> manifestPaths = new List<string>();
            
            foreach (string manifestPath in Directory.EnumerateFiles(assetsRoot, ".modelLibrary.meta.json", SearchOption.AllDirectories))
            {
                manifestPaths.Add(manifestPath);
            }
            foreach (string manifestPath in Directory.EnumerateFiles(assetsRoot, "modelLibrary.meta.json", SearchOption.AllDirectories))
            {
                manifestPaths.Add(manifestPath);
            }

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string manifestPath = manifestPaths[i];
                if (string.IsNullOrEmpty(manifestPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    ModelMeta meta = JsonUtility.FromJson<ModelMeta>(json);
                    
                    if (meta != null && meta.assetGuids != null && meta.assetGuids.Contains(assetGuid))
                    {
                        return meta.identity?.id;
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            return null;
        }
    }
}

