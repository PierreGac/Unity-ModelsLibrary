using NUnit.Framework;
using ModelLibrary.Editor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for the extension allowlist introduced in Phase 1
    /// (audit CRIT-05 + HIGH-06).
    /// </summary>
    public class FileExtensionsSecurityTests
    {
        [Test]
        public void IsAllowedPayloadExtension_Accepts_Model_Formats()
        {
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.FBX));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.OBJ));
        }

        [Test]
        public void IsAllowedPayloadExtension_Accepts_Texture_Formats()
        {
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.PNG));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.TGA));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.JPG));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.JPEG));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.PSD));
        }

        [Test]
        public void IsAllowedPayloadExtension_Accepts_Material_And_Prefab()
        {
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.MAT));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(FileExtensions.PREFAB));
        }

        [Test]
        public void IsAllowedPayloadExtension_Rejects_Code_Files()
        {
            // CRIT-05: .cs files would become EditorScripts on import.
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(FileExtensions.CS),
                ".cs must be rejected by allowlist to prevent RCE via EditorScript");
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(".dll"),
                ".dll must be rejected — managed assemblies load with full trust");
        }

        [Test]
        public void IsAllowedPayloadExtension_Rejects_Shader_Files()
        {
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(FileExtensions.SHADER));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(FileExtensions.SHADER_GRAPH));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(FileExtensions.HLSL));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(FileExtensions.CGINC));
        }

        [Test]
        public void IsAllowedPayloadExtension_Rejects_Assembly_Defs()
        {
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(".asmdef"),
                ".asmdef must be rejected — can change assembly references");
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(".asmref"));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(".rsp"),
                ".rsp must be rejected — can change compiler flags");
        }

        [Test]
        public void IsAllowedPayloadExtension_Rejects_Scene_Files()
        {
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(".unity"),
                ".unity must be rejected — could overwrite existing scenes on import");
        }

        [Test]
        public void IsAllowedPayloadExtension_Is_Case_Insensitive()
        {
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(".FBX"));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(".Png"));
            Assert.IsTrue(FileExtensions.IsAllowedPayloadExtension(".JPEG"));
        }

        [Test]
        public void IsAllowedPayloadExtension_Rejects_Null_Or_Empty()
        {
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(null));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension(""));
            Assert.IsFalse(FileExtensions.IsAllowedPayloadExtension("   "));
        }

        [Test]
        public void IsNotAllowedFileExtension_Still_Rejects_Code_And_Shaders()
        {
            // The denylist is kept as defense-in-depth.
            Assert.IsTrue(FileExtensions.IsNotAllowedFileExtension(FileExtensions.CS));
            Assert.IsTrue(FileExtensions.IsNotAllowedFileExtension(FileExtensions.SHADER));
            Assert.IsTrue(FileExtensions.IsNotAllowedFileExtension(FileExtensions.HLSL));
        }

        [Test]
        public void IsAcceptablePayloadExtension_Combines_Both_Checks()
        {
            // On the allowlist AND not on the denylist → true
            Assert.IsTrue(FileExtensions.IsAcceptablePayloadExtension(FileExtensions.FBX));
            Assert.IsTrue(FileExtensions.IsAcceptablePayloadExtension(FileExtensions.PNG));

            // Not on allowlist → false
            Assert.IsFalse(FileExtensions.IsAcceptablePayloadExtension(".dll"));
            Assert.IsFalse(FileExtensions.IsAcceptablePayloadExtension(".asmdef"));

            // On allowlist but on denylist → false (defensive)
            // (No extensions are on both lists in the current impl, but the
            // logic should still hold if one is added in the future.)
        }
    }
}
