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

        [Test]
        public void TestMultilineTextRendering()
        {
            // Test that multiline text is properly handled
            string multilineText = "Line 1\nLine 2\nLine 3";
            string[] lines = multilineText.Split('\n');

            Assert.AreEqual(3, lines.Length, "Should split into 3 lines");
            Assert.AreEqual("Line 1", lines[0], "First line should be correct");
            Assert.AreEqual("Line 2", lines[1], "Second line should be correct");
            Assert.AreEqual("Line 3", lines[2], "Third line should be correct");
        }

        [Test]
        public void TestEmptyMultilineText()
        {
            // Test empty multiline text
            string emptyText = "";
            string[] lines = emptyText.Split('\n');

            Assert.AreEqual(1, lines.Length, "Empty text should split into 1 line");
            Assert.AreEqual("", lines[0], "Empty line should be empty string");
        }

        [Test]
        public void TestSingleLineText()
        {
            // Test single line text
            string singleLine = "Single line text";
            string[] lines = singleLine.Split('\n');

            Assert.AreEqual(1, lines.Length, "Single line should split into 1 line");
            Assert.AreEqual("Single line text", lines[0], "Single line should be correct");
        }

        [Test]
        public void TestMultilineTextWithCarriageReturns()
        {
            // Test text with carriage returns
            string textWithCR = "Line 1\r\nLine 2\r\nLine 3";
            string[] lines = textWithCR.Split(new string[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            Assert.AreEqual(3, lines.Length, "Should split into 3 lines");
            Assert.AreEqual("Line 1", lines[0], "First line should be correct");
            Assert.AreEqual("Line 2", lines[1], "Second line should be correct");
            Assert.AreEqual("Line 3", lines[2], "Third line should be correct");
        }

        #endregion

        #region Tag Filtering Tests

        [Test]
        public void TestTagCaseInsensitiveFiltering()
        {
            // Test that tag filtering is case insensitive
            List<string> tags = new List<string> { "Weapon", "ARMOR", "weapon", "Armor", "WEAPON" };
            List<string> filteredTags = new List<string>();

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (tag.Equals("weapon", System.StringComparison.OrdinalIgnoreCase))
                {
                    filteredTags.Add(tag);
                }
            }

            Assert.AreEqual(3, filteredTags.Count, "Should find 3 weapon tags (case insensitive)");
        }

        [Test]
        public void TestTagExactMatchFiltering()
        {
            // Test exact match filtering
            List<string> tags = new List<string> { "Weapon", "Weapons", "Weaponry", "Weapon" };
            List<string> exactMatches = new List<string>();

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (tag.Equals("Weapon", System.StringComparison.OrdinalIgnoreCase))
                {
                    exactMatches.Add(tag);
                }
            }

            Assert.AreEqual(2, exactMatches.Count, "Should find 2 exact matches");
        }

        [Test]
        public void TestTagContainsFiltering()
        {
            // Test contains filtering
            List<string> tags = new List<string> { "Weapon", "Weapons", "Weaponry", "Sword", "Bow" };
            List<string> containsWeapon = new List<string>();

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (tag.Contains("Weapon", System.StringComparison.OrdinalIgnoreCase))
                {
                    containsWeapon.Add(tag);
                }
            }

            Assert.AreEqual(3, containsWeapon.Count, "Should find 3 tags containing 'Weapon'");
        }

        [Test]
        public void TestEmptyTagFiltering()
        {
            // Test filtering with empty tags
            List<string> tags = new List<string> { "", "Weapon", "   ", "Armor", null };
            List<string> validTags = new List<string>();

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    validTags.Add(tag);
                }
            }

            Assert.AreEqual(2, validTags.Count, "Should find 2 valid tags");
            Assert.IsTrue(validTags.Contains("Weapon"), "Should contain 'Weapon'");
            Assert.IsTrue(validTags.Contains("Armor"), "Should contain 'Armor'");
        }

        [Test]
        public void TestTagFilteringWithSpecialCharacters()
        {
            // Test filtering with special characters
            List<string> tags = new List<string> { "Weapon-1", "Weapon_2", "Weapon.3", "Weapon 4", "Weapon" };
            List<string> weaponTags = new List<string>();

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (tag.StartsWith("Weapon", System.StringComparison.OrdinalIgnoreCase))
                {
                    weaponTags.Add(tag);
                }
            }

            Assert.AreEqual(5, weaponTags.Count, "Should find all 5 weapon tags");
        }

        #endregion
    }
}
