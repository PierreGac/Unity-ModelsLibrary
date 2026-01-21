using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelVersionComparisonWindow view implementation for ModelLibraryWindow.
    /// Handles the version comparison view for displaying side-by-side metadata differences between versions.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Initializes version comparison state when navigating to the VersionComparison view.
        /// Sets up the comparison window with the model ID and preferred version for comparison.
        /// </summary>
        public void InitializeVersionComparisonState()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);
            string preferredRightVersion = GetViewParameter<string>("preferredRightVersion", null);

            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            // Store state for comparison
            _versionComparisonModelId = modelId;
            _versionComparisonInitialRightVersion = preferredRightVersion;

            // Create hidden version comparison instance if needed
            if (_versionComparisonInstance == null)
            {
                _versionComparisonInstance = CreateInstance<ModelVersionComparisonWindow>();
            }

            if (_versionComparisonInstance == null)
            {
                Debug.LogError("[ModelLibraryWindow] Failed to create ModelVersionComparisonWindow instance");
                return;
            }

            // Set model identification fields using helper methods
            SetPrivateField(_versionComparisonInstance, "_modelId", modelId);
            SetPrivateField(_versionComparisonInstance, "_initialRightVersion", preferredRightVersion);

            // Call Init method to load versions and initialize comparison
            InvokePrivateMethod(_versionComparisonInstance, "Init");
        }

        /// <summary>
        /// Draws the VersionComparison view.
        /// Displays a side-by-side comparison of two model versions highlighting metadata differences.
        /// </summary>
        private void DrawVersionComparisonView()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);

            // Validate parameters
            if (string.IsNullOrEmpty(modelId))
            {
                UIStyles.DrawPageHeader("Version Comparison", "Compare metadata across two versions.");
                EditorGUILayout.HelpBox("No model selected for comparison. Please select a model from the browser.", MessageType.Warning);
                if (GUILayout.Button("Back to Browser", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    NavigateToView(ViewType.Browser);
                }
                return;
            }

            // Initialize instance if needed or if model changed
            if (_versionComparisonInstance == null || _versionComparisonModelId != modelId)
            {
                InitializeVersionComparisonState();
            }

            // Render the version comparison window using helper method
            RenderEditorWindowInstance(_versionComparisonInstance);
        }
    }
}

