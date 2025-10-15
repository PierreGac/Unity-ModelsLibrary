using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    [Serializable]
    public class AssetRef
    {
        public string guid;       // Unity GUID (if applicable)
        public string name;       // Asset name
        public string relativePath; // Path inside version folder (if known)
        public string type;       // Type name, e.g., "Material", "Texture2D"
    }

    [Serializable]
    public class DependencyRef
    {
        public string guid; // GUID in original submitter project
        public string type; // Type name, e.g., "Material", "Texture2D"
        public string name; // Asset name if known
    }

    [Serializable]
    public class ModelImporterSettings
    {
        public string materialImportMode; // enum name
        public string materialSearch;     // enum name
        public string materialName;       // enum name
    }
    /// <summary>
    /// The complete metadata for a model version - this is the <see cref="MODEL_JSON"/> file stored with each version.
    /// This is the central data structure that describes everything about a model: what files it contains,
    /// who made it, when, what it looks like, and any feedback from users.
    /// 
    /// This file is stored at: &lt;repository&gt;/&lt;modelId&gt;/&lt;version&gt;/model.json
    /// </summary>
    [Serializable]
    public class ModelMeta
    {
        public const string MODEL_JSON = "model.json";

        /// <summary>
        /// Schema version for this ModelMeta data structure.
        /// Used for migration when loading older versions of the data.
        /// Increment this when making breaking changes to the data structure.
        /// </summary>
        public int schemaVersion = 1;

        /// <summary>
        /// Basic identity information (ID and name) that identifies this model family.
        /// All versions of the same model share the same identity.
        /// </summary>
        public ModelIdentity identity = new ModelIdentity();

        /// <summary>
        /// Version number in Semantic Versioning format (e.g., "1.2.3").
        /// Format: MAJOR.MINOR.PATCH where:
        /// - MAJOR: Breaking changes or major redesigns
        /// - MINOR: New features, additions, improvements
        /// - PATCH: Bug fixes, small corrections
        /// </summary>
        public string version;

        /// <summary>
        /// Human-readable description of what this model is and what it's used for.
        /// Should be clear and descriptive for other team members.
        /// Examples: "A medieval longsword with ornate handle and crossguard", "Low-poly tree for forest environments"
        /// </summary>
        public string description;

        /// <summary>
        /// Categorization tags to help with searching and filtering models.
        /// See Tags class for more details.
        /// </summary>
        public Tags tags = new Tags();

        /// <summary>
        /// Name of the person who created this model version.
        /// Retrieved from the user identity provider when the model is submitted.
        /// </summary>
        public string author;

        /// <summary>
        /// DateTime.Ticks of the creation date
        /// </summary>
        public long createdTimeTicks = 0;

        /// <summary>
        /// DateTime.Ticks when the last update have been done
        /// </summary>
        public long updatedTimeTicks = 0;

        /// <summary>
        /// List of file paths relative to the version root directory.
        /// These are the actual model files (FBX, textures, materials) that make up this version.
        /// Examples: "payload/myModel.fbx", "payload/textures/diffuse.png", "payload/materials/sword.mat"
        /// </summary>
        public List<string> payloadRelativePaths = new List<string>();

        /// <summary>
        /// Extracted material assets information for quick browsing.
        /// </summary>
        public List<AssetRef> materials = new List<AssetRef>();

        /// <summary>
        /// Extracted texture assets information for quick browsing.
        /// </summary>
        public List<AssetRef> textures = new List<AssetRef>();

        /// <summary>
        /// Unity Asset GUIDs of the files when they exist in a Unity project.
        /// This enables the system to detect if a model is already installed in a project
        /// and check for updates. GUIDs are Unity's way of uniquely identifying assets.
        /// </summary>
        public List<string> assetGuids = new List<string>();

        /// <summary>
        /// Screenshots or preview images that show what this model looks like.
        /// Stored as relative paths from the version root.
        /// Examples: "images/front_view.png", "images/wireframe.png", "images/in_game.png"
        /// </summary>
        public List<string> imageRelativePaths = new List<string>();

        /// <summary>
        /// Relative path to the automatically generated high-resolution preview.
        /// </summary>
        public string previewImagePath;

        /// <summary>
        /// When this model version was uploaded to the library.
        /// </summary>
        public long uploadTimeTicks = 0;


        /// <summary>
        /// Preferred install path inside the Unity project (e.g., "Assets/Models/Spaceship").
        /// </summary>
        public string installPath;

        /// <summary>
        /// Relative path from Assets folder for the model (e.g., "Models/Spaceship").
        /// Used as default install location when importing the model.
        /// </summary>
        public string relativePath;

        /// <summary>
        /// Feedback notes from developers to modelers about this specific version.
        /// These persist with the model and help track issues, suggestions, and improvements.
        /// </summary>
        public List<ModelNote> notes = new List<ModelNote>();

        /// <summary>
        /// GUIDs of other assets that this model depends on but aren't included in the payload.
        /// For example, if this model uses a shared texture or material from another model.
        /// This helps track dependencies and ensure all required assets are available.
        /// </summary>
        public List<string> dependencies = new List<string>();

        /// <summary>
        /// Detailed dependency info (type and name), complements the raw GUIDs.
        /// </summary>
        public List<DependencyRef> dependenciesDetailed = new List<DependencyRef>();

        /// <summary>
        /// Extra key-value pairs for future extensibility.
        /// Allows adding custom metadata without changing the core schema.
        /// Examples: "polyCount": "1500", "textureSize": "1024x1024", "exportSettings": "FBX2018"
        /// </summary>
        public Dictionary<string, string> extra = new Dictionary<string, string>();

        /// <summary>
        /// Per-FBX/OBJ importer settings captured at submit time, keyed by payload-relative path (e.g., "payload/model.fbx").
        /// </summary>
        public Dictionary<string, ModelImporterSettings> modelImporters = new Dictionary<string, ModelImporterSettings>();

        /// <summary>
        /// Historical changelog entries describing how this model evolved across versions.
        /// The latest entry should correspond to the current version.
        /// </summary>
        public List<ModelChangelogEntry> changelog = new List<ModelChangelogEntry>();

        /// <summary>
        /// Total number of vertices for the model payload (summed across meshes).
        /// </summary>
        public int vertexCount = 0;

        /// <summary>
        /// Total number of triangles for the model payload (summed across meshes).
        /// </summary>
        public int triangleCount = 0;
    }
}

