using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Batch upload keeps each source file when two files share a name.
    /// </summary>
    public class BatchRelativePathTests
    {
        private const string MODEL_FOLDER_NAME = "Hero";
        private const string MODEL_VERSION = "1.0.0";
        private const string BODY_RELATIVE = "payload/body/Cube.obj";
        private const string LOD_RELATIVE = "payload/lod/Cube.obj";
        private const string SHOT_RELATIVE = "images/shots/Preview.png";
        private const string THUMB_RELATIVE = "images/thumbs/Preview.png";
        private const string BODY_CONTENT = "body-cube";
        private const string LOD_CONTENT = "lod-cube";
        private const string SHOT_CONTENT = "shot-preview";
        private const string THUMB_CONTENT = "thumb-preview";
        private const int EXPECTED_SUCCESS_COUNT = 1;
        private const int EXPECTED_FAILURE_COUNT = 0;

        /// <summary>
        /// Two meshes and two images that share a filename are uploaded from their own folders.
        /// </summary>
        [Test]
        public async Task DuplicateBasenames_UploadAsTheirOwnRelativePaths()
        {
            string scanRoot = Path.Combine(Path.GetTempPath(), "ModelLibraryBatchPaths");
            string modelFolder = Path.Combine(scanRoot, MODEL_FOLDER_NAME);
            RecordingRepository repository = new RecordingRepository();
            ModelLibraryService service = new ModelLibraryService(repository);
            BatchUploadService batch = new BatchUploadService(service, new SimpleUserIdentityProvider());

            try
            {
                WriteFile(modelFolder, "body/Cube.obj", BODY_CONTENT);
                WriteFile(modelFolder, "lod/Cube.obj", LOD_CONTENT);
                WriteFile(modelFolder, "shots/Preview.png", SHOT_CONTENT);
                WriteFile(modelFolder, "thumbs/Preview.png", THUMB_CONTENT);

                List<BatchUploadService.BatchUploadItem> items = BatchUploadService.ScanDirectoryForModels(scanRoot);
                Assert.AreEqual(EXPECTED_SUCCESS_COUNT, items.Count);

                BatchUploadService.BatchUploadResult result = await batch.UploadBatchAsync(items);
                Assert.AreEqual(EXPECTED_SUCCESS_COUNT, result.successfulUploads.Count);
                Assert.AreEqual(EXPECTED_FAILURE_COUNT, result.failedUploads.Count);
                Assert.AreEqual(BODY_CONTENT, repository.ContentEndingWith(BODY_RELATIVE));
                Assert.AreEqual(LOD_CONTENT, repository.ContentEndingWith(LOD_RELATIVE));
                Assert.AreEqual(SHOT_CONTENT, repository.ContentEndingWith(SHOT_RELATIVE));
                Assert.AreEqual(THUMB_CONTENT, repository.ContentEndingWith(THUMB_RELATIVE));
                Assert.IsTrue(ContainsPath(repository.SavedMeta.payloadRelativePaths, BODY_RELATIVE));
                Assert.IsTrue(ContainsPath(repository.SavedMeta.payloadRelativePaths, LOD_RELATIVE));
                Assert.IsTrue(ContainsPath(repository.SavedMeta.imageRelativePaths, SHOT_RELATIVE));
                Assert.IsTrue(ContainsPath(repository.SavedMeta.imageRelativePaths, THUMB_RELATIVE));
            }
            finally
            {
                if (Directory.Exists(scanRoot))
                {
                    Directory.Delete(scanRoot, true);
                }
            }
        }

        private static void WriteFile(string modelFolder, string relativePath, string content)
        {
            string absolute = Path.Combine(modelFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absolute);
            Directory.CreateDirectory(directory);
            File.WriteAllText(absolute, content);
        }

        private static bool ContainsPath(List<string> paths, string relativePath)
        {
            if (paths == null)
            {
                return false;
            }

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Records the bytes uploaded for each repository path.
        /// </summary>
        private sealed class RecordingRepository : IModelRepository
        {
            private readonly Dictionary<string, string> _contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public ModelMeta SavedMeta { get; private set; }

            public string Root => "batch-paths";

            public string ContentEndingWith(string relativeSuffix)
            {
                List<string> keys = new List<string>(_contents.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i].Replace('\\', '/');
                    if (key.EndsWith(relativeSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return _contents[keys[i]];
                    }
                }

                Assert.Fail("Missing upload " + relativeSuffix);
                return string.Empty;
            }

            public Task<ModelIndex> LoadIndexAsync()
            {
                return Task.FromResult(new ModelIndex());
            }

            public Task SaveIndexAsync(ModelIndex index)
            {
                return Task.CompletedTask;
            }

            public Task<bool> TrySaveIndexIfUnchangedAsync(ModelIndex index, long expectedRevision)
            {
                return Task.FromResult(true);
            }

            public Task<ModelMeta> LoadMetaAsync(string modelId, string version)
            {
                throw new NotImplementedException();
            }

            public Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
            {
                SavedMeta = meta;
                return Task.CompletedTask;
            }

            public Task<bool> DirectoryExistsAsync(string relativePath)
            {
                return Task.FromResult(false);
            }

            public Task EnsureDirectoryAsync(string relativePath)
            {
                return Task.CompletedTask;
            }

            public Task<List<string>> ListFilesAsync(string relativeDir)
            {
                return Task.FromResult(new List<string>());
            }

            public Task UploadFileAsync(string relativePath, string localAbsolutePath)
            {
                _contents[relativePath] = File.ReadAllText(localAbsolutePath);
                return Task.CompletedTask;
            }

            public Task DownloadFileAsync(string relativePath, string localAbsolutePath)
            {
                throw new NotImplementedException();
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
