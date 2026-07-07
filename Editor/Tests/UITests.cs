using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for UI-related functionality.
    /// This consolidated test class covers UI feedback, text display, and tag filtering.
    /// </summary>
    public class UITests
    {
        #region Path Validation UI Tests

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

            for (int i = 0; i < invalidPaths.Count; i++)
            {
                string path = invalidPaths[i];
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

            for (int i = 0; i < validPaths.Count; i++)
            {
                string path = validPaths[i];
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
        public void TestPathTraversalErrorMessages()
        {
            // Test specific error messages for path traversal issues
            List<string> traversalPaths = new List<string>
            {
                "Models/../Other",
                "Models/../../Assets",
                "~/Models/Test"
            };

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
        public void TestSlashErrorMessages()
        {
            // Test specific error messages for slash issues
            List<string> slashPaths = new List<string>
            {
                "/Models/Test",
                "Models/Test/",
                "/Models/Test/"
            };

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

            for (int i = 0; i < emptyPaths.Count; i++)
            {
                string path = emptyPaths[i];
                List<string> errors = PathUtils.ValidateRelativePath(path);
                Assert.IsNotEmpty(errors, $"Path '{path}' should be invalid (empty/null)");
                Assert.IsTrue(errors.Any(e => e.Contains("required") || e.Contains("empty")),
                    $"Path '{path}' should have empty/required error");
            }
        }

        [Test]
        public void TestInvalidCharacterHandling()
        {
            // Test invalid character handling
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();

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

        #endregion

        #region Multiline Text Display Tests
        // INFO (audit INFO-03): These tests were tautologies — they tested
        // .NET BCL string.Split behavior, not any production code. They have
        // been removed and replaced with real production-code tests in
        // PathSecurityTests.cs and FileExtensionsSecurityTests.cs.

        // [Test] public void TestMultilineTextRendering() — REMOVED (tautology)
        // [Test] public void TestEmptyMultilineText()      — REMOVED (tautology)
        // [Test] public void TestSingleLineText()          — REMOVED (tautology)
        // [Test] public void TestMultilineTextWithCarriageReturns() — REMOVED (tautology)

        #endregion

        #region Tag Filtering Tests
        // INFO (audit INFO-03): TestTagCaseInsensitiveFiltering and
        // TestTagExactMatchFiltering were tautologies — they tested
        // .NET string.Equals, not any production code. They have been
        // removed. Real tests for ModelSearchUtils can be added later
        // when that utility is unit-testable (currently depends on
        // EditorApplication state).

        // [Test] public void TestTagCaseInsensitiveFiltering() — REMOVED (tautology)
        // [Test] public void TestTagExactMatchFiltering()        — REMOVED (tautology)

        #endregion

        // INFO (audit INFO-03): The following tests were tautologies that
        // tested .NET BCL string methods (.Equals, .Contains, .StartsWith,
        // string.IsNullOrWhiteSpace) rather than any production code. They
        // have been removed and replaced with real production-code tests in
        // PathSecurityTests.cs and FileExtensionsSecurityTests.cs.
        //
        // Removed tests:
        // - TestTagExactMatchFiltering (tested string.Equals)
        // - TestTagContainsFiltering    (tested string.Contains)
        // - TestEmptyTagFiltering       (tested string.IsNullOrWhiteSpace)
        // - TestTagFilteringWithSpecialCharacters (tested string.StartsWith)
    }
}
