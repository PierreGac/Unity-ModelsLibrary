using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for all path-related functionality.
    /// This consolidated test class covers path validation, sanitization, preservation, and UI feedback.
    /// </summary>
    public class PathTests
    {
        private const string __TEST_MODEL_NAME = "TestModel";
        private const string __TEST_VERSION = "1.0.0";
        private const string __TEST_DESCRIPTION = "Test model for path functionality";

        #region Path Validation Tests

        [Test]
        public void TestValidRelativePaths()
        {
            // Arrange
            List<string> validPaths = new List<string>
            {
                "Models/NewModel",
                "Models/Benne",
                "Prefabs/Weapons",
                "Textures/UI",
                "Scripts/Gameplay",
                "Models/Medieval/Weapons",
                "Models/Sci-Fi/Vehicles"
            };

            // Act & Assert
            for (int i = 0; i < validPaths.Count; i++)
            {
                string path = validPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsEmpty(errors, $"Path '{path}' should be valid but got errors: {string.Join(", ", errors)}");
            }
        }

        [Test]
        public void TestInvalidRelativePaths()
        {
            // Arrange
            List<string> invalidPaths = new List<string>
            {
                "Models/Benne/Materials",  // Materials folder
                "Models/../Other",         // Path traversal
                "Editor/Models",           // Reserved folder
                "/Models/Test",            // Leading slash
                "Models/Test/",            // Trailing slash
                "",                        // Empty path
                null                       // Null path
            };

            // Act & Assert
            for (int i = 0; i < invalidPaths.Count; i++)
            {
                string path = invalidPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
            }
        }

        [Test]
        public void TestMaterialsFolderValidation()
        {
            // Arrange
            List<string> materialsPaths = new List<string>
            {
                "Models/Benne/Materials",
                "Materials",
                "Assets/Models/Test/Materials"
            };

            // Act & Assert
            for (int i = 0; i < materialsPaths.Count; i++)
            {
                string path = materialsPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("Materials")),
                    $"Path '{path}' should have Materials-related error");
            }
        }

        [Test]
        public void TestPathTraversalValidation()
        {
            // Arrange
            List<string> traversalPaths = new List<string>
            {
                "Models/../Other",
                "Models/../../Assets",
                "~/Models/Test"
            };

            // Act & Assert
            for (int i = 0; i < traversalPaths.Count; i++)
            {
                string path = traversalPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("path traversal")),
                    $"Path '{path}' should have path traversal error");
            }
        }

        [Test]
        public void TestReservedFolderValidation()
        {
            // Arrange
            List<string> reservedPaths = new List<string>
            {
                "Editor/Models",
                "Resources/Models",
                "StreamingAssets/Models",
                "Plugins/Models"
            };

            // Act & Assert
            for (int i = 0; i < reservedPaths.Count; i++)
            {
                string path = reservedPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("reserved folder")),
                    $"Path '{path}' should have reserved folder error");
            }
        }

        [Test]
        public void TestSlashValidation()
        {
            // Arrange
            List<string> slashPaths = new List<string>
            {
                "/Models/Test",
                "Models/Test/",
                "/Models/Test/"
            };

            // Act & Assert
            for (int i = 0; i < slashPaths.Count; i++)
            {
                string path = slashPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("slash")),
                    $"Path '{path}' should have slash-related error");
            }
        }

        [Test]
        public void TestInvalidCharacterHandling()
        {
            // Arrange - Get the actual invalid characters for this platform
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();

            // Act & Assert
            for (int i = 0; i < invalidChars.Length; i++)
            {
                char invalidChar = invalidChars[i];
                string testPath = $"Models{invalidChar}Test";
                List<string> errors = PathUtils.ValidateRelativePath(testPath);
                Assert.IsNotEmpty(errors, $"Path with invalid character '{invalidChar}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("invalid character")),
                    $"Path with invalid character '{invalidChar}' should have invalid character error");
            }
        }

        [Test]
        public void TestPathLengthValidation()
        {
            // Arrange
            string longPath = "Models/" + new string('A', 250); // Exceeds 200 character limit

            // Act
            List<string> errors = PathUtils.ValidateRelativePath(longPath);

            // Assert
            Assert.IsNotEmpty(errors, "Long path should be invalid");
            Assert.IsTrue(errors.Any(e => e.Contains("too long")), "Should have length error");
        }

        #endregion

        #region Path Sanitization Tests

        [Test]
        public void TestSanitizePathSeparatorBasic()
        {
            // Test basic backslash to forward slash conversion
            string input = "Models\\Weapons\\Sword.fbx";
            string expected = "Models/Weapons/Sword.fbx";
            string result = PathUtils.SanitizePathSeparator(input);

            Assert.AreEqual(expected, result, "Should convert backslashes to forward slashes");
        }

        [Test]
        public void TestSanitizePathSeparatorDoubleSlashes()
        {
            // Test double slash removal
            List<string> testCases = new List<string>
            {
                "Models//Weapons",
                "Models///Weapons",
                "//Models//Weapons//",
                "Assets//Models//Weapons"
            };

            for (int i = 0; i < testCases.Count; i++)
            {
                string input = testCases[i];
                string result = PathUtils.SanitizePathSeparator(input);
                Assert.IsFalse(result.Contains("//"), $"Result should not contain double slashes: {result}");
            }
        }

        [Test]
        public void TestSanitizePathSeparatorMixedSeparators()
        {
            // Test mixed separators
            string input = "Models\\/Weapons\\Sword.fbx";
            string result = PathUtils.SanitizePathSeparator(input);

            Assert.IsFalse(result.Contains("\\"), "Result should not contain backslashes");
            Assert.IsFalse(result.Contains("//"), "Result should not contain double slashes");
            Assert.IsTrue(result.Contains("/"), "Result should contain forward slashes");
        }

        [Test]
        public void TestSanitizePathSeparatorEmptyAndNull()
        {
            // Test empty and null inputs
            Assert.AreEqual(null, PathUtils.SanitizePathSeparator(null), "Null input should return null");
            Assert.AreEqual("", PathUtils.SanitizePathSeparator(""), "Empty input should return empty");
        }

        [Test]
        public void TestSanitizePathSeparatorComplexCases()
        {
            // Test complex cases
            List<(string input, string expected)> testCases = new List<(string, string)>
            {
                ("Models\\\\Weapons", "Models/Weapons"),
                ("Models//Weapons//", "Models/Weapons/"),
                ("/Models//Weapons", "/Models/Weapons"),
                ("Assets\\\\Models\\\\Weapons", "Assets/Models/Weapons"),
                ("Models\\/Weapons\\/Sword", "Models/Weapons/Sword")
            };

            for (int i = 0; i < testCases.Count; i++)
            {
                (string input, string expected) = testCases[i];
                string result = PathUtils.SanitizePathSeparator(input);
                Assert.AreEqual(expected, result, $"Input '{input}' should produce '{expected}'");
            }
        }

        #endregion

        #region Path Preservation Tests

        [Test]
        public void TestModelDeployerResolveRelativePath()
        {
            // Test valid relative paths
            List<string> validPaths = new List<string>
            {
                "Models/Weapons",
                "Models/Medieval/Armor",
                "Prefabs/Vehicles",
                "Textures/UI",
                "Scripts/Gameplay"
            };

            for (int i = 0; i < validPaths.Count; i++)
            {
                string testPath = validPaths[i];
                // Use reflection to test private method
                System.Reflection.MethodInfo method = typeof(ModelDeployer).GetMethod("ResolveRelativePath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                Assert.IsNotNull(method, "ResolveRelativePath method should exist");

                string result = (string)method.Invoke(null, new object[] { testPath, __TEST_MODEL_NAME });

                Assert.IsNotNull(result, $"Result should not be null for path: {testPath}");
                Assert.IsFalse(result.StartsWith("Assets/"), $"Result should not start with 'Assets/' for path: {testPath}");
                Assert.IsTrue(result.Contains(testPath.Replace("Assets/", "")), $"Result should contain the path: {testPath}");
            }
        }

        [Test]
        public void TestModelDeployerResolveInstallPath()
        {
            // Test install path resolution
            List<string> providedPaths = new List<string>
            {
                "Assets/Models/Weapons",
                "Models/Medieval/Armor",
                "Prefabs/Vehicles"
            };

            // Test provided paths (should use as-is)
            for (int i = 0; i < providedPaths.Count; i++)
            {
                string testPath = providedPaths[i];
                System.Reflection.MethodInfo method = typeof(ModelDeployer).GetMethod("ResolveInstallPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                string result = (string)method.Invoke(null, new object[] { testPath, __TEST_MODEL_NAME });

                Assert.IsNotNull(result, $"Result should not be null for path: {testPath}");
                Assert.IsTrue(result.StartsWith("Assets/"), $"Result should start with 'Assets/' for path: {testPath}");
                // For provided paths, the result should contain the original path content, not necessarily the model name
                Assert.IsTrue(result.Contains("Weapons") || result.Contains("Armor") || result.Contains("Vehicles"),
                    $"Result should contain original path content for path: {testPath}");
            }

            // Test fallback paths (should use model name)
            List<string> fallbackPaths = new List<string> { null, "" };
            for (int i = 0; i < fallbackPaths.Count; i++)
            {
                string testPath = fallbackPaths[i];
                System.Reflection.MethodInfo method = typeof(ModelDeployer).GetMethod("ResolveInstallPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                string result = (string)method.Invoke(null, new object[] { testPath, __TEST_MODEL_NAME });

                Assert.IsNotNull(result, $"Result should not be null for path: {testPath}");
                Assert.IsTrue(result.StartsWith("Assets/"), $"Result should start with 'Assets/' for path: {testPath}");
                Assert.IsTrue(result.Contains("TestModel"), $"Result should contain model name for fallback path: {testPath}");
            }
        }

        [Test]
        public void TestPathPreservationWorkflow()
        {
            // Test the complete workflow of path preservation
            ModelMeta originalMeta = CreateTestModelMeta();
            originalMeta.relativePath = "Models/Weapons";
            originalMeta.installPath = "Assets/Models/Weapons";

            // Simulate the workflow steps
            string resolvedRelativePath = PathUtils.SanitizePathSeparator(originalMeta.relativePath);
            string resolvedInstallPath = PathUtils.SanitizePathSeparator(originalMeta.installPath);

            // Verify paths are preserved correctly
            Assert.AreEqual("Models/Weapons", resolvedRelativePath, "Relative path should be preserved");
            Assert.AreEqual("Assets/Models/Weapons", resolvedInstallPath, "Install path should be preserved");

            // Verify validation passes
            List<string> relativePathErrors = PathUtils.ValidateRelativePath(resolvedRelativePath);
            Assert.IsEmpty(relativePathErrors, "Resolved relative path should be valid");

            // Verify path consistency
            Assert.IsTrue(resolvedInstallPath.EndsWith(resolvedRelativePath),
                "Install path should end with relative path");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test ModelMeta object for testing purposes.
        /// </summary>
        /// <returns>Test ModelMeta object</returns>
        private static ModelMeta CreateTestModelMeta()
        {
            return new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = Guid.NewGuid().ToString("N"),
                    name = __TEST_MODEL_NAME
                },
                version = __TEST_VERSION,
                description = __TEST_DESCRIPTION,
                author = "TestAuthor",
                createdTimeTicks = DateTime.Now.Ticks,
                updatedTimeTicks = DateTime.Now.Ticks,
                uploadTimeTicks = DateTime.Now.Ticks,
                relativePath = "Models/TestModel",
                installPath = "Assets/Models/TestModel"
            };
        }

        #endregion
    }
}
