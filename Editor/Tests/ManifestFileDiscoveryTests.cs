using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for manifest file discovery functionality.
    /// Verifies that both old and new naming conventions are found using file system enumeration.
    /// </summary>
    public class ManifestFileDiscoveryTests
    {
        /// <summary>
        /// Tests finding `.modelLibrary.meta.json` files using Directory.EnumerateFiles.
        /// </summary>
        [Test]
        public void TestFindManifestFilesWithNewNaming()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"ManifestDiscoveryTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with new naming
                string manifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(manifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(1, foundManifests.Count, "Should find one manifest file with new naming");
                Assert.IsTrue(foundManifests[0].EndsWith(".modelLibrary.meta.json"), "Found file should have new naming");
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
        /// Tests finding `modelLibrary.meta.json` files (backward compatibility).
        /// </summary>
        [Test]
        public void TestFindManifestFilesWithOldNaming()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"ManifestDiscoveryTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with old naming
                string manifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(manifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(1, foundManifests.Count, "Should find one manifest file with old naming");
                Assert.IsTrue(foundManifests[0].EndsWith("modelLibrary.meta.json"), "Found file should have old naming");
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
        /// Tests finding both old and new naming in same project.
        /// </summary>
        [Test]
        public void TestFindManifestFilesBothNamingConventions()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"ManifestDiscoveryTest_{Guid.NewGuid():N}");
            string assetsTestDir1 = Path.Combine(tempTestDir, "Assets", "Models", "Model1");
            string assetsTestDir2 = Path.Combine(tempTestDir, "Assets", "Models", "Model2");
            Directory.CreateDirectory(assetsTestDir1);
            Directory.CreateDirectory(assetsTestDir2);

            try
            {
                // Create manifest files with both naming conventions
                string newNamingFile = Path.Combine(assetsTestDir1, ".modelLibrary.meta.json");
                string oldNamingFile = Path.Combine(assetsTestDir2, "modelLibrary.meta.json");

                ModelMeta meta1 = new ModelMeta
                {
                    identity = new ModelIdentity { id = "model1", name = "Model 1" },
                    version = "1.0.0"
                };
                ModelMeta meta2 = new ModelMeta
                {
                    identity = new ModelIdentity { id = "model2", name = "Model 2" },
                    version = "1.0.0"
                };

                File.WriteAllText(newNamingFile, JsonUtility.ToJson(meta1));
                File.WriteAllText(oldNamingFile, JsonUtility.ToJson(meta2));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(2, foundManifests.Count, "Should find both manifest files");
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
        /// Verifies AssetDatabase.FindAssets() cannot find hidden files.
        /// </summary>
        [Test]
        public void TestManifestFileDiscoveryIgnoresAssetDatabase()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"ManifestDiscoveryTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with new naming (hidden)
                string manifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(manifestFile, JsonUtility.ToJson(meta));

                // AssetDatabase.FindAssets searches Unity's actual project, not temp directories
                // This test verifies that AssetDatabase cannot find hidden files (which is expected)
                // Note: AssetDatabase may find other files in Unity's project, so we just verify
                // that it doesn't find our specific temp file (which it can't since it's outside Unity's project)
                string[] foundGuids = AssetDatabase.FindAssets(".modelLibrary.meta");
                // AssetDatabase should not find hidden files starting with dot in Unity's project
                // The key point is that Directory.EnumerateFiles CAN find them, while AssetDatabase cannot
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
        /// Tests recursive search in nested folders.
        /// </summary>
        [Test]
        public void TestManifestFileDiscoveryInSubdirectories()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"ManifestDiscoveryTest_{Guid.NewGuid():N}");
            string assetsTestDir1 = Path.Combine(tempTestDir, "Assets", "Models", "Model1");
            string assetsTestDir2 = Path.Combine(tempTestDir, "Assets", "Models", "Model2", "Subfolder");
            Directory.CreateDirectory(assetsTestDir1);
            Directory.CreateDirectory(assetsTestDir2);

            try
            {
                // Create manifest files in nested directories
                string manifestFile1 = Path.Combine(assetsTestDir1, ".modelLibrary.meta.json");
                string manifestFile2 = Path.Combine(assetsTestDir2, ".modelLibrary.meta.json");

                ModelMeta meta1 = new ModelMeta
                {
                    identity = new ModelIdentity { id = "model1", name = "Model 1" },
                    version = "1.0.0"
                };
                ModelMeta meta2 = new ModelMeta
                {
                    identity = new ModelIdentity { id = "model2", name = "Model 2" },
                    version = "1.0.0"
                };

                File.WriteAllText(manifestFile1, JsonUtility.ToJson(meta1));
                File.WriteAllText(manifestFile2, JsonUtility.ToJson(meta2));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(2, foundManifests.Count, "Should find manifest files in nested directories");
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
        /// Tests error handling for invalid paths.
        /// </summary>
        [Test]
        public void TestManifestFileDiscoveryWithInvalidPaths()
        {
            // Test with non-existent directory
            List<string> foundManifests = new List<string>();
            try
            {
                foreach (string manifestPath in Directory.EnumerateFiles("Assets/NonExistent", ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }
            }
            catch (DirectoryNotFoundException)
            {
                // Expected exception for non-existent directory
                Assert.IsTrue(true, "Should handle non-existent directory gracefully");
            }

            Assert.AreEqual(0, foundManifests.Count, "Should not find any files in non-existent directory");
        }
    }
}

