using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for managing preview texture operations.
    /// Handles loading and caching preview textures from the repository.
    /// </summary>
    public class ModelPreviewService
    {
        private readonly IModelRepository _repo;
        private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();

        public ModelPreviewService(IModelRepository repo)
        {
            _repo = repo;
        }

        public async Task<Texture2D> GetPreviewTextureAsync(string id, string version, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            // SECURITY (CRIT-02): Validate id/version (which come from models_index.json)
            // and reject any ".." in relativePath (which comes from model.json).
            if (!PathUtils.IsSafeIdentifier(id) || !PathUtils.IsSafeIdentifier(version))
            {
                UnityEngine.Debug.LogWarning($"[ModelPreviewService] Rejected unsafe id/version: {id}/{version}");
                return null;
            }
            string safeRelativePath = PathUtils.ValidateRelativePathStrict(relativePath);

            string key = PathUtils.SanitizePathSeparator(($"{id}/{version}/{safeRelativePath}")).ToLowerInvariant();
            if (_previewCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            string cacheRoot = EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, id, version));
            string localPath = Path.Combine(cacheRoot, safeRelativePath.Replace('/', Path.DirectorySeparatorChar));

            // SECURITY (CRIT-02): Assert localPath is inside cacheRoot before any IO.
            PathUtils.AssertInsideRoot(localPath, cacheRoot);

            if (!File.Exists(localPath))
            {
                string directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string repoPath = PathUtils.SanitizePathSeparator(($"{id}/{version}/{safeRelativePath}"));
                await AsyncProfiler.MeasureAsync("Repository.DownloadFile", () => _repo.DownloadFileAsync(repoPath, localPath));
            }

            if (!File.Exists(localPath))
            {
                return null;
            }

            byte[] data = await AsyncProfiler.MeasureAsync("IO.ReadTextureBytes", () => File.ReadAllBytesAsync(localPath));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!texture.LoadImage(data))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            texture.Apply();

            _previewCache[key] = texture;
            return texture;
        }
    }
}

