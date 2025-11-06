using System;
using System.Collections.Generic;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Sort mode for model entries.
    /// </summary>
    public enum ModelSortMode
    {
        /// <summary>Sort by model name (alphabetical).</summary>
        Name,
        /// <summary>Sort by update date (newest first).</summary>
        Date,
        /// <summary>Sort by version number (latest first).</summary>
        Version
    }

    /// <summary>
    /// Utility class for sorting model index entries.
    /// </summary>
    public static class ModelSortUtils
    {
        /// <summary>
        /// Sorts a list of model entries according to the specified sort mode.
        /// </summary>
        /// <param name="entries">List of entries to sort.</param>
        /// <param name="mode">Sort mode to use.</param>
        /// <returns>Sorted list of entries.</returns>
        public static List<ModelIndex.Entry> SortEntries(List<ModelIndex.Entry> entries, ModelSortMode mode)
        {
            return mode switch
            {
                ModelSortMode.Name => entries.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList(),
                ModelSortMode.Date => entries.OrderByDescending(e => e.updatedTimeTicks).ToList(),
                ModelSortMode.Version => entries.OrderByDescending(e =>
                {
                    if (SemVer.TryParse(e.latestVersion, out SemVer v))
                    {
                        return v;
                    }
                    return new SemVer(0, 0, 0);
                }).ToList(),
                _ => entries
            };
        }
    }
}

