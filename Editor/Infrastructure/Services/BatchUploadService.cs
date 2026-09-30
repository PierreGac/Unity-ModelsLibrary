using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    internal class BatchUploadService
    {
        /// <summary>Repository folder for mesh and material files.</summary>
        private const string PAYLOAD_DIRECTORY_NAME = "payload";
        /// <summary>Repository folder for preview images.</summary>
        private const string IMAGES_DIRECTORY_NAME = "images";

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
            /// <summary>
            /// Absolute source file for each path stored in the version folder.
            /// Filled while the folder is scanned and used when the files are copied.
            /// </summary>
            public Dictionary<string, string> sourceFilesByRelativePath { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                for (int i = 0; i < subdirectories.Length; i++)
                {
                    string subdir = subdirectories[i];
                    if (SafeFileEnumerator.IsReparsePoint(subdir))
                    {
                        continue;
                    }

                    if (!AssetDependencyResolver.DirectoryContainsPrimaryModel(subdir))
                    {
                        continue;
                    }

                    string folderName = Path.GetFileName(subdir);
                    items.Add(new BatchUploadItem
                    {
                        folderPath = subdir,
                        modelName = folderName,
                        version = "1.0.0",
                        description = "Model from " + folderName,
                        selected = true
                    });
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
        public Task<BatchUploadResult> UploadBatchAsync(List<BatchUploadItem> items)
        {
            return UploadBatchAsync(items, CancellationToken.None);
        }

        /// <summary>
        /// Uploads selected models one at a time and stops before the next item when cancelled.
        /// Cancellation is not recorded as a failed item. Temporary folders are still deleted.
        /// </summary>
        /// <param name="items">List of batch upload items (only selected items will be uploaded).</param>
        /// <param name="cancellationToken">Stops the batch before the next item or the next submit step.</param>
        /// <returns>BatchUploadResult containing lists of successful and failed uploads with error messages.</returns>
        public async Task<BatchUploadResult> UploadBatchAsync(List<BatchUploadItem> items, CancellationToken cancellationToken)
        {
            BatchUploadResult result = new BatchUploadResult();
            int total = items.Count(i => i.selected);
            int current = 0;

            try
            {
                foreach (BatchUploadItem item in items.Where(i => i.selected))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    try
                    {
                        EditorUtility.DisplayProgressBar("Batch Upload", $"Uploading {item.modelName} ({current}/{total})...", (float)current / total);

                        if (!AssetDependencyResolver.DirectoryContainsPrimaryModel(item.folderPath))
                        {
                            throw new InvalidOperationException(AssetDependencyResolver.PRIMARY_MODEL_REQUIRED_MESSAGE);
                        }

                        ModelMeta meta = await BuildMetaFromFolderAsync(item);

                        string tempRoot = Path.Combine(Path.GetTempPath(), $"BatchUpload_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempRoot);

                        try
                        {
                            await MaterializeFolderToTempAsync(item, tempRoot, meta);

                            string remotePath = await _service.SubmitNewVersionAsync(meta, tempRoot, "Batch upload", cancellationToken);
                            result.successfulUploads.Add(new BatchUploadResult.UploadInfo
                            {
                                modelName = item.modelName,
                                version = item.version,
                                remotePath = remotePath
                            });
                        }
                        finally
                        {
                            try
                            {
                                Directory.Delete(tempRoot, true);
                            }
                            catch (Exception cleanupEx)
                            {
                                Debug.LogWarning($"[BatchUploadService] Failed to clean up temporary directory {tempRoot}: {cleanupEx.Message}");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
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
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

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
            List<string> files = new List<string>(SafeFileEnumerator.EnumerateFilesSafe(item.folderPath));
            string sourceRoot = Path.GetFullPath(item.folderPath);

            List<string> payloadPaths = new List<string>();
            List<string> imagePaths = new List<string>();
            item.sourceFilesByRelativePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string relative = RelativePathInsideFolder(sourceRoot, file);
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string storedPath = null;

                if (AssetDependencyResolver.IsMeshAssetPath(file) || ext == FileExtensions.MAT)
                {
                    storedPath = PAYLOAD_DIRECTORY_NAME + "/" + relative;
                    payloadPaths.Add(storedPath);
                }
                else if (ext == FileExtensions.PNG || ext == FileExtensions.JPG || ext == FileExtensions.JPEG ||
                    ext == FileExtensions.TGA || ext == FileExtensions.PSD)
                {
                    storedPath = IMAGES_DIRECTORY_NAME + "/" + relative;
                    imagePaths.Add(storedPath);
                }

                if (storedPath != null)
                {
                    item.sourceFilesByRelativePath[storedPath] = file;
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
                imageRelativePaths = imagePaths,
                tags = new Tags { values = item.tags },
                installPath = InstallPathUtils.BuildInstallPath(item.modelName)
            };

            return await Task.FromResult(meta);
        }

        /// <summary>
        /// Copies each scanned source file to its stored relative path in the temporary version folder.
        /// </summary>
        /// <param name="item">Batch item whose source map was filled by the folder scan.</param>
        /// <param name="tempRoot">Absolute path to the temporary root directory where files should be copied.</param>
        /// <param name="meta">Model metadata recorded from the same scan.</param>
        private async Task MaterializeFolderToTempAsync(BatchUploadItem item, string tempRoot, ModelMeta meta)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            await Task.Run(() =>
            {
                string tempFull = Path.GetFullPath(tempRoot);
                List<string> storedPaths = new List<string>(item.sourceFilesByRelativePath.Keys);
                for (int i = 0; i < storedPaths.Count; i++)
                {
                    string storedPath = PathUtils.ValidateRelativePathStrict(storedPaths[i]);
                    string sourceFile = item.sourceFilesByRelativePath[storedPaths[i]];
                    string destFile = Path.Combine(tempFull, storedPath.Replace('/', Path.DirectorySeparatorChar));
                    string destFull = PathUtils.AssertInsideRoot(destFile, tempFull);
                    string destDirectory = Path.GetDirectoryName(destFull);
                    if (!string.IsNullOrEmpty(destDirectory))
                    {
                        Directory.CreateDirectory(destDirectory);
                    }

                    File.Copy(sourceFile, destFull, overwrite: true);
                }
            });
        }

        /// <summary>
        /// Returns the file's path relative to <paramref name="sourceRoot"/>, with forward slashes.
        /// </summary>
        /// <param name="sourceRoot">Absolute model folder that was scanned.</param>
        /// <param name="file">Absolute file inside that folder.</param>
        /// <returns>A safe relative path such as <c>body/Cube.obj</c>.</returns>
        private static string RelativePathInsideFolder(string sourceRoot, string file)
        {
            string root = Path.GetFullPath(sourceRoot);
            string fullFile = Path.GetFullPath(file);
            string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Batch file is outside the model folder: '{file}'");
            }

            string relative = fullFile.Substring(rootPrefix.Length);
            return PathUtils.ValidateRelativePathStrict(relative);
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
