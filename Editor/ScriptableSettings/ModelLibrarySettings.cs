
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Settings
{
    /// <summary>
    /// Project-wide settings asset stored under Assets/ModelLibrary/Editor/ScriptableSettings.
    /// Stores repository type & root URL/path.
    /// </summary>
    public class ModelLibrarySettings : ScriptableObject
    {
        private const string kAssetPath = "Assets/ModelLibrary/Editor/ScriptableSettings/ModelLibrarySettings.asset";

        public enum RepositoryKind { FileSystem, Http }

        [Header("Repository")]
        public RepositoryKind repositoryKind = RepositoryKind.FileSystem;

        [Tooltip("Root path or URL to the remote storage. For FileSystem, can be absolute or UNC. For HTTP, a base URL.")]
        public string repositoryRoot = "\\\\SERVER\\ModelLibrary"; // example UNC path

        [Header("Local Cache (optional)")]
        public string localCacheRoot = "Library/ModelLibraryCache"; // Editor cache

        public static ModelLibrarySettings GetOrCreate()
        {
            ModelLibrarySettings asset = AssetDatabase.LoadAssetAtPath<ModelLibrarySettings>(kAssetPath);
            if (asset == null)
            {
                asset = CreateInstance<ModelLibrarySettings>();
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



