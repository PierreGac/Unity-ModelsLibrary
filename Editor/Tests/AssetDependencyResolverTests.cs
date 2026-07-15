using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for <see cref="AssetDependencyResolver"/> path-level helpers.
    /// </summary>
    public class AssetDependencyResolverTests
    {
        [Test]
        public void IsMeshAssetPath_ReturnsTrue_ForFbxAndObj()
        {
            Assert.IsTrue(AssetDependencyResolver.IsMeshAssetPath("Assets/Models/Hero.fbx"));
            Assert.IsTrue(AssetDependencyResolver.IsMeshAssetPath("Assets/Models/Hero.OBJ"));
        }

        [Test]
        public void IsMeshAssetPath_ReturnsFalse_ForNonMeshPaths()
        {
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath(string.Empty));
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath(null));
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath("Assets/Materials/Hero.mat"));
            Assert.IsFalse(AssetDependencyResolver.IsMeshAssetPath("Assets/Textures/Hero.png"));
        }

        [Test]
        public void IsEligibleDependencyPath_RejectsSelfPath()
        {
            const string SOURCE_PATH = "Assets/Models/Hero.fbx";

            Assert.IsFalse(AssetDependencyResolver.IsEligibleDependencyPath(SOURCE_PATH, SOURCE_PATH));
        }

        [Test]
        public void IsEligibleDependencyPath_RejectsDeniedExtensions()
        {
            const string SOURCE_PATH = "Assets/Models/Hero.fbx";

            Assert.IsFalse(AssetDependencyResolver.IsEligibleDependencyPath(
                "Assets/Shaders/Custom.shader",
                SOURCE_PATH));
            Assert.IsFalse(AssetDependencyResolver.IsEligibleDependencyPath(
                "Assets/Scripts/EditorTool.cs",
                SOURCE_PATH));
        }

        [Test]
        public void IsEligibleDependencyPath_AcceptsAllowedExtensions()
        {
            const string SOURCE_PATH = "Assets/Models/Hero.fbx";

            Assert.IsTrue(AssetDependencyResolver.IsEligibleDependencyPath(
                "Assets/Materials/Hero.mat",
                SOURCE_PATH));
            Assert.IsTrue(AssetDependencyResolver.IsEligibleDependencyPath(
                "Assets/Textures/Hero.png",
                SOURCE_PATH));
            Assert.IsTrue(AssetDependencyResolver.IsEligibleDependencyPath(
                "Assets/Models/Accessory.fbx",
                SOURCE_PATH));
        }

        [Test]
        public void FormatDependencySourceLabel_ReturnsEmpty_WhenNoSourceNames()
        {
            Assert.AreEqual(string.Empty, AssetDependencyResolver.FormatDependencySourceLabel(null));
            Assert.AreEqual(string.Empty, AssetDependencyResolver.FormatDependencySourceLabel(new List<string>()));
        }

        [Test]
        public void FormatDependencySourceLabel_FormatsSingleAndMultipleMeshNames()
        {
            Assert.AreEqual(
                "Dependency of Hero",
                AssetDependencyResolver.FormatDependencySourceLabel(new List<string> { "Hero" }));

            Assert.AreEqual(
                "Dependency of Hero, Weapon",
                AssetDependencyResolver.FormatDependencySourceLabel(new List<string> { "Hero", "Weapon" }));
        }

        [Test]
        public void GetMeshDisplayName_ReturnsFileNameWithoutExtension()
        {
            Assert.AreEqual("Hero", AssetDependencyResolver.GetMeshDisplayName("Assets/Models/Hero.fbx"));
            Assert.AreEqual(string.Empty, AssetDependencyResolver.GetMeshDisplayName(string.Empty));
        }
    }
}
