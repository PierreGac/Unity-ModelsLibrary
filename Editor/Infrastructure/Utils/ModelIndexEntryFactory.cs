using System;
using System.Collections.Generic;
using ModelLibrary.Data;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Creates <see cref="ModelIndex.Entry"/> instances from <see cref="ModelMeta"/> for index updates and rebuilds.
    /// </summary>
    public static class ModelIndexEntryFactory
    {
        /// <summary>
        /// Builds an index entry from the latest-version metadata for a model family.
        /// </summary>
        /// <param name="meta">Model metadata for the version to represent in the index.</param>
        /// <returns>A new index entry populated from metadata.</returns>
        public static ModelIndex.Entry FromMeta(ModelMeta meta)
        {
            if (meta == null || meta.identity == null)
            {
                throw new ArgumentException("Metadata and identity are required.", nameof(meta));
            }

            string id = meta.identity.id ?? string.Empty;
            long updatedTicks = meta.updatedTimeTicks > 0 ? meta.updatedTimeTicks : DateTime.Now.Ticks;
            long releaseTicks = meta.uploadTimeTicks > 0
                ? meta.uploadTimeTicks
                : meta.createdTimeTicks > 0 ? meta.createdTimeTicks : DateTime.Now.Ticks;

            List<string> tagList = meta.tags?.values != null
                ? new List<string>(meta.tags.values)
                : new List<string>();

            return new ModelIndex.Entry
            {
                id = id,
                name = meta.identity.name ?? string.Empty,
                latestVersion = meta.version ?? string.Empty,
                description = meta.description ?? string.Empty,
                tags = tagList,
                updatedTimeTicks = updatedTicks,
                releaseTimeTicks = releaseTicks
            };
        }
    }
}
