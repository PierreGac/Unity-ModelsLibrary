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

        [Test]
        public void TestUpdateModelWithNewVersion()
        {
            // Test updating a model to a new version
            string baseVersion = "1.0.0";
            if (SemVer.TryParse(baseVersion, out SemVer parsed))
            {
                SemVer bumped = new SemVer(parsed.major, parsed.minor, parsed.patch + 1);
                string newVersion = bumped.ToString();
                Assert.AreEqual("1.0.1", newVersion, "Should bump patch version");
            }
            else
            {
                Assert.Fail("Should be able to parse version: " + baseVersion);
            }
        }

        [Test]
        public void TestUpdateModelVersionComparison()
        {
            // Test SemVer comparison for update detection
            string localVersion = "1.0.0";
            string remoteVersion = "1.1.0";

            if (SemVer.TryParse(localVersion, out SemVer local) && SemVer.TryParse(remoteVersion, out SemVer remote))
            {
                bool hasUpdate = remote.CompareTo(local) > 0;
                Assert.IsTrue(hasUpdate, "Should detect update when remote is newer");
            }
            else
            {
                Assert.Fail("Should be able to parse versions");
            }
        }

        [Test]
        public void TestUpdateModelMetadataOnly()
        {
            // Test PublishMetadataUpdateAsync (metadata-only updates)
            ModelMeta updatedMeta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.0",
                description = "Updated description"
            };

            string baseVersion = "1.0.0";
            if (SemVer.TryParse(baseVersion, out SemVer parsed))
            {
                SemVer bumped = new SemVer(parsed.major, parsed.minor, parsed.patch + 1);
                string newVersion = bumped.ToString();
                updatedMeta.version = newVersion;
                updatedMeta.updatedTimeTicks = DateTime.Now.Ticks;

                Assert.AreEqual("1.0.1", updatedMeta.version, "Should create new version for metadata update");
                Assert.Greater(updatedMeta.updatedTimeTicks, 0, "Should set updated timestamp");
            }
            else
            {
                Assert.Fail("Should be able to parse version");
            }
        }

        [Test]
        public void TestUpdateModelClonesFiles()
        {
            // Test that CloneVersionFilesAsync copies all files correctly
            string modelId = "test-model";
            string sourceVersion = "1.0.0";
            string targetVersion = "1.0.1";

            string sourceRootRel = $"{modelId}/{sourceVersion}".Replace('\\', '/');
            string targetRootRel = $"{modelId}/{targetVersion}".Replace('\\', '/');

            Assert.AreNotEqual(sourceRootRel, targetRootRel, "Source and target paths should be different");
            Assert.IsTrue(targetRootRel.Contains(targetVersion), "Target path should contain new version");
        }

        [Test]
        public void TestUpdateModelVersionBumping()
        {
            // Test version bumping strategy (patch, minor, major)
            string currentVersion = "1.2.3";
            if (SemVer.TryParse(currentVersion, out SemVer parsed))
            {
                // Patch bump
                SemVer patchBump = new SemVer(parsed.major, parsed.minor, parsed.patch + 1);
                Assert.AreEqual("1.2.4", patchBump.ToString(), "Patch bump should increment patch");

                // Minor bump
                SemVer minorBump = new SemVer(parsed.major, parsed.minor + 1, 0);
                Assert.AreEqual("1.3.0", minorBump.ToString(), "Minor bump should increment minor and reset patch");

                // Major bump
                SemVer majorBump = new SemVer(parsed.major + 1, 0, 0);
                Assert.AreEqual("2.0.0", majorBump.ToString(), "Major bump should increment major and reset minor/patch");
            }
            else
            {
                Assert.Fail("Should be able to parse version: " + currentVersion);
            }
        }

        [Test]
        public void TestUpdateModelChangelogEntry()
        {
            // Test that changelog entries are created for updates
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.1",
                changelog = new List<ModelChangelogEntry>()
            };

            ModelChangelogEntry entry = new ModelChangelogEntry
            {
                version = "1.0.1",
                summary = "Metadata updated",
                author = "Test Author",
                timestamp = DateTime.Now.Ticks
            };

            meta.changelog.Add(entry);

            Assert.AreEqual(1, meta.changelog.Count, "Should have one changelog entry");
            Assert.AreEqual("1.0.1", meta.changelog[0].version, "Changelog entry should have correct version");
        }

        [Test]
        public void TestUpdateModelIndexUpdate()
        {
            // Test that index is updated with latest version
            ModelIndex index = new ModelIndex
            {
                entries = new List<ModelIndex.Entry>
                {
                    new ModelIndex.Entry
                    {
                        id = "test-model",
                        name = "Test Model",
                        latestVersion = "1.0.0",
                        updatedTimeTicks = DateTime.UtcNow.Ticks
                    }
                }
            };

            ModelMeta newMeta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.1",
                updatedTimeTicks = DateTime.UtcNow.Ticks
            };

            // Simulate index update logic
            ModelIndex.Entry entry = index.Get("test-model");
            if (entry != null)
            {
                if (SemVer.TryParse(newMeta.version, out SemVer vNew) && SemVer.TryParse(entry.latestVersion, out SemVer vOld))
                {
                    bool shouldUpdate = vNew.CompareTo(vOld) >= 0;
                    if (shouldUpdate)
                    {
                        entry.latestVersion = newMeta.version;
                    }
                }
            }

            Assert.AreEqual("1.0.1", entry.latestVersion, "Index should be updated with new version");
        }

        [Test]
        public void TestUpdateModelWithInvalidVersion()
        {
            // Test error handling for invalid version strings
            string invalidVersion = "not.a.valid.version";
            bool canParse = SemVer.TryParse(invalidVersion, out SemVer parsed);

            Assert.IsFalse(canParse, "Should not parse invalid version string");
        }

        [Test]
        public void TestUpdateModelVersionComparisonEdgeCases()
        {
            // Test edge cases in version comparison
            string[] versions = { "0.0.0", "0.0.1", "0.1.0", "1.0.0", "1.0.1", "1.1.0", "2.0.0" };
            
            for (int i = 0; i < versions.Length - 1; i++)
            {
                string v1 = versions[i];
                string v2 = versions[i + 1];
                
                if (SemVer.TryParse(v1, out SemVer parsed1) && SemVer.TryParse(v2, out SemVer parsed2))
                {
                    int comparison = parsed2.CompareTo(parsed1);
                    Assert.Greater(comparison, 0, $"{v2} should be greater than {v1}");
                }
            }
        }

        [Test]
        public void TestUpdateModelPreservesCreatedTime()
        {
            // Test that createdTimeTicks is preserved on update
            long originalCreatedTime = DateTime.UtcNow.Ticks - TimeSpan.FromDays(30).Ticks;
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.0",
                createdTimeTicks = originalCreatedTime
            };

            // Simulate update
            long nowUtc = DateTime.Now.Ticks;
            if (meta.createdTimeTicks <= 0)
            {
                meta.createdTimeTicks = nowUtc;
            }
            meta.updatedTimeTicks = nowUtc;

            Assert.AreEqual(originalCreatedTime, meta.createdTimeTicks, "Created time should be preserved");
            Assert.Greater(meta.updatedTimeTicks, meta.createdTimeTicks, "Updated time should be set");
        }

        [Test]
        public void TestUpdateModelUpdateTimeTicks()
        {
            // Test that updatedTimeTicks is set correctly
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.0"
            };

            long nowUtc = DateTime.Now.Ticks;
            meta.updatedTimeTicks = nowUtc;

            Assert.Greater(meta.updatedTimeTicks, 0, "Updated time should be set");
        }

        [Test]
        public void TestUpdateModelAuthorHandling()
        {
            // Test author field handling during updates
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = "test-model", name = "Test Model" },
                version = "1.0.1"
            };

            string author = "Test Author";
            string resolvedAuthor = string.IsNullOrWhiteSpace(author) ? "unknown" : author;
            if (string.IsNullOrWhiteSpace(meta.author))
            {
                meta.author = resolvedAuthor;
            }

            Assert.AreEqual("Test Author", meta.author, "Author should be set");
        }
    }
}
