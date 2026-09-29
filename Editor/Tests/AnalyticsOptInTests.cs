using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEditor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Usage tracking stays off until enabled, and saved counts survive a reload.
    /// </summary>
    public class AnalyticsOptInTests
    {
        private const string MODEL_ID = "sample-cube";
        private const string MODEL_VERSION = "1.0.0";
        private const string MODEL_NAME = "Sample Cube";
        private const string IMPORT_EVENT = "import";
        private const string METADATA_KEY = "source";
        private const string METADATA_VALUE = "opt-in-test";
        private const int EXPECTED_IMPORT_COUNT = 1;
        private const int LEGACY_TIMESTAMP = 1;

        private bool _hadData;
        private string _savedData;
        private bool _hadEnabled;
        private bool _savedEnabled;

        /// <summary>
        /// Keeps the machine's analytics preferences out of the test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _hadData = EditorPrefs.HasKey(AnalyticsService.DATA_PREF_KEY);
            _savedData = EditorPrefs.GetString(AnalyticsService.DATA_PREF_KEY, string.Empty);
            _hadEnabled = EditorPrefs.HasKey(AnalyticsService.ENABLED_PREF_KEY);
            _savedEnabled = EditorPrefs.GetBool(AnalyticsService.ENABLED_PREF_KEY, false);
            EditorPrefs.DeleteKey(AnalyticsService.DATA_PREF_KEY);
            EditorPrefs.DeleteKey(AnalyticsService.ENABLED_PREF_KEY);
        }

        /// <summary>
        /// Restores the analytics preferences that were present before the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_hadData)
            {
                EditorPrefs.SetString(AnalyticsService.DATA_PREF_KEY, _savedData);
            }
            else
            {
                EditorPrefs.DeleteKey(AnalyticsService.DATA_PREF_KEY);
            }

            if (_hadEnabled)
            {
                EditorPrefs.SetBool(AnalyticsService.ENABLED_PREF_KEY, _savedEnabled);
            }
            else
            {
                EditorPrefs.DeleteKey(AnalyticsService.ENABLED_PREF_KEY);
            }
        }

        /// <summary>
        /// A missing switch does not record an import.
        /// </summary>
        [Test]
        public void DisabledByDefault_DoesNotRecord()
        {
            Assert.IsFalse(AnalyticsService.IsEnabled());
            AnalyticsService.RecordEvent(IMPORT_EVENT, MODEL_ID, MODEL_VERSION, MODEL_NAME);
            Assert.AreEqual(0, AnalyticsService.GetImportCount(MODEL_ID));
            Assert.IsFalse(EditorPrefs.HasKey(AnalyticsService.DATA_PREF_KEY));
        }

        /// <summary>
        /// Turning recording on stores the count, and turning it off stops further writes.
        /// </summary>
        [Test]
        public void Enabled_PersistsCount_AndOptOutStopsRecording()
        {
            AnalyticsService.SetEnabled(true);
            Assert.IsTrue(AnalyticsService.IsEnabled());

            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata[METADATA_KEY] = METADATA_VALUE;
            AnalyticsService.RecordEvent(IMPORT_EVENT, MODEL_ID, MODEL_VERSION, MODEL_NAME, metadata);

            Assert.AreEqual(EXPECTED_IMPORT_COUNT, AnalyticsService.GetImportCount(MODEL_ID));
            string savedJson = EditorPrefs.GetString(AnalyticsService.DATA_PREF_KEY, string.Empty);
            Assert.IsTrue(savedJson.Contains("importCountEntries"));
            Assert.IsTrue(savedJson.Contains(MODEL_ID));
            Assert.IsTrue(savedJson.Contains(METADATA_VALUE));

            List<AnalyticsService.AnalyticsEvent> events = AnalyticsService.GetEventsInRange(DateTime.MinValue, DateTime.MaxValue);
            Assert.AreEqual(EXPECTED_IMPORT_COUNT, events.Count);
            Assert.AreEqual(METADATA_VALUE, events[0].metadata[METADATA_KEY]);

            AnalyticsService.SetEnabled(false);
            Assert.IsFalse(AnalyticsService.IsEnabled());
            AnalyticsService.RecordEvent(IMPORT_EVENT, MODEL_ID, MODEL_VERSION, MODEL_NAME);
            Assert.AreEqual(EXPECTED_IMPORT_COUNT, AnalyticsService.GetImportCount(MODEL_ID));
        }

        /// <summary>
        /// Older EditorPrefs stored events only. Counts are rebuilt from those events.
        /// </summary>
        [Test]
        public void LegacyEvents_RebuildImportCount()
        {
            string json = "{\"events\":[{\"eventType\":\"" + IMPORT_EVENT
                + "\",\"modelId\":\"" + MODEL_ID
                + "\",\"modelVersion\":\"" + MODEL_VERSION
                + "\",\"modelName\":\"" + MODEL_NAME
                + "\",\"timestamp\":" + LEGACY_TIMESTAMP + "}]}";
            EditorPrefs.SetString(AnalyticsService.DATA_PREF_KEY, json);
            Assert.AreEqual(EXPECTED_IMPORT_COUNT, AnalyticsService.GetImportCount(MODEL_ID));
        }
    }
}
