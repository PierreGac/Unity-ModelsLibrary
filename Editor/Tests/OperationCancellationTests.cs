using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using ModelLibrary.Editor.Windows;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Download, submit, batch, and import stop at the next phase boundary when cancelled.
    /// </summary>
    public class OperationCancellationTests
    {
        private const string MODEL_ID = "cancel-model";
        private const string MODEL_VERSION = "1.0.0";
        private const string MODEL_NAME = "Cancel Model";
        private const string OBJ_FILE_NAME = "Cube.obj";
        private const string OBJ_BODY = "o CancelCube\nv 0 0 0\nf 1 1 1\n";
        private const string FIRST_PAYLOAD = "payload/A.obj";
        private const string SECOND_PAYLOAD = "payload/B.obj";
        private const int LISTED_FILE_COUNT = 2;
        private const int BATCH_ITEM_COUNT = 2;
        private const string IMPORT_DESTINATION = "Assets/__CancelImport6A";
        private const string USER_NAME_PREF = "ModelLibrary.UserName";
        private const string ANONYMOUS_USER = "anonymous";
        private const string BATCH_TEMP_SEARCH = "BatchUpload_*";
        private const string CHANGE_SUMMARY = "Cancel test";

        /// <summary>
        /// A cancelled download stops before the second file and removes the partial cache.
        /// </summary>
        [Test]
        public async Task Download_CancelBeforeSecondFile_DeletesCache()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancelRepository repository = new CancelRepository(cancellation);
            ModelLibraryService service = new ModelLibraryService(repository);
            string cacheRoot = VersionCacheRoot(MODEL_ID, MODEL_VERSION);
            bool cancelled = false;

            try
            {
                await service.DownloadModelVersionAsync(MODEL_ID, MODEL_VERSION, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, true);
                }

                cancellation.Dispose();
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(1, repository.DownloadCount);
            Assert.AreEqual(0, repository.SaveMetaCount);
            Assert.IsFalse(Directory.Exists(cacheRoot));
        }

        /// <summary>
        /// A cancelled submit does not upload, save metadata, or write the index.
        /// </summary>
        [Test]
        public async Task Submit_CancelBeforeUpload_DoesNotPublish()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ModelLibraryCancelSubmit");
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancelRepository repository = new CancelRepository(cancellation);
            ModelLibraryService service = new ModelLibraryService(repository);
            bool cancelled = false;

            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, OBJ_FILE_NAME), OBJ_BODY);
                ModelMeta meta = new ModelMeta();
                meta.identity = new ModelIdentity();
                meta.identity.id = MODEL_ID;
                meta.identity.name = MODEL_NAME;
                meta.version = MODEL_VERSION;
                await service.SubmitNewVersionAsync(meta, folder, CHANGE_SUMMARY, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }

                cancellation.Dispose();
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(0, repository.UploadCount);
            Assert.AreEqual(0, repository.SaveMetaCount);
            Assert.AreEqual(0, repository.SaveIndexCount);
        }

        /// <summary>
        /// A cancelled batch stops before the next item and deletes its temporary folder.
        /// </summary>
        [Test]
        public async Task Batch_CancelDuringFirstSubmit_StopsAndDeletesTemp()
        {
            string root = Path.Combine(Path.GetTempPath(), "ModelLibraryCancelBatch");
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancelRepository repository = new CancelRepository(cancellation);
            ModelLibraryService service = new ModelLibraryService(repository);
            BatchUploadService batch = new BatchUploadService(service, new SimpleUserIdentityProvider());
            HashSet<string> tempsBefore = SnapshotBatchTemps();
            bool cancelled = false;

            try
            {
                List<BatchUploadService.BatchUploadItem> items = new List<BatchUploadService.BatchUploadItem>();
                for (int i = 0; i < BATCH_ITEM_COUNT; i++)
                {
                    string folder = Path.Combine(root, "item-" + i.ToString());
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, OBJ_FILE_NAME), OBJ_BODY);
                    BatchUploadService.BatchUploadItem item = new BatchUploadService.BatchUploadItem();
                    item.folderPath = folder;
                    item.modelName = MODEL_NAME + " " + i.ToString();
                    item.version = MODEL_VERSION;
                    item.selected = true;
                    items.Add(item);
                }

                await batch.UploadBatchAsync(items, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }

                cancellation.Dispose();
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(0, repository.UploadCount);
            Assert.AreEqual(0, repository.SaveMetaCount);
            AssertNoNewBatchTemps(tempsBefore);
        }

        /// <summary>
        /// An already cancelled import does not create the destination folder.
        /// </summary>
        [Test]
        public async Task Import_PreCancelledToken_DoesNotCreateDestination()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            string destination = Path.GetFullPath(IMPORT_DESTINATION);
            bool cancelled = false;
            ModelMeta meta = new ModelMeta();
            meta.identity = new ModelIdentity();
            meta.identity.id = MODEL_ID;
            meta.identity.name = MODEL_NAME;
            meta.version = MODEL_VERSION;

            try
            {
                await ModelProjectImporter.ImportFromCacheAsync(
                    Path.GetTempPath(),
                    meta,
                    true,
                    IMPORT_DESTINATION,
                    false,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }

                cancellation.Dispose();
            }

            Assert.IsTrue(cancelled);
            Assert.IsFalse(Directory.Exists(destination));
        }

        /// <summary>
        /// Disabling the library window cancels the token later operations observe.
        /// </summary>
        [Test]
        public void WindowDisable_CancelsOperationToken()
        {
            bool hadUser = EditorPrefs.HasKey(USER_NAME_PREF);
            string savedUser = EditorPrefs.GetString(USER_NAME_PREF, string.Empty);
            EditorPrefs.SetString(USER_NAME_PREF, ANONYMOUS_USER);
            ModelLibraryWindow window = ScriptableObject.CreateInstance<ModelLibraryWindow>();

            try
            {
                CancellationToken token = window.OperationCancellationToken;
                window.CancelOperationsOnDisable();
                Assert.IsTrue(token.IsCancellationRequested);
                CancellationToken next = window.OperationCancellationToken;
                Assert.IsFalse(next.IsCancellationRequested);
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }

                if (hadUser)
                {
                    EditorPrefs.SetString(USER_NAME_PREF, savedUser);
                }
                else
                {
                    EditorPrefs.DeleteKey(USER_NAME_PREF);
                }
            }
        }

        private static string VersionCacheRoot(string id, string version)
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            return EditorPaths.LibraryPath(Path.Combine(settings.localCacheRoot, id, version));
        }

        private static HashSet<string> SnapshotBatchTemps()
        {
            string[] existing = Directory.GetDirectories(Path.GetTempPath(), BATCH_TEMP_SEARCH);
            return new HashSet<string>(existing);
        }

        private static void AssertNoNewBatchTemps(HashSet<string> tempsBefore)
        {
            string[] existing = Directory.GetDirectories(Path.GetTempPath(), BATCH_TEMP_SEARCH);
            for (int i = 0; i < existing.Length; i++)
            {
                Assert.IsTrue(tempsBefore.Contains(existing[i]), existing[i]);
            }
        }

        /// <summary>
        /// Repository double that cancels on the first directory create and counts later writes.
        /// </summary>
        private sealed class CancelRepository : IModelRepository
        {
            private readonly CancellationTokenSource _cancellation;

            public CancelRepository(CancellationTokenSource cancellation)
            {
                _cancellation = cancellation;
            }

            public int DownloadCount { get; private set; }

            public int UploadCount { get; private set; }

            public int SaveMetaCount { get; private set; }

            public int SaveIndexCount { get; private set; }

            public string Root => "cancel-test";

            public Task<ModelIndex> LoadIndexAsync()
            {
                throw new NotImplementedException();
            }

            public Task SaveIndexAsync(ModelIndex index)
            {
                SaveIndexCount++;
                return Task.CompletedTask;
            }

            public Task<bool> TrySaveIndexIfUnchangedAsync(ModelIndex index, long expectedRevision)
            {
                SaveIndexCount++;
                return Task.FromResult(true);
            }

            public Task<ModelMeta> LoadMetaAsync(string modelId, string version)
            {
                ModelMeta meta = new ModelMeta();
                meta.identity = new ModelIdentity();
                meta.identity.id = modelId;
                meta.identity.name = MODEL_NAME;
                meta.version = version;
                return Task.FromResult(meta);
            }

            public Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
            {
                SaveMetaCount++;
                return Task.CompletedTask;
            }

            public Task<bool> DirectoryExistsAsync(string relativePath)
            {
                return Task.FromResult(false);
            }

            public Task EnsureDirectoryAsync(string relativePath)
            {
                _cancellation.Cancel();
                return Task.CompletedTask;
            }

            public Task<List<string>> ListFilesAsync(string relativeDir)
            {
                List<string> files = new List<string>();
                files.Add(MODEL_ID + "/" + MODEL_VERSION + "/" + FIRST_PAYLOAD);
                files.Add(MODEL_ID + "/" + MODEL_VERSION + "/" + SECOND_PAYLOAD);
                Assert.AreEqual(LISTED_FILE_COUNT, files.Count);
                return Task.FromResult(files);
            }

            public Task UploadFileAsync(string relativePath, string localAbsolutePath)
            {
                UploadCount++;
                return Task.CompletedTask;
            }

            public Task DownloadFileAsync(string relativePath, string localAbsolutePath)
            {
                DownloadCount++;
                if (DownloadCount == 1)
                {
                    _cancellation.Cancel();
                }

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
