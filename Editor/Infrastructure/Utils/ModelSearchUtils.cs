using System;
using ModelLibrary.Data;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for searching and matching model index entries.
    /// Provides support for advanced search queries with AND/OR operators.
    /// </summary>
    public static class ModelSearchUtils
    {
        /// <summary>
        /// Checks if an entry matches the advanced search query.
        /// Supports AND/OR operators for tags: "tag1 AND tag2", "tag1 OR tag2"
        /// Also matches against model name and description.
        /// </summary>
        /// <param name="entry">The model index entry to check.</param>
        /// <param name="query">The search query string.</param>
        /// <returns>True if the entry matches the query, false otherwise.</returns>
        public static bool EntryMatchesAdvancedSearch(ModelIndex.Entry entry, string query)
        {
            if (entry == null || string.IsNullOrEmpty(query))
            {
                return false;
            }

            string trimmedQuery = query.Trim();

            // Check for AND/OR operators (case-insensitive)
            if (trimmedQuery.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // AND operator: all terms must match
                string[] terms = trimmedQuery.Split(new[] { " AND " }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < terms.Length; i++)
                {
                    if (!EntryMatchesTerm(entry, terms[i].Trim()))
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (trimmedQuery.IndexOf(" OR ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // OR operator: at least one term must match
                string[] terms = trimmedQuery.Split(new[] { " OR " }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < terms.Length; i++)
                {
                    if (EntryMatchesTerm(entry, terms[i].Trim()))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                // Simple search: match the whole query
                return EntryMatchesTerm(entry, trimmedQuery);
            }
        }

        /// <summary>
        /// Checks if an entry matches a single search term (name, description, or tags).
        /// </summary>
        /// <param name="entry">The model index entry to check.</param>
        /// <param name="term">The search term to match against.</param>
        /// <returns>True if the entry matches the term, false otherwise.</returns>
        public static bool EntryMatchesTerm(ModelIndex.Entry entry, string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return false;
            }

            // Match against name
            if (!string.IsNullOrEmpty(entry.name) && entry.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Match against description
            if (!string.IsNullOrEmpty(entry.description) && entry.description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Match against tags
            if (entry.tags != null)
            {
                for (int i = 0; i < entry.tags.Count; i++)
                {
                    string tag = entry.tags[i];
                    if (!string.IsNullOrEmpty(tag) && tag.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

