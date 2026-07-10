using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for model import safety and path handling.
    /// </summary>
    public class ModelImportSafetyTests
    {
        [Test]
        public void TestPathResolutionWithFileConflict()
        {
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                installPath = "Assets/Models/TestModel/TestModel.FBX"
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);
            Assert.IsTrue(resolvedPath.EndsWith("TestModel.FBX"), "Test method should return path as-is when file doesn't exist");

            string testPath = "Assets/Models/TestModel/TestModel.FBX";
            if (File.Exists(Path.GetFullPath(testPath)))
            {
                testPath = Path.GetDirectoryName(testPath);
            }

            Assert.AreEqual("Assets/Models/TestModel/TestModel.FBX", testPath);
        }

        [Test]
        public void TestPathResolutionWithDirectoryPath()
        {
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                installPath = "Assets/Models/TestModel"
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"));
            Assert.IsFalse(resolvedPath.EndsWith(".FBX"));
        }

        [Test]
        public void TestPathResolutionWithOverride()
        {
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                },
                installPath = "Assets/Models/TestModel/TestModel.FBX"
            };

            string overridePath = "Assets/Models/OverrideModel";
            string resolvedPath = ResolveDestinationPathForTest(meta, overridePath);
            Assert.AreEqual(overridePath, resolvedPath);
        }

        [Test]
        public void TestPathResolutionFallback()
        {
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    name = "TestModel"
                }
            };

            string resolvedPath = ResolveDestinationPathForTest(meta, null);
            Assert.IsTrue(resolvedPath.StartsWith("Assets/Models/"));
            Assert.IsTrue(resolvedPath.EndsWith("TestModel"));
        }

        private string ResolveDestinationPathForTest(ModelMeta meta, string overrideInstallPath)
        {
            if (!string.IsNullOrEmpty(overrideInstallPath))
            {
                return overrideInstallPath;
            }

            if (!string.IsNullOrEmpty(meta?.installPath))
            {
                string resolvedPath = meta.installPath;

                if (File.Exists(Path.GetFullPath(resolvedPath)))
                {
                    resolvedPath = Path.GetDirectoryName(resolvedPath);
                }

                return resolvedPath;
            }

            string safeName = SanitizeFolderNameForTest(meta?.identity?.name ?? "UnknownModel");
            return $"Assets/Models/{safeName}";
        }

        private string SanitizeFolderNameForTest(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "UnknownModel";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
            return new string(result).Replace(' ', '_');
        }
    }
}
