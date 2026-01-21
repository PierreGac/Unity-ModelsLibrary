using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelDetailsWindow view implementation for ModelLibraryWindow.
    /// Handles the model details view for displaying comprehensive metadata, editing, and management.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Hidden ModelDetailsWindow instance for reuse.
        /// This instance is created once and reused, but reinitialized when viewing different models.
        /// </summary>
        private ModelDetailsWindow _detailsWindowInstance;

        /// <summary>
        /// Initializes details state when navigating to the ModelDetails view.
        /// Marks notifications as read and initializes the details window instance.
        /// </summary>
        public void InitializeDetailsState()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);
            string version = GetViewParameter<string>("version", string.Empty);

            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                return;
            }

            // Mark notifications as read when details are opened
            NotificationStateManager.MarkNotesAsRead(modelId, version);
            NotificationStateManager.MarkUpdateAsRead(modelId);

            // Initialize the instance with the model information
            InitializeDetailsInstance(modelId, version);
        }

        /// <summary>
        /// Initializes the details window instance with model ID and version.
        /// Uses reflection to set private fields and initialize services.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model to display.</param>
        /// <param name="version">The version of the model to display.</param>
        private void InitializeDetailsInstance(string modelId, string version)
        {
            // Create instance if needed
            if (_detailsWindowInstance == null)
            {
                _detailsWindowInstance = CreateInstance<ModelDetailsWindow>();
            }

            if (_detailsWindowInstance == null)
            {
                Debug.LogError("[ModelLibraryWindow] Failed to create ModelDetailsWindow instance");
                return;
            }

            // Set model identification fields using helper methods
            SetPrivateField(_detailsWindowInstance, "_modelId", modelId);
            SetPrivateField(_detailsWindowInstance, "_version", version);

            // Initialize services
            IModelRepository repo = RepositoryFactory.CreateRepository();
            SetPrivateField(_detailsWindowInstance, "_service", new ModelLibraryService(repo));

            // Call Init method to load metadata and initialize editing state
            InvokePrivateMethod(_detailsWindowInstance, "Init");
        }

        /// <summary>
        /// Draws the ModelDetails view.
        /// Displays comprehensive model information including description, tags, structure, changelog, and notes.
        /// Allows Artists to edit metadata and delete versions.
        /// </summary>
        private void DrawModelDetailsView()
        {
            string modelId = GetViewParameter<string>("modelId", string.Empty);
            string version = GetViewParameter<string>("version", string.Empty);

            // Validate parameters
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                UIStyles.DrawPageHeader("Model Details", "Review metadata, notes, and changelog.");
                EditorGUILayout.HelpBox("No model selected. Please select a model from the browser.", MessageType.Warning);
                if (GUILayout.Button("Back to Browser", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    NavigateToView(ViewType.Browser);
                }
                return;
            }

            // Initialize instance if needed
            if (_detailsWindowInstance == null)
            {
                InitializeDetailsInstance(modelId, version);
            }
            else
            {
                // Check if we need to update the instance for a different model
                string currentModelId = GetPrivateField<string>(_detailsWindowInstance, "_modelId", string.Empty);
                string currentVersion = GetPrivateField<string>(_detailsWindowInstance, "_version", string.Empty);

                if (currentModelId != modelId || currentVersion != version)
                {
                    // Reinitialize for different model
                    InitializeDetailsInstance(modelId, version);
                }
            }

            // Render the details window using helper method
            RenderEditorWindowInstance(_detailsWindowInstance);
        }
    }
}

