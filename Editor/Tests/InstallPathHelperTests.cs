using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for install path resolution during import and update.
    /// </summary>
    public class InstallPathHelperTests
    {
        [Test]
        public void DetermineInstallPath_PrefersNewMetaInstallPathOverLocal()
        {
            InstallPathHelper helper = new InstallPathHelper();
            ModelMeta newMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "Ship" },
                installPath = "Assets/Art/Ship"
            };
            ModelMeta localMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "Ship" },
                installPath = "Assets/Models/Ship"
            };

            string resolved = helper.DetermineInstallPath(newMeta, localMeta, preferredInstallPath: null);

            Assert.AreEqual("Assets/Art/Ship", resolved);
        }

        [Test]
        public void DetermineInstallPath_FallsBackToLocalWhenNewMetaHasNoInstallPath()
        {
            InstallPathHelper helper = new InstallPathHelper();
            ModelMeta newMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "Ship" },
                installPath = null
            };
            ModelMeta localMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "Ship" },
                installPath = "Assets/Models/Ship"
            };

            string resolved = helper.DetermineInstallPath(newMeta, localMeta, preferredInstallPath: null);

            Assert.AreEqual("Assets/Models/Ship", resolved);
        }

        [Test]
        public void DetermineInstallPath_PreferredPathStillWinsForExplicitUserChoice()
        {
            InstallPathHelper helper = new InstallPathHelper();
            ModelMeta newMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "Ship" },
                installPath = "Assets/Art/Ship"
            };

            string resolved = helper.DetermineInstallPath(
                newMeta,
                localInstallMeta: null,
                preferredInstallPath: "Assets/Custom/Ship");

            Assert.AreEqual("Assets/Custom/Ship", resolved);
        }

        [Test]
        public void DetermineInstallPath_BuildsDefaultWhenNoPathsAvailable()
        {
            InstallPathHelper helper = new InstallPathHelper();
            ModelMeta newMeta = new ModelMeta
            {
                identity = new ModelIdentity { name = "My Ship" },
                installPath = null
            };

            string resolved = helper.DetermineInstallPath(newMeta, localInstallMeta: null, preferredInstallPath: null);

            Assert.AreEqual(InstallPathUtils.BuildInstallPath("My Ship"), resolved);
        }
    }

    /// <summary>
    /// Tests for loading a project object from an install path.
    /// </summary>
    public class InstallPathUtilsTests
    {
        private const string EXISTING_FOLDER_NAME = "ModelLibrary";
        private const string EXISTING_PROJECT_FOLDER = "Assets/" + EXISTING_FOLDER_NAME;
        private const string MISSING_PROJECT_FOLDER = "Assets/__ModelLibraryMissingPingTarget__";

        /// <summary>
        /// An existing project folder can be loaded from a project-relative install path.
        /// </summary>
        [Test]
        public void TryLoadProjectObject_LoadsExistingProjectFolder()
        {
            UnityEngine.Object asset;
            bool found = InstallPathUtils.TryLoadProjectObject(EXISTING_PROJECT_FOLDER, out asset);

            Assert.IsTrue(found);
            Assert.IsNotNull(asset);
        }

        /// <summary>
        /// An existing project folder can be loaded from its absolute path.
        /// </summary>
        [Test]
        public void TryLoadProjectObject_LoadsExistingFolderFromAbsolutePath()
        {
            string absolutePath = System.IO.Path.Combine(UnityEngine.Application.dataPath, EXISTING_FOLDER_NAME);
            UnityEngine.Object asset;
            bool found = InstallPathUtils.TryLoadProjectObject(absolutePath, out asset);

            Assert.IsTrue(found);
            Assert.IsNotNull(asset);
        }

        /// <summary>
        /// Missing, blank, and null paths do not resolve to a project object.
        /// </summary>
        [Test]
        public void TryLoadProjectObject_ReturnsFalseForMissingOrBlankPaths()
        {
            UnityEngine.Object missingAsset;
            bool missingFound = InstallPathUtils.TryLoadProjectObject(MISSING_PROJECT_FOLDER, out missingAsset);
            Assert.IsFalse(missingFound);
            Assert.IsNull(missingAsset);

            UnityEngine.Object blankAsset;
            bool blankFound = InstallPathUtils.TryLoadProjectObject("   ", out blankAsset);
            Assert.IsFalse(blankFound);
            Assert.IsNull(blankAsset);

            UnityEngine.Object nullAsset;
            bool nullFound = InstallPathUtils.TryLoadProjectObject(null, out nullAsset);
            Assert.IsFalse(nullFound);
            Assert.IsNull(nullAsset);
        }
    }
}
