using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // Cache for file existence checks to avoid repeated slow File.Exists() calls
        private readonly ConcurrentDictionary<string, bool> _fileExistsCache = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, bool> _directoryExistsCache = new ConcurrentDictionary<string, bool>();
        
        // Cache for directory contents to avoid repeated network calls
        private readonly ConcurrentDictionary<string, HashSet<string>> _directoryContentsCache = new ConcurrentDictionary<string, HashSet<string>>();
        private readonly ConcurrentDictionary<string, DateTime> _directoryCacheTimestamps = new ConcurrentDictionary<string, DateTime>();
        private static readonly TimeSpan DirectoryCacheExpiry = TimeSpan.FromMinutes(5); // Cache directory contents for 5 minutes

        /// <summary>
        /// Initialize the repository with a root directory path.
        /// </summary>
        /// <param name="root">The root directory path (local or UNC)</param>
        public FileSystemRepository(string root)
        {
            // Normalize path separators to match the current operating system
            Root = root.Replace('/', Path.DirectorySeparatorChar);
        }

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
        /// For network drives, uses directory listing cache to minimize network calls.
        /// </summary>
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
                        var directoryContents = GetDirectoryContentsCached(directory);
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
        /// Check if a path is on a network drive (UNC path or mapped network drive).
        /// </summary>
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
                    var drive = new DriveInfo(path.Substring(0, 2));
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
        /// Get directory contents with caching to minimize network calls.
        /// </summary>
        private HashSet<string> GetDirectoryContentsCached(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return null;

            var now = DateTime.UtcNow;
            
            // Check if we have a valid cached result
            if (_directoryContentsCache.TryGetValue(directoryPath, out var cachedContents) &&
                _directoryCacheTimestamps.TryGetValue(directoryPath, out var cacheTime) &&
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
                var files = Directory.GetFiles(directoryPath);
                var fileNames = new HashSet<string>(files.Length);
                
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
        /// </summary>
        private bool DirectoryExistsCached(string path)
        {
            return _directoryExistsCache.GetOrAdd(path, p => Directory.Exists(p));
        }

        /// <summary>
        /// Invalidate file existence cache for a specific path (call after file operations).
        /// </summary>
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
        /// Invalidate directory existence cache for a specific path (call after directory operations).
        /// </summary>
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

        /// <inheritdoc />
        public async Task<ModelIndex> LoadIndexAsync()
        {
            // Build the full path to the models index file
            string path = Join(Root, "models_index.json");

            // If the index file doesn't exist, return an empty index (new repository)
            UnityEngine.Debug.Log($"Loading index from {path}");
            if (!FileExistsCached(path))
            {
                return new ModelIndex();
            }

            // Read the JSON file asynchronously to avoid blocking the UI thread
            string json = await File.ReadAllTextAsync(path);

            // Parse the JSON into a ModelIndex object, or return empty if parsing fails
            return JsonUtil.FromJson<ModelIndex>(json) ?? new ModelIndex();
        }

        /// <inheritdoc />
        public async Task SaveIndexAsync(ModelIndex index)
        {
            // Build the full path to the models index file
            string path = Join(Root, "models_index.json");

            // Ensure the directory exists (in case the root directory doesn't exist yet)
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Convert the index to JSON format
            string json = JsonUtil.ToJson(index);

            // Write the JSON file asynchronously to avoid blocking the UI thread
            await File.WriteAllTextAsync(path, json);
            
            // Invalidate cache since we just created/updated the file
            InvalidateFileCache(path);
        }

        /// <inheritdoc />
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
            string json = await File.ReadAllTextAsync(path);

            // Parse and return the metadata
            return JsonUtil.FromJson<ModelMeta>(json);
        }

        /// <inheritdoc />
        public async Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
        {
            // Build the path to the model.json file: <root>/<modelId>/<version>/model.json
            string path = Join(Root, Join(modelId, Join(version, ModelMeta.MODEL_JSON)));

            // Ensure the directory structure exists
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Convert the metadata to JSON format
            string json = JsonUtil.ToJson(meta);

            // Write the JSON file asynchronously
            await File.WriteAllTextAsync(path, json);
            
            // Invalidate cache since we just created/updated the file
            InvalidateFileCache(path);
        }

        /// <inheritdoc />
        public Task<bool> DirectoryExistsAsync(string relativePath)
        {
            // Check if the directory exists at the repository-relative path
            string fullPath = Join(Root, relativePath);
            return Task.FromResult(DirectoryExistsCached(fullPath));
        }

        /// <inheritdoc />
        public Task EnsureDirectoryAsync(string relativePath)
        {
            // Create the directory (and any parent directories) if they don't exist
            string fullPath = Join(Root, relativePath);
            Directory.CreateDirectory(fullPath);
            
            // Invalidate directory cache since we just created it
            InvalidateDirectoryCache(fullPath);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<List<string>> ListFilesAsync(string relativeDir)
        {
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
            await Task.Run(() => File.Copy(localAbsolutePath, dst, overwrite: true));
            
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
            await Task.Run(() => File.Copy(src, localAbsolutePath, overwrite: true));
        }
    }
}
