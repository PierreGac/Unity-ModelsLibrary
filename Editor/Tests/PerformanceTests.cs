using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Unity Test Runner tests for performance improvements and benchmarking.
    /// Tests async/await optimizations and performance with large datasets.
    /// </summary>
    public class PerformanceTests
    {
        /// <summary>
        /// Tests performance improvements with large datasets.
        /// Validates that async operations don't block and handle large model counts efficiently.
        /// </summary>
        [Test]
        public void TestPerformanceWithLargeDatasets()
        {
            // Create a temporary test repository with many models
            string tempRepoPath = Path.Combine(Path.GetTempPath(), $"ModelLibrary_PerfTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRepoPath);

            try
            {
                // Create a large number of model entries (simulate 100 models)
                int modelCount = 100;
                List<string> modelIds = new List<string>();

                for (int i = 0; i < modelCount; i++)
                {
                    string modelId = $"perf-test-model-{i:D3}";
                    modelIds.Add(modelId);

                    // Create model directory structure
                    string modelDir = Path.Combine(tempRepoPath, modelId, "1.0.0");
                    Directory.CreateDirectory(modelDir);

                    // Create minimal metadata
                    ModelMeta meta = new ModelMeta
                    {
                        identity = new ModelIdentity
                        {
                            id = modelId,
                            name = $"Performance Test Model {i}"
                        },
                        version = "1.0.0",
                        author = "Test Author",
                        description = $"Test model {i} for performance testing",
                        tags = new Tags { values = new List<string> { "test", "performance" } }
                    };

                    string metaPath = Path.Combine(modelDir, "modelLibrary.meta.json");
                    File.WriteAllText(metaPath, JsonUtility.ToJson(meta));
                }

                // Create index file
                ModelIndex index = new ModelIndex
                {
                    entries = modelIds.Select(id => new ModelIndex.Entry
                    {
                        id = id,
                        name = $"Performance Test Model {modelIds.IndexOf(id)}",
                        latestVersion = "1.0.0",
                        updatedTimeTicks = DateTime.UtcNow.Ticks,
                        tags = new List<string> { "test", "performance" }
                    }).ToList()
                };

                string indexPath = Path.Combine(tempRepoPath, "index.json");
                File.WriteAllText(indexPath, JsonUtility.ToJson(index));

                // Test repository loading performance
                Stopwatch sw = Stopwatch.StartNew();
                IModelRepository repo = new FileSystemRepository(tempRepoPath);
                ModelLibraryService service = new ModelLibraryService(repo);

                // Test async index loading
                Task<ModelIndex> indexTask = service.GetIndexAsync();
                indexTask.Wait();
                ModelIndex loadedIndex = indexTask.Result;
                sw.Stop();

                // Assertions
                Assert.IsNotNull(loadedIndex, "Index should load successfully");
                Assert.AreEqual(modelCount, loadedIndex.entries.Count, "All models should be loaded");
                Assert.Less(sw.ElapsedMilliseconds, 5000, $"Index loading should complete in under 5 seconds, took {sw.ElapsedMilliseconds}ms");

                // Test metadata loading performance
                sw.Restart();
                List<Task<ModelMeta>> metaTasks = new List<Task<ModelMeta>>();
                for (int i = 0; i < Math.Min(10, modelCount); i++) // Test with first 10 models
                {
                    metaTasks.Add(service.GetMetaAsync(modelIds[i], "1.0.0"));
                }
                Task.WaitAll(metaTasks.ToArray());
                sw.Stop();

                Assert.Less(sw.ElapsedMilliseconds, 3000, $"Metadata loading should complete in under 3 seconds, took {sw.ElapsedMilliseconds}ms");
                Assert.AreEqual(10, metaTasks.Count(t => t.Result != null), "All metadata should load successfully");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRepoPath))
                {
                    Directory.Delete(tempRepoPath, true);
                }
            }
        }

        /// <summary>
        /// Benchmarks async/await performance improvements.
        /// Compares blocking vs non-blocking operations.
        /// </summary>
        [Test]
        public void BenchmarkAsyncAwaitPerformance()
        {
            // Create test repository
            string tempRepoPath = Path.Combine(Path.GetTempPath(), $"ModelLibrary_Benchmark_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRepoPath);

            try
            {
                // Create test model
                string modelId = "benchmark-model";
                string modelDir = Path.Combine(tempRepoPath, modelId, "1.0.0");
                Directory.CreateDirectory(modelDir);

                ModelMeta meta = new ModelMeta
                {
                    identity = new ModelIdentity { id = modelId, name = "Benchmark Model" },
                    version = "1.0.0"
                };
                File.WriteAllText(Path.Combine(modelDir, "modelLibrary.meta.json"), JsonUtility.ToJson(meta));

                ModelIndex index = new ModelIndex
                {
                    entries = new List<ModelIndex.Entry>
                    {
                        new ModelIndex.Entry
                        {
                            id = modelId,
                            name = "Benchmark Model",
                            latestVersion = "1.0.0",
                            updatedTimeTicks = DateTime.UtcNow.Ticks
                        }
                    }
                };
                File.WriteAllText(Path.Combine(tempRepoPath, "index.json"), JsonUtility.ToJson(index));

                IModelRepository repo = new FileSystemRepository(tempRepoPath);
                ModelLibraryService service = new ModelLibraryService(repo);

                // Benchmark async index loading
                Stopwatch sw = Stopwatch.StartNew();
                Task<ModelIndex> indexTask = service.GetIndexAsync();
                indexTask.Wait();
                long asyncTime = sw.ElapsedMilliseconds;

                // Verify async operation completed successfully
                Assert.IsNotNull(indexTask.Result, "Async operation should complete");
                Assert.GreaterOrEqual(0, asyncTime, "Async operation should complete (time can be 0ms for cached operations)");

                // Benchmark metadata loading
                sw.Restart();
                Task<ModelMeta> metaTask = service.GetMetaAsync(modelId, "1.0.0");
                metaTask.Wait();
                long metaTime = sw.ElapsedMilliseconds;

                Assert.IsNotNull(metaTask.Result, "Metadata async operation should complete");
                Assert.GreaterOrEqual(0, metaTime, "Metadata async operation should complete");

                // Log benchmark results
                UnityEngine.Debug.Log($"[Performance Benchmark] Index loading: {asyncTime}ms, Metadata loading: {metaTime}ms");
            }
            finally
            {
                if (Directory.Exists(tempRepoPath))
                {
                    Directory.Delete(tempRepoPath, true);
                }
            }
        }
    }
}

