using System;
using System.Collections.Generic;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Shared tag list operations used across editor windows.
    /// Provides case-insensitive duplicate detection and badge label formatting.
    /// </summary>
    public static class TagUtils
    {
        private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Checks whether a tag is already present in the list (case-insensitive).
        /// </summary>
        /// <param name="tags">The tag list to search.</param>
        /// <param name="tag">The tag value to check.</param>
        /// <returns>True when the tag is already present.</returns>
        public static bool ContainsTag(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            string trimmedTag = tag.Trim();
            for (int i = 0; i < tags.Count; i++)
            {
                string existingTag = tags[i];
                if (!string.IsNullOrWhiteSpace(existingTag) && TagComparer.Equals(existingTag.Trim(), trimmedTag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a tag to the list if it is not empty and not already present.
        /// </summary>
        /// <param name="tags">The mutable tag list.</param>
        /// <param name="tag">The tag value to add.</param>
        /// <param name="errorMessage">Error description when the add fails.</param>
        /// <returns>True when the tag was added successfully.</returns>
        public static bool TryAddTag(List<string> tags, string tag, out string errorMessage)
        {
            errorMessage = null;

            if (tags == null)
            {
                errorMessage = "Tag list is not available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                errorMessage = "Tag cannot be empty.";
                return false;
            }

            string trimmedTag = tag.Trim();
            if (ContainsTag(tags, trimmedTag))
            {
                errorMessage = $"Tag '{trimmedTag}' is already on this model.";
                return false;
            }

            tags.Add(trimmedTag);
            return true;
        }

        /// <summary>
        /// Formats a tag value as a badge label with the standard emoji prefix.
        /// </summary>
        /// <param name="tag">The tag value.</param>
        /// <returns>Formatted badge label.</returns>
        public static string FormatBadgeLabel(string tag)
        {
            return string.Concat(UIConstants.TAG_BADGE_EMOJI, " ", tag);
        }
    }
}
