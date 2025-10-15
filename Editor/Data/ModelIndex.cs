using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    /// <summary>
    /// The global registry of all models in the repository - this is the "models_index.json" file.
    /// This is a lightweight index that contains just enough information to browse and search models
    /// without having to load the full metadata for each model. Think of it like a table of contents.
    /// 
    /// This file is stored at: &lt;repository&gt;/models_index.json
    /// </summary>
    [Serializable]
    public class ModelIndex
    {
        /// <summary>
        /// A single entry in the model index representing one model family.
        /// Contains just the essential information needed for browsing and searching.
        /// </summary>
        [Serializable]
        public class Entry
        {
            /// <summary>
            /// The unique ID of this model family (same as ModelIdentity.Id).
            /// This is the primary key used to identify models.
            /// </summary>
            public string id;

            /// <summary>
            /// Human-readable name of the model (same as ModelIdentity.Name).
            /// Used for display in the browser and search functionality.
            /// </summary>
            public string name;

            /// <summary>
            /// The latest version number available for this model.
            /// Format: Semantic Versioning (e.g., "1.2.3")
            /// </summary>
            public string latestVersion;

            /// <summary>
            /// Short description/summary for quick browsing.
            /// Usually the first line or a condensed version of the full description.
            /// </summary>
            public string description;

            /// <summary>
            /// Tags associated with this model for categorization and filtering.
            /// Copied from the latest version's metadata.
            /// </summary>
            public List<string> tags = new List<string>();

            /// <summary>
            /// When this model was last updated (when the latest version was published).
            /// </summary>
            public long updatedTimeTicks = 0;

            /// <summary>
            /// When the latest version of this model was uploaded (release date).
            /// </summary>
            public long releaseTimeTicks = 0;

        }

        /// <summary>
        /// List of all model entries in the repository.
        /// This is the main data that gets displayed in the browser window.
        /// </summary>
        public List<Entry> entries = new List<Entry>();

        /// <summary>
        /// Find a specific model entry by its ID.
        /// This is used when we need to look up a model by its unique identifier.
        /// </summary>
        /// <param name="id">The model ID to search for</param>
        /// <returns>The entry if found, null otherwise</returns>
        public Entry Get(string id)
        {
            // Simple linear search through the entries
            // For large repositories, this could be optimized with a Dictionary
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id)
                {
                    return entries[i];
                }
            }
            return null;
        }
    }
}


