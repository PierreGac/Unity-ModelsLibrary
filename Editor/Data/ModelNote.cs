using System;

namespace ModelLibrary.Data
{
    /// <summary>
    /// A feedback note from developers to modelers about a specific model version.
    /// This enables communication between team members about model quality, issues, or suggestions.
    /// Notes are stored in the model's metadata and persist across the model's lifecycle.
    /// </summary>
    [Serializable]
    public class ModelNote
    {
        /// <summary>
        /// Username of the person who wrote this note.
        /// Retrieved from the user identity provider (usually stored in EditorPrefs).
        /// </summary>
        public string author;
        
        /// <summary>
        /// The actual feedback message content.
        /// Can be multiple lines and should be descriptive about the issue or suggestion.
        /// Examples: "The pivot point is off-center", "Great work on the texturing!", "Can we add more detail to the handle?"
        /// </summary>
        public string message;
        
        /// <summary>
        /// When this note was created
        /// </summary>
        public long createdTimeTicks;
        
        /// <summary>
        /// Optional context about what part of the model this note refers to.
        /// Examples: "pivot", "naming", "texture-resolution", "poly-count", "materials"
        /// Helps modelers understand which specific aspect needs attention.
        /// </summary>
        public string context;
        
        /// <summary>
        /// Category/type of note to help organize feedback.
        /// Predefined values: "bugfix", "improvements", "remarks", "question", "praise"
        /// This helps filter and prioritize different types of feedback.
        /// </summary>
        public string tag;
    }
}


