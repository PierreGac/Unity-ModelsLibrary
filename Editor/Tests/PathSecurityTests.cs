using System;
using System.IO;
using NUnit.Framework;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for the new path-security helpers introduced in Phase 1
    /// (audit CRIT-01, CRIT-02, CRIT-04, HIGH-01).
    /// </summary>
    /// <remarks>
    /// These tests close part of the test-coverage gap flagged in audit
    /// finding INFO-03: the old UITests were tautologies that tested .NET
    /// BCL behavior, not production code. These tests exercise real
    /// production methods.
    /// </remarks>
    public class PathSecurityTests
    {
        // ---------- IsSafeIdentifier ----------

        [Test]
        public void IsSafeIdentifier_Accepts_Guid_Hex_String()
        {
            Assert.IsTrue(PathUtils.IsSafeIdentifier("a1b2c3d4e5f67890a1b2c3d4e5f67890"),
                "32-char hex GUID should be accepted (the documented format for modelId)");
        }

        [Test]
        public void IsSafeIdentifier_Accepts_SemVer_Version()
        {
            Assert.IsTrue(PathUtils.IsSafeIdentifier("1.0.0"), "SemVer version should be accepted");
            Assert.IsTrue(PathUtils.IsSafeIdentifier("2.5.3-beta.1"), "Pre-release SemVer should be accepted");
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Parent_Traversal()
        {
            Assert.IsFalse(PathUtils.IsSafeIdentifier(".."), "'..' must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("../etc"), "'../etc' must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("foo/../bar"), "'foo/../bar' must be rejected");
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Path_Separators()
        {
            Assert.IsFalse(PathUtils.IsSafeIdentifier("foo/bar"), "Forward slash must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("foo\\bar"), "Backslash must be rejected");
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Drive_Separator()
        {
            Assert.IsFalse(PathUtils.IsSafeIdentifier("C:foo"), "Drive separator must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("foo:bar"), "Colon must be rejected");
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Null_Or_Empty()
        {
            Assert.IsFalse(PathUtils.IsSafeIdentifier(null));
            Assert.IsFalse(PathUtils.IsSafeIdentifier(""));
            Assert.IsFalse(PathUtils.IsSafeIdentifier("   "));
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Windows_Device_Names()
        {
            Assert.IsFalse(PathUtils.IsSafeIdentifier("CON"), "CON must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("PRN"), "PRN must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("AUX"), "AUX must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("NUL"), "NUL must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("COM1"), "COM1 must be rejected");
            Assert.IsFalse(PathUtils.IsSafeIdentifier("LPT1"), "LPT1 must be rejected");
        }

        [Test]
        public void IsSafeIdentifier_Rejects_Excessive_Length()
        {
            string longId = new string('a', 200);
            Assert.IsFalse(PathUtils.IsSafeIdentifier(longId), "Over-128-char identifiers must be rejected");
        }

        // ---------- AssertInsideRoot ----------

        [Test]
        public void AssertInsideRoot_Accepts_Path_Inside_Root()
        {
            string root = Path.GetTempPath();
            string path = Path.Combine(root, "subdir", "file.txt");
            string result = PathUtils.AssertInsideRoot(path, root);
            Assert.AreEqual(Path.GetFullPath(path), result);
        }

        [Test]
        public void AssertInsideRoot_Throws_On_Parent_Traversal()
        {
            string root = Path.Combine(Path.GetTempPath(), "ModelLibrary_Root");
            string path = Path.Combine(root, "..", "..", "etc", "passwd");
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.AssertInsideRoot(path, root));
        }

        [Test]
        public void AssertInsideRoot_Accepts_Path_Equal_To_Root()
        {
            string root = Path.GetTempPath();
            // path == root should be allowed.
            Assert.DoesNotThrow(() => PathUtils.AssertInsideRoot(root, root));
        }

        [Test]
        public void AssertInsideRoot_Rejects_Sibling_Prefix()
        {
            // "/foo/barbaz" should NOT be considered inside "/foo/bar".
            string root = Path.Combine(Path.GetTempPath(), "foo", "bar");
            string path = Path.Combine(Path.GetTempPath(), "foo", "barbaz");
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.AssertInsideRoot(path, root));
        }

        // ---------- ValidateRelativePathStrict ----------

        [Test]
        public void ValidateRelativePathStrict_Accepts_Normal_Path()
        {
            Assert.DoesNotThrow(() => PathUtils.ValidateRelativePathStrict("images/preview.png"));
            Assert.DoesNotThrow(() => PathUtils.ValidateRelativePathStrict("payload/sword.fbx"));
        }

        [Test]
        public void ValidateRelativePathStrict_Throws_On_Parent_Traversal()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.ValidateRelativePathStrict("../../etc/passwd"));
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.ValidateRelativePathStrict("images/../../../foo"));
        }

        [Test]
        public void ValidateRelativePathStrict_Throws_On_Empty()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.ValidateRelativePathStrict(""));
            Assert.Throws<InvalidOperationException>(() =>
                PathUtils.ValidateRelativePathStrict("   "));
        }
    }
}
