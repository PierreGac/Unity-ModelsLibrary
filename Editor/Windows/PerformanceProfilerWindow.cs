using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public class PerformanceProfilerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private double _lastRefresh;
        private const double REFRESH_INTERVAL = 1.0;

        public static void Open()
        {
            PerformanceProfilerWindow window = GetWindow<PerformanceProfilerWindow>("Model Library Performance");
            window.minSize = new Vector2(500f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            _lastRefresh = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        private void OnEditorUpdate()
        {
            if (!AsyncProfiler.Enabled)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRefresh > REFRESH_INTERVAL)
            {
                Repaint();
                _lastRefresh = now;
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(6f);
            DrawToolbar();
            GUILayout.Space(6f);

            if (!AsyncProfiler.Enabled)
            {
                EditorGUILayout.HelpBox("Async profiling is disabled. Enable it to start recording metrics.", MessageType.Info);
                return;
            }

            IReadOnlyList<AsyncProfiler.ProfileSnapshot> samples = AsyncProfiler.GetSnapshots();
            if (samples.Count == 0)
            {
                EditorGUILayout.HelpBox("No samples recorded yet. Interact with the Model Library to generate data.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawHeaderRow();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < samples.Count; i++)
                {
                    DrawSampleRow(samples[i]);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool enabled = AsyncProfiler.Enabled;
                bool newEnabled = GUILayout.Toggle(enabled, new GUIContent("Profiling", "Toggle async profiling for repository/service operations."), EditorStyles.toolbarButton);
                if (newEnabled != enabled)
                {
                    AsyncProfiler.Enabled = newEnabled;
                }

                GUILayout.Space(8f);

                double threshold = AsyncProfiler.WarningThresholdMs;
                GUILayout.Label(new GUIContent("Warn ≥", "Log a warning when an operation exceeds this duration (ms)."), EditorStyles.miniLabel, GUILayout.Width(50f));
                double newThreshold = EditorGUILayout.DoubleField(threshold, GUILayout.Width(60f));
                if (Math.Abs(newThreshold - threshold) > double.Epsilon)
                {
                    AsyncProfiler.WarningThresholdMs = Math.Max(1d, newThreshold);
                }

                GUILayout.Label("ms", EditorStyles.miniLabel, GUILayout.Width(20f));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!AsyncProfiler.Enabled))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Clear all recorded samples."), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    {
                        AsyncProfiler.Clear();
                    }
                }
            }
        }

        private static void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Operation", EditorStyles.boldLabel, GUILayout.Width(220f));
                GUILayout.Label("Count", EditorStyles.boldLabel, GUILayout.Width(50f));
                GUILayout.Label("Last (ms)", EditorStyles.boldLabel, GUILayout.Width(70f));
                GUILayout.Label("Avg (ms)", EditorStyles.boldLabel, GUILayout.Width(70f));
                GUILayout.Label("Max (ms)", EditorStyles.boldLabel, GUILayout.Width(70f));
                GUILayout.Label("Min (ms)", EditorStyles.boldLabel, GUILayout.Width(70f));
                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawSampleRow(AsyncProfiler.ProfileSnapshot sample)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(sample.OperationName, GUILayout.Width(220f));
                GUILayout.Label(sample.Count.ToString(), GUILayout.Width(50f));
                GUILayout.Label(sample.LastDurationMs.ToString("F1"), GUILayout.Width(70f));
                GUILayout.Label(sample.AverageDurationMs.ToString("F1"), GUILayout.Width(70f));
                GUILayout.Label(sample.LongestDurationMs.ToString("F1"), GUILayout.Width(70f));
                GUILayout.Label(sample.ShortestDurationMs.ToString("F1"), GUILayout.Width(70f));
                GUILayout.FlexibleSpace();
            }
        }
    }
}
