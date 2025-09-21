using System;

namespace ModelLibrary.Data
{
    /// <summary>
    /// Represents a single entry in the model's changelog, documenting updates applied to a version.
    /// </summary>
    [Serializable]
    public class ModelChangelogEntry
    {
        /// <summary>
        /// Version number that this changelog entry refers to.
        /// </summary>
        public string version;

        /// <summary>
        /// Human-readable description of the change (what changed).
        /// </summary>
        public string summary;

        /// <summary>
        /// Username of the person who performed the change.
        /// </summary>
        public string author;

        /// <summary>
        /// When the change was performed, stored as ISO8601 UTC.
        /// </summary>
        public long timestamp = 0;
    }
}
