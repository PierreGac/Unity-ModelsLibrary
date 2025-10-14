using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for path validation functionality.
    /// These tests verify that the PathUtils.ValidateRelativePath method works correctly with various inputs.
    /// </summary>
    public class PathValidationTests
    {
        [Test]
        public void TestValidRelativePaths()
        {
            // Arrange
            List<string> validPaths = new List<string>
            {
                "Models/NewModel",
                "Models/Benne",
                "Assets/Models/Test",
                "Prefabs/Weapons",
                "Textures/UI",
                "Scripts/Gameplay",
                "Models/Medieval/Weapons",
                "Models/Sci-Fi/Vehicles"
            };

            // Act & Assert
            foreach (string path in validPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsEmpty(errors, $"Path '{path}' should be valid but got errors: {string.Join(", ", errors)}");
            }
        }

        [Test]
        public void TestMaterialsFolderRestriction()
        {
            // Arrange
            List<string> invalidMaterialsPaths = new List<string>
            {
                "Models/Benne/Materials",
                "Models/Medieval/Weapons/Materials",
                "Materials",
                "Assets/Models/Test/Materials",
                "Models/Benne/Materials/",
                "Models/Benne\\Materials" // Windows path separator
            };

            // Act & Assert
            foreach (string path in invalidMaterialsPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (Materials folder)");
                Assert.IsTrue(errors.Any(e => e.Contains("Materials")), $"Path '{path}' should have Materials-related error");
            }
        }

        [Test]
        public void TestPathTraversalAttempts()
        {
            // Arrange
            List<string> traversalPaths = new List<string>
            {
                "Models/../Other",
                "Models/../../Assets",
                "Models/..",
                "~/Models/Test",
                "Models/Test/..",
                "Models/Test/../.."
            };

            // Act & Assert
            foreach (string path in traversalPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (path traversal)");
                Assert.IsTrue(errors.Any(e => e.Contains("path traversal")), $"Path '{path}' should have path traversal error");
            }
        }

        [Test]
        public void TestEmptyAndNullPaths()
        {
            // Arrange
            List<string> invalidPaths = new List<string>
            {
                null,
                "",
                "   ",
                "\t",
                "\n"
            };

            // Act & Assert
            foreach (string path in invalidPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (empty/null)");
                Assert.IsTrue(errors.Any(e => e.Contains("required") || e.Contains("empty")),
                    $"Path '{path}' should have empty/required error");
            }
        }

        [Test]
        public void TestReservedFolderNames()
        {
            // Arrange
            List<string> reservedPaths = new List<string>
            {
                "Editor/Models",
                "Resources/Models",
                "StreamingAssets/Models",
                "Plugins/Models",
                "Models/Editor/Test",
                "Models/Resources/Test",
                "Models/StreamingAssets/Test",
                "Models/Plugins/Test"
            };

            // Act & Assert
            foreach (string path in reservedPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (reserved folder)");
                Assert.IsTrue(errors.Any(e => e.Contains("reserved folder")),
                    $"Path '{path}' should have reserved folder error");
            }
        }

        [Test]
        public void TestInvalidCharacters()
        {
            // Arrange - Get the actual invalid characters for this platform
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();
            List<string> invalidCharPaths = new List<string>();

            // Create test paths with each invalid character
            foreach (char invalidChar in invalidChars)
            {
                invalidCharPaths.Add($"Models{invalidChar}Test");
            }

            // Act & Assert
            foreach (string path in invalidCharPaths)
            {
                // This should not throw an exception anymore
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (contains invalid character)");
                Assert.IsTrue(errors.Any(e => e.Contains("invalid character")),
                    $"Path '{path}' should have invalid character error");
            }
        }


        [Test]
        public void TestColonCharacterHandling()
        {
            // Test the colon character specifically - it may be valid on some platforms but problematic for Unity
            string colonPath = "Models:Test";
            List<string> errors = PathUtils.ValidateRelativePath(colonPath);

            // The colon might not be in the invalid characters list on all platforms
            // So we just verify that the method doesn't throw an exception
            Assert.DoesNotThrow(() => PathUtils.ValidateRelativePath(colonPath),
                "Colon character should not cause an exception");

            // If it's considered invalid, it should return an error
            // If it's considered valid, it might still have other validation issues
            // This test just ensures no exceptions are thrown
        }

        [Test]
        public void TestInvalidCharactersNoException()
        {
            // This test specifically verifies that invalid characters don't cause exceptions
            // Arrange - Get the actual invalid characters for this platform
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();
            List<string> problematicPaths = new List<string>();

            // Create test paths with each invalid character
            foreach (char invalidChar in invalidChars)
            {
                problematicPaths.Add($"Models{invalidChar}Test");
            }

            // Act & Assert - These should not throw exceptions
            foreach (string path in problematicPaths)
            {
                Assert.DoesNotThrow(() =>
                {
                    List<string> errors = PathUtils.ValidateRelativePath(path);
                    // We expect errors for invalid characters, but no exceptions should be thrown
                    Assert.IsNotEmpty(errors, $"Path '{path}' should return validation errors");
                }, $"Path '{path}' should not throw an exception");
            }
        }

        [Test]
        public void TestLeadingTrailingSlashes()
        {
            // Arrange
            List<string> slashPaths = new List<string>
            {
                "/Models/Test",
                "\\Models\\Test",
                "Models/Test/",
                "Models\\Test\\",
                "/Models/Test/",
                "\\Models\\Test\\"
            };

            // Act & Assert
            foreach (string path in slashPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (leading/trailing slashes)");
                Assert.IsTrue(errors.Any(e => e.Contains("slash")),
                    $"Path '{path}' should have slash-related error");
            }
        }

        [Test]
        public void TestPathLengthLimit()
        {
            // Arrange
            string longPath = "Models/" + new string('A', 200); // Exceeds 200 character limit

            // Act
            List<string> errors = PathUtils.ValidateRelativePath(longPath);

            // Assert
            Assert.IsNotEmpty(errors, "Long path should be invalid");
            Assert.IsTrue(errors.Any(e => e.Contains("too long")), "Should have length error");
        }

        [Test]
        public void TestIsMaterialsFolderPath()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Materials"));
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Models/Benne/Materials"));
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Models\\Benne\\Materials"));
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Materials/"));
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Models/Benne/Materials/"));
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("MATERIALS")); // Case insensitive
            Assert.IsTrue(PathUtils.IsMaterialsFolderPath("Models/Benne/materials")); // Case insensitive

            Assert.IsFalse(PathUtils.IsMaterialsFolderPath("Models/Benne"));
            Assert.IsFalse(PathUtils.IsMaterialsFolderPath("Models/Benne/Textures"));
            Assert.IsFalse(PathUtils.IsMaterialsFolderPath(""));
            Assert.IsFalse(PathUtils.IsMaterialsFolderPath(null));
            Assert.IsFalse(PathUtils.IsMaterialsFolderPath("Materials/Textures"));
        }

    }
}
