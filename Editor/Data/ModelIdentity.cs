using System;

namespace ModelLibrary.Data
{
    /// <summary>
    /// Stable identifier for a model across versions.
    /// This represents the "family" of a model - all versions of the same model share the same identity.
    /// Think of it like a product line where you might have "Car Model X" in versions 1.0, 1.1, 2.0, etc.
    /// </summary>
    [Serializable]
    public class ModelIdentity
    {
        /// <summary>
        /// Unique GUID string that identifies this model family across all versions.
        /// This never changes once assigned - it's the permanent "fingerprint" of the model.
        /// Format: 32-character hex string (e.g., "a1b2c3d4e5f6789012345678901234567890abcd")
        /// </summary>
        public string id;
        
        /// <summary>
        /// Human-readable name for the model (e.g., "Medieval Sword", "Sci-Fi Spaceship").
        /// This can be updated between versions but should remain recognizable.
        /// Used for display in UI and search functionality.
        /// </summary>
        public string name;
    }
}


