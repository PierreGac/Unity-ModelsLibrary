using ModelLibrary.Data;
using ModelLibrary.Editor.Serialization;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Verifies that future and unreadable model metadata are refused, while schema 0 files still load.
    /// </summary>
    public class FutureMetadataRejectionTests
    {
        private const int FUTURE_SCHEMA_VERSION = 99;
        private const string MODEL_ID = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
        private const string MODEL_NAME = "Hero";
        private const string MODEL_VERSION = "1.0.0";
        private const string ESCAPED_DESCRIPTION = "say \"hello\"";
        private const string EXTRA_KEY = "polyCount";
        private const string EXTRA_VALUE = "1500";

        /// <summary>
        /// A schema newer than the package understands is not returned as editable metadata.
        /// </summary>
        [Test]
        public void Schema99_IsRejected()
        {
            string json = BuildIdentityJson(FUTURE_SCHEMA_VERSION);
            Assert.Throws<System.InvalidOperationException>(() => JsonUtil.FromJsonModelMeta(json));
        }

        /// <summary>
        /// Truncated JSON does not become a partial model that could be saved.
        /// </summary>
        [Test]
        public void TruncatedJson_IsRejected()
        {
            string json = "{\"schemaVersion\":2,\"identity\":{\"id\":\"" + MODEL_ID + "\"";
            ModelMeta loaded = JsonUtil.FromJsonModelMeta(json);
            Assert.IsNull(loaded);
        }

        /// <summary>
        /// A quoted description survives load without the old regex fallback cutting it short.
        /// </summary>
        [Test]
        public void EscapedQuotes_LoadTheFullDescription()
        {
            string json = "{"
                + "\"schemaVersion\":" + ModelMetaMigration.CURRENT_SCHEMA_VERSION + ","
                + "\"version\":\"" + MODEL_VERSION + "\","
                + "\"identity\":{\"id\":\"" + MODEL_ID + "\",\"name\":\"" + MODEL_NAME + "\"},"
                + "\"description\":\"say \\\"hello\\\"\""
                + "}";
            ModelMeta loaded = JsonUtil.FromJsonModelMeta(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ESCAPED_DESCRIPTION, loaded.description);
            Assert.AreEqual(MODEL_ID, loaded.identity.id);
        }

        /// <summary>
        /// Metadata with no identity id is refused.
        /// </summary>
        [Test]
        public void MissingIdentity_IsRejected()
        {
            string json = "{\"schemaVersion\":" + ModelMetaMigration.CURRENT_SCHEMA_VERSION + ",\"version\":\"" + MODEL_VERSION + "\"}";
            Assert.Throws<System.InvalidOperationException>(() => JsonUtil.FromJsonModelMeta(json));
        }

        /// <summary>
        /// Schema 0 dictionary JSON accepted by Phase 3A still loads.
        /// </summary>
        [Test]
        public void Schema0LegacyFixture_StillLoads()
        {
            string json = "{"
                + "\"schemaVersion\":0,"
                + "\"version\":\"" + MODEL_VERSION + "\","
                + "\"identity\":{\"id\":\"" + MODEL_ID + "\",\"name\":\"" + MODEL_NAME + "\"},"
                + "\"extra\":{\"" + EXTRA_KEY + "\":\"" + EXTRA_VALUE + "\"}"
                + "}";
            ModelMeta loaded = JsonUtil.FromJsonModelMeta(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(EXTRA_VALUE, loaded.extra[EXTRA_KEY]);
            Assert.AreEqual(MODEL_ID, loaded.identity.id);
            Assert.AreEqual(ModelMetaMigration.CURRENT_SCHEMA_VERSION, loaded.schemaVersion);
        }

        private static string BuildIdentityJson(int schemaVersion)
        {
            return "{"
                + "\"schemaVersion\":" + schemaVersion + ","
                + "\"version\":\"" + MODEL_VERSION + "\","
                + "\"identity\":{\"id\":\"" + MODEL_ID + "\",\"name\":\"" + MODEL_NAME + "\"}"
                + "}";
        }
    }
}
