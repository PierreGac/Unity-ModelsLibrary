using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing navigation methods for ModelLibraryWindow.
    /// Handles view navigation and routing within the single-window interface.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Navigates to a specific view with optional parameters.
        /// </summary>
        /// <param name="viewType">The view type to navigate to.</param>
        /// <param name="parameters">Optional parameters for the view (e.g., modelId, version).</param>
        public void NavigateToView(ViewType viewType, Dictionary<string, object> parameters = null)
        {
            // Store current view as previous if not already navigating
            if (_currentView != viewType)
            {
                _previousView = _currentView;
                // Store a copy of current view parameters
                _previousViewParameters.Clear();
                foreach (KeyValuePair<string, object> param in _viewParameters)
                {
                    _previousViewParameters[param.Key] = param.Value;
                }

                // Cache model info when navigating to VersionComparison or Preview3D
                // This allows us to navigate back to details even if we came from browser
                if (viewType == ViewType.VersionComparison || viewType == ViewType.Preview3D)
                {
                    CacheModelInfoForNavigation(parameters);
                }
            }

            _currentView = viewType;

            // Clear previous parameters and set new ones
            _viewParameters.Clear();
            if (parameters != null)
            {
                foreach (KeyValuePair<string, object> param in parameters)
                {
                    _viewParameters[param.Key] = param.Value;
                }
            }

            // Clear inspected model cache when navigating to browser
            if (viewType == ViewType.Browser)
            {
                _inspectedModelId = null;
                _inspectedModelVersion = null;
            }

            // Initialize view state based on view type
            // This ensures each view is properly initialized when navigated to
            InitializeViewState(viewType);

            // Update window title based on view
            UpdateWindowTitleForView();

            Repaint();
        }

        /// <summary>
        /// Navigates back to the previous view, or to Browser if no previous view exists.
        /// Implements special navigation rules:
        /// - From ModelDetails: Always go to Browser
        /// - From VersionComparison: Go to ModelDetails (if modelId cached), otherwise Browser
        /// - From Preview3D: Go to ModelDetails (if modelId cached), otherwise Browser
        /// </summary>
        public void NavigateBack()
        {
            // Special navigation rules based on current view
            if (_currentView == ViewType.ModelDetails)
            {
                // From details window, always go to browser
                NavigateToView(ViewType.Browser);
                return;
            }
            else if (_currentView == ViewType.VersionComparison || _currentView == ViewType.Preview3D)
            {
                // From compare version or 3D preview, go to details window if modelId is cached
                if (!string.IsNullOrEmpty(_inspectedModelId) && !string.IsNullOrEmpty(_inspectedModelVersion))
                {
                    Dictionary<string, object> detailsParams = new Dictionary<string, object>
                    {
                        { "modelId", _inspectedModelId },
                        { "version", _inspectedModelVersion }
                    };
                    NavigateToView(ViewType.ModelDetails, detailsParams);
                    return;
                }
                else
                {
                    // No cached model, go to browser
                    NavigateToView(ViewType.Browser);
                    return;
                }
            }

            // Default behavior: use stored previous view
            if (_previousView.HasValue)
            {
                ViewType previous = _previousView.Value;
                // Create a copy of the previous view parameters
                Dictionary<string, object> previousParams = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> param in _previousViewParameters)
                {
                    previousParams[param.Key] = param.Value;
                }
                _previousView = null;
                _previousViewParameters.Clear();
                NavigateToView(previous, previousParams);
            }
            else
            {
                NavigateToView(ViewType.Browser);
            }
        }

        /// <summary>
        /// Gets a view parameter value by key.
        /// </summary>
        /// <typeparam name="T">The type of the parameter value.</typeparam>
        /// <param name="key">The parameter key.</param>
        /// <param name="defaultValue">Default value if parameter is not found.</param>
        /// <returns>The parameter value or default if not found.</returns>
        public T GetViewParameter<T>(string key, T defaultValue = default(T))
        {
            if (_viewParameters.TryGetValue(key, out object value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Checks if a view parameter exists.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <returns>True if the parameter exists, false otherwise.</returns>
        public bool HasViewParameter(string key)
        {
            return _viewParameters.ContainsKey(key);
        }

        /// <summary>
        /// Gets the current view type.
        /// </summary>
        /// <returns>The current view type.</returns>
        public ViewType GetCurrentView()
        {
            return _currentView;
        }

        /// <summary>
        /// Updates the window title based on the current view.
        /// </summary>
        private void UpdateWindowTitleForView()
        {
            string title = "Model Library";
            switch (_currentView)
            {
                case ViewType.Browser:
                    title = "Model Library";
                    break;
                case ViewType.FirstRunWizard:
                    title = "Model Library - Setup";
                    break;
                case ViewType.Submit:
                    title = "Model Library - Submit Model";
                    break;
                case ViewType.ModelDetails:
                    string modelId = GetViewParameter<string>("modelId", string.Empty);
                    if (!string.IsNullOrEmpty(modelId))
                    {
                        title = $"Model Library - {modelId}";
                    }
                    else
                    {
                        title = "Model Library - Model Details";
                    }
                    break;
                case ViewType.Help:
                    title = "Model Library - Help";
                    break;
                case ViewType.Shortcuts:
                    title = "Model Library - Keyboard Shortcuts";
                    break;
                case ViewType.Settings:
                    title = "Model Library - Settings";
                    break;
                case ViewType.ErrorLog:
                    title = "Model Library - Error Log";
                    break;
                case ViewType.PerformanceProfiler:
                    title = "Model Library - Performance Profiler";
                    break;
                case ViewType.Analytics:
                    title = "Model Library - Analytics";
                    break;
                case ViewType.BulkTag:
                    title = "Model Library - Bulk Tag Editor";
                    break;
                case ViewType.BatchUpload:
                    title = "Model Library - Batch Upload";
                    break;
                case ViewType.VersionComparison:
                    title = "Model Library - Version Comparison";
                    break;
                case ViewType.Preview3D:
                    title = "Model Library - 3D Preview";
                    break;
            }
            titleContent.text = title;
        }

        /// <summary>
        /// Caches model information when navigating to VersionComparison or Preview3D views.
        /// This information is used to navigate back to the details view.
        /// </summary>
        /// <param name="parameters">The parameters being passed to the new view.</param>
        private void CacheModelInfoForNavigation(Dictionary<string, object> parameters)
        {
            // First check if we're coming from ModelDetails (prefer that - has both modelId and version)
            if (_currentView == ViewType.ModelDetails)
            {
                string modelId = GetViewParameter<string>("modelId", string.Empty);
                string version = GetViewParameter<string>("version", string.Empty);
                if (!string.IsNullOrEmpty(modelId) && !string.IsNullOrEmpty(version))
                {
                    _inspectedModelId = modelId;
                    _inspectedModelVersion = version;
                }
            }
            // Otherwise, use the parameters passed to the new view
            else if (parameters != null)
            {
                string modelId = parameters.ContainsKey("modelId") ? parameters["modelId"] as string : null;
                if (!string.IsNullOrEmpty(modelId))
                {
                    _inspectedModelId = modelId;
                    // Try to get version from parameters (check both "version" and "preferredRightVersion")
                    string version = parameters.ContainsKey("version") ? parameters["version"] as string : null;
                    if (string.IsNullOrEmpty(version) && parameters.ContainsKey("preferredRightVersion"))
                    {
                        version = parameters["preferredRightVersion"] as string;
                    }
                    if (!string.IsNullOrEmpty(version))
                    {
                        _inspectedModelVersion = version;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the state for a specific view type.
        /// Calls the appropriate initialization method based on the view type.
        /// Each view has its own initialization logic to set up state, services, and UI components.
        /// </summary>
        /// <param name="viewType">The view type to initialize.</param>
        private void InitializeViewState(ViewType viewType)
        {
            switch (viewType)
            {
                case ViewType.Help:
                    InitializeHelpState();
                    break;
                case ViewType.FirstRunWizard:
                    InitializeWizardState();
                    break;
                case ViewType.Submit:
                    InitializeSubmitState();
                    break;
                case ViewType.BatchUpload:
                    InitializeBatchUploadState();
                    break;
                case ViewType.Settings:
                    InitializeSettingsState();
                    break;
                case ViewType.ModelDetails:
                    InitializeDetailsState();
                    break;
                case ViewType.VersionComparison:
                    InitializeVersionComparisonState();
                    break;
                case ViewType.Analytics:
                    InitializeAnalyticsState();
                    break;
                case ViewType.ErrorLog:
                    InitializeErrorLogState();
                    break;
                case ViewType.PerformanceProfiler:
                    InitializePerformanceProfilerState();
                    break;
                case ViewType.Preview3D:
                    InitializePreview3DState();
                    break;
                // Browser, Shortcuts, BulkTag, VersionComparison don't need explicit initialization
                // or are handled elsewhere
            }
        }

        /// <summary>
        /// Draws the "Previous" button in the toolbar area when not on the Browser view.
        /// The button appears at the top of the window and allows navigation back to the previous view.
        /// </summary>
        private void DrawPreviousButton()
        {
            if (_currentView == ViewType.Browser)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUIContent previousContent = new GUIContent("← Previous", "Go back to the previous screen");
                if (GUILayout.Button(previousContent, EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    NavigateBack();
                }
                GUILayout.FlexibleSpace();
            }
        }
    }
}

