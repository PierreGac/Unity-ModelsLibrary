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
    public class AnalyticsService
    {
        /// <summary>EditorPrefs key for storing analytics data.</summary>
        private const string __AnalyticsPrefKey = "ModelLibrary.Analytics";
        /// <summary>Maximum number of analytics entries to keep.</summary>
        private const int __MaxEntries = 10000;

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
            public Dictionary<string, string> metadata;
        }

        /// <summary>
        /// Analytics data container.
        /// </summary>
        [Serializable]
        private class AnalyticsData
        {
            public List<AnalyticsEvent> events = new List<AnalyticsEvent>();
            public Dictionary<string, int> modelImportCounts = new Dictionary<string, int>();
            public Dictionary<string, int> modelViewCounts = new Dictionary<string, int>();
            public Dictionary<string, DateTime> lastAccessed = new Dictionary<string, DateTime>();
        }

        /// <summary>
        /// Loads analytics data from EditorPrefs.
        /// </summary>
        private static AnalyticsData LoadAnalytics()
        {
            string json = EditorPrefs.GetString(__AnalyticsPrefKey, "{}");
            try
            {
                AnalyticsData data = JsonUtility.FromJson<AnalyticsData>(json);
                return data ?? new AnalyticsData();
            }
            catch
            {
                return new AnalyticsData();
            }
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
                string json = JsonUtility.ToJson(data);
                EditorPrefs.SetString(__AnalyticsPrefKey, json);
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
            if (eventType == "import" || eventType == "update")
            {
                if (!data.modelImportCounts.ContainsKey(modelId))
                {
                    data.modelImportCounts[modelId] = 0;
                }
                data.modelImportCounts[modelId]++;
            }

            if (eventType == "view")
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
        /// Clears all analytics data.
        /// </summary>
        public static void ClearAnalytics() => EditorPrefs.DeleteKey(__AnalyticsPrefKey);
    }

    /// <summary>
    /// Usage statistics for a model.
    /// </summary>
    [Serializable]
    public class ModelUsageStats
    {
        public string modelId;
        public int importCount;
        public int viewCount;
        public DateTime lastAccessed;
    }
}

