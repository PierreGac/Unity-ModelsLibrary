using System;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for local version detection and update status logic.
    /// </summary>
    public class VersionDetectionTests
    {
        [Test]
        public void TestNeedsUpgradeWithUnknownVersion()
        {
            // Test that unknown versions don't trigger update notifications
            string localVersion = "(unknown)";
            string remoteVersion = "1.2.0";

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Unknown local version should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithNullVersion()
        {
            // Test that null local versions don't trigger update notifications
            string localVersion = null;
            string remoteVersion = "1.2.0";

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Null local version should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithEmptyVersion()
        {
            // Test that empty local versions don't trigger update notifications
            string localVersion = "";
            string remoteVersion = "1.2.0";

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Empty local version should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithNullRemoteVersion()
        {
            // Test that null remote versions don't trigger update notifications
            string localVersion = "1.0.0";
            string remoteVersion = null;

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Null remote version should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithValidVersions()
        {
            // Test valid version comparisons
            Assert.IsTrue(NeedsUpgrade("1.0.0", "1.0.1"), "Patch version should trigger upgrade");
            Assert.IsTrue(NeedsUpgrade("1.0.0", "1.1.0"), "Minor version should trigger upgrade");
            Assert.IsTrue(NeedsUpgrade("1.0.0", "2.0.0"), "Major version should trigger upgrade");

            Assert.IsFalse(NeedsUpgrade("1.0.1", "1.0.0"), "Newer version should not trigger upgrade");
            Assert.IsFalse(NeedsUpgrade("1.0.0", "1.0.0"), "Same version should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithSameVersions()
        {
            // Test that identical versions don't trigger upgrade
            string localVersion = "1.2.3";
            string remoteVersion = "1.2.3";

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Identical versions should not trigger upgrade");
        }

        [Test]
        public void TestNeedsUpgradeWithOlderRemoteVersion()
        {
            // Test that older remote versions don't trigger upgrade
            string localVersion = "1.2.3";
            string remoteVersion = "1.2.2";

            bool needsUpgrade = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(needsUpgrade, "Older remote version should not trigger upgrade");
        }

        [Test]
        public void TestVersionDetectionLogic()
        {
            // Test the core version detection logic
            string[] testCases = {
                "1.0.0", "1.0.1", "1.1.0", "2.0.0",
                "0.0.1", "10.20.30", "999.999.999"
            };

            foreach (string version in testCases)
            {
                bool canParse = SemVer.TryParse(version, out SemVer parsed);
                Assert.IsTrue(canParse, $"Should be able to parse version: {version}");
                Assert.AreEqual(version, parsed.ToString(), "Parsed version should match original");
            }
        }

        [Test]
        public void TestUpdateStatusWithValidVersion()
        {
            // Test update status detection with valid versions
            string localVersion = "1.0.0";
            string remoteVersion = "1.1.0";

            bool hasUpdate = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsTrue(hasUpdate, "Should detect update when remote is newer");
        }

        [Test]
        public void TestUpdateStatusWithUnknownVersion()
        {
            // Test update status detection with unknown local version
            string localVersion = "(unknown)";
            string remoteVersion = "1.1.0";

            bool hasUpdate = NeedsUpgrade(localVersion, remoteVersion);
            Assert.IsFalse(hasUpdate, "Should not detect update with unknown local version");
        }

        /// <summary>
        /// Helper method to test the NeedsUpgrade logic from ModelLibraryWindow.
        /// This replicates the logic from the actual implementation.
        /// </summary>
        private static bool NeedsUpgrade(string localVersion, string remoteVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion)) { return false; }
            if (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)")
            {
                // Don't show as needing upgrade if we can't determine the local version
                return false;
            }

            if (SemVer.TryParse(localVersion, out SemVer local) && SemVer.TryParse(remoteVersion, out SemVer remote))
            {
                return remote.CompareTo(local) > 0;
            }

            return false;
        }
    }
}
