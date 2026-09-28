using System.IO;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for safe cache file writes.
    /// </summary>
    public class SafeFileWriterTests
    {
        [Test]
        public void WriteAllText_ReplacesReadOnlyFile()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SafeFileWriter_" + System.Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempRoot, "cache", ".model.json");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, "old");
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            try
            {
                SafeFileWriter.WriteAllText(filePath, "new");

                Assert.AreEqual("new", File.ReadAllText(filePath));
                Assert.IsFalse(File.Exists(filePath + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void WriteAllText_InterruptedBeforeReplace_KeepsPreviousFile()
        {
            const string PREVIOUS_CONTENT = "previous-valid";
            const string REPLACEMENT_CONTENT = "replacement";
            const string TEMP_SUFFIX = ".tmp";
            const string INJECTED_FAILURE_MESSAGE = "injected failure before replacement";

            string tempRoot = Path.Combine(Path.GetTempPath(), "SafeFileWriter_" + System.Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempRoot, "models_index.json");
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(filePath, PREVIOUS_CONTENT);

            try
            {
                SafeFileWriter.BeforeDestinationReplace = () =>
                {
                    string tempFilePath = filePath + TEMP_SUFFIX;
                    Assert.IsTrue(File.Exists(tempFilePath));
                    Assert.AreEqual(REPLACEMENT_CONTENT, File.ReadAllText(tempFilePath));
                    Assert.AreEqual(PREVIOUS_CONTENT, File.ReadAllText(filePath));
                    throw new IOException(INJECTED_FAILURE_MESSAGE);
                };

                IOException thrown = Assert.Throws<IOException>(() => SafeFileWriter.WriteAllText(filePath, REPLACEMENT_CONTENT));

                Assert.AreEqual(INJECTED_FAILURE_MESSAGE, thrown.Message);
                Assert.AreEqual(PREVIOUS_CONTENT, File.ReadAllText(filePath));
                Assert.IsFalse(File.Exists(filePath + TEMP_SUFFIX));
            }
            finally
            {
                SafeFileWriter.BeforeDestinationReplace = null;
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void DeleteDirectory_RemovesReadOnlyFiles()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SafeFileWriter_" + System.Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempRoot, "nested", ".model.json");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, "cached");
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            try
            {
                SafeFileWriter.DeleteDirectory(tempRoot);

                Assert.IsFalse(Directory.Exists(tempRoot));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
