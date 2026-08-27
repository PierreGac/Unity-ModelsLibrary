using System.Collections.Generic;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Windows;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for Update Existing catalog selection by model id.
    /// </summary>
    public class SubmitUpdateSelectionTests
    {
        /// <summary>
        /// Finds a catalog entry by id regardless of name sort order.
        /// </summary>
        [Test]
        public void FindExistingModelIndex_MatchesIdCaseInsensitive()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            int index = ModelSubmitWindow.FindExistingModelIndex(models, "MODEL-B");

            Assert.AreEqual(1, index);
        }

        /// <summary>
        /// Returns -1 when the catalog does not contain the requested id.
        /// </summary>
        [Test]
        public void FindExistingModelIndex_ReturnsMinusOneWhenMissing()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            int index = ModelSubmitWindow.FindExistingModelIndex(models, "missing-id");

            Assert.AreEqual(-1, index);
        }

        /// <summary>
        /// Returns -1 for null catalogs or empty ids.
        /// </summary>
        [Test]
        public void FindExistingModelIndex_HandlesNullAndEmpty()
        {
            Assert.AreEqual(-1, ModelSubmitWindow.FindExistingModelIndex(null, "model-a"));
            Assert.AreEqual(-1, ModelSubmitWindow.FindExistingModelIndex(CreateCatalog(), null));
            Assert.AreEqual(-1, ModelSubmitWindow.FindExistingModelIndex(CreateCatalog(), string.Empty));
        }

        /// <summary>
        /// Pre-selects the requested catalog model when it exists.
        /// </summary>
        [Test]
        public void TryResolveUpdateSelection_SelectsTargetModel()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            bool resolved = ModelSubmitWindow.TryResolveUpdateSelection(
                models,
                "model-b",
                0,
                out int selectedIndex,
                out bool modelIdMissing);

            Assert.IsTrue(resolved);
            Assert.IsFalse(modelIdMissing);
            Assert.AreEqual(1, selectedIndex);
        }

        /// <summary>
        /// Reports a missing id and does not invent a fallback selection.
        /// </summary>
        [Test]
        public void TryResolveUpdateSelection_MissingIdHasNoSelection()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            bool resolved = ModelSubmitWindow.TryResolveUpdateSelection(
                models,
                "does-not-exist",
                0,
                out int selectedIndex,
                out bool modelIdMissing);

            Assert.IsFalse(resolved);
            Assert.IsTrue(modelIdMissing);
            Assert.AreEqual(-1, selectedIndex);
        }

        /// <summary>
        /// Requires an explicit picker choice when Update Existing has no target model id.
        /// </summary>
        [Test]
        public void TryResolveUpdateSelection_WithoutTargetRequiresExplicitPick()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            bool resolved = ModelSubmitWindow.TryResolveUpdateSelection(
                models,
                null,
                2,
                out int selectedIndex,
                out bool modelIdMissing);

            Assert.IsFalse(resolved);
            Assert.IsFalse(modelIdMissing);
            Assert.AreEqual(-1, selectedIndex);
        }

        /// <summary>
        /// An empty target id does not fall back to the first catalog entry.
        /// </summary>
        [Test]
        public void TryResolveUpdateSelection_EmptyTargetDoesNotSelectFirstModel()
        {
            List<ModelIndex.Entry> models = CreateCatalog();

            bool resolved = ModelSubmitWindow.TryResolveUpdateSelection(
                models,
                string.Empty,
                99,
                out int selectedIndex,
                out bool modelIdMissing);

            Assert.IsFalse(resolved);
            Assert.IsFalse(modelIdMissing);
            Assert.AreEqual(-1, selectedIndex);
        }

        /// <summary>
        /// Empty catalogs cannot resolve a selection.
        /// </summary>
        [Test]
        public void TryResolveUpdateSelection_EmptyCatalogFails()
        {
            bool resolved = ModelSubmitWindow.TryResolveUpdateSelection(
                new List<ModelIndex.Entry>(),
                "model-a",
                0,
                out int selectedIndex,
                out bool modelIdMissing);

            Assert.IsFalse(resolved);
            Assert.IsTrue(modelIdMissing);
            Assert.AreEqual(-1, selectedIndex);
        }

        /// <summary>
        /// Formats picker labels with name and latest version.
        /// </summary>
        [Test]
        public void FormatModelOption_IncludesNameAndVersion()
        {
            ModelIndex.Entry entry = new ModelIndex.Entry
            {
                id = "model-a",
                name = "Crate",
                latestVersion = "1.2.0"
            };

            string label = ModelUpdatePickerDropdown.FormatModelOption(entry);

            Assert.AreEqual("Crate (latest v1.2.0)", label);
        }

        /// <summary>
        /// Artist and Admin may submit; Developer may not.
        /// </summary>
        [Test]
        public void CanSubmitModels_AllowsArtistAndAdminOnly()
        {
            Assert.IsFalse(SimpleUserIdentityProvider.CanSubmitModels(UserRole.Developer));
            Assert.IsTrue(SimpleUserIdentityProvider.CanSubmitModels(UserRole.Artist));
            Assert.IsTrue(SimpleUserIdentityProvider.CanSubmitModels(UserRole.Admin));
        }

        private static List<ModelIndex.Entry> CreateCatalog()
        {
            return new List<ModelIndex.Entry>
            {
                new ModelIndex.Entry { id = "model-a", name = "Alpha", latestVersion = "1.0.0" },
                new ModelIndex.Entry { id = "model-b", name = "Bravo", latestVersion = "2.0.0" },
                new ModelIndex.Entry { id = "model-c", name = "Charlie", latestVersion = "3.0.0" }
            };
        }
    }
}
