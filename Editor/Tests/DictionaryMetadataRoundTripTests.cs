using System.Collections.Generic;
using ModelLibrary.Data;
using ModelLibrary.Editor.Serialization;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Round-trips dictionary metadata through schema 0, schema 1, and the current schema.
    /// </summary>
    public class DictionaryMetadataRoundTripTests
    {
        private const int SCHEMA_0 = 0;
        private const int SCHEMA_1 = 1;
        private const string MODEL_ID = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
        private const string MODEL_NAME = "Hero";
        private const string MODEL_VERSION = "1.0.0";
        private const string EXTRA_KEY = "polyCount";
        private const string EXTRA_VALUE = "1500";
        private const string IMPORTER_KEY = "payload/hero.fbx";
        private const string MATERIAL_IMPORT_MODE = "ImportViaMaterialDescription";
        private const string MATERIAL_SEARCH = "RecursiveUp";
        private const string MATERIAL_NAME = "ByBaseTextureName";
        private const string INDEX_VERSION_OLD = "1.0.0";
        private const string INDEX_VERSION_NEW = "1.1.0";
        private const int INDEX_VERSION_COUNT = 2;

        /// <summary>
        /// Schema 0 JSON that stored dictionaries as objects still loads and saves those values.
        /// </summary>
        [Test]
        public void Schema0_RoundTripsDictionaryFields()
        {
            AssertMetaRoundTrip(SCHEMA_0);
        }

        /// <summary>
        /// Schema 1 JSON that stored dictionaries as objects still loads and saves those values.
        /// </summary>
        [Test]
        public void Schema1_RoundTripsDictionaryFields()
        {
            AssertMetaRoundTrip(SCHEMA_1);
        }

        /// <summary>
        /// The current schema stores extra metadata and importer settings through entry lists.
        /// </summary>
        [Test]
        public void CurrentSchema_RoundTripsDictionaryFields()
        {
            ModelMeta meta = new ModelMeta();
            meta.identity = new ModelIdentity();
            meta.identity.id = MODEL_ID;
            meta.identity.name = MODEL_NAME;
            meta.version = MODEL_VERSION;
            meta.extra[EXTRA_KEY] = EXTRA_VALUE;
            meta.modelImporters[IMPORTER_KEY] = CreateImporterSettings();

            string json = JsonUtil.ToJson(meta);
            Assert.IsTrue(json.Contains("extraEntries"));
            Assert.IsTrue(json.Contains("modelImporterEntries"));
            ModelMeta loaded = JsonUtil.FromJsonModelMeta(json);
            AssertMetaFields(loaded);
            Assert.AreEqual(ModelMetaMigration.CURRENT_SCHEMA_VERSION, loaded.schemaVersion);
        }

        /// <summary>
        /// Index version maps survive save and load.
        /// </summary>
        [Test]
        public void ModelIndex_RoundTripsVersions()
        {
            string legacyJson = "{\"entries\":[],\"versions\":{\"" + MODEL_ID + "\":[\"" + INDEX_VERSION_OLD + "\",\"" + INDEX_VERSION_NEW + "\"]}}";
            ModelIndex legacy = JsonUtil.FromJson<ModelIndex>(legacyJson);
            AssertVersions(legacy);

            ModelIndex index = new ModelIndex();
            index.versions[MODEL_ID] = new List<string>();
            index.versions[MODEL_ID].Add(INDEX_VERSION_OLD);
            index.versions[MODEL_ID].Add(INDEX_VERSION_NEW);
            string json = JsonUtil.ToJson(index);
            Assert.IsTrue(json.Contains("versionEntries"));
            ModelIndex loaded = JsonUtil.FromJson<ModelIndex>(json);
            AssertVersions(loaded);
        }

        private static void AssertMetaRoundTrip(int schemaVersion)
        {
            string json = BuildLegacyMetaJson(schemaVersion);
            ModelMeta loaded = JsonUtil.FromJsonModelMeta(json);
            AssertMetaFields(loaded);
            Assert.AreEqual(ModelMetaMigration.CURRENT_SCHEMA_VERSION, loaded.schemaVersion);

            string saved = JsonUtil.ToJson(loaded);
            ModelMeta roundTrip = JsonUtil.FromJsonModelMeta(saved);
            AssertMetaFields(roundTrip);
            Assert.AreEqual(ModelMetaMigration.CURRENT_SCHEMA_VERSION, roundTrip.schemaVersion);
        }

        private static void AssertMetaFields(ModelMeta meta)
        {
            Assert.IsNotNull(meta);
            Assert.AreEqual(EXTRA_VALUE, meta.extra[EXTRA_KEY]);
            Assert.IsTrue(meta.modelImporters.ContainsKey(IMPORTER_KEY));
            ModelImporterSettings settings = meta.modelImporters[IMPORTER_KEY];
            Assert.IsNotNull(settings);
            Assert.AreEqual(MATERIAL_IMPORT_MODE, settings.materialImportMode);
            Assert.AreEqual(MATERIAL_SEARCH, settings.materialSearch);
            Assert.AreEqual(MATERIAL_NAME, settings.materialName);
        }

        private static void AssertVersions(ModelIndex index)
        {
            Assert.IsNotNull(index);
            Assert.IsTrue(index.versions.ContainsKey(MODEL_ID));
            List<string> versions = index.versions[MODEL_ID];
            Assert.AreEqual(INDEX_VERSION_COUNT, versions.Count);
            Assert.AreEqual(INDEX_VERSION_OLD, versions[0]);
            Assert.AreEqual(INDEX_VERSION_NEW, versions[1]);
        }

        private static ModelImporterSettings CreateImporterSettings()
        {
            ModelImporterSettings settings = new ModelImporterSettings();
            settings.materialImportMode = MATERIAL_IMPORT_MODE;
            settings.materialSearch = MATERIAL_SEARCH;
            settings.materialName = MATERIAL_NAME;
            return settings;
        }

        private static string BuildLegacyMetaJson(int schemaVersion)
        {
            return "{"
                + "\"schemaVersion\":" + schemaVersion + ","
                + "\"version\":\"" + MODEL_VERSION + "\","
                + "\"identity\":{\"id\":\"" + MODEL_ID + "\",\"name\":\"" + MODEL_NAME + "\"},"
                + "\"extra\":{\"" + EXTRA_KEY + "\":\"" + EXTRA_VALUE + "\"},"
                + "\"modelImporters\":{\"" + IMPORTER_KEY + "\":{"
                + "\"materialImportMode\":\"" + MATERIAL_IMPORT_MODE + "\","
                + "\"materialSearch\":\"" + MATERIAL_SEARCH + "\","
                + "\"materialName\":\"" + MATERIAL_NAME + "\"}}"
                + "}";
        }
    }
}
