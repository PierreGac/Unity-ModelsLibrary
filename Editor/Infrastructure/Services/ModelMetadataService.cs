using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Service for managing model metadata operations.
    /// Handles loading, saving, and updating model metadata.
    /// </summary>
    public class ModelMetadataService
    {
        private readonly IModelRepository _repo;

        public ModelMetadataService(IModelRepository repo)
        {
            _repo = repo;
        }

        public async Task<ModelMeta> GetMetaAsync(string id, string version)
        {
            return await AsyncProfiler.MeasureAsync("Service.GetMeta", () => _repo.LoadMetaAsync(id, version));
        }

        /// <summary>
        /// Publishes a metadata-only update by creating a new version with updated metadata.
        /// Clones all files from the base version, creates a new version with bumped version number,
        /// saves the updated metadata, and updates the index. This is used for metadata edits without file changes.
        /// </summary>
        /// <param name="updatedMeta">Updated model metadata with modified fields (description, tags, etc.).</param>
        /// <param name="baseVersion">The version to clone files from (typically the current latest version).</param>
        /// <param name="changeSummary">Changelog summary describing what metadata was changed.</param>
        /// <param name="author">Author name for the metadata update.</param>
        /// <param name="bumpStrategy">Optional function to customize version bumping (defaults to patch increment).</param>
        /// <returns>The updated ModelMeta with the new version number.</returns>
        public async Task<ModelMeta> PublishMetadataUpdateAsync(ModelMeta updatedMeta, string baseVersion, string changeSummary, string author, Func<SemVer, SemVer> bumpStrategy = null)
        {
            if (updatedMeta == null)
            {
                throw new ArgumentNullException(nameof(updatedMeta));
            }

            if (updatedMeta.identity == null || string.IsNullOrWhiteSpace(updatedMeta.identity.id))
            {
                throw new InvalidOperationException("Model identity required for metadata update.");
            }

            string sourceVersion = string.IsNullOrWhiteSpace(baseVersion) ? updatedMeta.version : baseVersion;
            if (string.IsNullOrWhiteSpace(sourceVersion))
            {
                throw new InvalidOperationException("Base version required for metadata update.");
            }

            if (!SemVer.TryParse(sourceVersion, out SemVer parsedSource))
            {
                throw new InvalidOperationException($"Invalid base version '{sourceVersion}'.");
            }

            SemVer bumped = bumpStrategy != null ? bumpStrategy(parsedSource) : new SemVer(parsedSource.major, parsedSource.minor, parsedSource.patch + 1);
            string newVersion = bumped.ToString();

            long nowUtc = DateTime.Now.Ticks;
            if (updatedMeta.createdTimeTicks <= 0)
            {
                updatedMeta.createdTimeTicks = nowUtc;
            }
            updatedMeta.updatedTimeTicks = nowUtc;
            updatedMeta.version = newVersion;

            string resolvedAuthor = string.IsNullOrWhiteSpace(author) ? "unknown" : author;
            if (string.IsNullOrWhiteSpace(updatedMeta.author))
            {
                updatedMeta.author = resolvedAuthor;
            }

            EnsureChangelogEntry(updatedMeta, string.IsNullOrWhiteSpace(changeSummary) ? "Metadata updated" : changeSummary, resolvedAuthor, newVersion, nowUtc);

            await CloneVersionFilesAsync(updatedMeta.identity.id, sourceVersion, newVersion);
            await _repo.SaveMetaAsync(updatedMeta.identity.id, newVersion, updatedMeta);
            return updatedMeta;
        }

        /// <summary>
        /// Clones all files from a source version to a target version in the repository.
        /// Used when creating metadata-only updates - copies all payload and image files to the new version.
        /// Downloads files to a temporary location, then uploads them to the new version path.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="sourceVersion">The version to clone files from.</param>
        /// <param name="targetVersion">The new version to clone files to.</param>
        private async Task CloneVersionFilesAsync(string modelId, string sourceVersion, string targetVersion)
        {
            if (string.Equals(sourceVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string sourceRootRel = $"{modelId}/{sourceVersion}".Replace('\\', '/');
            string targetRootRel = $"{modelId}/{targetVersion}".Replace('\\', '/');

            await _repo.EnsureDirectoryAsync(targetRootRel);

            List<string> files = await _repo.ListFilesAsync(sourceRootRel) ?? new List<string>();
            if (files.Count == 0)
            {
                return;
            }

            string prefix = sourceRootRel.TrimEnd('/') + "/";
            string tempRoot = Path.Combine(Path.GetTempPath(), "ModelClone_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (string repoRel in files)
                {
                    string normalized = PathUtils.SanitizePathSeparator(repoRel);
                    if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string subRel = normalized[prefix.Length..];
                    if (string.Equals(subRel, ModelMeta.MODEL_JSON, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string tempPath = Path.Combine(tempRoot, subRel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? tempRoot);
                    await _repo.DownloadFileAsync(normalized, tempPath);
                    await _repo.UploadFileAsync($"{targetRootRel}/{subRel}", tempPath);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        /// <summary>
        /// Ensures a changelog entry exists for the specified version in the model metadata.
        /// Creates a new entry if one doesn't exist, or updates the existing entry if it does.
        /// Sanitizes the summary and author fields before adding.
        /// </summary>
        /// <param name="meta">Model metadata to add the changelog entry to.</param>
        /// <param name="summary">Changelog summary text.</param>
        /// <param name="author">Author name for the changelog entry.</param>
        /// <param name="version">Version string for the changelog entry.</param>
        /// <param name="timestamp">Timestamp for the changelog entry (in ticks).</param>
        private static void EnsureChangelogEntry(ModelMeta meta, string summary, string author, string version, long timestamp)
        {
            meta.changelog ??= new List<ModelChangelogEntry>();

            string sanitizedSummary = string.IsNullOrWhiteSpace(summary) ? "Updated" : summary.Trim();
            string sanitizedAuthor = string.IsNullOrWhiteSpace(author) ? "unknown" : author.Trim();
            long sanitizedTimestamp = timestamp <= 0 ? DateTime.Now.Ticks : timestamp;

            ModelChangelogEntry existing = meta.changelog.LastOrDefault(e => string.Equals(e.version, version, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                meta.changelog.Add(new ModelChangelogEntry
                {
                    version = version,
                    summary = sanitizedSummary,
                    author = sanitizedAuthor,
                    timestamp = sanitizedTimestamp
                });
            }
            else
            {
                existing.summary = sanitizedSummary;
                existing.author = sanitizedAuthor;
                existing.timestamp = sanitizedTimestamp;
            }
        }
    }
}


