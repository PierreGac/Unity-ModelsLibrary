using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Verifies that a server file listing cannot write a download outside the version cache.
    /// </summary>
    public class HttpDownloadContainmentTests
    {
        private const string MODEL_ID = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
        private const string VERSION = "1.0.0";
        private const string OUTSIDE_FILE_NAME = "outside.dll";
        private const string ESCAPING_LISTING = MODEL_ID + "/" + VERSION + "/../../" + OUTSIDE_FILE_NAME;
        private const string DOWNLOAD_BODY = "escaped";

        /// <summary>
        /// A listing of id/version/../../outside.dll must throw and leave no file outside the version cache.
        /// </summary>
        [Test]
        public async Task DownloadModelVersionAsync_RejectsListingThatEscapesCache()
        {
            string cacheRoot = GetVersionCacheRoot();
            string escapedDestination = Path.GetFullPath(Path.Combine(cacheRoot, "..", "..", OUTSIDE_FILE_NAME));
            bool escapedExisted = File.Exists(escapedDestination);
            ListingRepository repository = new ListingRepository(ESCAPING_LISTING);
            ModelLibraryService service = new ModelLibraryService(repository);

            try
            {
                InvalidOperationException rejected = null;
                try
                {
                    await service.DownloadModelVersionAsync(MODEL_ID, VERSION);
                }
                catch (InvalidOperationException ex)
                {
                    rejected = ex;
                }

                Assert.IsNotNull(rejected, "Expected the escaping listing to be rejected.");
                Assert.AreEqual(0, repository.WrittenPaths.Count);
                if (!escapedExisted)
                {
                    Assert.IsFalse(File.Exists(escapedDestination));
                }
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, true);
                }

                string modelCache = Path.GetDirectoryName(cacheRoot);
                if (!string.IsNullOrEmpty(modelCache) && Directory.Exists(modelCache) && Directory.GetFileSystemEntries(modelCache).Length == 0)
                {
                    Directory.Delete(modelCache);
                }

                if (!escapedExisted && File.Exists(escapedDestination))
                {
                    File.Delete(escapedDestination);
                }
            }
        }

        private static string GetVersionCacheRoot()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            return EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, MODEL_ID, VERSION));
        }

        /// <summary>
        /// Repository double that returns one poisoned listing and writes any download it is asked to perform.
        /// </summary>
        private sealed class ListingRepository : IModelRepository
        {
            private readonly string _listedPath;

            public ListingRepository(string listedPath)
            {
                _listedPath = listedPath;
                WrittenPaths = new List<string>();
            }

            public List<string> WrittenPaths { get; }

            public string Root => "http://downloads.test";

            public Task<ModelIndex> LoadIndexAsync()
            {
                throw new NotImplementedException();
            }

            public Task SaveIndexAsync(ModelIndex index)
            {
                throw new NotImplementedException();
            }

            public Task<bool> TrySaveIndexIfUnchangedAsync(ModelIndex index, long expectedRevision)
            {
                throw new NotImplementedException();
            }

            public Task<ModelMeta> LoadMetaAsync(string modelId, string version)
            {
                ModelMeta meta = new ModelMeta();
                meta.identity = new ModelIdentity();
                meta.identity.id = modelId;
                meta.version = version;
                return Task.FromResult(meta);
            }

            public Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
            {
                throw new NotImplementedException();
            }

            public Task<bool> DirectoryExistsAsync(string relativePath)
            {
                throw new NotImplementedException();
            }

            public Task EnsureDirectoryAsync(string relativePath)
            {
                throw new NotImplementedException();
            }

            public Task<List<string>> ListFilesAsync(string relativeDir)
            {
                List<string> files = new List<string>();
                files.Add(_listedPath);
                return Task.FromResult(files);
            }

            public Task UploadFileAsync(string relativePath, string localAbsolutePath)
            {
                throw new NotImplementedException();
            }

            public Task DownloadFileAsync(string relativePath, string localAbsolutePath)
            {
                WrittenPaths.Add(localAbsolutePath);
                string directory = Path.GetDirectoryName(localAbsolutePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(localAbsolutePath, DOWNLOAD_BODY);
                return Task.CompletedTask;
            }

            public Task<bool> DeleteVersionAsync(string modelId, string version)
            {
                throw new NotImplementedException();
            }

            public Task<bool> DeleteModelAsync(string modelId)
            {
                throw new NotImplementedException();
            }
        }
    }
}
