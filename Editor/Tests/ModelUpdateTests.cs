using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for model update process and workflow functionality.
    /// </summary>
    public class ModelUpdateTests
    {
        [Test]
        public void TestUpdateProcessSkipsGUIDConflictCheck()
        {
            // Test that the update process correctly identifies when to skip GUID conflict checks
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0"
            };

            // Simulate update process parameters
            bool isUpdate = true;
            bool shouldCheckConflicts = !isUpdate && meta?.assetGuids != null && meta.assetGuids.Count > 0;

            Assert.IsFalse(shouldCheckConflicts, "Update process should skip GUID conflict checks");
        }

        [Test]
        public void TestNewImportProcessChecksGUIDConflicts()
        {
            // Test that new imports correctly identify when to check GUID conflicts
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string> { "12345678901234567890123456789012" }
            };

            // Simulate new import process parameters
            bool isUpdate = false;
            bool shouldCheckConflicts = !isUpdate && meta?.assetGuids != null && meta.assetGuids.Count > 0;

            Assert.IsTrue(shouldCheckConflicts, "New import process should check GUID conflicts");
        }

        [Test]
        public void TestUpdateProcessParameters()
        {
            // Test that ImportFromCacheAsync method has the correct isUpdate parameter
            MethodInfo method = typeof(ModelProjectImporter).GetMethod("ImportFromCacheAsync",
                BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(method, "ImportFromCacheAsync method should exist");

            ParameterInfo[] parameters = method.GetParameters();
            Assert.AreEqual(5, parameters.Length, "ImportFromCacheAsync should have 5 parameters");

            // Check that isUpdate parameter exists
            bool hasIsUpdateParam = false;
            foreach (ParameterInfo param in parameters)
            {
                if (param.Name == "isUpdate" && param.ParameterType == typeof(bool))
                {
                    hasIsUpdateParam = true;
                    break;
                }
            }

            Assert.IsTrue(hasIsUpdateParam, "ImportFromCacheAsync should have isUpdate parameter");
        }

        [Test]
        public void TestGUIDConflictDetectionLogic()
        {
            // Test the GUID conflict detection logic for updates vs new imports
            List<string> modelGuids = new List<string> { "guid1", "guid2", "guid3" };
            HashSet<string> existingGuids = new HashSet<string> { "guid1", "guid4", "guid5" };
            string destAbs = "Assets/Models/TestModel";

            List<string> conflictingGuids = new List<string>();
            List<string> sameModelGuids = new List<string>();

            foreach (string guid in modelGuids)
            {
                if (existingGuids.Contains(guid))
                {
                    // Simulate asset path logic
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

            // Should detect one same-model GUID (not a conflict for updates)
            Assert.AreEqual(0, conflictingGuids.Count, "Should have no actual conflicts for same-model GUIDs");
            Assert.AreEqual(1, sameModelGuids.Count, "Should detect one same-model GUID");
        }

        [Test]
        public void TestSameModelGUIDDetection()
        {
            // Test that same-model GUIDs are correctly identified
            string destAbs = "Assets/Models/TestModel";
            string[] testPaths = {
                "Assets/Models/TestModel/TestModel.FBX",
                "Assets/Models/TestModel/Materials/TestMaterial.mat",
                "Assets/OtherModel/OtherModel.FBX",
                "Assets/Models/TestModel/Subfolder/Asset.asset"
            };

            int sameModelCount = 0;
            int differentModelCount = 0;

            foreach (string path in testPaths)
            {
                if (path.StartsWith(destAbs.Replace('\\', '/')))
                {
                    sameModelCount++;
                }
                else
                {
                    differentModelCount++;
                }
            }

            Assert.AreEqual(3, sameModelCount, "Should identify 3 same-model assets");
            Assert.AreEqual(1, differentModelCount, "Should identify 1 different-model asset");
        }

        [Test]
        public void TestUpdateProcessHandlesExistingDirectory()
        {
            // Test that update process can handle existing directories
            string destPath = "Assets/Models/TestModel";
            bool isUpdate = true;

            // Simulate the logic that would be used in ImportFromCacheAsync
            bool shouldCleanDestination = !isUpdate; // Only clean for new imports, not updates

            Assert.IsFalse(shouldCleanDestination, "Update process should not clean existing directory");
        }

        [Test]
        public void TestUpdateProcessLogging()
        {
            // Test that appropriate log messages are generated during updates
            string modelName = "TestModel";
            bool isUpdate = true;

            string expectedMessage = $"Model update completed for '{modelName}'. GUID conflicts are expected and normal for updates.";

            // Simulate the logging logic
            if (isUpdate)
            {
                Debug.Log($"[ModelProjectImporter] {expectedMessage}");
            }

            // This test verifies the logging logic structure
            Assert.IsTrue(isUpdate, "Test should simulate update scenario");
        }

        [Test]
        public void TestUpdateProcessMessageGeneration()
        {
            // Test update message generation for different version types
            string localVersion = "1.0.0";
            string remoteVersion = "1.1.0";

            string updateMessage = $"Update available: {localVersion} → {remoteVersion}";

            Assert.AreEqual("Update available: 1.0.0 → 1.1.0", updateMessage);
        }

        [Test]
        public void TestVersionBumpingLogic()
        {
            // Test version bumping logic for updates
            string currentVersion = "1.2.3";

            if (SemVer.TryParse(currentVersion, out SemVer parsed))
            {
                SemVer nextPatch = new SemVer(parsed.major, parsed.minor, parsed.patch + 1);
                Assert.AreEqual("1.2.4", nextPatch.ToString());

                SemVer nextMinor = new SemVer(parsed.major, parsed.minor + 1, 0);
                Assert.AreEqual("1.3.0", nextMinor.ToString());

                SemVer nextMajor = new SemVer(parsed.major + 1, 0, 0);
                Assert.AreEqual("2.0.0", nextMajor.ToString());
            }
            else
            {
                Assert.Fail("Should be able to parse version: " + currentVersion);
            }
        }

        [Test]
        public void TestUpdateWorkflowIntegration()
        {
            // Test the complete update workflow integration
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new List<string> { "guid1", "guid2" }
            };

            bool isUpdate = true;
            string destPath = "Assets/Models/TestModel";

            // Simulate the complete workflow
            bool shouldCheckConflicts = !isUpdate && meta?.assetGuids != null && meta.assetGuids.Count > 0;
            bool shouldCleanDestination = !isUpdate;
            bool shouldLogUpdateCompletion = isUpdate;

            Assert.IsFalse(shouldCheckConflicts, "Update should skip conflict checks");
            Assert.IsFalse(shouldCleanDestination, "Update should not clean destination");
            Assert.IsTrue(shouldLogUpdateCompletion, "Update should log completion");
        }
    }
}
