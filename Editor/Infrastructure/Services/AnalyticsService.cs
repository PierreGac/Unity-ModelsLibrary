using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for tracking model usage analytics and generating reports.
    /// Tracks model imports, updates, views, and other user interactions.
    /// </summary>
    internal class AnalyticsService
    {
        /// <summary>EditorPrefs key for storing analytics data.</summary>
        internal const string DATA_PREF_KEY = "ModelLibrary.Analytics";

        /// <summary>EditorPrefs key for the opt-in switch. Missing means off.</summary>
        internal const string ENABLED_PREF_KEY = "ModelLibrary.Analytics.Enabled";

        /// <summary>Shown next to the switch. Counts are not uploaded.</summary>
        internal const string LOCAL_ONLY_MESSAGE = "Usage counts stay on this machine in Editor preferences. They are not sent to a server.";

        /// <summary>Maximum number of analytics entries to keep.</summary>
        private const int __MaxEntries = 10000;

        private const string IMPORT_EVENT = "import";
        private const string UPDATE_EVENT = "update";
        private const string VIEW_EVENT = "view";

        /// <summary>
        /// Data structure for a single analytics event.
        /// </summary>
        [Serializable]
        public class AnalyticsEvent
        {
            public string eventType;
            public string modelId;
            public string modelVersion;
            public string modelName;
            public long timestamp;

            /// <summary>In-memory metadata. JsonUtility stores <see cref="metadataEntries"/> instead.</summary>
            [NonSerialized]
            public Dictionary<string, string> metadata = new Dictionary<string, string>();

            /// <summary>Serializable metadata pairs.</summary>
            public List<StringPairEntry> metadataEntries = new List<StringPairEntry>();
        }

        /// <summary>One string key and string value stored in analytics JSON.</summary>
        [Serializable]
        public class StringPairEntry
        {
            public string key;
            public string value;
        }

        /// <summary>One model id and integer count stored in analytics JSON.</summary>
        [Serializable]
        private class StringCountEntry
        {
            public string key;
            public int count;
        }

        /// <summary>One model id and UTC tick count stored in analytics JSON.</summary>
        [Serializable]
        private class StringTicksEntry
        {
            public string key;
            public long ticks;
        }

        /// <summary>
        /// Analytics data container.
        /// Dictionary fields are not written by JsonUtility, so the entry lists are the stored form.
        /// </summary>
        [Serializable]
        private class AnalyticsData
        {
            public List<AnalyticsEvent> events = new List<AnalyticsEvent>();

            [NonSerialized]
            public Dictionary<string, int> modelImportCounts = new Dictionary<string, int>();

            [NonSerialized]
            public Dictionary<string, int> modelViewCounts = new Dictionary<string, int>();

            [NonSerialized]
            public Dictionary<string, DateTime> lastAccessed = new Dictionary<string, DateTime>();

            public List<StringCountEntry> importCountEntries = new List<StringCountEntry>();
            public List<StringCountEntry> viewCountEntries = new List<StringCountEntry>();
            public List<StringTicksEntry> lastAccessedEntries = new List<StringTicksEntry>();
        }

        /// <summary>
        /// True when this Editor is allowed to record usage. The default is off.
        /// </summary>
        public static bool IsEnabled()
        {
            return EditorPrefs.GetBool(ENABLED_PREF_KEY, false);
        }

        /// <summary>
        /// Turns local usage recording on or off. Existing saved counts are left in place.
        /// </summary>
        /// <param name="enabled">True to record later imports and views.</param>
        public static void SetEnabled(bool enabled)
        {
            EditorPrefs.SetBool(ENABLED_PREF_KEY, enabled);
        }

        /// <summary>
        /// Loads analytics data from EditorPrefs.
        /// </summary>
        private static AnalyticsData LoadAnalytics()
        {
            string json = EditorPrefs.GetString(DATA_PREF_KEY, "{}");
            AnalyticsData data;
            try
            {
                data = JsonUtility.FromJson<AnalyticsData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Analytics] Failed to load analytics: {exception.Message}");
                data = null;
            }

            if (data == null)
            {
                data = new AnalyticsData();
            }

            EnsureLists(data);
            ReadEntries(data);
            RebuildCountsFromEventsWhenMissing(data);
            return data;
        }

        /// <summary>
        /// Saves analytics data to EditorPrefs.
        /// </summary>
        private static void SaveAnalytics(AnalyticsData data)
        {
            // Limit entries to prevent EditorPrefs from getting too large
            if (data.events.Count > __MaxEntries)
            {
                data.events = data.events.OrderByDescending(e => e.timestamp)
                    .Take(__MaxEntries)
                    .ToList();
            }

            try
            {
                WriteEntries(data);
                string json = JsonUtility.ToJson(data);
                EditorPrefs.SetString(DATA_PREF_KEY, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Failed to save analytics: {ex.Message}");
            }
        }

        /// <summary>
        /// Records an analytics event.
        /// </summary>
        /// <param name="eventType">Type of event (e.g., "import", "update", "view").</param>
        /// <param name="modelId">ID of the model.</param>
        /// <param name="modelVersion">Version of the model.</param>
        /// <param name="modelName">Name of the model.</param>
        /// <param name="metadata">Optional metadata dictionary.</param>
        public static void RecordEvent(string eventType, string modelId, string modelVersion = null,
            string modelName = null, Dictionary<string, string> metadata = null)
        {
            if (!IsEnabled())
            {
                return;
            }

            AnalyticsData data = LoadAnalytics();

            AnalyticsEvent evt = new AnalyticsEvent
            {
                eventType = eventType,
                modelId = modelId,
                modelVersion = modelVersion,
                modelName = modelName,
                timestamp = DateTime.UtcNow.Ticks,
                metadata = metadata ?? new Dictionary<string, string>()
            };

            data.events.Add(evt);

            // Update aggregated counts
            if (eventType == IMPORT_EVENT || eventType == UPDATE_EVENT)
            {
                if (!data.modelImportCounts.ContainsKey(modelId))
                {
                    data.modelImportCounts[modelId] = 0;
                }
                data.modelImportCounts[modelId]++;
            }

            if (eventType == VIEW_EVENT)
            {
                if (!data.modelViewCounts.ContainsKey(modelId))
                {
                    data.modelViewCounts[modelId] = 0;
                }
                data.modelViewCounts[modelId]++;
            }

            // Update last accessed time
            data.lastAccessed[modelId] = DateTime.UtcNow;

            SaveAnalytics(data);
        }

        /// <summary>
        /// Gets the import count for a model.
        /// </summary>
        public static int GetImportCount(string modelId)
        {
            AnalyticsData data = LoadAnalytics();
            return data.modelImportCounts.TryGetValue(modelId, out int count) ? count : 0;
        }

        /// <summary>
        /// Gets the view count for a model.
        /// </summary>
        public static int GetViewCount(string modelId)
        {
            AnalyticsData data = LoadAnalytics();
            return data.modelViewCounts.TryGetValue(modelId, out int count) ? count : 0;
        }

        /// <summary>
        /// Gets the most imported models.
        /// </summary>
        public static List<ModelUsageStats> GetMostImportedModels(int count = 10)
        {
            AnalyticsData data = LoadAnalytics();
            return data.modelImportCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => new ModelUsageStats
                {
                    modelId = kvp.Key,
                    importCount = kvp.Value,
                    viewCount = data.modelViewCounts.TryGetValue(kvp.Key, out int views) ? views : 0,
                    lastAccessed = data.lastAccessed.TryGetValue(kvp.Key, out DateTime last) ? last : DateTime.MinValue
                })
                .ToList();
        }

        /// <summary>
        /// Gets the most viewed models.
        /// </summary>
        public static List<ModelUsageStats> GetMostViewedModels(int count = 10)
        {
            AnalyticsData data = LoadAnalytics();
            return data.modelViewCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => new ModelUsageStats
                {
                    modelId = kvp.Key,
                    importCount = data.modelImportCounts.TryGetValue(kvp.Key, out int imports) ? imports : 0,
                    viewCount = kvp.Value,
                    lastAccessed = data.lastAccessed.TryGetValue(kvp.Key, out DateTime last) ? last : DateTime.MinValue
                })
                .ToList();
        }

        /// <summary>
        /// Gets usage statistics for a specific model.
        /// </summary>
        public static ModelUsageStats GetModelStats(string modelId)
        {
            AnalyticsData data = LoadAnalytics();
            return new ModelUsageStats
            {
                modelId = modelId,
                importCount = data.modelImportCounts.TryGetValue(modelId, out int imports) ? imports : 0,
                viewCount = data.modelViewCounts.TryGetValue(modelId, out int views) ? views : 0,
                lastAccessed = data.lastAccessed.TryGetValue(modelId, out DateTime last) ? last : DateTime.MinValue
            };
        }

        /// <summary>
        /// Gets event counts grouped by event type.
        /// </summary>
        public static Dictionary<string, int> GetEventCountsByType()
        {
            AnalyticsData data = LoadAnalytics();
            return data.events
                .GroupBy(e => e.eventType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Gets events within a time range.
        /// </summary>
        public static List<AnalyticsEvent> GetEventsInRange(DateTime startTime, DateTime endTime)
        {
            AnalyticsData data = LoadAnalytics();
            long startTicks = startTime.Ticks;
            long endTicks = endTime.Ticks;

            return data.events
                .Where(e => e.timestamp >= startTicks && e.timestamp <= endTicks)
                .OrderByDescending(e => e.timestamp)
                .ToList();
        }

        /// <summary>
        /// Clears saved usage counts. The opt-in switch is left as the user set it.
        /// </summary>
        public static void ClearAnalytics() => EditorPrefs.DeleteKey(DATA_PREF_KEY);

        private static void EnsureLists(AnalyticsData data)
        {
            if (data.events == null)
            {
                data.events = new List<AnalyticsEvent>();
            }

            if (data.importCountEntries == null)
            {
                data.importCountEntries = new List<StringCountEntry>();
            }

            if (data.viewCountEntries == null)
            {
                data.viewCountEntries = new List<StringCountEntry>();
            }

            if (data.lastAccessedEntries == null)
            {
                data.lastAccessedEntries = new List<StringTicksEntry>();
            }

            for (int i = 0; i < data.events.Count; i++)
            {
                AnalyticsEvent analyticsEvent = data.events[i];
                if (analyticsEvent != null && analyticsEvent.metadataEntries == null)
                {
                    analyticsEvent.metadataEntries = new List<StringPairEntry>();
                }
            }
        }

        private static void ReadEntries(AnalyticsData data)
        {
            data.modelImportCounts = ReadCounts(data.importCountEntries);
            data.modelViewCounts = ReadCounts(data.viewCountEntries);
            data.lastAccessed = ReadTicks(data.lastAccessedEntries);
            for (int i = 0; i < data.events.Count; i++)
            {
                AnalyticsEvent analyticsEvent = data.events[i];
                if (analyticsEvent == null)
                {
                    continue;
                }

                analyticsEvent.metadata = ReadPairs(analyticsEvent.metadataEntries);
            }
        }

        private static void WriteEntries(AnalyticsData data)
        {
            data.importCountEntries = WriteCounts(data.modelImportCounts);
            data.viewCountEntries = WriteCounts(data.modelViewCounts);
            data.lastAccessedEntries = WriteTicks(data.lastAccessed);
            for (int i = 0; i < data.events.Count; i++)
            {
                AnalyticsEvent analyticsEvent = data.events[i];
                if (analyticsEvent == null)
                {
                    continue;
                }

                analyticsEvent.metadataEntries = WritePairs(analyticsEvent.metadata);
            }
        }

        /// <summary>
        /// Older saves stored events only. Dictionaries were dropped by JsonUtility.
        /// </summary>
        private static void RebuildCountsFromEventsWhenMissing(AnalyticsData data)
        {
            if (data.importCountEntries.Count > 0 || data.viewCountEntries.Count > 0 || data.events.Count == 0)
            {
                return;
            }

            for (int i = 0; i < data.events.Count; i++)
            {
                AnalyticsEvent analyticsEvent = data.events[i];
                if (analyticsEvent == null || string.IsNullOrEmpty(analyticsEvent.modelId))
                {
                    continue;
                }

                if (analyticsEvent.eventType == IMPORT_EVENT || analyticsEvent.eventType == UPDATE_EVENT)
                {
                    IncrementCount(data.modelImportCounts, analyticsEvent.modelId);
                }

                if (analyticsEvent.eventType == VIEW_EVENT)
                {
                    IncrementCount(data.modelViewCounts, analyticsEvent.modelId);
                }

                data.lastAccessed[analyticsEvent.modelId] = new DateTime(analyticsEvent.timestamp, DateTimeKind.Utc);
            }

            WriteEntries(data);
        }

        private static void IncrementCount(Dictionary<string, int> counts, string modelId)
        {
            if (!counts.ContainsKey(modelId))
            {
                counts[modelId] = 0;
            }

            counts[modelId]++;
        }

        private static Dictionary<string, int> ReadCounts(List<StringCountEntry> entries)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < entries.Count; i++)
            {
                StringCountEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                counts[entry.key] = entry.count;
            }

            return counts;
        }

        private static List<StringCountEntry> WriteCounts(Dictionary<string, int> counts)
        {
            List<StringCountEntry> entries = new List<StringCountEntry>();
            if (counts == null)
            {
                return entries;
            }

            foreach (KeyValuePair<string, int> pair in counts)
            {
                StringCountEntry entry = new StringCountEntry();
                entry.key = pair.Key;
                entry.count = pair.Value;
                entries.Add(entry);
            }

            return entries;
        }

        private static Dictionary<string, DateTime> ReadTicks(List<StringTicksEntry> entries)
        {
            Dictionary<string, DateTime> times = new Dictionary<string, DateTime>();
            for (int i = 0; i < entries.Count; i++)
            {
                StringTicksEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                times[entry.key] = new DateTime(entry.ticks, DateTimeKind.Utc);
            }

            return times;
        }

        private static List<StringTicksEntry> WriteTicks(Dictionary<string, DateTime> times)
        {
            List<StringTicksEntry> entries = new List<StringTicksEntry>();
            if (times == null)
            {
                return entries;
            }

            foreach (KeyValuePair<string, DateTime> pair in times)
            {
                StringTicksEntry entry = new StringTicksEntry();
                entry.key = pair.Key;
                entry.ticks = pair.Value.Ticks;
                entries.Add(entry);
            }

            return entries;
        }

        private static Dictionary<string, string> ReadPairs(List<StringPairEntry> entries)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            if (entries == null)
            {
                return pairs;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                StringPairEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                pairs[entry.key] = entry.value;
            }

            return pairs;
        }

        private static List<StringPairEntry> WritePairs(Dictionary<string, string> pairs)
        {
            List<StringPairEntry> entries = new List<StringPairEntry>();
            if (pairs == null)
            {
                return entries;
            }

            foreach (KeyValuePair<string, string> pair in pairs)
            {
                StringPairEntry entry = new StringPairEntry();
                entry.key = pair.Key;
                entry.value = pair.Value;
                entries.Add(entry);
            }

            return entries;
        }
    }

    /// <summary>
    /// Usage statistics for a model.
    /// </summary>
    [Serializable]
    internal class ModelUsageStats
    {
        public string modelId;
        public int importCount;
        public int viewCount;
        public DateTime lastAccessed;
    }
}
