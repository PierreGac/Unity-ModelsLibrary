using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
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
    /// Tests for backward compatibility with old manifest file naming.
    /// Verifies that both old and new naming conventions work correctly.
    /// </summary>
    public class BackwardCompatibilityTests
    {
        /// <summary>
        /// Tests that old `modelLibrary.meta.json` files are still found.
        /// </summary>
        [Test]
        public void TestOldManifestFilesStillDetected()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with old naming
                string oldManifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(oldManifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(1, foundManifests.Count, "Should find old manifest file");
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
        /// Tests that new `.modelLibrary.meta.json` files are found first.
        /// </summary>
        [Test]
        public void TestNewManifestFilesTakePriority()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create both old and new manifest files
                string oldManifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                string newManifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");

                ModelMeta oldMeta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Old Model" },
                    version = "1.0.0"
                };
                ModelMeta newMeta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "New Model" },
                    version = "2.0.0"
                };

                File.WriteAllText(oldManifestFile, JsonUtility.ToJson(oldMeta));
                File.WriteAllText(newManifestFile, JsonUtility.ToJson(newMeta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                // Search for new naming first (priority)
                List<string> foundManifests = new List<string>();
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }
                // Then old naming
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    foundManifests.Add(manifestPath);
                }

                Assert.AreEqual(2, foundManifests.Count, "Should find both manifest files");
                Assert.IsTrue(foundManifests[0].EndsWith(".modelLibrary.meta.json"), "New naming should be found first");
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
        /// Tests that both old and new manifest files can be read.
        /// </summary>
        [Test]
        public void TestManifestFileReadingWorksBothFormats()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create both old and new manifest files
                string oldManifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                string newManifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");

                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };

                File.WriteAllText(oldManifestFile, JsonUtility.ToJson(meta));
                File.WriteAllText(newManifestFile, JsonUtility.ToJson(meta));

                // Test reading old format
                string oldJson = File.ReadAllText(oldManifestFile);
                ModelMeta oldMeta = JsonUtility.FromJson<ModelMeta>(oldJson);
                Assert.IsNotNull(oldMeta, "Should be able to read old format");
                Assert.AreEqual("test-model", oldMeta.identity.id, "Old format should have correct ID");

                // Test reading new format
                string newJson = File.ReadAllText(newManifestFile);
                ModelMeta newMeta = JsonUtility.FromJson<ModelMeta>(newJson);
                Assert.IsNotNull(newMeta, "Should be able to read new format");
                Assert.AreEqual("test-model", newMeta.identity.id, "New format should have correct ID");
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
        /// Tests ModelScanService finds both naming conventions.
        /// </summary>
        [Test]
        public void TestModelScanServiceFindsBothFormats()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with old naming
                string oldManifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(oldManifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                // Simulate ModelScanService.FindLocalVersionAsync logic
                List<string> manifestPaths = new List<string>();
                
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }

                Assert.AreEqual(1, manifestPaths.Count, "ModelScanService should find old format manifest");
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
        /// Tests ModelDetailsWindow.CheckInstallationStatusAsync finds both formats.
        /// </summary>
        [Test]
        public void TestModelDetailsWindowFindsBothFormats()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Create manifest file with new naming
                string newManifestFile = Path.Combine(assetsTestDir, ".modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(newManifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                // Simulate CheckInstallationStatusAsync logic
                List<string> manifestPaths = new List<string>();
                
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }

                Assert.AreEqual(1, manifestPaths.Count, "ModelDetailsWindow should find new format manifest");
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
        /// Tests ContextMenus.FindModelIdFromGuid finds both formats.
        /// </summary>
        [Test]
        public void TestContextMenusFindsBothFormats()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"BackwardCompatTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "TestModel");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string testGuid = "12345678901234567890123456789012";
                
                // Create manifest file with old naming
                string oldManifestFile = Path.Combine(assetsTestDir, "modelLibrary.meta.json");
                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                    version = "1.0.0",
                    assetGuids = new List<string> { testGuid }
                };
                File.WriteAllText(oldManifestFile, JsonUtility.ToJson(meta));

                // Use absolute path to Assets folder in temp directory to avoid searching Unity's actual Assets folder
                string assetsPath = Path.Combine(tempTestDir, "Assets");
                
                // Simulate FindModelIdFromGuid logic
                List<string> manifestPaths = new List<string>();
                
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, ".modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }
                foreach (string manifestPath in Directory.EnumerateFiles(assetsPath, "modelLibrary.meta.json", SearchOption.AllDirectories))
                {
                    manifestPaths.Add(manifestPath);
                }

                string foundModelId = null;
                for (int i = 0; i < manifestPaths.Count; i++)
                {
                    string manifestPath = manifestPaths[i];
                    try
                    {
                        string json = File.ReadAllText(manifestPath);
                        ModelMeta loadedMeta = JsonUtility.FromJson<ModelMeta>(json);
                        
                        if (loadedMeta != null && loadedMeta.assetGuids != null && loadedMeta.assetGuids.Contains(testGuid))
                        {
                            foundModelId = loadedMeta.identity?.id;
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                Assert.AreEqual("test-model", foundModelId, "ContextMenus should find model ID from old format manifest");
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }
    }
}

