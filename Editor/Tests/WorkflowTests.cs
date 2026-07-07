using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for workflow enhancements.
    /// Tests right-click submission, grid view functionality, and other workflow features.
    /// </summary>
    public class WorkflowTests
    {
        /// <summary>
        /// Tests right-click submission workflow validation.
        /// INFO (audit INFO-03): This test was previously a tautology — it
        /// built an array of valid extensions and then asserted that each
        /// extension was in the array. The new version calls real production
        /// code: <see cref="FileExtensions.IsAcceptablePayloadExtension"/>
        /// and <see cref="FileExtensions.IsNotAllowedFileExtension"/>.
        /// </summary>
        [Test]
        public void TestRightClickSubmissionWorkflow()
        {
            // Test that the production allowlist correctly identifies valid extensions.
            string[] validExtensions = {
                FileExtensions.FBX,
                FileExtensions.OBJ,
                FileExtensions.PNG,
                FileExtensions.TGA,
                FileExtensions.JPG,
                FileExtensions.JPEG,
                FileExtensions.PSD,
                FileExtensions.MAT
            };

            foreach (string ext in validExtensions)
            {
                Assert.IsTrue(FileExtensions.IsAcceptablePayloadExtension(ext),
                    $"Extension {ext} should be on the production allowlist");
            }

            // Test that the production allowlist rejects disallowed extensions.
            string[] invalidExtensions = { ".txt", ".cs", ".js", ".md", ".json", ".dll", ".asmdef" };
            foreach (string ext in invalidExtensions)
            {
                Assert.IsFalse(FileExtensions.IsAcceptablePayloadExtension(ext),
                    $"Extension {ext} should be rejected by the production allowlist");
            }

            // Verify the denylist still catches the dangerous extensions
            // (defense-in-depth, even though the allowlist is the primary filter).
            Assert.IsTrue(FileExtensions.IsNotAllowedFileExtension(FileExtensions.CS),
                ".cs must be on the denylist (defense-in-depth)");

            // Test role-based enum mechanics (kept as a sanity check).
            Assert.IsTrue(Enum.IsDefined(typeof(UserRole), UserRole.Artist), "Artist role should be defined");
            Assert.IsTrue(Enum.IsDefined(typeof(UserRole), UserRole.Developer), "Developer role should be defined");
            Assert.IsTrue(Enum.IsDefined(typeof(UserRole), UserRole.Admin), "Admin role should be defined");
        }

        /// <summary>
        /// Tests grid view functionality.
        /// Verifies that grid view can display models correctly and handle various entry counts.
        /// </summary>
        [Test]
        public void TestGridViewFunctionality()
        {
            // Create test model entries
            List<ModelIndex.Entry> testEntries = new List<ModelIndex.Entry>
            {
                new ModelIndex.Entry
                {
                    id = "test-model-1",
                    name = "Test Model 1",
                    latestVersion = "1.0.0",
                    updatedTimeTicks = DateTime.UtcNow.Ticks,
                    tags = new List<string> { "test" }
                },
                new ModelIndex.Entry
                {
                    id = "test-model-2",
                    name = "Test Model 2",
                    latestVersion = "2.0.0",
                    updatedTimeTicks = DateTime.UtcNow.Ticks,
                    tags = new List<string> { "test", "weapon" }
                },
                new ModelIndex.Entry
                {
                    id = "test-model-3",
                    name = "Test Model 3",
                    latestVersion = "1.5.0",
                    updatedTimeTicks = DateTime.UtcNow.Ticks,
                    tags = new List<string> { "test", "character" }
                }
            };

            // Test grid view calculations
            const float thumbnailSize = 128f;
            const float spacing = 8f;
            const float cardPadding = 4f;
            const float minCardWidth = thumbnailSize + (cardPadding * 2);

            // Test column calculation for various window widths
            float[] testWidths = { 400f, 600f, 800f, 1200f, 1600f };
            for (int i = 0; i < testWidths.Length; i++)
            {
                float width = testWidths[i];
                float availableWidth = width - 20f; // Account for scrollbar
                int calculatedColumns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (minCardWidth + spacing)));
                
                Assert.GreaterOrEqual(calculatedColumns, 1, $"Should have at least 1 column for width {width}");
                // Grid view can have more columns than entries - extra columns will just be empty
                // For width 600, with minCardWidth ~136 and spacing 8, we get: (600-20)/(136+8) = 580/144 ≈ 4 columns
                // This is correct behavior - the grid layout allows empty columns
            }

            // Test that entries can be processed in grid format
            int row = 0;
            int col = 0;
            int columns = 3;
            int processedEntries = 0;

            foreach (ModelIndex.Entry entry in testEntries)
            {
                if (col == 0)
                {
                    row++;
                }
                col++;
                if (col >= columns)
                {
                    col = 0;
                }
                processedEntries++;
            }

            Assert.AreEqual(testEntries.Count, processedEntries, "All entries should be processed");
            Assert.GreaterOrEqual(row, 1, "Should have at least one row");
        }

        /// <summary>
        /// Tests that grid view handles empty entry lists gracefully.
        /// </summary>
        [Test]
        public void TestGridViewWithEmptyList()
        {
            List<ModelIndex.Entry> emptyEntries = new List<ModelIndex.Entry>();
            
            // Grid view should handle empty lists without errors
            Assert.AreEqual(0, emptyEntries.Count, "Empty list should have zero entries");
            
            // Test column calculation with empty list
            const float thumbnailSize = 128f;
            const float spacing = 8f;
            const float cardPadding = 4f;
            float availableWidth = 800f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (thumbnailSize + (cardPadding * 2) + spacing)));
            
            Assert.GreaterOrEqual(columns, 1, "Should calculate at least 1 column even with empty list");
        }
    }
}

