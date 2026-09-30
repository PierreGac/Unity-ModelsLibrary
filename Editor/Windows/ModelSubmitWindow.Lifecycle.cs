using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing Unity lifecycle methods and initialization for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Opens the model submission window.
        /// Checks user role and only allows Artists or Admins to access the submission interface.
        /// Prevents opening during play mode.
        /// Now navigates to the Submit view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        /// <param name="resolveMeshDependencies">
        /// When true, automatically resolves and adds FBX/OBJ mesh dependencies from the current selection.
        /// </param>
        /// <param name="selectedAssetGuids">
        /// Optional asset GUIDs captured at entry time before the Model Library window takes focus.
        /// </param>
        public static void Open(bool resolveMeshDependencies = false, string[] selectedAssetGuids = null)
        {
            if (!TryGetSubmitHostWindow(out ModelLibraryWindow window))
            {
                return;
            }

            window.NavigateToSubmitView(resolveMeshDependencies, selectedAssetGuids);
        }

        /// <summary>
        /// Opens the submission form in Update Existing mode for a catalog model.
        /// </summary>
        /// <param name="modelId">Catalog model id to pre-select.</param>
        public static void OpenForUpdate(string modelId)
        {
            if (!TryGetSubmitHostWindow(out ModelLibraryWindow window))
            {
                return;
            }

            window.NavigateToSubmitViewForUpdate(modelId);
        }

        /// <summary>
        /// Validates play mode and role, then returns the Model Library host window.
        /// </summary>
        /// <param name="window">The host window when the checks succeed.</param>
        /// <returns>True when submission UI may be opened.</returns>
        private static bool TryGetSubmitHostWindow(out ModelLibraryWindow window)
        {
            window = null;
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Open During Play Mode",
                    "The Model Submission window cannot be opened while the application is playing.\n\n" +
                    "Please stop play mode first.",
                    "OK");
                return false;
            }

            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (!identityProvider.CanSubmitModels())
            {
                EditorUtility.DisplayDialog("Access Denied",
                    "Model submission is only available for Artists or Admins. Please switch to Artist role in User Settings.",
                    "OK");
                return false;
            }

            window = GetWindow<ModelLibraryWindow>("Model Library");
            return window != null;
        }

        private void OnEnable()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new Repository.FileSystemRepository(settings.repositoryRoot)
                : new Repository.HttpRepository(settings.repositoryRoot);

            _service = new ModelLibraryService(repo);
            _suppressCancelDialog = false;
            EnsureSubmitCancellation();
            LoadIndexAsync().FireAndForget();
        }

        /// <summary>
        /// Called by Unity when the window is disabled or destroyed.
        /// STABILITY (HIGH-09): Releases cached preview textures to prevent leaks.
        /// </summary>
        private void OnDisable()
        {
            _suppressCancelDialog = true;
            _cancelSubmission = true;
            if (_submitCancellation != null && !_submitCancellation.IsCancellationRequested)
            {
                _submitCancellation.Cancel();
            }

            ClearPreviewTextureCache();
        }

        /// <summary>
        /// Creates a fresh submit token after the previous one was cancelled.
        /// </summary>
        private void EnsureSubmitCancellation()
        {
            if (_submitCancellation != null && !_submitCancellation.IsCancellationRequested)
            {
                return;
            }

            _submitCancellation = new CancellationTokenSource();
        }

        private void OnGUI()
        {
            // Draw notification if present
            DrawNotification();

            EditorGUILayout.HelpBox("Add model assets on the Assets tab, then fill in the rest of the form.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUIContent helpButtonContent = new GUIContent("Help", "Open submission workflow guidance");
                if (GUILayout.Button(helpButtonContent, GUILayout.Width(70), GUILayout.Height(22)))
                {
                    ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Submission);
                }
            }

            EditorGUILayout.Space();

            string[] modeLabels = { "New Model", "Update Existing" };
            int newModeIndex = GUILayout.Toolbar((int)_mode, modeLabels);
            if (newModeIndex != (int)_mode)
            {
                _mode = (SubmitMode)newModeIndex;
                OnModeChanged();
            }
            EditorGUILayout.Space();

            bool metadataReady = true;
            if (_mode == SubmitMode.Update)
            {
                metadataReady = DrawUpdateSelection();
            }
            else
            {
                DrawNameField();
            }

            EditorGUILayout.Space(5);

            ProcessKeyboardShortcuts(metadataReady);

            // Form tabs
            string[] tabLabels = { "Basic Info", "Assets", "Images", "Advanced" };
            int newTabIndex = GUILayout.Toolbar((int)_selectedTab, tabLabels);
            if (newTabIndex != (int)_selectedTab)
            {
                _selectedTab = (FormTab)newTabIndex;
            }

            EditorGUILayout.Space(5);

            // Tab content
            using (new EditorGUI.DisabledScope(_mode == SubmitMode.Update && !metadataReady))
            {
                switch (_selectedTab)
                {
                    case FormTab.BasicInfo:
                        DrawBasicInfoTab();
                        break;
                    case FormTab.Assets:
                        DrawAssetsTab();
                        break;
                    case FormTab.Images:
                        DrawImagesTab();
                        break;
                    case FormTab.Advanced:
                        DrawAdvancedTab();
                        break;
                }
            }

            GUILayout.FlexibleSpace();

            // Draft saving buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent saveDraftContent = new GUIContent("Save Draft", "Save the current form progress to resume later");
                if (GUILayout.Button(saveDraftContent, GUILayout.Height(25)))
                {
                    SaveDraft();
                    EditorUtility.DisplayDialog("Draft Saved", "Your submission form has been saved as a draft.", "OK");
                }

                if (EditorPrefs.HasKey(__DRAFT_PREF_KEY))
                {
                    GUIContent restoreDraftContent = new GUIContent("Restore Draft", "Load the saved draft into the form");
                    if (GUILayout.Button(restoreDraftContent, GUILayout.Height(25)))
                    {
                        RestoreSavedDraft();
                    }

                    GUIContent clearDraftContent = new GUIContent("Clear Draft", "Delete the saved draft and start fresh");
                    if (GUILayout.Button(clearDraftContent, GUILayout.Height(25)))
                    {
                        if (EditorUtility.DisplayDialog("Clear Draft", "Are you sure you want to clear the saved draft?", "Yes", "No"))
                        {
                            ClearDraft();
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            // Submit button — always enabled unless a submission is already in progress.
            // Validation issues are shown in a dialog when the user clicks Submit.
            if (_isSubmitting)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(100)))
                    {
                        _cancelSubmission = true;
                        if (_submitCancellation != null && !_submitCancellation.IsCancellationRequested)
                        {
                            _submitCancellation.Cancel();
                        }
                    }
                    GUILayout.Label("Submitting...", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else if (UIStyles.DrawPrimaryButton("Submit", GUILayout.Height(30)))
            {
                Submit().FireAndForget();
            }
        }
    }
}
