using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelPreview3DWindow view implementation for ModelLibraryWindow.
    /// Handles the 3D preview view for interactive model visualization with rotation, zoom, and inspection.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Hidden ModelPreview3DWindow instance for reuse.
        /// This instance is created once and reused, but reinitialized when previewing different models.
        /// </summary>
        private ModelPreview3DWindow _preview3DInstance;

        /// <summary>
        /// Initializes preview 3D state when navigating to the Preview3D view.
        /// Sets up the preview window with model ID, version, and loading state.
        /// </summary>
        public void InitializePreview3DState()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);
            string version = GetViewParameter<string>("version", string.Empty);

            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                return;
            }

            // Create hidden preview 3D instance if needed
            if (_preview3DInstance == null)
            {
                _preview3DInstance = CreateInstance<ModelPreview3DWindow>();
            }

            if (_preview3DInstance == null)
            {
                Debug.LogError("[ModelLibraryWindow] Failed to create ModelPreview3DWindow instance");
                return;
            }

            // Set model identification fields using helper methods
            SetPrivateField(_preview3DInstance, "_modelId", modelId);
            SetPrivateField(_preview3DInstance, "_version", version);

            // Set loading state to true before starting to load (prevents "No meshes found" message)
            SetPrivateField(_preview3DInstance, "_isLoading", true);

            // Reset service, meta, and meshes for new model
            SetPrivateField(_preview3DInstance, "_service", null);
            SetPrivateField(_preview3DInstance, "_meta", null);

            // Clear meshes collection
            object meshes = GetPrivateField<object>(_preview3DInstance, "_meshes", null);
            if (meshes != null && meshes is System.Collections.ICollection collection)
            {
                System.Reflection.MethodInfo clearMethod = meshes.GetType().GetMethod("Clear");
                if (clearMethod != null)
                {
                    clearMethod.Invoke(meshes, null);
                }
            }

            // Call OnEnable to initialize preview utility (creates PreviewUtility3D)
            InvokePrivateMethod(_preview3DInstance, "OnEnable");

            // Call LoadModelAsync to start loading the model (this will set _isLoading to false when done)
            InvokePrivateMethod(_preview3DInstance, "LoadModelAsync");
        }

        /// <summary>
        /// Draws the Preview3D view.
        /// Displays an interactive 3D preview with camera controls for rotating and zooming.
        /// Shows loading indicator while the model is being loaded.
        /// </summary>
        private void DrawPreview3DView()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);
            string version = GetViewParameter<string>("version", string.Empty);

            // Validate parameters
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                UIStyles.DrawPageHeader("3D Preview", "Inspect model geometry and materials.");
                EditorGUILayout.HelpBox("No model selected for preview. Please select a model from the browser.", MessageType.Warning);
                if (GUILayout.Button("Back to Browser", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    NavigateToView(ViewType.Browser);
                }
                return;
            }

            // Initialize instance if needed
            if (_preview3DInstance == null)
            {
                InitializePreview3DState();
            }
            else
            {
                // Check if we need to update the instance for a different model
                string currentModelId = GetPrivateField<string>(_preview3DInstance, "_modelId", string.Empty);
                string currentVersion = GetPrivateField<string>(_preview3DInstance, "_version", string.Empty);

                if (currentModelId != modelId || currentVersion != version)
                {
                    // Set loading state before initializing for new model
                    SetPrivateField(_preview3DInstance, "_isLoading", true);
                    InitializePreview3DState();
                }
            }

            // Render the preview 3D window using helper method
            RenderEditorWindowInstance(_preview3DInstance);
        }
    }
}

