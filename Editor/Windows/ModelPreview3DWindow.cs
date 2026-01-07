using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Interactive 3D preview window for viewing model meshes.
    /// Allows users to rotate, zoom, and inspect models in real-time.
    /// </summary>
    public class ModelPreview3DWindow : EditorWindow
    {
        // Constants for magic numbers
        private const float __DEFAULT_CAMERA_ROTATION_X = 30f;
        private const float __DEFAULT_CAMERA_ROTATION_Y = 30f;
        private const float __DEFAULT_CAMERA_DISTANCE = 5f;
        private const float __MIN_CAMERA_DISTANCE = 1f;
        private const float __MAX_CAMERA_DISTANCE = 20f;
        private const float __MOUSE_ROTATION_SENSITIVITY = 0.5f;
        private const float __MOUSE_ZOOM_SENSITIVITY = 0.1f;
        private const float __MIN_CAMERA_ROTATION_X = -90f;
        private const float __MAX_CAMERA_ROTATION_X = 90f;
        private const float __PREVIEW_MIN_SIZE = 400f;
        private const float __UI_SPACING = 10f;
        private const float __ROTATION_LABEL_WIDTH = 60f;
        private const float __RESET_BUTTON_WIDTH = 60f;
        private const float __DEFAULT_BOUNDS_SIZE = 1f;
        private const float __ERROR_BACKGROUND_RED = 0.5f;
        private const float __PREVIEW_BACKGROUND_GRAY = 0.2f;
        private const int __DEFAULT_SELECTED_MESH_INDEX = 0;

        // Performance: Cache texture extensions array to avoid allocation on every call
        private static readonly string[] __TEXTURE_EXTENSIONS = { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.psd" };

        // Performance: Cache mesh names array to avoid allocation every frame

        private string[] _cachedMeshNames;
        private int _lastMeshCount = -1;

        /// <summary>The unique identifier of the model being previewed.</summary>
        private string _modelId;
        /// <summary>The version of the model being previewed.</summary>
        private string _version;
        /// <summary>Service instance for repository operations.</summary>
        private ModelLibraryService _service;
        /// <summary>The loaded model metadata.</summary>
        private ModelMeta _meta;
        /// <summary>3D preview utility for rendering meshes.</summary>
        private PreviewUtility3D _previewUtil;
        /// <summary>List of meshes found in the model.</summary>
        private List<MeshInfo> _meshes = new List<MeshInfo>();
        /// <summary>Currently selected mesh index.</summary>
        private int _selectedMeshIndex = __DEFAULT_SELECTED_MESH_INDEX;
        /// <summary>Camera rotation angle around the model.</summary>
        private Vector2 _cameraRotation = new Vector2(__DEFAULT_CAMERA_ROTATION_X, __DEFAULT_CAMERA_ROTATION_Y);
        /// <summary>Camera distance from the model.</summary>
        private float _cameraDistance = __DEFAULT_CAMERA_DISTANCE;
        /// <summary>Preview area rectangle.</summary>
        private Rect _previewRect;
        /// <summary>Whether the preview is currently loading.</summary>
        private bool _isLoading = false;
        /// <summary>List of temporary asset paths that need cleanup on window close.</summary>
        private List<string> _tempAssets = new List<string>();
        /// <summary>Cache root path for the downloaded model version (for cleanup on errors).</summary>
        private string _cacheRoot = null;
        /// <summary>Dictionary mapping texture names to their asset paths for reloading when focus is regained.</summary>
        private Dictionary<string, string> _texturePaths = new Dictionary<string, string>();
        /// <summary>Dictionary mapping materials to their texture property mappings for reloading.</summary>
        private Dictionary<Material, Dictionary<string, string>> _materialTextureMappings = new Dictionary<Material, Dictionary<string, string>>();
        /// <summary>Track whether the window had focus in the previous frame.</summary>
        private bool _hadFocus = false;
        /// <summary>Whether we've subscribed to EditorApplication.update for focus detection.</summary>
        private bool _subscribedToUpdate = false;

        /// <summary>
        /// Information about a mesh in the model.
        /// </summary>
        private class MeshInfo
        {
            public string name;
            public Mesh mesh;
            public Material material;
            public Bounds bounds;
        }

        /// <summary>
        /// Opens the 3D preview window for a specific model version.
        /// Prevents opening during play mode.
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version of the model to preview.</param>
        /// <summary>
        /// Opens the 3D preview window.
        /// Now navigates to the Preview3D view in ModelLibraryWindow instead of opening a separate window.
        /// </summary>
        public static void Open(string modelId, string version)
        {
            // Don't open during play mode
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ModelPreview3DWindow] Cannot open 3D preview during play mode.");
                return;
            }

            // Navigate to Preview3D view in ModelLibraryWindow
            ModelLibraryWindow window = GetWindow<ModelLibraryWindow>("Model Library");
            if (window != null)
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "modelId", modelId },
                    { "version", version }
                };
                window.NavigateToView(ModelLibraryWindow.ViewType.Preview3D, parameters);
                // Initialize preview state will be handled in the view implementation
            }
        }

        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// Initializes the preview utility and subscribes to update events for focus detection.
        /// </summary>
        private void OnEnable()
        {
            _previewUtil = new PreviewUtility3D();
            _hadFocus = focusedWindow == this;
            
            // Subscribe to EditorApplication.update to detect focus changes
            if (!_subscribedToUpdate)
            {
                EditorApplication.update += OnEditorUpdate;
                _subscribedToUpdate = true;
            }
        }
        
        /// <summary>
        /// Called every frame by EditorApplication.update to detect focus changes.
        /// </summary>
        private void OnEditorUpdate()
        {
            bool hasFocus = focusedWindow == this;
            
            // If we just regained focus, reload textures and force repaint
            if (!_hadFocus && hasFocus)
            {
                if (ReloadTexturesIfNeeded())
                {
                    Repaint();
                }
            }
            
            _hadFocus = hasFocus;
        }

        /// <summary>
        /// Unity lifecycle method called when the window is disabled.
        /// Cleans up the preview utility and temporary assets.
        /// </summary>
        private void OnDisable()
        {
            // Unsubscribe from update events
            if (_subscribedToUpdate)
            {
                EditorApplication.update -= OnEditorUpdate;
                _subscribedToUpdate = false;
            }
            
            CleanupPreviewResources();
        }

        /// <summary>
        /// Unity lifecycle method called when the window is destroyed.
        /// Ensures all resources are fully released, including forcing garbage collection.
        /// Note: GC.Collect() is expensive and should only be used when necessary (e.g., file handle cleanup on window close).
        /// </summary>
        private void OnDestroy()
        {
            CleanupPreviewResources();
            
            // Force garbage collection to release any remaining file handles
            // This is necessary because Unity's asset system may hold file handles that prevent deletion
            // We use forced collection with blocking to ensure all finalizers run before we return
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        }

        /// <summary>
        /// Cleans up all preview resources including meshes, materials, preview utility, and temporary assets.
        /// </summary>
        private void CleanupPreviewResources()
        {
            // Clean up preview utility
            if (_previewUtil != null)
            {
                _previewUtil.Dispose();
                _previewUtil = null;
            }

            // Clean up mesh copies (materials are assets, so they'll be cleaned up with asset deletion)
            for (int i = 0; i < _meshes.Count; i++)
            {
                MeshInfo meshInfo = _meshes[i];
                if (meshInfo.mesh != null)
                {
                    DestroyImmediate(meshInfo.mesh);
                    meshInfo.mesh = null;
                }
                if (meshInfo.material != null)
                {
                    DestroyImmediate(meshInfo.material);
                    meshInfo.material = null;
                }
            }
            _meshes.Clear();

            CleanupTempAssets();

            // Clear texture path mappings
            _texturePaths.Clear();
            _materialTextureMappings.Clear();

            // Clear cache root reference
            _cacheRoot = null;

            // Refresh asset database to ensure cleanup is complete
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Cleans up temporary assets created during preview.
        /// Called both during normal cleanup and on errors.
        /// </summary>
        private void CleanupTempAssets()
        {
            // Clean up temporary assets and directories
            // Process in reverse order to delete files before directories
            for (int i = _tempAssets.Count - 1; i >= 0; i--)
            {
                string tempAsset = _tempAssets[i];
                try
                {
                    if (Directory.Exists(tempAsset))
                    {
                        // Delete entire directory - this will delete all assets inside
                        FileUtil.DeleteFileOrDirectory(tempAsset);
                        string metaPath = tempAsset + ".meta";
                        if (File.Exists(metaPath))
                        {
                            FileUtil.DeleteFileOrDirectory(metaPath);
                        }
                    }
                    else if (File.Exists(tempAsset))
                    {
                        // Use AssetDatabase.DeleteAsset to properly delete assets
                        // This handles both files and their .meta files
                        AssetDatabase.DeleteAsset(tempAsset);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModelPreview3DWindow] Failed to delete temp asset {tempAsset}: {ex.Message}");
                }
            }
            _tempAssets.Clear();
        }

        /// <summary>
        /// Attempts to clean up the cache folder if it's in a locked or bad state.
        /// This helps recover from access denied errors during preview.
        /// </summary>
        /// <param name="cacheRoot">The cache root path to clean up.</param>
        private void TryCleanupCacheFolder(string cacheRoot)
        {
            if (string.IsNullOrEmpty(cacheRoot) || !Directory.Exists(cacheRoot))
            {
                return;
            }

            try
            {
                // Wait a bit for any file handles to be released
                System.Threading.Thread.Sleep(500);

                // Try to delete any temporary files that might be locked
                string[] tempFiles = Directory.GetFiles(cacheRoot, "*", SearchOption.AllDirectories);
                for (int i = 0; i < tempFiles.Length; i++)
                {
                    string file = tempFiles[i];
                    try
                    {
                        // Try to remove read-only attributes
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore errors setting attributes
                    }
                }

                Debug.Log($"[ModelPreview3DWindow] Attempted cleanup of cache folder: {cacheRoot}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModelPreview3DWindow] Failed to cleanup cache folder {cacheRoot}: {ex.Message}. Manual deletion may be required.");
            }
        }

        /// <summary>
        /// Asynchronously loads the model metadata and extracts mesh information.
        /// </summary>
        private async Task LoadModelAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_modelId) || string.IsNullOrEmpty(_version))
                {
                    _isLoading = false;
                    return;
                }

                // Initialize service if needed
                if (_service == null)
                {
                    Repository.IModelRepository repo = RepositoryFactory.CreateRepository();
                    _service = new ModelLibraryService(repo);
                }

                // Load metadata
                _meta = await _service.GetMetaAsync(_modelId, _version);

                if (_meta == null)
                {
                    _isLoading = false;
                    Repaint();
                    return;
                }

                // Download and load meshes
                await LoadMeshesAsync();

                _isLoading = false;
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Model Failed", $"Failed to load model for 3D preview: {ex.Message}", ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}, Version: {_version}");
                _isLoading = false;
                Repaint();
            }
        }

        /// <summary>
        /// Loads mesh assets from the cached model version.
        /// </summary>
        private async Task LoadMeshesAsync()
        {
            _meshes.Clear();
            _cacheRoot = null;

            if (_meta == null || _meta.assetGuids == null)
            {
                return;
            }

            try
            {
                // Download the model version to cache
                (string cacheRoot, ModelMeta meta) = await _service.DownloadModelVersionAsync(_modelId, _version);
                _cacheRoot = cacheRoot; // Track cache root for cleanup

                // Find FBX/OBJ files in the cache
                string[] meshFiles = Directory.GetFiles(cacheRoot, "*.fbx", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(cacheRoot, "*.obj", SearchOption.AllDirectories))
                    .ToArray();

                if (meshFiles.Length == 0)
                {
                    return;
                }

                // Load the first mesh file found
                string meshFile = meshFiles[0];
                string relativePath = PathUtils.SanitizePathSeparator(meshFile[cacheRoot.Length..].TrimStart(Path.DirectorySeparatorChar));

                // Create a temporary directory for all preview assets
                string tempDir = $"Assets/TempPreview_{Guid.NewGuid():N}";
                Directory.CreateDirectory(tempDir);

                // Import FBX as a temporary asset
                string tempAssetPath = Path.Combine(tempDir, Path.GetFileName(meshFile));
                File.Copy(meshFile, tempAssetPath, overwrite: true);

                // IMPORTANT: Import textures FIRST, then materials, so we can re-link textures
                // Find and copy texture files from cache to temp directory
                // Exclude preview images (in "images" folder or named "auto_preview")
                List<string> textureFiles = new List<string>();

                foreach (string ext in __TEXTURE_EXTENSIONS)
                {
                    string[] allTextures = Directory.GetFiles(cacheRoot, ext, SearchOption.AllDirectories);
                    foreach (string textureFile in allTextures)
                    {
                        // Skip preview images - exclude files in "images" folder or named "auto_preview"
                        string textureRelativePath = PathUtils.SanitizePathSeparator(textureFile[cacheRoot.Length..].TrimStart(Path.DirectorySeparatorChar));
                        string textureFileName = Path.GetFileNameWithoutExtension(textureFile).ToLowerInvariant();


                        if (textureRelativePath.StartsWith("images/", StringComparison.OrdinalIgnoreCase) ||
                            textureFileName == "auto_preview" || textureFileName.StartsWith("auto_preview"))
                        {
                            continue; // Skip preview images
                        }


                        textureFiles.Add(textureFile);
                    }
                }

                // Copy and import texture files first - they get new GUIDs
                Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();
                Dictionary<string, string> textureNameToPath = new Dictionary<string, string>();
                foreach (string textureFile in textureFiles)
                {
                    string textureFileName = Path.GetFileName(textureFile);
                    string tempTexturePath = Path.Combine(tempDir, textureFileName);
                    if (!File.Exists(tempTexturePath))
                    {
                        File.Copy(textureFile, tempTexturePath, overwrite: true);
                        AssetDatabase.ImportAsset(tempTexturePath, ImportAssetOptions.ForceUpdate);

                        // Check if this is a normal map texture and configure it accordingly
                        string textureNameLower = Path.GetFileNameWithoutExtension(textureFile).ToLowerInvariant();
                        bool isNormalMap = textureNameLower.Contains("normal") ||
                                          textureNameLower.Contains("bump") ||
                                          textureNameLower.Contains("nrm") ||
                                          textureNameLower.Contains("_n") ||
                                          textureNameLower.EndsWith("_normal") ||
                                          textureNameLower.EndsWith("_bump");

                        if (isNormalMap)
                        {
                            TextureImporter textureImporter = AssetImporter.GetAtPath(tempTexturePath) as TextureImporter;
                            if (textureImporter != null)
                            {
                                textureImporter.textureType = TextureImporterType.NormalMap;
                                textureImporter.SaveAndReimport();
                                Debug.Log($"Configured normal map: {textureFileName}");
                            }
                        }

                        _tempAssets.Add(tempTexturePath);

                        // Load the texture asset
                        Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tempTexturePath);
                        if (loadedTex != null)
                        {
                            string texName = Path.GetFileNameWithoutExtension(textureFile);
                            loadedTextures[texName] = loadedTex;
                            textureNameToPath[texName] = tempTexturePath;
                            // Store texture path for reloading when focus is regained
                            _texturePaths[texName] = tempTexturePath;
                            //Debug.Log($"Loaded texture file: {texName} from {tempTexturePath}");
                        }
                    }
                }

                // Now find and copy material files (.mat) from cache
                // After import, we'll re-link textures manually since GUIDs changed
                string[] matFiles = Directory.GetFiles(cacheRoot, "*.mat", SearchOption.AllDirectories);
                Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>();

                foreach (string matFile in matFiles)
                {
                    string matFileName = Path.GetFileName(matFile);
                    string tempMatPath = Path.Combine(tempDir, matFileName);
                    File.Copy(matFile, tempMatPath, overwrite: true);
                    AssetDatabase.ImportAsset(tempMatPath, ImportAssetOptions.ForceUpdate);
                    _tempAssets.Add(tempMatPath);

                    // Load the material asset
                    Material loadedMat = AssetDatabase.LoadAssetAtPath<Material>(tempMatPath);
                    if (loadedMat != null)
                    {
                        string matName = Path.GetFileNameWithoutExtension(matFile);
                        loadedMaterials[matName] = loadedMat;
                        Debug.Log($"Loaded material file: {matName} from {tempMatPath}");

                        // Re-link textures by matching texture names
                        // The material references textures, but GUIDs are broken after copying, so we find by name
                        if (loadedMat.shader != null)
                        {
                            int propertyCount = loadedMat.shader.GetPropertyCount();
                            for (int i = 0; i < propertyCount; i++)
                            {
                                if (loadedMat.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                                {
                                    string propName = loadedMat.shader.GetPropertyName(i);

                                    // Skip Unity internal properties that require Texture2DArray (like unity_Lightmaps)
                                    // These properties cannot accept regular Texture2D and will cause dimension mismatch errors
                                    if (propName == "unity_Lightmaps" || propName == "unity_LightmapsInd" || propName == "unity_ShadowMasks" ||
                                        propName.StartsWith("unity_") && (propName.Contains("Lightmap") || propName.Contains("Shadow")))
                                    {
                                        continue; // Skip array texture properties
                                    }

                                    Texture oldTex = loadedMat.GetTexture(propName);

                                    // Golden rule: Only re-link textures if the source material had a texture
                                    // If oldTex is null, preserve that null value - don't try to fill it
                                    if (oldTex == null)
                                    {
                                        // Source material has no texture for this property - keep it null
                                        continue;
                                    }

                                    // Try to find matching texture by the referenced texture's name
                                    string texName = oldTex.name;
                                    // Remove common suffixes Unity adds
                                    if (texName.Contains(" (Texture2D)"))
                                    {
                                        texName = texName.Replace(" (Texture2D)", "");
                                    }

                                    Texture2D matchingTex = null;
                                    loadedTextures.TryGetValue(texName, out matchingTex);

                                    // Check if this is a normal map property (used for configuration)
                                    bool isNormalMapProperty = propName.Contains("Bump") ||
                                                               propName.Contains("Normal") ||
                                                               propName == "_BumpMap" ||
                                                               propName == "_NormalMap" ||
                                                               propName == "_DetailNormalMap" ||
                                                               propName == "_BumpTex";

                                    // If still not found by exact name, try matching by property name and common texture naming patterns
                                    if (matchingTex == null && loadedTextures.Count > 0)
                                    {
                                        if (isNormalMapProperty)
                                        {
                                            // Try common names for normal/bump textures
                                            string[] normalMapNames = { "normal", "bump", "nrm", "_n", "_normal", "_bump" };
                                            foreach (string normalName in normalMapNames)
                                            {
                                                foreach (string texKey in loadedTextures.Keys)
                                                {
                                                    if (texKey.ToLower().Contains(normalName))
                                                    {
                                                        matchingTex = loadedTextures[texKey];
                                                        Debug.Log($"Matched normal map texture '{texKey}' to property '{propName}' by pattern '{normalName}'");
                                                        break;
                                                    }
                                                }
                                                if (matchingTex != null)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                        // Common mappings: _BaseMap/_MainTex -> diffuse/albedo/base textures
                                        else if (propName == "_BaseMap" || propName == "_MainTex")
                                        {
                                            // Try common names for base/diffuse textures
                                            string[] commonNames = { "diffuse", "albedo", "base", "color", "texture", matName.ToLower() };
                                            foreach (string commonName in commonNames)
                                            {
                                                foreach (string texKey in loadedTextures.Keys)
                                                {
                                                    if (texKey.ToLower().Contains(commonName))
                                                    {
                                                        matchingTex = loadedTextures[texKey];
                                                        Debug.Log($"Matched texture '{texKey}' to property '{propName}' by pattern '{commonName}'");
                                                        break;
                                                    }
                                                }
                                                if (matchingTex != null)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (matchingTex != null)
                                    {
                                        // Store mapping for reloading when focus is regained
                                        if (!_materialTextureMappings.ContainsKey(loadedMat))
                                        {
                                            _materialTextureMappings[loadedMat] = new Dictionary<string, string>();
                                        }
                                        _materialTextureMappings[loadedMat][propName] = matchingTex.name;
                                        
                                        // Configure texture as normal map if this is a normal map property
                                        if (isNormalMapProperty)
                                        {
                                            string texturePath = textureNameToPath.ContainsKey(matchingTex.name)
                                                ? textureNameToPath[matchingTex.name]
                                                : AssetDatabase.GetAssetPath(matchingTex);

                                            if (!string.IsNullOrEmpty(texturePath) && File.Exists(texturePath))
                                            {
                                                TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                                                if (textureImporter != null && textureImporter.textureType != TextureImporterType.NormalMap)
                                                {
                                                    textureImporter.textureType = TextureImporterType.NormalMap;
                                                    textureImporter.SaveAndReimport();
                                                    Debug.Log($"Configured normal map from property '{propName}': {matchingTex.name}");
                                                }
                                            }
                                        }

                                        try
                                        {
                                            loadedMat.SetTexture(propName, matchingTex);
                                            Debug.Log($"Re-linked texture '{matchingTex.name}' to property '{propName}' in material '{matName}'");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogWarning($"Failed to set texture '{matchingTex.name}' to property '{propName}' in material '{matName}': {ex.Message}. This property may require a different texture type.");
                                        }
                                    }
                                    else
                                    {
                                        // Could not find matching texture to re-link - property may have broken reference
                                        // This is okay - we preserve source material data as much as possible
                                        Debug.Log($"No matching texture found for property '{propName}' in material '{matName}' (source had texture '{oldTex.name}'). Available textures: {string.Join(", ", loadedTextures.Keys)}");
                                    }
                                }
                            }

                            // Preserve important material properties for correct rendering
                            // This ensures alpha clipping, specular highlights, and environment reflections work correctly
                            MaterialPropertyPreserver.PreserveMaterialProperties(loadedMat, matName, enableLogging: true);
                        }
                    }
                }

                // Import the FBX - Unity will find textures and materials in the same directory
                AssetDatabase.ImportAsset(tempAssetPath, ImportAssetOptions.ForceUpdate);

                // Keep track of temp assets for cleanup
                _tempAssets.Add(tempAssetPath);
                _tempAssets.Add(tempDir); // Track the directory for cleanup

                // Load all sub-assets from the imported FBX (meshes, materials, and textures)
                UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(tempAssetPath);

                // Find all materials and textures in the imported FBX
                List<Material> materials = new List<Material>();
                List<Texture2D> textures = new List<Texture2D>();
                foreach (UnityEngine.Object asset in allAssets)
                {
                    if (asset is Material material)
                    {
                        materials.Add(material);
                    }
                    else if (asset is Texture2D texture)
                    {
                        textures.Add(texture);
                    }
                }

                // Load the GameObject to access meshes
                GameObject tempObj = AssetDatabase.LoadAssetAtPath<GameObject>(tempAssetPath);
                if (tempObj != null)
                {
                    MeshFilter[] meshFilters = tempObj.GetComponentsInChildren<MeshFilter>();
                    for (int i = 0; i < meshFilters.Length; i++)
                    {
                        MeshFilter filter = meshFilters[i];
                        if (filter.sharedMesh != null)
                        {
                            // Create a copy of the mesh to avoid issues when asset is deleted
                            Mesh meshCopy = Instantiate(filter.sharedMesh);

                            // Load material directly from the imported FBX asset
                            // Materials are sub-assets of the FBX, so they're loaded with the FBX
                            Material materialToUse = null;

                            // Try to match material by renderer's material name, or use index
                            Renderer renderer = filter.GetComponent<Renderer>();
                            string materialNameToMatch = null;
                            if (renderer != null && renderer.sharedMaterial != null)
                            {
                                materialNameToMatch = renderer.sharedMaterial.name;
                                // Remove " (Instance)" suffix if present
                                if (materialNameToMatch.EndsWith(" (Instance)"))
                                {
                                    materialNameToMatch = materialNameToMatch[..^" (Instance)".Length];
                                }

                                // First try to find in loaded .mat files (these have proper texture references)
                                if (loadedMaterials.TryGetValue(materialNameToMatch, out Material loadedMat))
                                {
                                    materialToUse = loadedMat;
                                }
                                else
                                {
                                    // Fallback to FBX sub-asset material
                                    materialToUse = materials.Find(m => m.name == materialNameToMatch || m.name == materialNameToMatch.Replace(" (Instance)", ""));
                                }
                            }

                            // Fallback to index-based matching
                            if (materialToUse == null && i < materials.Count)
                            {
                                materialToUse = materials[i];
                            }

                            // Fallback to first material if available
                            if (materialToUse == null && materials.Count > 0)
                            {
                                materialToUse = materials[0];
                            }

                            // Fallback to first loaded .mat file
                            if (materialToUse == null && loadedMaterials.Count > 0)
                            {
                                materialToUse = loadedMaterials.Values.First();
                            }

                            // Create a material instance for preview to ensure keyword changes take effect
                            // Using the asset directly can prevent keyword changes from being applied
                            Material materialToUseForPreview = null;
                            if (materialToUse != null)
                            {
                                // Create an instance so we can safely modify keywords and properties
                                materialToUseForPreview = new Material(materialToUse);

                                // Copy texture mappings from source material to preview material for reloading
                                if (_materialTextureMappings.TryGetValue(materialToUse, out Dictionary<string, string> sourceMappings))
                                {
                                    _materialTextureMappings[materialToUseForPreview] = new Dictionary<string, string>(sourceMappings);
                                }

                                // Preserve important material properties for correct rendering
                                // This ensures alpha clipping, specular highlights, and environment reflections work correctly
                                MaterialPropertyPreserver.PreserveMaterialProperties(materialToUseForPreview, materialToUseForPreview.name, enableLogging: false);

                                // Log texture information to verify textures are loaded
                                if (materialToUseForPreview.shader != null)
                                {
                                    int propertyCount = materialToUseForPreview.shader.GetPropertyCount();
                                    for (int j = 0; j < propertyCount; j++)
                                    {
                                        if (materialToUseForPreview.shader.GetPropertyType(j) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                                        {
                                            string propName = materialToUseForPreview.shader.GetPropertyName(j);
                                            Texture tex = materialToUseForPreview.GetTexture(propName);
                                            /*if (tex == null)
                                            {
                                                Debug.LogWarning($"Material '{materialToUseForPreview.name}' texture property '{propName}' is null. Material asset path: {AssetDatabase.GetAssetPath(materialToUseForPreview)}");
                                            }*/
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Create default material if no material found
                                Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
                                if (defaultShader == null)
                                {
                                    defaultShader = Shader.Find("Standard");
                                }
                                materialToUseForPreview = new Material(defaultShader);
                            }

                            MeshInfo info = new MeshInfo
                            {
                                name = filter.name,
                                mesh = meshCopy,
                                material = materialToUseForPreview,
                                bounds = meshCopy.bounds
                            };
                            _meshes.Add(info);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                string errorMessage = $"Access denied while loading meshes for 3D preview. This may occur if:\n" +
                    "• Files are locked by another process\n" +
                    "• Unity is still importing assets\n" +
                    "• Cache folder is in use\n\n" +
                    "The cache folder may need to be manually deleted:\n" +
                    (_cacheRoot != null ? _cacheRoot : "Cache path unknown");
                
                ErrorHandler.ShowError("Access Denied - 3D Preview", errorMessage, ex);
                ErrorLogger.LogError("Load Meshes Failed", $"Access denied: {ex.Message}", ErrorHandler.ErrorCategory.FileSystem, ex, $"ModelId: {_modelId}, Version: {_version}, CacheRoot: {_cacheRoot}");
                
                // Attempt to clean up cache if it's in a bad state
                if (_cacheRoot != null)
                {
                    TryCleanupCacheFolder(_cacheRoot);
                }
            }
            catch (IOException ex) when (ex.Message.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         ex.Message.IndexOf("being used by another process", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string errorMessage = $"File access error while loading meshes for 3D preview. This may occur if:\n" +
                    "• Files are locked by another process\n" +
                    "• Unity is still importing assets\n" +
                    "• Cache folder is in use\n\n" +
                    "The cache folder may need to be manually deleted:\n" +
                    (_cacheRoot != null ? _cacheRoot : "Cache path unknown");
                
                ErrorHandler.ShowError("File Access Error - 3D Preview", errorMessage, ex);
                ErrorLogger.LogError("Load Meshes Failed", $"File access error: {ex.Message}", ErrorHandler.ErrorCategory.FileSystem, ex, $"ModelId: {_modelId}, Version: {_version}, CacheRoot: {_cacheRoot}");
                
                // Attempt to clean up cache if it's in a bad state
                if (_cacheRoot != null)
                {
                    TryCleanupCacheFolder(_cacheRoot);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Meshes Failed", $"Failed to load meshes: {ex.Message}", ErrorHandler.CategorizeException(ex), ex, $"ModelId: {_modelId}, Version: {_version}, CacheRoot: {_cacheRoot}");
                
                // Attempt to clean up cache on any error to prevent locked state
                if (_cacheRoot != null)
                {
                    TryCleanupCacheFolder(_cacheRoot);
                }
            }
            finally
            {
                // Always ensure temp assets are cleaned up, even on error
                // This prevents leaving orphaned assets in the project
                if (_tempAssets.Count > 0)
                {
                    CleanupTempAssets();
                }
            }
        }

        /// <summary>
        /// Unity GUI method called to draw the window.
        /// </summary>
        private void OnGUI()
        {
            if (_isLoading)
            {
                EditorGUILayout.HelpBox("Loading model...", MessageType.Info);
                return;
            }

            if (_meta == null)
            {
                EditorGUILayout.HelpBox("Failed to load model metadata. Please check your repository connection and try again.", MessageType.Error);
                if (GUILayout.Button("Retry"))
                {
                    _ = LoadModelAsync();
                }
                return;
            }

            if (_meshes.Count == 0)
            {
                EditorGUILayout.HelpBox("No meshes found in this model.", MessageType.Info);
                return;
            }

            // Model info
            string modelName = _meta?.identity?.name ?? "Unknown Model";
            string version = _meta?.version ?? "Unknown";
            EditorGUILayout.LabelField(modelName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Version: {version}");
            
            // Display asset paths
            if (!string.IsNullOrEmpty(_meta.installPath))
            {
                EditorGUILayout.LabelField("Install Path:", _meta.installPath);
            }
            if (!string.IsNullOrEmpty(_meta.relativePath))
            {
                EditorGUILayout.LabelField("Relative Path:", _meta.relativePath);
            }

            EditorGUILayout.Space(__UI_SPACING);

            // Mesh selection
            if (_meshes.Count > 1)
            {
                // Performance: Cache mesh names array to avoid allocation every frame
                if (_cachedMeshNames == null || _lastMeshCount != _meshes.Count)
                {
                    _cachedMeshNames = new string[_meshes.Count];
                    for (int i = 0; i < _meshes.Count; i++)
                    {
                        _cachedMeshNames[i] = _meshes[i].name;
                    }
                    _lastMeshCount = _meshes.Count;
                }
                _selectedMeshIndex = EditorGUILayout.Popup("Mesh", _selectedMeshIndex, _cachedMeshNames);
            }

            EditorGUILayout.Space(__UI_SPACING);

            // Preview controls
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rotation:", GUILayout.Width(__ROTATION_LABEL_WIDTH));
                _cameraRotation.x = EditorGUILayout.Slider(_cameraRotation.x, 0f, 360f);
                _cameraRotation.y = EditorGUILayout.Slider(_cameraRotation.y, 0f, 360f);

                if (GUILayout.Button("Reset", GUILayout.Width(__RESET_BUTTON_WIDTH)))
                {
                    _cameraRotation = new Vector2(__DEFAULT_CAMERA_ROTATION_X, __DEFAULT_CAMERA_ROTATION_Y);
                    _cameraDistance = __DEFAULT_CAMERA_DISTANCE;
                }
            }

            EditorGUILayout.LabelField("Distance:", _cameraDistance.ToString("F2"));
            _cameraDistance = EditorGUILayout.Slider(_cameraDistance, __MIN_CAMERA_DISTANCE, __MAX_CAMERA_DISTANCE);

            EditorGUILayout.Space(__UI_SPACING);

            // 3D Preview area
            _previewRect = GUILayoutUtility.GetRect(__PREVIEW_MIN_SIZE, __PREVIEW_MIN_SIZE, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Always draw background first
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(_previewRect, new Color(__PREVIEW_BACKGROUND_GRAY, __PREVIEW_BACKGROUND_GRAY, __PREVIEW_BACKGROUND_GRAY, 1f));
            }

            // Reload textures if they became null (e.g., after losing focus)
            // Do this before rendering so textures are ready
            bool texturesReloaded = ReloadTexturesIfNeeded();
            
            // If textures were reloaded, force a repaint on the next frame to ensure preview updates
            if (texturesReloaded && Event.current.type == EventType.Repaint)
            {
                EditorApplication.delayCall += () => Repaint();
            }

            if (_previewUtil != null && _selectedMeshIndex >= 0 && _selectedMeshIndex < _meshes.Count && _previewRect.width > 0 && _previewRect.height > 0)
            {
                MeshInfo meshInfo = _meshes[_selectedMeshIndex];

                if (Event.current.type == EventType.Repaint)
                {
                    // Draw the preview texture on top of background
                    RenderPreview(meshInfo);
                }
            }

            // Handle mouse input for rotation
            HandleMouseInput();
        }

        /// <summary>
        /// Renders the 3D preview of the selected mesh.
        /// </summary>
        private void RenderPreview(MeshInfo meshInfo)
        {
            if (meshInfo.mesh == null || _previewUtil == null)
            {
                Debug.LogWarning($"Cannot render preview: mesh={meshInfo.mesh != null}, previewUtil={_previewUtil != null}");
                return;
            }

            // Quick check: if material has null textures, try to reload them immediately before rendering
            if (meshInfo.material != null && meshInfo.material.shader != null)
            {
                int propertyCount = meshInfo.material.shader.GetPropertyCount();
                for (int i = 0; i < propertyCount; i++)
                {
                    if (meshInfo.material.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        string propName = meshInfo.material.shader.GetPropertyName(i);
                        Texture tex = meshInfo.material.GetTexture(propName);
                        if (tex == null && _materialTextureMappings.TryGetValue(meshInfo.material, out Dictionary<string, string> mappings))
                        {
                            if (mappings.TryGetValue(propName, out string texName) && _texturePaths.TryGetValue(texName, out string texturePath))
                            {
                                if (File.Exists(texturePath))
                                {
                                    Texture2D quickReload = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                                    if (quickReload != null)
                                    {
                                        meshInfo.material.SetTexture(propName, quickReload);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            try
            {
                // Calculate camera position based on rotation and distance
                // Use mesh bounds directly to ensure we have valid bounds
                Bounds bounds = meshInfo.mesh.bounds;
                if (bounds.size == Vector3.zero)
                {
                    // Fallback to default bounds if mesh has no bounds
                    bounds = new Bounds(Vector3.zero, Vector3.one);
                }

                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size <= 0f)
                {
                    size = __DEFAULT_BOUNDS_SIZE;
                }

                float angleX = _cameraRotation.x * Mathf.Deg2Rad;
                float angleY = _cameraRotation.y * Mathf.Deg2Rad;

                Vector3 direction = new Vector3(
                    Mathf.Sin(angleY) * Mathf.Cos(angleX),
                    Mathf.Sin(angleX),
                    Mathf.Cos(angleY) * Mathf.Cos(angleX)
                );

                Vector3 cameraPos = bounds.center + direction * (size * _cameraDistance);
                // Render the preview with custom camera position
                Texture preview = _previewUtil.Render(meshInfo.mesh, meshInfo.material, _previewRect, cameraPos, bounds.center);
                if (preview != null)
                {
                    GUI.DrawTexture(_previewRect, preview, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // Debug: log when preview is null
                    EditorGUI.DrawRect(_previewRect, new Color(__PREVIEW_BACKGROUND_GRAY, __PREVIEW_BACKGROUND_GRAY, __PREVIEW_BACKGROUND_GRAY, 1f));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Render Preview Failed", $"Failed to render preview: {ex.Message}", ErrorHandler.ErrorCategory.Unknown, ex, $"Mesh: {meshInfo.name}, Material: {meshInfo.material?.name ?? "null"}");
                EditorGUI.DrawRect(_previewRect, new Color(__ERROR_BACKGROUND_RED, 0f, 0f, 1f)); // Red background on error
            }
        }

        /// <summary>
        /// Reloads textures that have become null, typically after Unity loses and regains focus.
        /// Returns true if any textures were reloaded.
        /// </summary>
        /// <returns>True if textures were reloaded, false otherwise.</returns>
        private bool ReloadTexturesIfNeeded()
        {
            if (_meshes.Count == 0 || _texturePaths.Count == 0)
            {
                return false;
            }

            bool needsRepaint = false;
            int reloadedCount = 0;

            for (int i = 0; i < _meshes.Count; i++)
            {
                MeshInfo meshInfo = _meshes[i];
                if (meshInfo.material == null)
                {
                    continue;
                }

                // Get texture mappings for this material (try direct lookup first, then by matching)
                Dictionary<string, string> textureMappings = null;
                if (!_materialTextureMappings.TryGetValue(meshInfo.material, out textureMappings))
                {
                    // Try to find mappings by checking all materials - material instance might be different object
                    foreach (KeyValuePair<Material, Dictionary<string, string>> kvp in _materialTextureMappings)
                    {
                        if (kvp.Key != null && kvp.Key.shader == meshInfo.material.shader && 
                            kvp.Key.name == meshInfo.material.name)
                        {
                            textureMappings = kvp.Value;
                            // Update mapping to use current material instance
                            _materialTextureMappings[meshInfo.material] = new Dictionary<string, string>(textureMappings);
                            break;
                        }
                    }
                }
                
                // If we still don't have mappings, try to rebuild them by checking all texture properties
                if (textureMappings == null && meshInfo.material.shader != null)
                {
                    textureMappings = new Dictionary<string, string>();
                    int propertyCount = meshInfo.material.shader.GetPropertyCount();
                    for (int j = 0; j < propertyCount; j++)
                    {
                        if (meshInfo.material.shader.GetPropertyType(j) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            string propName = meshInfo.material.shader.GetPropertyName(j);
                            Texture tex = meshInfo.material.GetTexture(propName);
                            if (tex != null && !string.IsNullOrEmpty(tex.name))
                            {
                                // Try to find matching texture path
                                foreach (KeyValuePair<string, string> texPath in _texturePaths)
                                {
                                    if (texPath.Key.Equals(tex.name, StringComparison.OrdinalIgnoreCase) ||
                                        texPath.Key.Contains(tex.name) || tex.name.Contains(texPath.Key))
                                    {
                                        textureMappings[propName] = texPath.Key;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    if (textureMappings.Count > 0)
                    {
                        _materialTextureMappings[meshInfo.material] = textureMappings;
                    }
                }

                if (textureMappings == null || textureMappings.Count == 0)
                {
                    continue;
                }

                // Check each texture property and reload if null
                foreach (KeyValuePair<string, string> mapping in textureMappings)
                {
                    string propName = mapping.Key;
                    string texName = mapping.Value;

                    Texture currentTex = meshInfo.material.GetTexture(propName);
                    
                    // If texture is null but we have a path, reload it
                    if (currentTex == null && _texturePaths.TryGetValue(texName, out string texturePath))
                    {
                        // Verify the file still exists
                        if (!File.Exists(texturePath))
                        {
                            continue;
                        }
                        
                        // Force asset database refresh first to ensure assets are loaded
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        
                        Texture2D reloadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if (reloadedTex != null)
                        {
                            try
                            {
                                // Store old texture for comparison
                                Texture oldTex = meshInfo.material.GetTexture(propName);
                                
                                // Set texture and force update
                                meshInfo.material.SetTexture(propName, reloadedTex);
                                
                                // Force Unity to recognize the material change
                                UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(meshInfo.material, false);
                                
                                // Verify the texture was actually set
                                Texture verifyTex = meshInfo.material.GetTexture(propName);
                                if (verifyTex != null && verifyTex == reloadedTex)
                                {
                                    // Force material update and mark for repaint
                                    EditorUtility.SetDirty(meshInfo.material);
                                    needsRepaint = true;
                                    reloadedCount++;
                                    
                                    // Force preview utility to re-render by invalidating any cached state
                                    if (_previewUtil != null)
                                    {
                                        // Trigger a repaint to force re-render with new texture
                                        EditorApplication.delayCall += () => Repaint();
                                    }
                                    
                                    Debug.Log($"[ModelPreview3DWindow] Successfully reloaded texture '{texName}' for property '{propName}' on material '{meshInfo.material.name}'");
                                }
                                else
                                {
                                    Debug.LogWarning($"[ModelPreview3DWindow] Texture reloaded but not properly set. Property: {propName}, Texture: {texName}, Material: {meshInfo.material.name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ModelPreview3DWindow] Failed to reload texture '{texName}' for property '{propName}': {ex.Message}");
                            }
                        }
                    }
                }
            }

            if (reloadedCount > 0)
            {
                Debug.Log($"[ModelPreview3DWindow] Reloaded {reloadedCount} texture(s) after focus regain - forcing repaint");
                // Force immediate repaint to show updated textures
                Repaint();
                return true;
            }
            else if (needsRepaint)
            {
                Repaint();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Handles mouse input for rotating the camera.
        /// </summary>
        private void HandleMouseInput()
        {
            Event e = Event.current;

            if (_previewRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    _cameraRotation.y += e.delta.x * __MOUSE_ROTATION_SENSITIVITY;
                    _cameraRotation.x += e.delta.y * __MOUSE_ROTATION_SENSITIVITY;
                    _cameraRotation.x = Mathf.Clamp(_cameraRotation.x, __MIN_CAMERA_ROTATION_X, __MAX_CAMERA_ROTATION_X);
                    Repaint();
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance + e.delta.y * __MOUSE_ZOOM_SENSITIVITY, __MIN_CAMERA_DISTANCE, __MAX_CAMERA_DISTANCE);
                    Repaint();
                    e.Use();
                }
            }
        }
    }
}


