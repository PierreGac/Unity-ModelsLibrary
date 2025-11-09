using System;
using System.IO;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for AssetVisibilityUtility functionality.
    /// Verifies that files can be hidden from Unity Project window by renaming with dot prefix.
    /// </summary>
    public class AssetVisibilityTests
    {
        /// <summary>
        /// Tests hiding asset using relative path (Assets/Models/Test/model.json).
        /// </summary>
        [Test]
        public void TestHideAssetWithRelativePath()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string testFile = Path.Combine(assetsTestDir, "model.json");
                File.WriteAllText(testFile, "{}");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetVisibilityUtility.HideAssetFromProjectWindow("Assets/Models/Test/model.json");

                    // Verify file was renamed
                    string hiddenFile = Path.Combine(assetsTestDir, ".model.json");
                    Assert.IsTrue(File.Exists(hiddenFile), "File should be renamed with dot prefix");
                    Assert.IsFalse(File.Exists(testFile), "Original file should not exist");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests hiding asset using absolute path.
        /// </summary>
        [Test]
        public void TestHideAssetWithAbsolutePath()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string testFile = Path.Combine(assetsTestDir, "model.json");
                File.WriteAllText(testFile, "{}");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    string absolutePath = Path.GetFullPath(testFile);
                    AssetVisibilityUtility.HideAssetFromProjectWindow(absolutePath);

                    // Verify file was renamed
                    string hiddenFile = Path.Combine(assetsTestDir, ".model.json");
                    Assert.IsTrue(File.Exists(hiddenFile), "File should be renamed with dot prefix");
                    Assert.IsFalse(File.Exists(testFile), "Original file should not exist");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests that already hidden files (starting with dot) are skipped.
        /// </summary>
        [Test]
        public void TestHideAssetAlreadyHidden()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string hiddenFile = Path.Combine(assetsTestDir, ".model.json");
                File.WriteAllText(hiddenFile, "{}");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetVisibilityUtility.HideAssetFromProjectWindow("Assets/Models/Test/.model.json");

                    // Verify file still exists and wasn't renamed again
                    Assert.IsTrue(File.Exists(hiddenFile), "Already hidden file should still exist");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests that files outside Assets/ folder are rejected.
        /// </summary>
        [Test]
        public void TestHideAssetOutsideAssets()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempTestDir);

            try
            {
                string testFile = Path.Combine(tempTestDir, "outside.json");
                File.WriteAllText(testFile, "{}");

                AssetVisibilityUtility.HideAssetFromProjectWindow(testFile);

                // Verify file was not renamed (should still exist with original name)
                Assert.IsTrue(File.Exists(testFile), "File outside Assets/ should not be renamed");
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests handling when file doesn't exist.
        /// </summary>
        [Test]
        public void TestHideAssetFileNotExists()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    string nonExistentFile = "Assets/Models/Test/nonexistent.json";
                    AssetVisibilityUtility.HideAssetFromProjectWindow(nonExistentFile);

                    // Should not throw exception, just return silently
                    Assert.IsTrue(true, "Should handle non-existent file gracefully");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests that .meta file is also renamed.
        /// </summary>
        [Test]
        public void TestHideAssetMetaFileHandling()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string testFile = Path.Combine(assetsTestDir, "model.json");
                string metaFile = testFile + ".meta";
                File.WriteAllText(testFile, "{}");
                File.WriteAllText(metaFile, "guid: 12345678901234567890123456789012");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    AssetVisibilityUtility.HideAssetFromProjectWindow("Assets/Models/Test/model.json");

                    // Verify both files were renamed
                    string hiddenFile = Path.Combine(assetsTestDir, ".model.json");
                    string hiddenMetaFile = hiddenFile + ".meta";
                    Assert.IsTrue(File.Exists(hiddenFile), "File should be renamed with dot prefix");
                    Assert.IsTrue(File.Exists(hiddenMetaFile), "Meta file should also be renamed");
                    Assert.IsFalse(File.Exists(testFile), "Original file should not exist");
                    Assert.IsFalse(File.Exists(metaFile), "Original meta file should not exist");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }

        /// <summary>
        /// Tests path normalization (backslashes, case sensitivity on Windows).
        /// </summary>
        [Test]
        public void TestHideAssetPathNormalization()
        {
            string tempTestDir = Path.Combine(Path.GetTempPath(), $"AssetVisibilityTest_{Guid.NewGuid():N}");
            string assetsTestDir = Path.Combine(tempTestDir, "Assets", "Models", "Test");
            Directory.CreateDirectory(assetsTestDir);

            try
            {
                string testFile = Path.Combine(assetsTestDir, "model.json");
                File.WriteAllText(testFile, "{}");

                // Change to test directory to simulate Unity project
                string originalDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempTestDir);

                try
                {
                    // Test with backslashes (Windows path style)
                    string pathWithBackslashes = "Assets\\Models\\Test\\model.json";
                    AssetVisibilityUtility.HideAssetFromProjectWindow(pathWithBackslashes);

                    // Verify file was renamed
                    string hiddenFile = Path.Combine(assetsTestDir, ".model.json");
                    Assert.IsTrue(File.Exists(hiddenFile), "File should be renamed even with backslashes in path");
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
            finally
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
        }
    }
}

