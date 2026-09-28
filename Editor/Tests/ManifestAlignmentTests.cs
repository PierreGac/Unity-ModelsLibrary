using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Checks that the package manifest and install docs match the approved identity and version.
    /// </summary>
    public class ManifestAlignmentTests
    {
        private const string PACKAGE_NAME = "com.models-library";
        private const string PACKAGE_VERSION = "1.0.12";
        private const string UNITY_VERSION = "6000.2";
        private const string UNITY_RELEASE = "6f2";
        private const string LICENSE_ID = "MIT";
        private const string ROOT_GIT_URL = "https://github.com/PierreGac/Unity-ModelsLibrary.git";
        private const string RELEASE_TAG_FRAGMENT = "#v1.0.12";
        private const string SUBFOLDER_GIT_QUERY = "?path=Assets/ModelLibrary";
        private const string CHANGELOG_HEADING = "## [1.0.12]";
        private const string LICENSE_FILE_NAME = "LICENSE";
        private const string LICENSE_MARKDOWN_FILE_NAME = "LICENSE.md";

        /// <summary>
        /// The manifest names the retained package, canonical version, Unity release, and SPDX license.
        /// </summary>
        [Test]
        public void PackageManifest_MatchesApprovedIdentityAndVersion()
        {
            string packageRoot = GetPackageRoot();
            string manifest = File.ReadAllText(Path.Combine(packageRoot, "package.json"));

            Assert.IsTrue(manifest.Contains("\"name\": \"" + PACKAGE_NAME + "\""));
            Assert.IsTrue(manifest.Contains("\"version\": \"" + PACKAGE_VERSION + "\""));
            Assert.IsTrue(manifest.Contains("\"unity\": \"" + UNITY_VERSION + "\""));
            Assert.IsTrue(manifest.Contains("\"unityRelease\": \"" + UNITY_RELEASE + "\""));
            Assert.IsTrue(manifest.Contains("\"license\": \"" + LICENSE_ID + "\""));
            Assert.IsTrue(manifest.Contains("\"url\": \"" + ROOT_GIT_URL + "\""));
            Assert.IsFalse(manifest.Contains(SUBFOLDER_GIT_QUERY));
            Assert.IsTrue(File.Exists(Path.Combine(packageRoot, LICENSE_FILE_NAME)));
            Assert.IsFalse(File.Exists(Path.Combine(packageRoot, LICENSE_MARKDOWN_FILE_NAME)));
        }

        /// <summary>
        /// Install instructions use the repository-root Git URL and the canonical tag.
        /// </summary>
        [Test]
        public void InstallDocs_UseRootGitUrlAndCanonicalTag()
        {
            string packageRoot = GetPackageRoot();
            string readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
            string discussion = File.ReadAllText(Path.Combine(packageRoot, "unity-discussions-post.md"));
            string changelog = File.ReadAllText(Path.Combine(packageRoot, "CHANGELOG.md"));

            Assert.IsTrue(readme.Contains(ROOT_GIT_URL));
            Assert.IsTrue(readme.Contains(RELEASE_TAG_FRAGMENT));
            Assert.IsFalse(readme.Contains(SUBFOLDER_GIT_QUERY));
            Assert.IsTrue(discussion.Contains(ROOT_GIT_URL + RELEASE_TAG_FRAGMENT));
            Assert.IsFalse(discussion.Contains(SUBFOLDER_GIT_QUERY));
            Assert.IsTrue(changelog.Contains(CHANGELOG_HEADING));
            Assert.IsTrue(changelog.Contains("v" + PACKAGE_VERSION));
        }

        private static string GetPackageRoot()
        {
            return Path.Combine(Application.dataPath, "ModelLibrary");
        }
    }
}
