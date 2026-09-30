using System.Collections.Generic;
using System.IO;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Windows;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Embedded editor windows are destroyed when the library navigates away or closes.
    /// </summary>
    public class EmbeddedEditorViewTests
    {
        private const string USER_NAME_PREF = "ModelLibrary.UserName";
        private const string ANONYMOUS_USER = "anonymous";
        private const string MODEL_ID = "lifecycle-model";
        private const string MODEL_VERSION = "1.0.0";
        private const string REPOSITORY_FOLDER_NAME = "ModelLibraryEmbeddedViewRepo";
        private const int EXTRA_INSTANCE = 1;

        private bool _hadUser;
        private string _savedUser;
        private ModelLibrarySettings.RepositoryKind _savedKind;
        private string _savedRoot;
        private string _repositoryRoot;

        /// <summary>
        /// Points the library at an empty local folder and keeps the browser on the setup wizard.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _hadUser = EditorPrefs.HasKey(USER_NAME_PREF);
            _savedUser = EditorPrefs.GetString(USER_NAME_PREF, string.Empty);
            EditorPrefs.SetString(USER_NAME_PREF, ANONYMOUS_USER);

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            _savedKind = settings.repositoryKind;
            _savedRoot = settings.repositoryRoot;
            _repositoryRoot = Path.Combine(Path.GetTempPath(), REPOSITORY_FOLDER_NAME);
            Directory.CreateDirectory(_repositoryRoot);
            settings.repositoryKind = ModelLibrarySettings.RepositoryKind.FileSystem;
            settings.repositoryRoot = _repositoryRoot;
            settings.SaveProjectCopy();
        }

        /// <summary>
        /// Restores the project repository and the user name.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryKind = _savedKind;
            settings.repositoryRoot = _savedRoot;
            settings.SaveProjectCopy();

            if (Directory.Exists(_repositoryRoot))
            {
                Directory.Delete(_repositoryRoot, true);
            }

            if (_hadUser)
            {
                EditorPrefs.SetString(USER_NAME_PREF, _savedUser);
            }
            else
            {
                EditorPrefs.DeleteKey(USER_NAME_PREF);
            }
        }

        /// <summary>
        /// Opening each embedded view, leaving it, and closing the library leaves no hidden window or update subscription.
        /// </summary>
        [Test]
        public void NavigateAndClose_ReleasesEmbeddedWindows()
        {
            int profilerSubs = PerformanceProfilerWindow.ActiveUpdateSubscriptions;
            int previewSubs = ModelPreview3DWindow.ActiveUpdateSubscriptions;
            int profilers = CountWindows<PerformanceProfilerWindow>();
            int analytics = CountWindows<AnalyticsWindow>();
            int errorLogs = CountWindows<ErrorLogViewerWindow>();
            int submits = CountWindows<ModelSubmitWindow>();
            int details = CountWindows<ModelDetailsWindow>();
            int comparisons = CountWindows<ModelVersionComparisonWindow>();
            int previews = CountWindows<ModelPreview3DWindow>();

            ModelLibraryWindow window = ScriptableObject.CreateInstance<ModelLibraryWindow>();
            try
            {
                Dictionary<string, object> modelParameters = new Dictionary<string, object>();
                modelParameters["modelId"] = MODEL_ID;
                modelParameters["version"] = MODEL_VERSION;

                LeaveView(window, ModelLibraryWindow.ViewType.PerformanceProfiler, null, profilers, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.Analytics, null, analytics, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.ErrorLog, null, errorLogs, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.Submit, null, submits, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.ModelDetails, modelParameters, details, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.VersionComparison, modelParameters, comparisons, EXTRA_INSTANCE);
                LeaveView(window, ModelLibraryWindow.ViewType.Preview3D, modelParameters, previews, EXTRA_INSTANCE);

                Assert.AreEqual(profilerSubs, PerformanceProfilerWindow.ActiveUpdateSubscriptions);
                Assert.AreEqual(previewSubs, ModelPreview3DWindow.ActiveUpdateSubscriptions);
                Assert.AreEqual(profilers, CountWindows<PerformanceProfilerWindow>());
                Assert.AreEqual(analytics, CountWindows<AnalyticsWindow>());
                Assert.AreEqual(errorLogs, CountWindows<ErrorLogViewerWindow>());
                Assert.AreEqual(submits, CountWindows<ModelSubmitWindow>());
                Assert.AreEqual(details, CountWindows<ModelDetailsWindow>());
                Assert.AreEqual(comparisons, CountWindows<ModelVersionComparisonWindow>());
                Assert.AreEqual(previews, CountWindows<ModelPreview3DWindow>());

                window.NavigateToView(ModelLibraryWindow.ViewType.PerformanceProfiler);
                Assert.AreEqual(profilerSubs + EXTRA_INSTANCE, PerformanceProfilerWindow.ActiveUpdateSubscriptions);
                UnityEngine.Object.DestroyImmediate(window);
                window = null;

                Assert.AreEqual(profilerSubs, PerformanceProfilerWindow.ActiveUpdateSubscriptions);
                Assert.AreEqual(previewSubs, ModelPreview3DWindow.ActiveUpdateSubscriptions);
                Assert.AreEqual(profilers, CountWindows<PerformanceProfilerWindow>());
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        private static void LeaveView(ModelLibraryWindow window, ModelLibraryWindow.ViewType viewType, Dictionary<string, object> parameters, int countBefore, int extraInstances)
        {
            window.NavigateToView(viewType, parameters);
            Assert.AreEqual(countBefore + extraInstances, CountFor(viewType));
            window.NavigateToView(ModelLibraryWindow.ViewType.Browser);
            Assert.AreEqual(countBefore, CountFor(viewType));
        }

        private static int CountFor(ModelLibraryWindow.ViewType viewType)
        {
            switch (viewType)
            {
                case ModelLibraryWindow.ViewType.PerformanceProfiler:
                    return CountWindows<PerformanceProfilerWindow>();
                case ModelLibraryWindow.ViewType.Analytics:
                    return CountWindows<AnalyticsWindow>();
                case ModelLibraryWindow.ViewType.ErrorLog:
                    return CountWindows<ErrorLogViewerWindow>();
                case ModelLibraryWindow.ViewType.Submit:
                    return CountWindows<ModelSubmitWindow>();
                case ModelLibraryWindow.ViewType.ModelDetails:
                    return CountWindows<ModelDetailsWindow>();
                case ModelLibraryWindow.ViewType.VersionComparison:
                    return CountWindows<ModelVersionComparisonWindow>();
                case ModelLibraryWindow.ViewType.Preview3D:
                    return CountWindows<ModelPreview3DWindow>();
                default:
                    return 0;
            }
        }

        private static int CountWindows<T>() where T : UnityEngine.Object
        {
            T[] found = Resources.FindObjectsOfTypeAll<T>();
            return found.Length;
        }
    }
}
