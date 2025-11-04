using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for batch uploading multiple models at once from directory structures.
    /// Scans directories for model folders, allows metadata editing, and uploads models sequentially.
    /// Provides progress tracking and error reporting for batch operations.
    /// </summary>
    public class BatchUploadService
    {
        /// <summary>The model library service for repository operations.</summary>
        private readonly ModelLibraryService _service;
        /// <summary>User identity provider for getting author information.</summary>
        private readonly IUserIdentityProvider _identityProvider;

        /// <summary>
        /// Initializes a new instance of the BatchUploadService.
        /// </summary>
        /// <param name="service">The model library service to use for uploads.</param>
        /// <param name="identityProvider">The user identity provider for author information.</param>
        public BatchUploadService(ModelLibraryService service, IUserIdentityProvider identityProvider)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _identityProvider = identityProvider ?? throw new ArgumentNullException(nameof(identityProvider));
        }

        /// <summary>
        /// Represents a model folder ready for batch upload.
        /// Contains folder path, metadata fields, and selection state.
        /// </summary>
        public class BatchUploadItem
        {
            /// <summary>Absolute path to the folder containing the model files.</summary>
            public string folderPath { get; set; }
            /// <summary>Display name of the model (typically derived from folder name).</summary>
            public string modelName { get; set; }
            /// <summary>Version string in SemVer format (e.g., "1.0.0").</summary>
            public string version { get; set; }
            /// <summary>Model description text.</summary>
            public string description { get; set; }
            /// <summary>List of tags for categorizing the model.</summary>
            public List<string> tags { get; set; } = new List<string>();
            /// <summary>Whether this item is selected for upload.</summary>
            public bool selected { get; set; } = true;
        }

        /// <summary>
        /// Scans a directory for model folders and returns batch upload items.
        /// Searches subdirectories for folders containing FBX or OBJ files.
        /// Each subdirectory with model files is treated as a separate model.
        /// </summary>
        /// <param name="directoryPath">Absolute path to the directory to scan.</param>
        /// <returns>List of BatchUploadItem objects representing found models.</returns>
        public static List<BatchUploadItem> ScanDirectoryForModels(string directoryPath)
        {
            List<BatchUploadItem> items = new List<BatchUploadItem>();

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return items;
            }

            try
            {
                // Look for subdirectories that might contain model files
                string[] subdirectories = Directory.GetDirectories(directoryPath);
                foreach (string subdir in subdirectories)
                {
                    // Check if this directory contains model files (FBX, OBJ)
                    string[] modelFiles = Directory.GetFiles(subdir, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext == FileExtensions.FBX || ext == FileExtensions.OBJ;
                        }).ToArray();

                    if (modelFiles.Length > 0)
                    {
                        string folderName = Path.GetFileName(subdir);
                        items.Add(new BatchUploadItem
                        {
                            folderPath = subdir,
                            modelName = folderName,
                            version = "1.0.0",
                            description = $"Model from {folderName}",
                            selected = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BatchUploadService] Error scanning directory {directoryPath}: {ex.Message}");
            }

            return items;
        }

        /// <summary>
        /// Uploads a batch of selected models sequentially to the repository.
        /// Processes each selected item: builds metadata, materializes temporary version folders, and submits.
        /// Provides progress tracking and collects success/failure information for each upload.
        /// </summary>
        /// <param name="items">List of batch upload items (only selected items will be uploaded).</param>
        /// <returns>BatchUploadResult containing lists of successful and failed uploads with error messages.</returns>
        public async Task<BatchUploadResult> UploadBatchAsync(List<BatchUploadItem> items)
        {
            BatchUploadResult result = new BatchUploadResult();
            int total = items.Count(i => i.selected);
            int current = 0;

            foreach (BatchUploadItem item in items.Where(i => i.selected))
            {
                current++;
                try
                {
                    EditorUtility.DisplayProgressBar("Batch Upload", $"Uploading {item.modelName} ({current}/{total})...", (float)current / total);

                    // Build metadata from folder contents
                    ModelMeta meta = await BuildMetaFromFolderAsync(item);

                    // Create temporary version folder
                    string tempRoot = Path.Combine(Path.GetTempPath(), $"BatchUpload_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempRoot);
                    
                    try
                    {
                        // Materialize the version folder structure
                        await MaterializeFolderToTempAsync(item.folderPath, tempRoot, meta);

                        // Submit to repository
                        string remotePath = await _service.SubmitNewVersionAsync(meta, tempRoot, "Batch upload");
                        result.successfulUploads.Add(new BatchUploadResult.UploadInfo
                        {
                            modelName = item.modelName,
                            version = item.version,
                            remotePath = remotePath
                        });
                    }
                    finally
                    {
                        // Cleanup temp folder
                        try { Directory.Delete(tempRoot, true); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    result.failedUploads.Add(new BatchUploadResult.UploadInfo
                    {
                        modelName = item.modelName,
                        version = item.version,
                        errorMessage = ex.Message
                    });
                    Debug.LogError($"[BatchUploadService] Failed to upload {item.modelName}: {ex.Message}");
                }
            }

            EditorUtility.ClearProgressBar();
            return result;
        }

        /// <summary>
        /// Builds ModelMeta from the contents of a folder containing model files.
        /// Scans the folder for FBX/OBJ files (payload), image files (images), and other assets.
        /// Creates metadata with a new GUID, timestamps, and user-provided information.
        /// </summary>
        /// <param name="item">The batch upload item containing folder path and metadata fields.</param>
        /// <returns>Complete ModelMeta object ready for submission.</returns>
        private async Task<ModelMeta> BuildMetaFromFolderAsync(BatchUploadItem item)
        {
            string[] files = Directory.GetFiles(item.folderPath, "*.*", SearchOption.AllDirectories);
            
            List<string> payloadPaths = new List<string>();
            List<string> imagePaths = new List<string>();

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string relativePath = file.Substring(item.folderPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                
                if (ext == FileExtensions.FBX || ext == FileExtensions.OBJ || ext == FileExtensions.MAT)
                {
                    payloadPaths.Add($"payload/{Path.GetFileName(file)}");
                }
                else if (ext == FileExtensions.PNG || ext == FileExtensions.JPG || ext == FileExtensions.JPEG || 
                         ext == FileExtensions.TGA || ext == FileExtensions.PSD)
                {
                    imagePaths.Add(file);
                }
            }

            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = Guid.NewGuid().ToString("N"),
                    name = item.modelName
                },
                version = item.version,
                description = item.description,
                author = _identityProvider.GetUserName(),
                createdTimeTicks = DateTime.Now.Ticks,
                updatedTimeTicks = DateTime.Now.Ticks,
                uploadTimeTicks = DateTime.Now.Ticks,
                payloadRelativePaths = payloadPaths,
                tags = new Tags { values = item.tags }
            };

            return await Task.FromResult(meta);
        }

        /// <summary>
        /// Copies files from a source folder to a temporary upload folder structure.
        /// Creates the standard version folder layout: payload/ (for model files) and images/ (for preview images).
        /// Copies payload files and images to their respective directories in the temp folder.
        /// </summary>
        /// <param name="sourceFolder">Absolute path to the source folder containing model files.</param>
        /// <param name="tempRoot">Absolute path to the temporary root directory where files should be copied.</param>
        /// <param name="meta">Model metadata containing payload and image paths.</param>
        private async Task MaterializeFolderToTempAsync(string sourceFolder, string tempRoot, ModelMeta meta)
        {
            await Task.Run(() =>
            {
                // Create payload directory
                string payloadDir = Path.Combine(tempRoot, "payload");
                Directory.CreateDirectory(payloadDir);

                // Copy payload files
                foreach (string relPath in meta.payloadRelativePaths)
                {
                    string fileName = Path.GetFileName(relPath);
                    // Find the file in the source folder
                    string[] foundFiles = Directory.GetFiles(sourceFolder, fileName, SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        string sourceFile = foundFiles[0];
                        string destFile = Path.Combine(payloadDir, fileName);
                        File.Copy(sourceFile, destFile, overwrite: true);
                    }
                }

                // Copy images
                if (meta.imageRelativePaths != null && meta.imageRelativePaths.Count > 0)
                {
                    string imagesDir = Path.Combine(tempRoot, "images");
                    Directory.CreateDirectory(imagesDir);
                    // Image paths are absolute, copy them directly
                    foreach (string imagePath in meta.imageRelativePaths)
                    {
                        if (File.Exists(imagePath))
                        {
                            string destFile = Path.Combine(imagesDir, Path.GetFileName(imagePath));
                            File.Copy(imagePath, destFile, overwrite: true);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Result of a batch upload operation containing success and failure information.
        /// </summary>
        public class BatchUploadResult
        {
            /// <summary>List of successfully uploaded models with their remote paths.</summary>
            public List<UploadInfo> successfulUploads { get; set; } = new List<UploadInfo>();
            /// <summary>List of failed uploads with error messages.</summary>
            public List<UploadInfo> failedUploads { get; set; } = new List<UploadInfo>();

            /// <summary>
            /// Information about a single upload attempt (successful or failed).
            /// </summary>
            public class UploadInfo
            {
                /// <summary>Display name of the model that was uploaded (or attempted).</summary>
                public string modelName { get; set; }
                /// <summary>Version string of the uploaded model.</summary>
                public string version { get; set; }
                /// <summary>Repository-relative path where the model was uploaded (only for successful uploads).</summary>
                public string remotePath { get; set; }
                /// <summary>Error message describing why the upload failed (only for failed uploads).</summary>
                public string errorMessage { get; set; }
            }
        }
    }
}

