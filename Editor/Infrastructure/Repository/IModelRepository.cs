using System.Collections.Generic;
using System.Threading.Tasks;
using ModelLibrary.Data;

namespace ModelLibrary.Editor.Repository
{
    /// <summary>
    /// Storage-agnostic repository API.
    /// Implementations can point at a local/UNC folder, HTTP endpoint, S3, etc.
    /// </summary>
    public interface IModelRepository
    {
        /// <summary>
        /// Load the global models index from the remote storage.
        /// </summary>
        Task<ModelIndex> LoadIndexAsync();
        /// <summary>
        /// Persist the global models index to the remote storage.
        /// </summary>
        Task SaveIndexAsync(ModelIndex index);

        /// <summary>
        /// Load a specific model version's metadata.
        /// </summary>
        Task<ModelMeta> LoadMetaAsync(string modelId, string version);
        /// <summary>
        /// Save a specific model version's metadata.
        /// </summary>
        Task SaveMetaAsync(string modelId, string version, ModelMeta meta);

        /// <summary>
        /// Check whether a directory exists at a repository-relative path.
        /// </summary>
        Task<bool> DirectoryExistsAsync(string relativePath);
        /// <summary>
        /// Ensure a directory exists at a repository-relative path.
        /// </summary>
        Task EnsureDirectoryAsync(string relativePath);
        /// <summary>
        /// List files recursively under the given repository-relative directory.
        /// </summary>
        Task<List<string>> ListFilesAsync(string relativeDir);
        /// <summary>
        /// Upload a local file to a repository-relative path, creating directories if needed.
        /// </summary>
        Task UploadFileAsync(string relativePath, string localAbsolutePath);
        /// <summary>
        /// Download a repository-relative file to a local absolute path, creating directories if needed.
        /// </summary>
        Task DownloadFileAsync(string relativePath, string localAbsolutePath);

        /// <summary>
        /// Delete a specific model version from the repository.
        /// This removes the entire version folder including metadata, payload, and images.
        /// </summary>
        /// <param name="modelId">The model ID (GUID).</param>
        /// <param name="version">The version string (e.g., "1.0.0").</param>
        /// <returns>True if the version was successfully deleted; false if it didn't exist or deletion failed.</returns>
        Task<bool> DeleteVersionAsync(string modelId, string version);

        /// <summary>
        /// Repository root string (absolute path or base URL).
        /// </summary>
        string Root { get; }
    }
}

