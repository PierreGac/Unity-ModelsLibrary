using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for tag filtering functionality with mixed case inputs.
    /// These tests verify that tag comparisons work correctly regardless of case.
    /// </summary>
    public class TagFilteringTests
    {
        [Test]
        public void TestCaseInsensitiveTagMatching()
        {
            // Arrange
            List<string> modelTags = new List<string> { "Weapon", "MEDIEVAL", "Low-Poly", "sci-fi" };
            List<string> selectedTags = new List<string> { "weapon", "medieval", "LOW-POLY", "SCI-FI" };

            // Act & Assert
            foreach (string selectedTag in selectedTags)
            {
                bool match = modelTags.Any(t => string.Equals(t, selectedTag, System.StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(match, $"Tag '{selectedTag}' should match a model tag");
            }
        }

        [Test]
        public void TestMixedCaseTagFiltering()
        {
            // Arrange
            List<TestModelEntry> entries = new List<TestModelEntry>
            {
                new TestModelEntry { name = "Sword", tags = new List<string> { "Weapon", "Medieval", "Steel" } },
                new TestModelEntry { name = "Shield", tags = new List<string> { "WEAPON", "MEDIEVAL", "Wood" } },
                new TestModelEntry { name = "Bow", tags = new List<string> { "weapon", "medieval", "Wood" } },
                new TestModelEntry { name = "Spaceship", tags = new List<string> { "Vehicle", "Sci-Fi", "Metal" } }
            };

            // Test filtering with mixed case search terms
            List<string> searchTerms = new List<string> { "weapon", "MEDIEVAL" };

            // Act
            List<TestModelEntry> filteredEntries = FilterEntriesByTags(entries, searchTerms);

            // Assert
            Assert.AreEqual(3, filteredEntries.Count); // Should match Sword, Shield, and Bow
            Assert.IsTrue(filteredEntries.Any(e => e.name == "Sword"));
            Assert.IsTrue(filteredEntries.Any(e => e.name == "Shield"));
            Assert.IsTrue(filteredEntries.Any(e => e.name == "Bow"));
            Assert.IsFalse(filteredEntries.Any(e => e.name == "Spaceship"));
        }

        [Test]
        public void TestEmptyAndNullTagHandling()
        {
            // Arrange
            List<TestModelEntry> entries = new List<TestModelEntry>
            {
                new TestModelEntry { name = "Model1", tags = new List<string> { "tag1", "", "tag2" } },
                new TestModelEntry { name = "Model2", tags = new List<string> { "tag1", null, "tag2" } },
                new TestModelEntry { name = "Model3", tags = null }
            };

            // Test that empty and null tags don't cause errors
            List<string> searchTerms = new List<string> { "tag1" };

            // Act
            List<TestModelEntry> filteredEntries = FilterEntriesByTags(entries, searchTerms);

            // Assert
            Assert.AreEqual(2, filteredEntries.Count); // Should match Model1 and Model2
            Assert.IsTrue(filteredEntries.Any(e => e.name == "Model1"));
            Assert.IsTrue(filteredEntries.Any(e => e.name == "Model2"));
            Assert.IsFalse(filteredEntries.Any(e => e.name == "Model3"));
        }

        [Test]
        public void TestSpecialCharactersInTags()
        {
            // Arrange
            List<string> modelTags = new List<string> { "Low-Poly", "Sci-Fi", "3D-Model", "Text_With_Underscores" };
            List<string> searchTerms = new List<string> { "low-poly", "sci-fi", "3d-model", "text_with_underscores" };

            // Act & Assert
            foreach (string searchTerm in searchTerms)
            {
                bool match = modelTags.Any(t => string.Equals(t, searchTerm, System.StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(match, $"Special character tag '{searchTerm}' should match");
            }
        }

        [Test]
        public void TestWhitespaceHandlingInTags()
        {
            // Arrange
            List<string> modelTags = new List<string> { "  Weapon  ", "Medieval ", " Low-Poly" };
            List<string> searchTerms = new List<string> { "Weapon", "Medieval", "Low-Poly" };

            // Act & Assert
            foreach (string searchTerm in searchTerms)
            {
                bool match = modelTags.Any(t => string.Equals(t.Trim(), searchTerm, System.StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(match, $"Whitespace handling tag '{searchTerm}' should match");
            }
        }

        [Test]
        public void TestNoMatchingTags()
        {
            // Arrange
            List<TestModelEntry> entries = new List<TestModelEntry>
            {
                new TestModelEntry { name = "Model1", tags = new List<string> { "tag1", "tag2" } },
                new TestModelEntry { name = "Model2", tags = new List<string> { "tag3", "tag4" } }
            };

            List<string> searchTerms = new List<string> { "nonexistent" };

            // Act
            List<TestModelEntry> filteredEntries = FilterEntriesByTags(entries, searchTerms);

            // Assert
            Assert.AreEqual(0, filteredEntries.Count);
        }

        [Test]
        public void TestPartialTagMatching()
        {
            // Arrange
            List<TestModelEntry> entries = new List<TestModelEntry>
            {
                new TestModelEntry { name = "Model1", tags = new List<string> { "weapon", "medieval" } },
                new TestModelEntry { name = "Model2", tags = new List<string> { "weapon", "sci-fi" } },
                new TestModelEntry { name = "Model3", tags = new List<string> { "medieval", "sci-fi" } }
            };

            List<string> searchTerms = new List<string> { "weapon", "medieval" };

            // Act
            List<TestModelEntry> filteredEntries = FilterEntriesByTags(entries, searchTerms);

            // Assert
            Assert.AreEqual(1, filteredEntries.Count);
            Assert.AreEqual("Model1", filteredEntries[0].name);
        }

        [Test]
        public void TestCaseSensitiveVsInsensitive()
        {
            // Arrange
            List<string> modelTags = new List<string> { "Weapon", "weapon", "WEAPON" };
            string searchTerm = "weapon";

            // Act
            List<bool> caseInsensitiveMatches = modelTags.Select(t =>
                string.Equals(t, searchTerm, System.StringComparison.OrdinalIgnoreCase)).ToList();
            List<bool> caseSensitiveMatches = modelTags.Select(t =>
                string.Equals(t, searchTerm, System.StringComparison.Ordinal)).ToList();

            // Assert
            Assert.IsTrue(caseInsensitiveMatches.All(m => m), "All tags should match with case-insensitive comparison");
            Assert.IsFalse(caseSensitiveMatches.All(m => m), "Not all tags should match with case-sensitive comparison");
            Assert.IsTrue(caseSensitiveMatches[1], "Only the exact case match should be true");
        }

        /// <summary>
        /// Simulates the tag filtering logic used in ModelLibraryWindow.cs
        /// </summary>
        private static List<TestModelEntry> FilterEntriesByTags(List<TestModelEntry> entries, List<string> selectedTags)
        {
            List<TestModelEntry> filteredEntries = new List<TestModelEntry>();

            foreach (TestModelEntry entry in entries)
            {
                if (entry.tags == null || entry.tags.Count == 0)
                {
                    continue;
                }

                bool hasAllTags = true;
                foreach (string selectedTag in selectedTags)
                {
                    bool match = entry.tags.Any(t => !string.IsNullOrEmpty(t) &&
                        string.Equals(t, selectedTag, System.StringComparison.OrdinalIgnoreCase));
                    if (!match)
                    {
                        hasAllTags = false;
                        break;
                    }
                }

                if (hasAllTags)
                {
                    filteredEntries.Add(entry);
                }
            }

            return filteredEntries;
        }

        /// <summary>
        /// Test model entry for filtering tests
        /// </summary>
        private class TestModelEntry
        {
            public string name { get; set; }
            public List<string> tags { get; set; }
        }
    }
}
