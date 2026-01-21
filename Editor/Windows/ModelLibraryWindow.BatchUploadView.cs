using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Partial class containing BatchUploadWindow view implementation for ModelLibraryWindow.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        private const float __BATCH_UPLOAD_LABEL_WIDTH = 120f;
        private const float __BATCH_UPLOAD_BROWSE_WIDTH = 80f;
        private const float __BATCH_UPLOAD_SELECT_WIDTH = 100f;
        private const float __BATCH_UPLOAD_UPLOAD_HEIGHT = 35f;
        private const float __BATCH_UPLOAD_ITEM_HEIGHT = 40f;
        private const float __BATCH_UPLOAD_ITEMS_SCROLL_HEIGHT = 140f;
        /// <summary>
        /// Word-wrapped text area style for automatic line wrapping in batch upload forms.
        /// Shared style instance to avoid allocations.
        /// </summary>
        private static GUIStyle _batchUploadWordWrappedTextAreaStyle;

        /// <summary>
        /// Gets or creates the word-wrapped text area style for batch upload forms.
        /// </summary>
        /// <returns>A GUIStyle with word wrapping enabled for text areas.</returns>
        private static GUIStyle GetBatchUploadWordWrappedTextAreaStyle()
        {
            _batchUploadWordWrappedTextAreaStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };
            return _batchUploadWordWrappedTextAreaStyle;
        }

        /// <summary>
        /// Initializes batch upload state when navigating to the BatchUpload view.
        /// Sets up the service instances needed for batch upload operations.
        /// </summary>
        public void InitializeBatchUploadState()
        {
            // Initialize main service if needed
            if (_service == null)
            {
                IModelRepository repo = RepositoryFactory.CreateRepository();
                _service = new ModelLibraryService(repo);
            }

            // Initialize batch upload service with identity provider
            _batchUploadService = new BatchUploadService(_service, new SimpleUserIdentityProvider());
        }

        /// <summary>
        /// Draws the BatchUpload view.
        /// Provides a UI for selecting a directory, scanning for models, and uploading multiple models at once.
        /// Only accessible to users with the Artist role.
        /// </summary>
        private void DrawBatchUploadView()
        {
            // Check role access - batch upload is restricted to Artists
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                EditorGUILayout.HelpBox("Batch upload is only available for Artists. Please switch to Artist role in User Settings.", MessageType.Warning);
                if (GUILayout.Button("Go to Settings", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    NavigateToView(ViewType.Settings);
                }
                return;
            }

            UIStyles.DrawPageHeader("Batch Upload Models", "Scan a folder and upload multiple models at once.");

            EditorGUILayout.HelpBox(
                "Select a directory containing model folders. Each subdirectory with FBX/OBJ files will be treated as a separate model.",
                MessageType.Info);

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Source directory selection
            using (EditorGUILayout.HorizontalScope scope = new EditorGUILayout.HorizontalScope(UIStyles.CardBox))
            {
                EditorGUILayout.LabelField("Source Directory:", UIStyles.MutedLabel, GUILayout.Width(__BATCH_UPLOAD_LABEL_WIDTH));
                EditorGUILayout.TextField(_batchUploadSourceDirectory);
                if (GUILayout.Button("Browse...", GUILayout.Width(__BATCH_UPLOAD_BROWSE_WIDTH)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Directory with Models", _batchUploadSourceDirectory, "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _batchUploadSourceDirectory = selected;
                        ScanBatchUploadDirectory();
                    }
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Scan button
            if (GUILayout.Button("Scan Directory", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
            {
                ScanBatchUploadDirectory();
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Upload items list
            if (_batchUploadItems.Count > 0)
            {
                UIStyles.DrawSectionHeader($"Found {_batchUploadItems.Count} model(s)");
                _batchUploadScrollPosition = EditorGUILayout.BeginScrollView(_batchUploadScrollPosition, GUILayout.Height(__BATCH_UPLOAD_ITEMS_SCROLL_HEIGHT));

                for (int i = 0; i < _batchUploadItems.Count; i++)
                {
                    DrawBatchUploadItem(_batchUploadItems[i]);
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Batch actions
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All", GUILayout.Width(__BATCH_UPLOAD_SELECT_WIDTH)))
                    {
                        for (int i = 0; i < _batchUploadItems.Count; i++)
                        {
                            _batchUploadItems[i].selected = true;
                        }
                    }
                    if (GUILayout.Button("Deselect All", GUILayout.Width(__BATCH_UPLOAD_SELECT_WIDTH)))
                    {
                        for (int i = 0; i < _batchUploadItems.Count; i++)
                        {
                            _batchUploadItems[i].selected = false;
                        }
                    }
                }

                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Upload button
                int selectedCount = _batchUploadItems.Count(i => i.selected);
                using (new EditorGUI.DisabledScope(selectedCount == 0 || _batchUploadIsUploading))
                {
                    if (GUILayout.Button($"Upload Selected ({selectedCount})", GUILayout.Height(__BATCH_UPLOAD_UPLOAD_HEIGHT)))
                    {
                        _ = UploadBatchSelectedAsync();
                    }
                }
            }
            else if (!string.IsNullOrEmpty(_batchUploadSourceDirectory))
            {
                EditorGUILayout.HelpBox("No models found in the selected directory. Make sure subdirectories contain FBX or OBJ files.", MessageType.Warning);
            }
        }

        /// <summary>
        /// Draws a single batch upload item in the list.
        /// Displays model name, version, description, tags, and folder path with editing capabilities.
        /// </summary>
        /// <param name="item">The batch upload item to display.</param>
        private void DrawBatchUploadItem(BatchUploadService.BatchUploadItem item)
        {
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));
                    EditorGUILayout.LabelField(item.modelName, UIStyles.SectionHeader);
                }

                item.version = EditorGUILayout.TextField("Version", item.version);
                // Constrain text area to available width and enable word wrapping for automatic line breaks
                Rect textAreaRect = GUILayoutUtility.GetRect(0, __BATCH_UPLOAD_ITEM_HEIGHT, GUILayout.ExpandWidth(true));
                item.description = EditorGUI.TextArea(textAreaRect, item.description, GetBatchUploadWordWrappedTextAreaStyle());

                // Tags
                EditorGUILayout.LabelField("Tags (comma-separated):", UIStyles.MutedLabel);
                string tagsText = string.Join(", ", item.tags);
                tagsText = EditorGUILayout.TextField(tagsText);
                item.tags = tagsText.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                EditorGUILayout.LabelField($"Path: {item.folderPath}", UIStyles.MutedLabel);
            }
        }

        /// <summary>
        /// Scans the selected directory for model folders and populates the upload items list.
        /// </summary>
        private void ScanBatchUploadDirectory()
        {
            if (string.IsNullOrEmpty(_batchUploadSourceDirectory) || !Directory.Exists(_batchUploadSourceDirectory))
            {
                EditorUtility.DisplayDialog("Invalid Directory", "Please select a valid directory.", "OK");
                return;
            }

            _batchUploadItems = BatchUploadService.ScanDirectoryForModels(_batchUploadSourceDirectory);
            Repaint();
        }

        /// <summary>
        /// Uploads all selected models from the upload items list.
        /// </summary>
        private async Task UploadBatchSelectedAsync()
        {
            _batchUploadIsUploading = true;
            try
            {
                if (_batchUploadService == null)
                {
                    InitializeBatchUploadState();
                }

                BatchUploadService.BatchUploadResult result = await _batchUploadService.UploadBatchAsync(_batchUploadItems);

                // Show results
                string message = $"Upload Complete!\n\n";
                message += $"Successful: {result.successfulUploads.Count}\n";
                message += $"Failed: {result.failedUploads.Count}";

                if (result.failedUploads.Count > 0)
                {
                    message += "\n\nFailed uploads:\n";
                    for (int i = 0; i < result.failedUploads.Count; i++)
                    {
                        BatchUploadService.BatchUploadResult.UploadInfo info = result.failedUploads[i];
                        message += $"• {info.modelName}: {info.errorMessage}\n";
                    }
                }

                EditorUtility.DisplayDialog("Batch Upload Results", message, "OK");

                // Refresh index
                if (_service != null)
                {
                    await _service.RefreshIndexAsync();
                }

                // Refresh browser view if we're still on batch upload view
                if (_currentView == ViewType.BatchUpload)
                {
                    RefreshIndex();
                }

                // Clear selection and refresh
                _batchUploadItems.Clear();
                Repaint();
            }
            catch (System.Exception ex)
            {
                ErrorHandler.ShowError("Upload Error", $"An error occurred during batch upload: {ex.Message}", ex);
            }
            finally
            {
                _batchUploadIsUploading = false;
            }
        }
    }
}

