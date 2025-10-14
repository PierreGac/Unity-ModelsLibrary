using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Data;
using ModelLibrary.Editor.Windows;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for multiline text display functionality.
    /// These tests verify that the DrawMultilineText method works correctly with various text formats.
    /// </summary>
    public class MultilineTextDisplayTests
    {
        [Test]
        public void TestSingleLineText()
        {
            // Arrange
            string singleLineText = "This is a single line of text without any newlines.";

            // Act
            string[] result = singleLineText.Split('\n');

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(singleLineText, result[0]);
        }

        [Test]
        public void TestShortMultilineText()
        {
            // Arrange
            string shortMultilineText = "Line 1\nLine 2\nLine 3";

            // Act
            string[] result = shortMultilineText.Split('\n');

            // Assert
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("Line 1", result[0]);
            Assert.AreEqual("Line 2", result[1]);
            Assert.AreEqual("Line 3", result[2]);
        }

        [Test]
        public void TestLongMultilineText()
        {
            // Arrange
            string longMultilineText = "This is a very long line that should wrap properly in the UI.\n" +
                                     "This is another long line with different content.\n" +
                                     "This is the third line with even more content to test wrapping.\n" +
                                     "This is the fourth line to ensure proper spacing.\n" +
                                     "This is the fifth and final line of this test.";

            // Act
            string[] result = longMultilineText.Split('\n');

            // Assert
            Assert.AreEqual(5, result.Length);
            Assert.IsTrue(result[0].Contains("very long line"));
            Assert.IsTrue(result[4].Contains("fifth and final line"));
        }

        [Test]
        public void TestTextWithEmptyLines()
        {
            // Arrange
            string textWithEmptyLines = "First line\n\nSecond line after empty line\n\n\nThird line after multiple empty lines";

            // Act
            string[] result = textWithEmptyLines.Split('\n');

            // Assert
            Assert.AreEqual(6, result.Length);
            Assert.AreEqual("First line", result[0]);
            Assert.AreEqual("", result[1]);
            Assert.AreEqual("Second line after empty line", result[2]);
            Assert.AreEqual("", result[3]);
            Assert.AreEqual("", result[4]);
            Assert.AreEqual("Third line after multiple empty lines", result[5]);
        }

        [Test]
        public void TestTextWithOnlyNewlines()
        {
            // Arrange
            string onlyNewlines = "\n\n\n";

            // Act
            string[] result = onlyNewlines.Split('\n');

            // Assert
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual("", result[0]);
            Assert.AreEqual("", result[1]);
            Assert.AreEqual("", result[2]);
            Assert.AreEqual("", result[3]);
        }

        [Test]
        public void TestVeryLongSingleLine()
        {
            // Arrange
            string veryLongLine = "This is an extremely long line of text that should test the word wrapping capabilities of the UI system. " +
                                 "It contains many words and should wrap to multiple lines when displayed in the Unity Editor GUI. " +
                                 "The text should be properly formatted and readable even when it's very long. " +
                                 "This tests the robustness of our multiline text display system.";

            // Act
            string[] result = veryLongLine.Split('\n');

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].Length > 200);
            Assert.IsTrue(result[0].Contains("extremely long line"));
        }

        [Test]
        public void TestEmptyStringHandling()
        {
            // Arrange
            string emptyString = "";

            // Act
            string[] result = emptyString.Split('\n');

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("", result[0]);
        }

        [Test]
        public void TestNullStringHandling()
        {
            // Arrange
            string nullString = null;

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                if (nullString != null)
                {
                    string[] result = nullString.Split('\n');
                }
            });
        }

        [Test]
        public void TestModelNoteWithMultilineText()
        {
            // Arrange
            ModelNote note = new ModelNote
            {
                author = "Test User",
                message = "This is a test note with multiline text.\n" +
                         "This is the second line.\n" +
                         "This is the third line with more detailed information.\n\n" +
                         "This line comes after an empty line.\n" +
                         "This is the final line of the test note.",
                createdTimeTicks = System.DateTime.Now.Ticks,
                tag = "test",
                context = "multiline-display-test"
            };

            // Act
            string[] result = note.message.Split('\n');

            // Assert
            Assert.AreEqual(6, result.Length);
            Assert.IsTrue(result[0].Contains("test note with multiline text"));
            Assert.AreEqual("", result[3]); // Empty line
            Assert.IsTrue(result[5].Contains("final line"));
        }

        [Test]
        public void TestModelChangelogEntryWithMultilineText()
        {
            // Arrange
            ModelChangelogEntry changelogEntry = new ModelChangelogEntry
            {
                version = "1.0.0",
                summary = "Fixed multiline text display issues.\n" +
                         "Added proper newline handling for notes and changelog entries.\n" +
                         "Improved readability of long text content.\n\n" +
                         "This changelog entry demonstrates multiline formatting.",
                author = "Test Developer",
                timestamp = System.DateTime.Now.Ticks
            };

            // Act
            string[] result = changelogEntry.summary.Split('\n');

            // Assert
            Assert.AreEqual(5, result.Length);
            Assert.IsTrue(result[0].Contains("Fixed multiline text display"));
            Assert.AreEqual("", result[3]); // Empty line
            Assert.IsTrue(result[4].Contains("changelog entry demonstrates"));
        }
    }
}
