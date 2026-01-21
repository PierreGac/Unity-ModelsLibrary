using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public class PerformanceProfilerWindow : EditorWindow
    {
        private const float __COLUMN_OPERATION_WIDTH = 220f;
        private const float __COLUMN_COUNT_WIDTH = 50f;
        private const float __COLUMN_VALUE_WIDTH = 70f;
        private const float __LABEL_WARN_WIDTH = 50f;
        private const float __FIELD_THRESHOLD_WIDTH = 60f;
        private const float __LABEL_UNIT_WIDTH = 20f;
        private const float __BUTTON_CLEAR_WIDTH = 60f;
        private Vector2 _scroll;
        private double _lastRefresh;
        private const double REFRESH_INTERVAL = 1.0;

        /// <summary>
        /// Opens the performance profiler window.
        /// Now navigates to the PerformanceProfiler view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            // Navigate to PerformanceProfiler view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.PerformanceProfiler);
            }
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
            GUILayout.Space(UIConstants.SPACING_SMALL);
            UIStyles.DrawPageHeader("Performance Profiler", "Track async operation timings and warnings.");
            DrawToolbar();
            GUILayout.Space(UIConstants.SPACING_SMALL);

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

            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
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
                bool newEnabled = GUILayout.Toggle(enabled, new GUIContent("Profiling", "Toggle async profiling for repository/service operations."), UIStyles.ToolbarButton);
                if (newEnabled != enabled)
                {
                    AsyncProfiler.Enabled = newEnabled;
                }

                GUILayout.Space(UIConstants.SPACING_STANDARD);

                double threshold = AsyncProfiler.WarningThresholdMs;
                GUILayout.Label(new GUIContent("Warn ≥", "Log a warning when an operation exceeds this duration (ms)."), UIStyles.MutedLabel, GUILayout.Width(__LABEL_WARN_WIDTH));
                double newThreshold = EditorGUILayout.DoubleField(threshold, GUILayout.Width(__FIELD_THRESHOLD_WIDTH));
                if (Math.Abs(newThreshold - threshold) > double.Epsilon)
                {
                    AsyncProfiler.WarningThresholdMs = Math.Max(1d, newThreshold);
                }

                GUILayout.Label("ms", UIStyles.MutedLabel, GUILayout.Width(__LABEL_UNIT_WIDTH));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!AsyncProfiler.Enabled))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Clear all recorded samples."), UIStyles.ToolbarButton, GUILayout.Width(__BUTTON_CLEAR_WIDTH)))
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
                GUILayout.Label("Operation", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_OPERATION_WIDTH));
                GUILayout.Label("Count", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_COUNT_WIDTH));
                GUILayout.Label("Last (ms)", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label("Avg (ms)", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label("Max (ms)", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label("Min (ms)", UIStyles.SectionHeader, GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawSampleRow(AsyncProfiler.ProfileSnapshot sample)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(sample.OperationName, GUILayout.Width(__COLUMN_OPERATION_WIDTH));
                GUILayout.Label(sample.Count.ToString(), GUILayout.Width(__COLUMN_COUNT_WIDTH));
                GUILayout.Label(sample.LastDurationMs.ToString("F1"), GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label(sample.AverageDurationMs.ToString("F1"), GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label(sample.LongestDurationMs.ToString("F1"), GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.Label(sample.ShortestDurationMs.ToString("F1"), GUILayout.Width(__COLUMN_VALUE_WIDTH));
                GUILayout.FlexibleSpace();
            }
        }
    }
}
