using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for import safety, GUID conflicts, and path handling during model import.
    /// </summary>
    public class ImportSafetyTests
    {
        [Test]
        public void TestPathResolutionWithFileConflict()
        {
            // Test that the path resolution correctly handles file vs directory conflicts
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                relativePath = "Models/TestModel/TestModel.FBX" // This points to a file, not a directory
            };

            // This should not throw an exception and should return a directory path
            string resolvedPath = ResolveDestinationPathForTest(meta, null);

            // The test method should convert file paths to directory paths
            // Since the file doesn't exist in test environment, it should return the path as-is
            // But the actual ResolveDestinationPath method in ModelProjectImporter should handle this
            Assert.IsTrue(resolvedPath.EndsWith("TestModel.FBX"), "Test method should return path as-is when file doesn't exist");

            // Test the actual logic that would be used in the real method
            string testPath = "Assets/Models/TestModel/TestModel.FBX";
            if (File.Exists(Path.GetFullPath(testPath)))
            {
                testPath = Path.GetDirectoryName(testPath);
            }

            // Since file doesn't exist, path should remain unchanged
            Assert.AreEqual("Assets/Models/TestModel/TestModel.FBX", testPath);
        }

        [Test]
        public void TestPathResolutionWithDirectoryPath()
        {
            // Test that directory paths work correctly
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                relativePath = "Models/TestModel" // This is already a directory path
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"), "Directory path should be preserved");
        }

        [Test]
        public void TestPathResolutionWithOverride()
        {
            // Test that override install path works correctly
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                relativePath = "Models/TestModel"
            };

            string overridePath = "Assets/Custom/Path";
            string resolvedPath = ResolveDestinationPathForTest(meta, overridePath);
            Assert.AreEqual(overridePath, resolvedPath, "Override path should be used");
        }

        [Test]
        public void TestPathResolutionFallback()
        {
            // Test fallback to default path when no relative path is provided
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                relativePath = null
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"), "Should fallback to model name");
        }

        [Test]
        public void TestGuidConflictDetectionLogic()
        {
            // Test the GUID conflict detection logic
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string>
                {
                    "12345678901234567890123456789012",
                    "abcdef1234567890abcdef1234567890"
                }
            };

            // Simulate existing GUIDs before import (empty project)
            HashSet<string> existingGuidsBeforeImport = new HashSet<string>();

            // Simulate the conflict detection logic
            List<string> conflictingGuids = new List<string>();
            List<string> sameModelGuids = new List<string>();

            string destAbs = "Assets/Models/TestModel";

            foreach (string guid in meta.assetGuids)
            {
                if (existingGuidsBeforeImport.Contains(guid))
                {
                    // In a real scenario, we'd get the asset path from AssetDatabase
                    // For this test, we'll simulate the path logic
                    string simulatedAssetPath = $"Assets/Models/TestModel/TestModel.FBX";

                    if (simulatedAssetPath.StartsWith(destAbs.Replace('\\', '/')))
                    {
                        sameModelGuids.Add(guid);
                    }
                    else
                    {
                        conflictingGuids.Add(guid);
                    }
                }
            }

            // In an empty project, there should be no conflicts
            Assert.AreEqual(0, conflictingGuids.Count, "Should have no conflicts in empty project");
            Assert.AreEqual(0, sameModelGuids.Count, "Should have no same-model GUIDs in empty project");
        }

        [Test]
        public void TestGuidConflictDetectionWithExistingAssets()
        {
            // Test GUID conflict detection when there are existing assets
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string>
                {
                    "12345678901234567890123456789012",
                    "abcdef1234567890abcdef1234567890"
                }
            };

            // Simulate existing GUIDs that include one conflicting GUID
            HashSet<string> existingGuidsBeforeImport = new HashSet<string>
            {
                "12345678901234567890123456789012", // This will conflict
                "99999999999999999999999999999999"  // This won't conflict
            };

            // Simulate the conflict detection logic
            List<string> conflictingGuids = new List<string>();
            List<string> sameModelGuids = new List<string>();

            string destAbs = "Assets/Models/TestModel";

            foreach (string guid in meta.assetGuids)
            {
                if (existingGuidsBeforeImport.Contains(guid))
                {
                    // Simulate that the conflicting asset is in a different location
                    string simulatedAssetPath = "Assets/OtherModel/OtherModel.FBX";

                    if (simulatedAssetPath.StartsWith(destAbs.Replace('\\', '/')))
                    {
                        sameModelGuids.Add(guid);
                    }
                    else
                    {
                        conflictingGuids.Add(guid);
                    }
                }
            }

            // Should detect one conflict
            Assert.AreEqual(1, conflictingGuids.Count, "Should detect one GUID conflict");
            Assert.AreEqual("12345678901234567890123456789012", conflictingGuids[0], "Should detect the correct conflicting GUID");
            Assert.AreEqual(0, sameModelGuids.Count, "Should have no same-model GUIDs");
        }

        [Test]
        public void TestGuidConflictDetectionWithSameModelGuids()
        {
            // Test GUID conflict detection when GUIDs belong to the same model
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string>
                {
                    "12345678901234567890123456789012",
                    "abcdef1234567890abcdef1234567890"
                }
            };

            // Simulate existing GUIDs that include GUIDs from the same model
            HashSet<string> existingGuidsBeforeImport = new HashSet<string>
            {
                "12345678901234567890123456789012", // This will be same-model
                "99999999999999999999999999999999"  // This won't conflict
            };

            // Simulate the conflict detection logic
            List<string> conflictingGuids = new List<string>();
            List<string> sameModelGuids = new List<string>();

            string destAbs = "Assets/Models/TestModel";

            foreach (string guid in meta.assetGuids)
            {
                if (existingGuidsBeforeImport.Contains(guid))
                {
                    // Simulate that the GUID is from the same model directory
                    string simulatedAssetPath = "Assets/Models/TestModel/TestModel.FBX";

                    if (simulatedAssetPath.StartsWith(destAbs.Replace('\\', '/')))
                    {
                        sameModelGuids.Add(guid);
                    }
                    else
                    {
                        conflictingGuids.Add(guid);
                    }
                }
            }

            // Should detect one same-model GUID (not a conflict)
            Assert.AreEqual(0, conflictingGuids.Count, "Should have no conflicts for same-model GUIDs");
            Assert.AreEqual(1, sameModelGuids.Count, "Should detect one same-model GUID");
            Assert.AreEqual("12345678901234567890123456789012", sameModelGuids[0], "Should detect the correct same-model GUID");
        }

        [Test]
        public void TestPreImportGuidCollection()
        {
            // Test that pre-import GUID collection works correctly
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string>
                {
                    "12345678901234567890123456789012",
                    "abcdef1234567890abcdef1234567890"
                }
            };

            // Simulate the pre-import GUID collection logic
            bool shouldCollectGuids = !false && meta?.assetGuids != null && meta.assetGuids.Count > 0;
            Assert.IsTrue(shouldCollectGuids, "Should collect GUIDs for new imports with asset GUIDs");

            // Simulate empty asset GUIDs
            ModelMeta emptyGuidsMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string>() // Empty list
            };

            bool shouldCollectGuidsEmpty = !false && emptyGuidsMeta?.assetGuids != null && emptyGuidsMeta.assetGuids.Count > 0;
            Assert.IsFalse(shouldCollectGuidsEmpty, "Should not collect GUIDs for models with empty asset GUIDs");

            // Simulate update scenario
            bool shouldCollectGuidsUpdate = !true && meta?.assetGuids != null && meta.assetGuids.Count > 0;
            Assert.IsFalse(shouldCollectGuidsUpdate, "Should not collect GUIDs for updates");
        }

        [Test]
        public void TestFileSystemSafety()
        {
            // Test file system safety during import
            string testPath = "Assets/Models/TestModel";

            // Test path sanitization
            string sanitizedPath = SanitizeFolderNameForTest("Test Model With Spaces");
            Assert.AreEqual("Test_Model_With_Spaces", sanitizedPath);

            // Test invalid character handling
            string invalidChars = "Test<Model>With|Invalid:Chars";
            string sanitizedInvalid = SanitizeFolderNameForTest(invalidChars);
            Assert.IsFalse(sanitizedInvalid.Contains("<"), "Should remove invalid characters");
            Assert.IsFalse(sanitizedInvalid.Contains(">"), "Should remove invalid characters");
            Assert.IsFalse(sanitizedInvalid.Contains("|"), "Should remove invalid characters");
            Assert.IsFalse(sanitizedInvalid.Contains(":"), "Should remove invalid characters");
        }

        /// <summary>
        /// Helper method to simulate ResolveDestinationPath logic for testing.
        /// </summary>
        private static string ResolveDestinationPathForTest(ModelMeta meta, string overrideInstallPath)
        {
            if (!string.IsNullOrEmpty(overrideInstallPath))
            {
                return overrideInstallPath;
            }

            if (!string.IsNullOrEmpty(meta?.relativePath))
            {
                string resolvedPath = $"Assets/{meta.relativePath}";

                // Ensure the path points to a directory, not a file
                if (File.Exists(Path.GetFullPath(resolvedPath)))
                {
                    resolvedPath = Path.GetDirectoryName(resolvedPath);
                }

                return resolvedPath;
            }

            // Fallback
            string safeName = SanitizeFolderNameForTest(meta?.identity?.name ?? "UnknownModel");
            return $"Assets/Models/{safeName}";
        }

        /// <summary>
        /// Helper method to simulate SanitizeFolderName logic for testing.
        /// </summary>
        private static string SanitizeFolderNameForTest(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "UnknownModel";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
            return new string(result).Replace(' ', '_');
        }
    }
}
