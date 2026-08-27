using System.Collections.Generic;
using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing ModelSubmitWindow view implementation for ModelLibraryWindow.
    /// Handles the submission form view for creating new models or updating existing ones.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Navigates to the Submit view with a fresh form.
        /// Use this entry point from context menus and browser actions.
        /// </summary>
        /// <param name="resolveMeshDependencies">
        /// When true, automatically resolves and adds FBX/OBJ mesh dependencies from the current selection.
        /// </param>
        /// <param name="selectedAssetGuids">
        /// Optional asset GUIDs captured at entry time (e.g. from a context menu click before focus changes).
        /// </param>
        public void NavigateToSubmitView(bool resolveMeshDependencies = false, string[] selectedAssetGuids = null)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            if (resolveMeshDependencies)
            {
                parameters[__RESOLVE_MESH_DEPENDENCIES_PARAM] = true;
            }

            if (selectedAssetGuids != null && selectedAssetGuids.Length > 0)
            {
                parameters[__SUBMIT_SELECTION_GUIDS_PARAM] = selectedAssetGuids;
            }

            NavigateToView(ViewType.Submit, parameters.Count > 0 ? parameters : null);
        }

        /// <summary>
        /// Navigates to the Submit view already in Update Existing mode for a catalog model.
        /// Does not pre-populate assets from the Project selection.
        /// </summary>
        /// <param name="modelId">Catalog model id to pre-select. When empty, Update Existing opens with the searchable picker.</param>
        public void NavigateToSubmitViewForUpdate(string modelId)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { __SUBMIT_MODE_PARAM, ModelSubmitWindow.SubmitMode.Update }
            };
            if (!string.IsNullOrEmpty(modelId))
            {
                parameters[__SUBMIT_UPDATE_MODEL_ID_PARAM] = modelId;
            }

            NavigateToView(ViewType.Submit, parameters);
        }

        /// <summary>
        /// Initializes submit state when navigating to the Submit view.
        /// Always schedules a full form reset before the next draw.
        /// </summary>
        public void InitializeSubmitState()
        {
            _resetSubmitFormOnNextDraw = true;
            _resolveMeshDependenciesOnNextDraw = GetViewParameter<bool>(__RESOLVE_MESH_DEPENDENCIES_PARAM, false);
            _submitSelectionGuidsOnNextDraw = GetViewParameter<string[]>(__SUBMIT_SELECTION_GUIDS_PARAM, null);
            _submitModeOnNextDraw = GetViewParameter<ModelSubmitWindow.SubmitMode>(
                __SUBMIT_MODE_PARAM,
                ModelSubmitWindow.SubmitMode.New);
            _submitUpdateModelIdOnNextDraw = GetViewParameter<string>(__SUBMIT_UPDATE_MODEL_ID_PARAM, null);
        }

        /// <summary>
        /// Draws the Submit view.
        /// Uses a hidden ModelSubmitWindow instance to render the full submission form.
        /// </summary>
        private void DrawSubmitView()
        {
            bool needsResetAfterCreate = _submitWindowInstance == null && _resetSubmitFormOnNextDraw;

            if (_submitWindowInstance != null && _resetSubmitFormOnNextDraw)
            {
                ApplySubmitFormReset();
            }

            DrawEditorWindowView(ref _submitWindowInstance);

            if (needsResetAfterCreate && _submitWindowInstance != null)
            {
                ApplySubmitFormReset();
            }
        }

        /// <summary>
        /// Resets the submit form and repaints so stale values are not shown.
        /// </summary>
        private void ApplySubmitFormReset()
        {
            if (_submitModeOnNextDraw == ModelSubmitWindow.SubmitMode.Update)
            {
                _submitWindowInstance.PrepareForUpdateSubmission(_submitUpdateModelIdOnNextDraw);
            }
            else
            {
                _submitWindowInstance.PrepareForNewSubmission(_resolveMeshDependenciesOnNextDraw, _submitSelectionGuidsOnNextDraw);
            }

            _resetSubmitFormOnNextDraw = false;
            _resolveMeshDependenciesOnNextDraw = false;
            _submitSelectionGuidsOnNextDraw = null;
            _submitModeOnNextDraw = ModelSubmitWindow.SubmitMode.New;
            _submitUpdateModelIdOnNextDraw = null;
            Repaint();
        }
    }
}
