using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Mutable IMGUI cache for tag picker layout and selection lookups.
    /// Encapsulates revision tracking to avoid rebuilding badge layout every repaint.
    /// </summary>
    public class TagPickerState
    {
        /// <summary>
        /// Precomputed label and width for one catalog tag badge button.
        /// </summary>
        public struct CatalogTagBadgeEntry
        {
            public string Tag;
            public string Label;
            public float ButtonWidth;
            public bool AlreadyAdded;
        }

        private int _tagsRevision;
        private int _catalogTagsRevision;
        private int _selectedTagsLookupRevision = -1;
        private int _catalogTagBadgeCacheRevision = -1;

        private readonly HashSet<string> _selectedTagsLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CatalogTagBadgeEntry> _catalogTagBadgeCache = new List<CatalogTagBadgeEntry>();

        /// <summary>
        /// Scroll position for the existing tags badge list.
        /// </summary>
        public Vector2 ExistingTagsScroll { get; set; }

        /// <summary>
        /// Marks selected tags dirty so lookup and catalog caches rebuild on next draw.
        /// </summary>
        public void MarkTagsDirty()
        {
            _tagsRevision++;
        }

        /// <summary>
        /// Marks catalog tags dirty so catalog badge cache rebuilds on next draw.
        /// </summary>
        public void MarkCatalogTagsDirty()
        {
            _catalogTagsRevision++;
        }

        /// <summary>
        /// Rebuilds the case-insensitive lookup for selected tags when needed.
        /// </summary>
        /// <param name="selectedTags">Currently selected tags.</param>
        public void RebuildSelectedTagsLookup(IReadOnlyList<string> selectedTags)
        {
            if (_selectedTagsLookupRevision == _tagsRevision)
            {
                return;
            }

            _selectedTagsLookupRevision = _tagsRevision;
            _selectedTagsLookup.Clear();

            if (selectedTags == null)
            {
                return;
            }

            for (int i = 0; i < selectedTags.Count; i++)
            {
                string tag = selectedTags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    _selectedTagsLookup.Add(tag.Trim());
                }
            }
        }

        /// <summary>
        /// Returns whether a tag is in the selected-tags lookup (case-insensitive).
        /// </summary>
        /// <param name="tag">Tag value to check.</param>
        /// <returns>True when the tag is already selected.</returns>
        public bool IsTagSelected(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            return _selectedTagsLookup.Contains(tag.Trim());
        }

        /// <summary>
        /// Rebuilds precomputed catalog tag badge layout data when tags or selection change.
        /// </summary>
        /// <param name="catalogTags">Sorted catalog tags from the model index.</param>
        /// <param name="selectedTags">Currently selected tags on the model.</param>
        /// <param name="addedStyle">GUIStyle for tags already on the model.</param>
        /// <param name="availableStyle">GUIStyle for tags not yet on the model.</param>
        /// <returns>Read-only list of precomputed catalog badge entries.</returns>
        public IReadOnlyList<CatalogTagBadgeEntry> GetCatalogBadgeCache(
            IReadOnlyList<string> catalogTags,
            IReadOnlyList<string> selectedTags,
            GUIStyle addedStyle,
            GUIStyle availableStyle)
        {
            int revisionKey = (_catalogTagsRevision * 397) ^ _tagsRevision;
            if (_catalogTagBadgeCacheRevision == revisionKey)
            {
                return _catalogTagBadgeCache;
            }

            _catalogTagBadgeCacheRevision = revisionKey;
            _catalogTagBadgeCache.Clear();
            RebuildSelectedTagsLookup(selectedTags);

            if (catalogTags == null)
            {
                return _catalogTagBadgeCache;
            }

            for (int i = 0; i < catalogTags.Count; i++)
            {
                string tag = catalogTags[i];
                bool alreadyAdded = IsTagSelected(tag);
                GUIStyle badgeStyle = alreadyAdded ? addedStyle : availableStyle;
                string label = TagUtils.FormatBadgeLabel(tag);
                float buttonWidth = badgeStyle.CalcSize(new GUIContent(label)).x + UIConstants.TAG_BADGE_HORIZONTAL_PADDING;

                CatalogTagBadgeEntry entry = new CatalogTagBadgeEntry
                {
                    Tag = tag,
                    Label = label,
                    ButtonWidth = buttonWidth,
                    AlreadyAdded = alreadyAdded
                };
                _catalogTagBadgeCache.Add(entry);
            }

            return _catalogTagBadgeCache;
        }
    }
}
