using System;
using System.Collections.Generic;

namespace ModelLibrary.Editor.Data
{
    /// <summary>
    /// Data class representing a saved filter preset.
    /// Contains a search query and selected tags that can be quickly applied to the model browser.
    /// </summary>
    [Serializable]
    public class FilterPreset
    {
        /// <summary>The name of the preset for display in the UI.</summary>
        public string name;
        /// <summary>The search query string associated with this preset.</summary>
        public string searchQuery;
        /// <summary>The list of selected tags for this preset.</summary>
        public List<string> selectedTags;
    }
}

