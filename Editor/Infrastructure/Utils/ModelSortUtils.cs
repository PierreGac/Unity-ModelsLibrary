using System;
using System.Collections.Generic;
using ModelLibrary.Data;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Sort mode for model entries.
    /// </summary>
    public enum ModelSortMode
    {
        Name,
        Date,
        Version
    }

    /// <summary>
    /// Utility class for sorting model index entries.
    /// </summary>
    public static class ModelSortUtils
    {
        /// <summary>
        /// Returns a sorted copy of the supplied list.
        /// </summary>
        public static List<ModelIndex.Entry> SortEntries(List<ModelIndex.Entry> entries, ModelSortMode mode)
        {
            if (entries == null)
            {
                return new List<ModelIndex.Entry>();
            }

            List<ModelIndex.Entry> copy = new List<ModelIndex.Entry>(entries);
            SortEntriesInPlace(copy, mode);
            return copy;
        }

        /// <summary>
        /// Sorts an existing buffer without allocating a second list. Intended for
        /// per-OnGUI browser filtering where the same list is reused every event.
        /// </summary>
        public static void SortEntriesInPlace(List<ModelIndex.Entry> entries, ModelSortMode mode)
        {
            if (entries == null || entries.Count < 2)
            {
                return;
            }

            switch (mode)
            {
                case ModelSortMode.Date:
                    entries.Sort(CompareByDateDescending);
                    break;
                case ModelSortMode.Version:
                    entries.Sort(CompareByVersionDescending);
                    break;
                default:
                    entries.Sort(CompareByName);
                    break;
            }
        }

        private static int CompareByName(ModelIndex.Entry left, ModelIndex.Entry right)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(left?.name, right?.name);
        }

        private static int CompareByDateDescending(ModelIndex.Entry left, ModelIndex.Entry right)
        {
            long leftTicks = left?.updatedTimeTicks ?? 0L;
            long rightTicks = right?.updatedTimeTicks ?? 0L;
            return rightTicks.CompareTo(leftTicks);
        }

        private static int CompareByVersionDescending(ModelIndex.Entry left, ModelIndex.Entry right)
        {
            bool leftParsed = SemVer.TryParse(left?.latestVersion, out SemVer leftVersion);
            bool rightParsed = SemVer.TryParse(right?.latestVersion, out SemVer rightVersion);

            if (leftParsed && rightParsed)
            {
                return rightVersion.CompareTo(leftVersion);
            }
            if (leftParsed)
            {
                return -1;
            }
            if (rightParsed)
            {
                return 1;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(right?.latestVersion, left?.latestVersion);
        }
    }
}
