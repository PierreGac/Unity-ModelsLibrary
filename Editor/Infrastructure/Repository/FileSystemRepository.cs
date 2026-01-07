using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Repository
{
    /// <summary>
    /// Repository backed by a local/UNC directory.
    /// Stores index as models_index.json and model versions under &lt;root&gt;/&lt;modelId&gt;/&lt;version&gt;/.
    /// This is the simplest repository implementation - just files and folders on disk.
    /// Can work with local drives, network shares (UNC paths), or any accessible file system.
    /// </summary>
    public class FileSystemRepository : IModelRepository
    {
        /// <summary>
        /// The root directory path where all model data is stored.
        /// This could be a local path like "C:\Models" or a UNC path like "\\server\models".
        /// </summary>
        public string Root { get; }

        // Performance Optimization: Caching
        /// <summary>Cache for file existence checks to avoid repeated slow File.Exists() calls, especially on network drives.</summary>
        private readonly ConcurrentDictionary<string, bool> _fileExistsCache = new ConcurrentDictionary<string, bool>();
        /// <summary>Cache for directory existence checks to avoid repeated slow Directory.Exists() calls.</summary>
        private readonly ConcurrentDictionary<string, bool> _directoryExistsCache = new ConcurrentDictionary<string, bool>();

        // Network Optimization: Directory Listing Cache
        /// <summary>Cache for directory contents to minimize network calls for UNC paths.</summary>
        private readonly ConcurrentDictionary<string, HashSet<string>> _directoryContentsCache = new ConcurrentDictionary<string, HashSet<string>>();
        /// <summary>Timestamps for directory cache entries to implement expiration.</summary>
        private readonly ConcurrentDictionary<string, DateTime> _directoryCacheTimestamps = new ConcurrentDictionary<string, DateTime>();
        /// <summary>Time span before directory cache entries expire (5 minutes).</summary>
        private static readonly TimeSpan DirectoryCacheExpiry = TimeSpan.FromMinutes(5);

        // Windows Error Codes for Network Authentication
        /// <summary>Windows error code: ERROR_ACCESS_DENIED (5) - Access is denied.</summary>
        private const int ERROR_ACCESS_DENIED = 5;
        /// <summary>Windows error code: ERROR_BAD_NETPATH (53) - The network path was not found.</summary>
        private const int ERROR_BAD_NETPATH = 53;
        /// <summary>Windows error code: ERROR_LOGON_FAILURE (1326) - The user name or password is incorrect.</summary>
        private const int ERROR_LOGON_FAILURE = 1326;

        /// <summary>
        /// Initialize the repository with a root directory path.
        /// Normalizes path separators to match the current operating system.
        /// </summary>
        /// <param name="root">The root directory path (local or UNC)</param>
        public FileSystemRepository(string root) => Root = root.Replace('/', Path.DirectorySeparatorChar);

        /// <summary>
        /// Helper method to safely join path segments and normalize separators.
        /// This ensures consistent path handling across different operating systems.
        /// </summary>
        /// <param name="a">First path segment</param>
        /// <param name="b">Second path segment</param>
        /// <returns>Combined path with normalized separators</returns>
        private static string Join(string a, string b) => Path.Combine(a, b).Replace('/', Path.DirectorySeparatorChar);

        /// <summary>
        /// Cached file existence check to avoid repeated slow File.Exists() calls.
        /// For network drives (UNC paths), uses directory listing cache to minimize network calls.
        /// Falls back to direct File.Exists() for local drives or when cache lookup fails.
        /// </summary>
        /// <param name="path">Absolute path to the file to check.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        private bool FileExistsCached(string path)
        {
            return _fileExistsCache.GetOrAdd(path, p =>
            {
                // For network drives, try to use directory listing cache first
                if (IsNetworkPath(p))
                {
                    string directory = Path.GetDirectoryName(p);
                    string fileName = Path.GetFileName(p);

                    if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
                    {
                        HashSet<string> directoryContents = GetDirectoryContentsCached(directory);
                        if (directoryContents != null && directoryContents.Contains(fileName))
                        {
                            return true;
                        }
                    }
                }

                // Fallback to direct File.Exists() call
                return File.Exists(p);
            });
        }

        /// <summary>
        /// Checks if an IOException represents a network authentication error.
        /// Detects common Windows error codes and message patterns that indicate authentication failures.
        /// </summary>
        /// <param name="ex">The IOException to check. Can be null.</param>
        /// <returns>True if the exception indicates a network authentication error, false otherwise.</returns>
        private static bool IsNetworkAuthError(IOException ex)
        {
            if (ex == null)
            {
                return false;
            }

            // Check for common network authentication error codes
            // Extract the low 16 bits of HResult which contain the Windows error code
            int errorCode = ex.HResult & 0xFFFF;
            
            // Check for known network authentication error codes
            bool isKnownErrorCode = errorCode == ERROR_ACCESS_DENIED || 
                                    errorCode == ERROR_LOGON_FAILURE || 
                                    errorCode == ERROR_BAD_NETPATH;
            
            // Also check error message for common authentication-related phrases
            string message = ex.Message ?? string.Empty;
            bool hasAuthMessage = message.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  message.IndexOf("logon failure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  message.IndexOf("network path", StringComparison.OrdinalIgnoreCase) >= 0;

            return isKnownErrorCode || hasAuthMessage;
        }

        /// <summary>
        /// Checks if a network path is accessible by attempting to access the root directory.
        /// Uses a lightweight check (Directory.Exists) instead of enumerating files to avoid performance issues.
        /// </summary>
        /// <param name="path">The path to check. Can be null or empty.</param>
        /// <returns>True if the path is accessible, false if access is denied or the path is invalid.</returns>
        private static bool CanAccessNetworkPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                string rootPath = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(rootPath))
                {
                    // Local path without drive letter, assume accessible
                    return true;
                }

                // Use lightweight Directory.Exists check instead of Directory.GetFiles
                // Directory.GetFiles can be extremely slow on network drives and may timeout
                // Directory.Exists is much faster and sufficient for accessibility check
                return Directory.Exists(rootPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - path exists but we don't have permission
                return false;
            }
            catch (IOException)
            {
                // Network error or path doesn't exist
                return false;
            }
            catch (Exception)
            {
                // Any other exception indicates the path is not accessible
                return false;
            }
        }

        /// <summary>
        /// Checks if a path is on a network drive (UNC path or mapped network drive).
        /// Detects both UNC paths (\\server\share) and mapped network drives (Z: where Z is mapped).
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the path is on a network drive, false otherwise.</returns>
        private static bool IsNetworkPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Check for UNC path (\\server\share)
            if (path.StartsWith(@"\\"))
                return true;

            // Check for mapped network drive (Z: where Z is mapped to network)
            if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
            {
                try
                {
                    DriveInfo drive = new DriveInfo(path.Substring(0, 2));
                    return drive.DriveType == DriveType.Network;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets directory contents with caching to minimize network calls for UNC paths.
        /// Cache entries expire after DirectoryCacheExpiry (5 minutes) to ensure data freshness.
        /// Returns null if the directory doesn't exist or cannot be read.
        /// </summary>
        /// <param name="directoryPath">Absolute path to the directory to list.</param>
        /// <returns>Set of file names in the directory, or null if directory doesn't exist or cannot be read.</returns>
        private HashSet<string> GetDirectoryContentsCached(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return null;

            DateTime now = DateTime.UtcNow;

            // Check if we have a valid cached result
            if (_directoryContentsCache.TryGetValue(directoryPath, out HashSet<string> cachedContents) &&
                _directoryCacheTimestamps.TryGetValue(directoryPath, out DateTime cacheTime) &&
                now - cacheTime < DirectoryCacheExpiry)
            {
                return cachedContents;
            }

            // Directory doesn't exist or cache expired, try to get fresh data
            if (!Directory.Exists(directoryPath))
            {
                _directoryContentsCache.TryRemove(directoryPath, out _);
                _directoryCacheTimestamps.TryRemove(directoryPath, out _);
                return null;
            }

            try
            {
                // Get all files in the directory
                string[] files = Directory.GetFiles(directoryPath);
                HashSet<string> fileNames = new HashSet<string>(files.Length);

                foreach (string file in files)
                {
                    fileNames.Add(Path.GetFileName(file));
                }

                // Cache the result
                _directoryContentsCache[directoryPath] = fileNames;
                _directoryCacheTimestamps[directoryPath] = now;

                return fileNames;
            }
            catch
            {
                // If we can't read the directory, remove any stale cache
                _directoryContentsCache.TryRemove(directoryPath, out _);
                _directoryCacheTimestamps.TryRemove(directoryPath, out _);
                return null;
            }
        }

        /// <summary>
        /// Cached directory existence check to avoid repeated slow Directory.Exists() calls.
        /// Uses a thread-safe concurrent dictionary for caching results.
        /// </summary>
        /// <param name="path">Absolute path to the directory to check.</param>
        /// <returns>True if the directory exists, false otherwise.</returns>
        private bool DirectoryExistsCached(string path) => _directoryExistsCache.GetOrAdd(path, p => Directory.Exists(p));

        /// <summary>
        /// Invalidates file existence cache for a specific path.
        /// Also invalidates the parent directory's contents cache since the file list has changed.
        /// Should be called after file creation, deletion, or modification operations.
        /// </summary>
        /// <param name="path">Absolute path to the file whose cache should be invalidated.</param>
        private void InvalidateFileCache(string path)
        {
            _fileExistsCache.TryRemove(path, out _);

            // Also invalidate directory contents cache for the parent directory
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                _directoryContentsCache.TryRemove(directory, out _);
                _directoryCacheTimestamps.TryRemove(directory, out _);
            }
        }

        /// <summary>
        /// Invalidates directory existence and contents cache for a specific path.
        /// Also invalidates the parent directory's contents cache.
        /// Should be called after directory creation, deletion, or modification operations.
        /// </summary>
        /// <param name="path">Absolute path to the directory whose cache should be invalidated.</param>
        private void InvalidateDirectoryCache(string path)
        {
            _directoryExistsCache.TryRemove(path, out _);
            _directoryContentsCache.TryRemove(path, out _);
            _directoryCacheTimestamps.TryRemove(path, out _);

            // Also invalidate parent directory contents cache
            string parentDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                _directoryContentsCache.TryRemove(parentDirectory, out _);
                _directoryCacheTimestamps.TryRemove(parentDirectory, out _);
            }
        }

        /// <summary>
        /// Loads the global models index from the repository.
        /// The index file is stored at the repository root as "models_index.json".
        /// Returns an empty index if the file doesn't exist (new repository).
        /// </summary>
        /// <returns>The model index, or an empty index if the file doesn't exist.</returns>
        public async Task<ModelIndex> LoadIndexAsync()
        {
            // Build the full path to the models index file
            string path = Join(Root, "models_index.json");

            // If the index file doesn't exist, return an empty index (new repository)
            UnityEngine.Debug.Log($"Loading index from {path}");
            
            try
            {
                if (!FileExistsCached(path))
                {
                    // Check if directory is accessible (especially for network paths)
                    if (IsNetworkPath(path))
                    {
                        if (!CanAccessNetworkPath(path))
                        {
                            throw new UnauthorizedAccessException($"Cannot access network path: {path}. Please verify your Windows credentials are correct and you are logged into the server.");
                        }
                    }
                    return new ModelIndex();
                }

                // Read the JSON file asynchronously to avoid blocking the UI thread
                string json = await AsyncProfiler.MeasureAsync("FileSystemRepository.ReadIndex", () => File.ReadAllTextAsync(path));

                // Parse the JSON into a ModelIndex object, or return empty if parsing fails
                return JsonUtil.FromJson<ModelIndex>(json) ?? new ModelIndex();
            }
            catch (IOException ex) when (IsNetworkAuthError(ex))
            {
                throw new UnauthorizedAccessException($"Network authentication required for repository: {Root}. Please verify your credentials and network connection.", ex);
            }
            catch (UnauthorizedAccessException)
            {
                // Re-throw UnauthorizedAccessException as-is (already has proper message)
                throw;
            }
        }

        /// <summary>
        /// Saves the global models index to the repository.
        /// Creates the root directory if it doesn't exist.
        /// Invalidates the file cache after writing to ensure subsequent reads get fresh data.
        /// </summary>
        /// <param name="index">The model index to save.</param>
        public async Task SaveIndexAsync(ModelIndex index)
        {
            // Build the full path to the models index file
            string path = Join(Root, "models_index.json");

            // Ensure the directory exists (in case the root directory doesn't exist yet)
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Convert the index to JSON format
            string json = JsonUtil.ToJson(index);

            // Write the JSON file asynchronously to avoid blocking the UI thread
            await AsyncProfiler.MeasureAsync("FileSystemRepository.WriteIndex", () => File.WriteAllTextAsync(path, json));

            // Invalidate cache since we just created/updated the file
            InvalidateFileCache(path);
        }

        /// <summary>
        /// Loads a specific model version's metadata from the repository.
        /// The metadata file is stored at: &lt;root&gt;/&lt;modelId&gt;/&lt;version&gt;/model.json
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version string (e.g., "1.0.0").</param>
        /// <returns>The model metadata object.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the metadata file doesn't exist.</exception>
        public async Task<ModelMeta> LoadMetaAsync(string modelId, string version)
        {
            // Build the path to the model.json file: <root>/<modelId>/<version>/model.json
            string path = Join(Root, Join(modelId, Join(version, ModelMeta.MODEL_JSON)));

            // If the metadata file doesn't exist, throw an exception
            if (!FileExistsCached(path))
            {
                throw new FileNotFoundException($"Meta not found: {path}");
            }

            // Read the JSON file asynchronously
            string json = await AsyncProfiler.MeasureAsync("FileSystemRepository.ReadMeta", () => File.ReadAllTextAsync(path));

            // Parse and return the metadata
            return JsonUtil.FromJson<ModelMeta>(json);
        }

        /// <summary>
        /// Saves a specific model version's metadata to the repository.
        /// Creates the version directory structure if it doesn't exist.
        /// The metadata file is stored at: &lt;root&gt;/&lt;modelId&gt;/&lt;version&gt;/model.json
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version string (e.g., "1.0.0").</param>
        /// <param name="meta">The model metadata to save.</param>
        public async Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
        {
            // Build the path to the model.json file: <root>/<modelId>/<version>/model.json
            string path = Join(Root, Join(modelId, Join(version, ModelMeta.MODEL_JSON)));

            // Ensure the directory structure exists
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Convert the metadata to JSON format
            string json = JsonUtil.ToJson(meta);

            // Write the JSON file asynchronously
            await AsyncProfiler.MeasureAsync("FileSystemRepository.WriteMeta", () => File.WriteAllTextAsync(path, json));

            // Invalidate cache since we just created/updated the file
            InvalidateFileCache(path);
        }

        /// <summary>
        /// Checks whether a directory exists at a repository-relative path.
        /// Uses cached directory existence checks for performance.
        /// </summary>
        /// <param name="relativePath">Repository-relative path to check.</param>
        /// <returns>True if the directory exists, false otherwise.</returns>
        public Task<bool> DirectoryExistsAsync(string relativePath)
        {
            // Check if the directory exists at the repository-relative path
            string fullPath = Join(Root, relativePath);
            return Task.FromResult(DirectoryExistsCached(fullPath));
        }

        /// <summary>
        /// Ensures a directory exists at a repository-relative path.
        /// Creates the directory and all parent directories if they don't exist.
        /// Invalidates the directory cache after creation.
        /// </summary>
        /// <param name="relativePath">Repository-relative path where the directory should exist.</param>
        public Task EnsureDirectoryAsync(string relativePath)
        {
            // Create the directory (and any parent directories) if they don't exist
            string fullPath = Join(Root, relativePath);
            Directory.CreateDirectory(fullPath);

            // Invalidate directory cache since we just created it
            InvalidateDirectoryCache(fullPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Lists all files recursively under the given repository-relative directory.
        /// Returns paths relative to the repository root, using forward slashes as separators.
        /// Returns an empty list if the directory doesn't exist.
        /// </summary>
        /// <param name="relativeDir">Repository-relative path to the directory to list.</param>
        /// <returns>List of file paths relative to the repository root.</returns>
        public Task<List<string>> ListFilesAsync(string relativeDir)
        {
            Stopwatch stopwatch = AsyncProfiler.Enabled ? Stopwatch.StartNew() : null;

            // Build the absolute path to the directory
            string abs = Join(Root, relativeDir);
            List<string> list = new List<string>();

            // Only proceed if the directory actually exists
            if (DirectoryExistsCached(abs))
            {
                // Get all files recursively (including subdirectories)
                foreach (string f in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
                {
                    // Convert absolute path back to repository-relative path
                    string rel = f[Root.Length..].TrimStart(Path.DirectorySeparatorChar);

                    // Normalize separators to forward slashes for consistency
                    rel = rel.Replace(Path.DirectorySeparatorChar, '/');

                    list.Add(rel);
                }
            }

            if (stopwatch != null)
            {
                stopwatch.Stop();
                AsyncProfiler.Record("FileSystemRepository.ListFiles", stopwatch.Elapsed.TotalMilliseconds);
            }

            return Task.FromResult(list);
        }

        /// <inheritdoc />
        public async Task UploadFileAsync(string relativePath, string localAbsolutePath)
        {
            // Build the destination path in the repository
            string dst = Join(Root, relativePath);

            // Ensure the destination directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dst));

            // Copy the file asynchronously, overwriting if it already exists
            await AsyncProfiler.MeasureAsync("FileSystemRepository.UploadFile", () => Task.Run(() => File.Copy(localAbsolutePath, dst, overwrite: true)));

            // Invalidate cache since we just created/updated the file
            InvalidateFileCache(dst);
        }

        /// <inheritdoc />
        public async Task DownloadFileAsync(string relativePath, string localAbsolutePath)
        {
            // Build the source path in the repository
            string src = Join(Root, relativePath);

            // Ensure the destination directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(localAbsolutePath));

            // Check if the source file exists
            if (!FileExistsCached(src))
            {
                throw new FileNotFoundException(src);
            }

            // Copy the file asynchronously, overwriting if it already exists
            await AsyncProfiler.MeasureAsync("FileSystemRepository.DownloadFile", () => Task.Run(() => File.Copy(src, localAbsolutePath, overwrite: true)));
        }

        /// <summary>
        /// Deletes a specific model version from the repository.
        /// Recursively removes the entire version directory including all payload files, images, and metadata.
        /// Invalidates all relevant caches after deletion.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version string to delete (e.g., "1.0.0").</param>
        /// <returns>True if the version was successfully deleted; false if it didn't exist or deletion failed.</returns>
        public async Task<bool> DeleteVersionAsync(string modelId, string version)
        {
            string versionDir = Path.Combine(Root, modelId, version);
            string normalizedVersionDir = PathUtils.NormalizePath(versionDir);

            // Check if the version directory exists
            if (!Directory.Exists(normalizedVersionDir))
            {
                return false; // Version doesn't exist
            }

            try
            {
                // Delete the entire version directory asynchronously to avoid blocking the UI thread
                await AsyncProfiler.MeasureAsync("FileSystemRepository.DeleteVersion", () => Task.Run(() =>
                {
                    Directory.Delete(normalizedVersionDir, recursive: true);
                }));

                // Invalidate caches for this path to ensure fresh data on subsequent operations
                _directoryExistsCache.TryRemove(normalizedVersionDir, out _);
                _directoryContentsCache.TryRemove(normalizedVersionDir, out _);
                _directoryCacheTimestamps.TryRemove(normalizedVersionDir, out _);

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Delete Version Failed", 
                    $"Failed to delete version {modelId}/{version}: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}, Version: {version}");
                return false;
            }
        }

        /// <summary>
        /// Deletes an entire model from the repository.
        /// Recursively removes the entire model directory including all versions, payload files, images, and metadata.
        /// Invalidates all relevant caches after deletion.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <returns>True if the model was successfully deleted; false if it didn't exist or deletion failed.</returns>
        public async Task<bool> DeleteModelAsync(string modelId)
        {
            string modelDir = Path.Combine(Root, modelId);
            string normalizedModelDir = PathUtils.NormalizePath(modelDir);

            // Check if the model directory exists
            if (!Directory.Exists(normalizedModelDir))
            {
                return false; // Model doesn't exist
            }

            try
            {
                // Delete the entire model directory asynchronously to avoid blocking the UI thread
                await AsyncProfiler.MeasureAsync("FileSystemRepository.DeleteModel", () => Task.Run(() =>
                {
                    Directory.Delete(normalizedModelDir, recursive: true);
                }));

                // Invalidate caches for this path and all subdirectories to ensure fresh data on subsequent operations
                _directoryExistsCache.TryRemove(normalizedModelDir, out _);
                _directoryContentsCache.TryRemove(normalizedModelDir, out _);
                _directoryCacheTimestamps.TryRemove(normalizedModelDir, out _);

                // Also invalidate any cached entries that start with the model directory path
                var keysToRemove = new List<string>();
                foreach (string key in _directoryExistsCache.Keys)
                {
                    if (key.StartsWith(normalizedModelDir, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }
                foreach (string key in keysToRemove)
                {
                    _directoryExistsCache.TryRemove(key, out _);
                    _directoryContentsCache.TryRemove(key, out _);
                    _directoryCacheTimestamps.TryRemove(key, out _);
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Delete Model Failed", 
                    $"Failed to delete model {modelId}: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}");
                return false;
            }
        }
    }
}
