using NUnit.Framework;
using UnityEngine;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for the SemVer utility class functionality.
    /// </summary>
    public class SemVerTests
    {
        [Test]
        public void TestSemVerParsing()
        {
            // Test valid versions
            Assert.IsTrue(SemVer.TryParse("1.0.0", out SemVer v1));
            Assert.AreEqual(1, v1.major);
            Assert.AreEqual(0, v1.minor);
            Assert.AreEqual(0, v1.patch);

            Assert.IsTrue(SemVer.TryParse("2.5.3", out SemVer v2));
            Assert.AreEqual(2, v2.major);
            Assert.AreEqual(5, v2.minor);
            Assert.AreEqual(3, v2.patch);

            // Test invalid versions
            Assert.IsFalse(SemVer.TryParse("", out SemVer v3));
            Assert.IsFalse(SemVer.TryParse(null, out SemVer v4));
            Assert.IsFalse(SemVer.TryParse("1.0", out SemVer v5));
            Assert.IsFalse(SemVer.TryParse("1.0.0.0", out SemVer v6));
            Assert.IsFalse(SemVer.TryParse("1.0.a", out SemVer v7));
        }

        [Test]
        public void TestSemVerComparison()
        {
            // Test basic version comparison
            Assert.IsTrue(SemVer.TryParse("1.0.0", out SemVer v1));
            Assert.IsTrue(SemVer.TryParse("1.0.1", out SemVer v2));
            Assert.IsTrue(SemVer.TryParse("1.1.0", out SemVer v3));
            Assert.IsTrue(SemVer.TryParse("2.0.0", out SemVer v4));

            // Test comparison results
            Assert.AreEqual(-1, v1.CompareTo(v2)); // 1.0.0 < 1.0.1
            Assert.AreEqual(1, v2.CompareTo(v1));  // 1.0.1 > 1.0.0
            Assert.AreEqual(0, v1.CompareTo(v1));  // 1.0.0 == 1.0.0

            // Test major version comparison
            Assert.AreEqual(-1, v1.CompareTo(v4)); // 1.0.0 < 2.0.0
            Assert.AreEqual(1, v4.CompareTo(v1));  // 2.0.0 > 1.0.0

            // Test minor version comparison
            Assert.AreEqual(-1, v1.CompareTo(v3)); // 1.0.0 < 1.1.0
            Assert.AreEqual(1, v3.CompareTo(v1));  // 1.1.0 > 1.0.0
        }

        [Test]
        public void TestSemVerToString()
        {
            SemVer v1 = new SemVer(1, 2, 3);
            Assert.AreEqual("1.2.3", v1.ToString());

            SemVer v2 = new SemVer(0, 0, 1);
            Assert.AreEqual("0.0.1", v2.ToString());

            SemVer v3 = new SemVer(10, 20, 30);
            Assert.AreEqual("10.20.30", v3.ToString());
        }

        [Test]
        public void TestVersionComparisonLogic()
        {
            // Test complex version comparison scenarios
            SemVer v1_0_0 = new SemVer(1, 0, 0);
            SemVer v1_0_1 = new SemVer(1, 0, 1);
            SemVer v1_1_0 = new SemVer(1, 1, 0);
            SemVer v2_0_0 = new SemVer(2, 0, 0);

            // Test patch version updates
            Assert.IsTrue(v1_0_1.CompareTo(v1_0_0) > 0, "Patch version should be newer");
            Assert.IsTrue(v1_0_0.CompareTo(v1_0_1) < 0, "Older patch version should be older");

            // Test minor version updates
            Assert.IsTrue(v1_1_0.CompareTo(v1_0_0) > 0, "Minor version should be newer");
            Assert.IsTrue(v1_0_0.CompareTo(v1_1_0) < 0, "Older minor version should be older");

            // Test major version updates
            Assert.IsTrue(v2_0_0.CompareTo(v1_0_0) > 0, "Major version should be newer");
            Assert.IsTrue(v1_0_0.CompareTo(v2_0_0) < 0, "Older major version should be older");

            // Test equal versions
            SemVer v1_0_0_copy = new SemVer(1, 0, 0);
            Assert.AreEqual(0, v1_0_0.CompareTo(v1_0_0_copy), "Identical versions should be equal");
        }

        [Test]
        public void TestEdgeCaseVersions()
        {
            // Test edge cases for version parsing
            Assert.IsTrue(SemVer.TryParse("0.0.0", out SemVer v1));
            Assert.AreEqual(0, v1.major);
            Assert.AreEqual(0, v1.minor);
            Assert.AreEqual(0, v1.patch);

            Assert.IsTrue(SemVer.TryParse("999.999.999", out SemVer v2));
            Assert.AreEqual(999, v2.major);
            Assert.AreEqual(999, v2.minor);
            Assert.AreEqual(999, v2.patch);

            // Test invalid edge cases
            Assert.IsFalse(SemVer.TryParse("1.0.0.0", out SemVer v3));
            Assert.IsFalse(SemVer.TryParse("1.0", out SemVer v4));
            Assert.IsFalse(SemVer.TryParse("1", out SemVer v5));
            Assert.IsFalse(SemVer.TryParse("a.b.c", out SemVer v6));
            Assert.IsFalse(SemVer.TryParse("1.0.-1", out SemVer v7));

            // Test negative version numbers
            Assert.IsFalse(SemVer.TryParse("-1.0.0", out SemVer v8));
            Assert.IsFalse(SemVer.TryParse("1.-1.0", out SemVer v9));
            Assert.IsFalse(SemVer.TryParse("1.0.-1", out SemVer v10));
            Assert.IsFalse(SemVer.TryParse("-1.-1.-1", out SemVer v11));
        }
    }
}
