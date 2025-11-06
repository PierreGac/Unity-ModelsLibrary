using System;
using System.Collections.Generic;
using ModelLibrary.Data;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages tag counting and caching for the Model Library browser.
    /// Tracks how many models use each tag and maintains a sorted list of tags.
    /// </summary>
    public class TagCacheManager
    {
        private readonly Dictionary<string, int> _tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _sortedTags = new List<string>();

        /// <summary>
        /// Gets the dictionary of tag counts (case-insensitive keys).
        /// </summary>
        public IReadOnlyDictionary<string, int> TagCounts => _tagCounts;

        /// <summary>
        /// Gets the sorted list of all available tags.
        /// </summary>
        public IReadOnlyList<string> SortedTags => _sortedTags;

        /// <summary>
        /// Updates the tag cache by analyzing all entries in the model index.
        /// </summary>
        /// <param name="index">The model index to analyze.</param>
        public void UpdateTagCache(ModelIndex index)
        {
            _tagCounts.Clear();
            _sortedTags.Clear();

            if (index?.entries == null)
            {
                return;
            }

            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelIndex.Entry entry = index.entries[i];
                if (entry?.tags == null)
                {
                    continue;
                }

                for (int j = 0; j < entry.tags.Count; j++)
                {
                    string tag = entry.tags[j];
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    if (_tagCounts.TryGetValue(tag, out int count))
                    {
                        _tagCounts[tag] = count + 1;
                    }
                    else
                    {
                        _tagCounts[tag] = 1;
                    }
                }
            }

            if (_tagCounts.Count > 0)
            {
                _sortedTags.AddRange(_tagCounts.Keys);
                _sortedTags.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}

