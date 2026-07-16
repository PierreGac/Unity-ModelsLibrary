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
}
