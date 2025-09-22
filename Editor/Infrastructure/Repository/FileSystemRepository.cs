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

        /// <inheritdoc />
        public async Task<ModelIndex> LoadIndexAsync()
        {
            // Build the full path to the models index file
            string path = Join(Root, "models_index.json");

            // If the index file doesn't exist, return an empty index (new repository)
            if (!File.Exists(path))
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
        }

        /// <inheritdoc />
        public async Task<ModelMeta> LoadMetaAsync(string modelId, string version)
        {
            // Build the path to the model.json file: <root>/<modelId>/<version>/model.json
            string path = Join(Root, Join(modelId, Join(version, ModelMeta.MODEL_JSON)));

            // If the metadata file doesn't exist, throw an exception
            if (!File.Exists(path))
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
        }

        /// <inheritdoc />
        public Task<bool> DirectoryExistsAsync(string relativePath)
        {
            // Check if the directory exists at the repository-relative path
            string fullPath = Join(Root, relativePath);
            return Task.FromResult(Directory.Exists(fullPath));
        }

        /// <inheritdoc />
        public Task EnsureDirectoryAsync(string relativePath)
        {
            // Create the directory (and any parent directories) if they don't exist
            string fullPath = Join(Root, relativePath);
            Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<List<string>> ListFilesAsync(string relativeDir)
        {
            // Build the absolute path to the directory
            string abs = Join(Root, relativeDir);
            List<string> list = new List<string>();

            // Only proceed if the directory actually exists
            if (Directory.Exists(abs))
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
        }

        /// <inheritdoc />
        public async Task DownloadFileAsync(string relativePath, string localAbsolutePath)
        {
            // Build the source path in the repository
            string src = Join(Root, relativePath);

            // Ensure the destination directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(localAbsolutePath));

            // Check if the source file exists
            if (!File.Exists(src))
            {
                throw new FileNotFoundException(src);
            }

            // Copy the file asynchronously, overwriting if it already exists
            await Task.Run(() => File.Copy(src, localAbsolutePath, overwrite: true));
        }
    }
}
