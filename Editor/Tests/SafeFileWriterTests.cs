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
