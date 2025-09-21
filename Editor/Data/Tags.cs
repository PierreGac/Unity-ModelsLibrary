using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    /// <summary>
    /// Container for model tags - simple string labels that help categorize and search models.
    /// Tags are like hashtags: "medieval", "weapon", "low-poly", "sci-fi", etc.
    /// This wrapper class exists because Unity's JsonUtility has limitations with direct List serialization.
    /// </summary>
    [Serializable]
    public class Tags
    {
        /// <summary>
        /// List of tag strings. Each tag should be a single word or hyphenated phrase.
        /// Examples: "medieval", "weapon", "low-poly", "sci-fi", "character", "environment"
        /// Tags are case-sensitive and should be consistent across the project.
        /// </summary>
        public List<string> values = new List<string>();
    }
}


