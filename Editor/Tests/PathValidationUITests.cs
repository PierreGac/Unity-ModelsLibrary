using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for path validation UI feedback functionality.
    /// These tests verify that the UI feedback system works correctly with various path inputs.
    /// </summary>
    public class PathValidationUITests
    {
        [Test]
        public void TestPathValidationErrorDetection()
        {
            // Test that various invalid paths are correctly detected
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

            foreach (string path in invalidPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
            }
        }

        [Test]
        public void TestPathValidationSuccessDetection()
        {
            // Test that valid paths are correctly identified
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

            foreach (string path in validPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsEmpty(errors, $"Path '{path}' should be valid but got errors: {string.Join(", ", errors)}");
            }
        }

        [Test]
        public void TestMaterialsFolderErrorMessages()
        {
            // Test specific error messages for Materials folder issues
            List<string> materialsPaths = new List<string>
            {
                "Models/Benne/Materials",
                "Materials",
                "Assets/Models/Test/Materials"
            };

            foreach (string path in materialsPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("Materials")),
                    $"Path '{path}' should have Materials-related error");
            }
        }

        [Test]
        public void TestPathTraversalErrorMessages()
        {
            // Test specific error messages for path traversal issues
            List<string> traversalPaths = new List<string>
            {
                "Models/../Other",
                "Models/../../Assets",
                "~/Models/Test"
            };

            foreach (string path in traversalPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("path traversal")),
                    $"Path '{path}' should have path traversal error");
            }
        }

        [Test]
        public void TestReservedFolderErrorMessages()
        {
            // Test specific error messages for reserved folder issues
            List<string> reservedPaths = new List<string>
            {
                "Editor/Models",
                "Resources/Models",
                "StreamingAssets/Models",
                "Plugins/Models"
            };

            foreach (string path in reservedPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("reserved folder")),
                    $"Path '{path}' should have reserved folder error");
            }
        }

        [Test]
        public void TestSlashErrorMessages()
        {
            // Test specific error messages for slash issues
            List<string> slashPaths = new List<string>
            {
                "/Models/Test",
                "Models/Test/",
                "/Models/Test/"
            };

            foreach (string path in slashPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("slash")),
                    $"Path '{path}' should have slash-related error");
            }
        }

        [Test]
        public void TestMultipleErrorHandling()
        {
            // Test that multiple errors are handled correctly
            string multiErrorPath = "/Models/Benne/Materials/"; // Has both slash and Materials errors

            List<string> errors = PathUtils.ValidateRelativePath(multiErrorPath);
            Assert.IsNotEmpty(errors, "Path with multiple errors should be invalid");
            Assert.IsTrue(errors.Count > 1, "Should have multiple validation errors");
        }

        [Test]
        public void TestEmptyAndNullPathHandling()
        {
            // Test that empty and null paths are handled gracefully
            List<string> emptyPaths = new List<string> { null, "", "   ", "\t" };

            foreach (string path in emptyPaths)
            {
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (empty/null)");
                Assert.IsTrue(errors.Any(e => e.Contains("required") || e.Contains("empty")),
                    $"Path '{path}' should have empty/required error");
            }
        }

        [Test]
        public void TestPathLengthValidation()
        {
            // Test path length validation
            string longPath = "Models/" + new string('A', 250); // Exceeds 200 character limit

            List<string> errors = PathUtils.ValidateRelativePath(longPath);
            Assert.IsNotEmpty(errors, "Long path should be invalid");
            Assert.IsTrue(errors.Any(e => e.Contains("too long")), "Should have length error");
        }

        [Test]
        public void TestInvalidCharacterHandling()
        {
            // Test invalid character handling
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();

            foreach (char invalidChar in invalidChars)
            {
                string testPath = $"Models{invalidChar}Test";
                List<string> errors = PathUtils.ValidateRelativePath(testPath);
                Assert.IsNotEmpty(errors, $"Path with invalid character '{invalidChar}' should be invalid");
                Assert.IsTrue(errors.Any(e => e.Contains("invalid character")),
                    $"Path with invalid character '{invalidChar}' should have invalid character error");
            }
        }
    }
}
