
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Settings
{
    /// <summary>
    /// Project-wide library settings.
    /// The writable copy is stored in project-owned storage. The package asset is only a migration source.
    /// </summary>
    public partial class ModelLibrarySettings : ScriptableObject
    {
        /// <summary>
        /// Repository root shipped as the unconfigured placeholder.
        /// </summary>
        public const string UNCONFIGURED_REPOSITORY_ROOT = "\\\\SERVER\\ModelLibrary";

        /// <summary>
        /// Default editor cache directory, relative to the Unity project.
        /// </summary>
        public const string DEFAULT_LOCAL_CACHE_ROOT = "Library/ModelLibraryCache";

        public enum RepositoryKind { FileSystem, Http }

        [Header("Repository")]
        public RepositoryKind repositoryKind = RepositoryKind.FileSystem;

        [Tooltip("Root path or URL to the remote storage. For FileSystem, can be absolute or UNC. For HTTP, a base URL.")]
        public string repositoryRoot = UNCONFIGURED_REPOSITORY_ROOT;

        [Header("Local Cache (optional)")]
        public string localCacheRoot = DEFAULT_LOCAL_CACHE_ROOT;
    }
}
