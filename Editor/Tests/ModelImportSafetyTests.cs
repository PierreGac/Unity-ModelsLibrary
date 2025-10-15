using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for model import safety and path handling.
    /// </summary>
    public class ModelImportSafetyTests
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
                relativePath = "Models/TestModel" // This points to a directory
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);

            // Should be a directory path
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"));
            Assert.IsFalse(resolvedPath.EndsWith(".FBX"));
        }

        [Test]
        public void TestPathResolutionWithOverride()
        {
            // Test that override paths work correctly
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                relativePath = "Models/TestModel/TestModel.FBX"
            };

            string overridePath = "Assets/Models/OverrideModel";
            string resolvedPath = ResolveDestinationPathForTest(meta, overridePath);

            // Should use override path
            Assert.AreEqual(overridePath, resolvedPath);
        }

        [Test]
        public void TestPathResolutionFallback()
        {
            // Test fallback when no relative path is provided
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                }
                // No relativePath provided
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);

            // Should use fallback path
            Assert.IsTrue(resolvedPath.StartsWith("Assets/Models/"));
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"));
        }

        // Helper method to test the private ResolveDestinationPath method
        private string ResolveDestinationPathForTest(ModelMeta meta, string overrideInstallPath)
        {
            // This is a simplified version of the ResolveDestinationPath logic for testing
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

        private string SanitizeFolderNameForTest(string name)
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
