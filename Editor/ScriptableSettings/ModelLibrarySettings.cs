
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Settings
{
    /// <summary>
    /// Project-wide settings asset stored in Resources folder for runtime access.
    /// Stores repository type & root URL/path.
    /// </summary>
    public class ModelLibrarySettings : ScriptableObject
    {
        private const string kResourcePath = "ModelLibrarySettings";
        private const string kAssetPath = "Assets/ModelLibrary/Resources/ModelLibrarySettings.asset";

        public enum RepositoryKind { FileSystem, Http }

        [Header("Repository")]
        public RepositoryKind repositoryKind = RepositoryKind.FileSystem;

        [Tooltip("Root path or URL to the remote storage. For FileSystem, can be absolute or UNC. For HTTP, a base URL.")]
        public string repositoryRoot = "\\\\SERVER\\ModelLibrary"; // example UNC path

        [Header("Local Cache (optional)")]
        public string localCacheRoot = "Library/ModelLibraryCache"; // Editor cache

        public static ModelLibrarySettings GetOrCreate()
        {
            // Try to load from Resources first (works in both Editor and Runtime)
            ModelLibrarySettings asset = Resources.Load<ModelLibrarySettings>(kResourcePath);
            
            if (asset == null)
            {
                // Create new instance
                asset = CreateInstance<ModelLibrarySettings>();
                
                // In Editor, save to Resources folder
                string dir = System.IO.Path.GetDirectoryName(kAssetPath);
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                AssetDatabase.CreateAsset(asset, kAssetPath);
                AssetDatabase.SaveAssets();
            }
            
            return asset;
        }
    }
}



