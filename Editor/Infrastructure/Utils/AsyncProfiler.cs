using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Lightweight async profiling helper that records timing information for awaited operations.
    /// Metrics can be inspected via the Performance Profiler window.
    /// </summary>
    internal static class AsyncProfiler
    {
        private const string PREF_KEY_ENABLED = "ModelLibrary.AsyncProfiler.Enabled";
        private const string PREF_KEY_THRESHOLD = "ModelLibrary.AsyncProfiler.WarningThresholdMs";

        private static readonly ConcurrentDictionary<string, ProfileSample> __samples = new ConcurrentDictionary<string, ProfileSample>(StringComparer.OrdinalIgnoreCase);

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PREF_KEY_ENABLED, false);
            set
            {
                if (value == Enabled)
                {
                    return;
                }

                EditorPrefs.SetBool(PREF_KEY_ENABLED, value);
                if (!value)
                {
                    Clear();
                }
            }
        }

        public static double WarningThresholdMs
        {
            get => EditorPrefs.GetFloat(PREF_KEY_THRESHOLD, 250f);
            set => EditorPrefs.SetFloat(PREF_KEY_THRESHOLD, Mathf.Max(1f, (float)value));
        }

        public static async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action)
        {
            if (!Enabled)
            {
                return await action();
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                return await action();
            }
            finally
            {
                sw.Stop();
                Record(operationName, sw.Elapsed.TotalMilliseconds);
            }
        }

        public static async Task MeasureAsync(string operationName, Func<Task> action)
        {
            if (!Enabled)
            {
                await action();
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await action();
            }
            finally
            {
                sw.Stop();
                Record(operationName, sw.Elapsed.TotalMilliseconds);
            }
        }

        public static void Record(string operationName, double durationMs)
        {
            if (!Enabled)
            {
                return;
            }

            ProfileSample updated = __samples.AddOrUpdate(
                operationName,
                _ => new ProfileSample(durationMs),
                (_, existing) => existing.WithSample(durationMs));

            if (durationMs >= WarningThresholdMs)
            {
                UnityEngine.Debug.LogWarning($"[ModelLibrary][AsyncProfiler] '{operationName}' took {durationMs:F1} ms (warning threshold {WarningThresholdMs:F0} ms)");
            }
        }

        public static void Clear() => __samples.Clear();

        public static IReadOnlyList<ProfileSnapshot> GetSnapshots()
        {
            List<ProfileSnapshot> list = new List<ProfileSnapshot>(__samples.Count);
            foreach (KeyValuePair<string, ProfileSample> kvp in __samples)
            {
                list.Add(new ProfileSnapshot(kvp.Key, kvp.Value));
            }

            list.Sort((a, b) => b.LastDurationMs.CompareTo(a.LastDurationMs));
            return list;
        }

        internal readonly struct ProfileSample
        {
            public readonly int Count;
            public readonly double TotalMs;
            public readonly double LongestMs;
            public readonly double ShortestMs;
            public readonly double LastMs;

            public ProfileSample(double firstSample)
            {
                Count = 1;
                TotalMs = firstSample;
                LongestMs = firstSample;
                ShortestMs = firstSample;
                LastMs = firstSample;
            }

            private ProfileSample(int count, double totalMs, double longestMs, double shortestMs, double lastMs)
            {
                Count = count;
                TotalMs = totalMs;
                LongestMs = longestMs;
                ShortestMs = shortestMs;
                LastMs = lastMs;
            }

            public ProfileSample WithSample(double durationMs)
            {
                double longest = Math.Max(LongestMs, durationMs);
                double shortest = Count == 0 ? durationMs : Math.Min(ShortestMs, durationMs);
                return new ProfileSample(Count + 1, TotalMs + durationMs, longest, shortest, durationMs);
            }

            public double AverageMs => Count == 0 ? 0 : TotalMs / Count;
        }

        public readonly struct ProfileSnapshot
        {
            public string OperationName { get; }
            public int Count { get; }
            public double TotalDurationMs { get; }
            public double AverageDurationMs { get; }
            public double LongestDurationMs { get; }
            public double ShortestDurationMs { get; }
            public double LastDurationMs { get; }

            public ProfileSnapshot(string operationName, ProfileSample sample)
            {
                OperationName = operationName;
                Count = sample.Count;
                TotalDurationMs = sample.TotalMs;
                AverageDurationMs = sample.AverageMs;
                LongestDurationMs = sample.LongestMs;
                ShortestDurationMs = sample.ShortestMs;
                LastDurationMs = sample.LastMs;
            }
        }
    }
}
