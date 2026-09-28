using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Checks imported-asset .meta pairing and the root documentation folder.
    /// </summary>
    public class ImportedAssetMetaTests
    {
        private const string DOCUMENTATION_FOLDER_NAME = "Documentation~";
        private const string BROWSER_IMAGE_NAME = "browser.jpg";
        private const string DETAILS_IMAGE_NAME = "existing_model_details.jpg";
        private const string SUBMIT_IMAGE_NAME = "submit_01.jpg";
        private const string META_EXTENSION = ".meta";
        private const string TILDE_SUFFIX = "~";
        private const char DOT_PREFIX = '.';

        /// <summary>
        /// Every imported file and folder has a .meta pair, and no .meta file sits inside a tilde folder.
        /// </summary>
        [Test]
        public void ImportedAssets_HaveMetaPairs_AndTildeFoldersHaveNone()
        {
            string packageRoot = GetPackageRoot();
            List<string> problems = new List<string>();
            CollectPairingProblems(packageRoot, packageRoot, problems);
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));

            string documentationRoot = Path.Combine(packageRoot, DOCUMENTATION_FOLDER_NAME);
            Assert.IsTrue(Directory.Exists(documentationRoot));
            Assert.IsFalse(Directory.Exists(Path.Combine(packageRoot, "Editor", DOCUMENTATION_FOLDER_NAME)));
            Assert.IsTrue(File.Exists(Path.Combine(documentationRoot, BROWSER_IMAGE_NAME)));
            Assert.IsTrue(File.Exists(Path.Combine(documentationRoot, DETAILS_IMAGE_NAME)));
            Assert.IsTrue(File.Exists(Path.Combine(documentationRoot, SUBMIT_IMAGE_NAME)));
            AssertNoMetaFiles(documentationRoot, problems);
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        private static void CollectPairingProblems(string packageRoot, string directory, List<string> problems)
        {
            string[] childDirectories = Directory.GetDirectories(directory);
            for (int i = 0; i < childDirectories.Length; i++)
            {
                string childDirectory = childDirectories[i];
                string name = Path.GetFileName(childDirectory);
                if (IsIgnoredByUnity(name))
                {
                    AssertNoMetaFiles(childDirectory, problems);
                    continue;
                }

                string relativeDirectory = GetRelativePath(packageRoot, childDirectory);
                if (!File.Exists(childDirectory + META_EXTENSION))
                {
                    problems.Add("Missing folder meta: " + relativeDirectory);
                }

                CollectPairingProblems(packageRoot, childDirectory, problems);
            }

            string[] childFiles = Directory.GetFiles(directory);
            for (int i = 0; i < childFiles.Length; i++)
            {
                string childFile = childFiles[i];
                string name = Path.GetFileName(childFile);
                if (IsIgnoredByUnity(name))
                {
                    continue;
                }

                string relativeFile = GetRelativePath(packageRoot, childFile);
                if (name.EndsWith(META_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    string assetPath = childFile.Substring(0, childFile.Length - META_EXTENSION.Length);
                    if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
                    {
                        problems.Add("Orphan meta: " + relativeFile);
                    }
                }
                else if (!File.Exists(childFile + META_EXTENSION))
                {
                    problems.Add("Missing meta: " + relativeFile);
                }
            }
        }

        private static void AssertNoMetaFiles(string directory, List<string> problems)
        {
            string[] metaFiles = Directory.GetFiles(directory, "*" + META_EXTENSION, SearchOption.AllDirectories);
            for (int i = 0; i < metaFiles.Length; i++)
            {
                problems.Add("Meta inside ignored folder: " + metaFiles[i]);
            }
        }

        private static bool IsIgnoredByUnity(string name)
        {
            return name.EndsWith(TILDE_SUFFIX, StringComparison.Ordinal) || name.StartsWith(DOT_PREFIX.ToString(), StringComparison.Ordinal);
        }

        private static string GetRelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(fullRoot.Length);
            }

            return fullPath;
        }

        private static string GetPackageRoot()
        {
            return Path.Combine(Application.dataPath, "ModelLibrary");
        }
    }
}
