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
    /// Window for batch uploading multiple models at once from a directory structure.
    /// Scans a selected directory for model folders (subdirectories containing FBX/OBJ files),
    /// allows artists to review and edit metadata for each model, then uploads them sequentially.
    /// Only accessible to users with the Artist role.
    /// </summary>
    public class BatchUploadWindow : EditorWindow
    {
        private const float __SOURCE_LABEL_WIDTH = 120f;
        private const float __BUTTON_BROWSE_WIDTH = 80f;
        private const float __BUTTON_SELECT_WIDTH = 100f;
        private const float __UPLOAD_BUTTON_HEIGHT = 35f;
        private const float __UPLOAD_ITEM_TEXT_HEIGHT = 40f;
        private const float __UPLOAD_ITEMS_SCROLL_HEIGHT = 100f;

        /// <summary>
        /// Word-wrapped text area style for automatic line wrapping.
        /// </summary>
        private static GUIStyle _wordWrappedTextAreaStyle;
        
        /// <summary>
        /// Gets or creates the word-wrapped text area style.
        /// </summary>
        private static GUIStyle GetWordWrappedTextAreaStyle()
        {
            _wordWrappedTextAreaStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };
            return _wordWrappedTextAreaStyle;
        }
        /// <summary>Selected source directory containing model folders.</summary>
        private string _sourceDirectory = string.Empty;
        /// <summary>List of model items found during directory scanning.</summary>
        private List<BatchUploadService.BatchUploadItem> _uploadItems = new List<BatchUploadService.BatchUploadItem>();
        /// <summary>Scroll position for the upload items list.</summary>
        private Vector2 _scrollPosition;
        /// <summary>Flag indicating if a batch upload is currently in progress.</summary>
        private bool _isUploading = false;
        /// <summary>Service instance for repository operations.</summary>
        private ModelLibraryService _service;
        /// <summary>Service instance for batch upload operations.</summary>
        private BatchUploadService _batchService;

        /// <summary>
        /// Opens the batch upload window.
        /// Checks user role and only allows Artists to access batch upload functionality.
        /// Now navigates to the BatchUpload view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open()
        {
            // Only allow Artists to use batch upload
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                EditorUtility.DisplayDialog("Access Denied",
                    "Batch upload is only available for Artists. Please switch to Artist role in User Settings.",
                    "OK");
                return;
            }

            // Navigate to BatchUpload view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                window.NavigateToView(ModelLibraryWindow.ViewType.BatchUpload);
                window.InitializeBatchUploadState();
            }
        }

        private void OnEnable()
        {
            IModelRepository repo = RepositoryFactory.CreateRepository();
            _service = new ModelLibraryService(repo);
            _batchService = new BatchUploadService(_service, new SimpleUserIdentityProvider());
        }

        private void OnGUI()
        {
            UIStyles.DrawPageHeader("Batch Upload Models", "Scan a folder and upload multiple models at once.");

            EditorGUILayout.HelpBox(
                "Select a directory containing model folders. Each subdirectory with FBX/OBJ files will be treated as a separate model.",
                MessageType.Info);

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Source directory selection
            using (EditorGUILayout.HorizontalScope scope = new EditorGUILayout.HorizontalScope(UIStyles.CardBox))
            {
                EditorGUILayout.LabelField("Source Directory:", UIStyles.MutedLabel, GUILayout.Width(__SOURCE_LABEL_WIDTH));
                EditorGUILayout.TextField(_sourceDirectory);
                if (GUILayout.Button("Browse...", GUILayout.Width(__BUTTON_BROWSE_WIDTH)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Directory with Models", _sourceDirectory, "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _sourceDirectory = selected;
                        ScanDirectory();
                    }
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Scan button
            if (GUILayout.Button("Scan Directory", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
            {
                ScanDirectory();
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Upload items list
            if (_uploadItems.Count > 0)
            {
                UIStyles.DrawSectionHeader($"Found {_uploadItems.Count} model(s)");
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(__UPLOAD_ITEMS_SCROLL_HEIGHT));

                for (int i = 0; i < _uploadItems.Count; i++)
                {
                    BatchUploadService.BatchUploadItem item = _uploadItems[i];
                    DrawUploadItem(item);
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Batch actions
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All", GUILayout.Width(__BUTTON_SELECT_WIDTH)))
                    {
                        for (int i = 0; i < _uploadItems.Count; i++)
                        {
                            BatchUploadService.BatchUploadItem item = _uploadItems[i];
                            item.selected = true;
                        }
                    }
                    if (GUILayout.Button("Deselect All", GUILayout.Width(__BUTTON_SELECT_WIDTH)))
                    {
                        for (int i = 0; i < _uploadItems.Count; i++)
                        {
                            BatchUploadService.BatchUploadItem item = _uploadItems[i];
                            item.selected = false;
                        }
                    }
                }

                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

                // Upload button
                int selectedCount = _uploadItems.Count(i => i.selected);
                using (new EditorGUI.DisabledScope(selectedCount == 0 || _isUploading))
                {
                    if (GUILayout.Button($"Upload Selected ({selectedCount})", GUILayout.Height(__UPLOAD_BUTTON_HEIGHT)))
                    {
                        _ = UploadSelectedAsync();
                    }
                }
            }
            else if (!string.IsNullOrEmpty(_sourceDirectory))
            {
                EditorGUILayout.HelpBox("No models found in the selected directory. Make sure subdirectories contain FBX or OBJ files.", MessageType.Warning);
            }
        }

        private void DrawUploadItem(BatchUploadService.BatchUploadItem item)
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
                Rect textAreaRect = GUILayoutUtility.GetRect(0, __UPLOAD_ITEM_TEXT_HEIGHT, GUILayout.ExpandWidth(true));
                item.description = EditorGUI.TextArea(textAreaRect, item.description, GetWordWrappedTextAreaStyle());

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
        /// Each subdirectory containing FBX or OBJ files is treated as a separate model.
        /// </summary>
        private void ScanDirectory()
        {
            if (string.IsNullOrEmpty(_sourceDirectory) || !Directory.Exists(_sourceDirectory))
            {
                EditorUtility.DisplayDialog("Invalid Directory", "Please select a valid directory.", "OK");
                return;
            }

            _uploadItems = BatchUploadService.ScanDirectoryForModels(_sourceDirectory);
            Repaint();
        }

        /// <summary>
        /// Uploads all selected models from the upload items list.
        /// Processes each selected item sequentially, builds metadata, and submits to the repository.
        /// Displays a summary dialog with successful and failed uploads.
        /// </summary>
        private async Task UploadSelectedAsync()
        {
            _isUploading = true;
            try
            {
                BatchUploadService.BatchUploadResult result = await _batchService.UploadBatchAsync(_uploadItems);

                // Show results
                string message = $"Upload Complete!\n\n";
                message += $"Successful: {result.successfulUploads.Count}\n";
                message += $"Failed: {result.failedUploads.Count}";

                if (result.failedUploads.Count > 0)
                {
                    message += "\n\nFailed uploads:\n";
                    foreach (BatchUploadService.BatchUploadResult.UploadInfo info in result.failedUploads)
                    {
                        message += $"• {info.modelName}: {info.errorMessage}\n";
                    }
                }

                EditorUtility.DisplayDialog("Batch Upload Results", message, "OK");

                // Refresh index
                await _service.RefreshIndexAsync();

                // Clear selection and refresh
                _uploadItems.Clear();
                Repaint();
            }
            catch (System.Exception ex)
            {
                ErrorHandler.ShowError("Upload Error", $"An error occurred during batch upload: {ex.Message}", ex);
            }
            finally
            {
                _isUploading = false;
            }
        }
    }
}

