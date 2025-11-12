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
        private int _selectedMeshIndex = 0;
        /// <summary>Camera rotation angle around the model.</summary>
        private Vector2 _cameraRotation = new Vector2(30f, 30f);
        /// <summary>Camera distance from the model.</summary>
        private float _cameraDistance = 5f;
        /// <summary>Preview area rectangle.</summary>
        private Rect _previewRect;
        /// <summary>Whether the preview is currently loading.</summary>
        private bool _isLoading = false;
        /// <summary>List of temporary asset paths that need cleanup on window close.</summary>
        private List<string> _tempAssets = new List<string>();

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
        /// </summary>
        /// <param name="modelId">The unique identifier of the model.</param>
        /// <param name="version">The version of the model to preview.</param>
        public static void Open(string modelId, string version)
        {
            ModelPreview3DWindow window = GetWindow<ModelPreview3DWindow>("3D Preview");
            window._modelId = modelId;
            window._version = version;
            window._service = null;
            window._meta = null;
            window._meshes.Clear();
            window._selectedMeshIndex = 0;
            window._isLoading = true;
            window.Show();
            window.Repaint();
            _ = window.LoadModelAsync();
        }

        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// Initializes the preview utility.
        /// </summary>
        private void OnEnable() => _previewUtil = new PreviewUtility3D();

        /// <summary>
        /// Unity lifecycle method called when the window is disabled.
        /// Cleans up the preview utility and temporary assets.
        /// </summary>
        private void OnDisable()
        {
            if (_previewUtil != null)
            {
                _previewUtil.Dispose();
                _previewUtil = null;
            }

            // Clean up mesh copies (materials are assets, so they'll be cleaned up with asset deletion)
            foreach (MeshInfo meshInfo in _meshes)
            {
                if (meshInfo.mesh != null)
                {
                    DestroyImmediate(meshInfo.mesh);
                }
                // Don't destroy materials - they're assets that will be deleted with the temp directory
            }
            _meshes.Clear();

            // Clean up temporary assets and directories
            // Process in reverse order to delete files before directories
            for (int i = _tempAssets.Count - 1; i >= 0; i--)
            {
                string tempAsset = _tempAssets[i];
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
            _tempAssets.Clear();

            // Refresh asset database to ensure cleanup is complete
            AssetDatabase.Refresh();
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
                    ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                    Repository.IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                        ? new Repository.FileSystemRepository(settings.repositoryRoot)
                        : new Repository.HttpRepository(settings.repositoryRoot);
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
                Debug.LogError($"Failed to load model for 3D preview: {ex.Message}");
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

            if (_meta == null || _meta.assetGuids == null)
            {
                return;
            }

            try
            {
                // Download the model version to cache
                (string cacheRoot, ModelMeta meta) = await _service.DownloadModelVersionAsync(_modelId, _version);

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
                string[] textureExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.psd" };
                List<string> textureFiles = new List<string>();

                foreach (string ext in textureExtensions)
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
                            Debug.Log($"Loaded texture file: {texName} from {tempTexturePath}");
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

                            // Preserve alpha clipping settings
                            if (loadedMat.HasProperty("_AlphaClip"))
                            {
                                float alphaClipValue = loadedMat.GetFloat("_AlphaClip");
                                bool alphaClipping = true;

                                // Set the _ALPHATEST_ON keyword for URP/Built-in shaders
                                if (alphaClipping)
                                {
                                    loadedMat.EnableKeyword("_ALPHATEST_ON");
                                    Debug.Log($"Enabled _ALPHATEST_ON keyword for material '{matName}' (AlphaClip: {alphaClipValue})");
                                }
                                else
                                {
                                    loadedMat.DisableKeyword("_ALPHATEST_ON");
                                }

                                // Ensure _Cutoff property is preserved and set correctly (alpha clipping threshold)
                                if (loadedMat.HasProperty("_Cutoff"))
                                {
                                    float currentCutoff = loadedMat.GetFloat("_Cutoff");
                                    // Re-set the value to ensure it's applied (sometimes values don't persist after import)
                                    loadedMat.SetFloat("_Cutoff", currentCutoff);
                                    Debug.Log($"Set _Cutoff value for material '{matName}': {currentCutoff}");
                                }

                                Debug.Log($"Configured alpha clipping for material '{matName}': {alphaClipping} (AlphaClip value: {alphaClipValue})");
                            }

                            // Preserve specular highlights setting
                            if (loadedMat.HasProperty("_SpecularHighlights"))
                            {
                                float specularHighlights = loadedMat.GetFloat("_SpecularHighlights");
                                loadedMat.SetFloat("_SpecularHighlights", specularHighlights);
                                Debug.Log($"Preserved _SpecularHighlights for material '{matName}': {specularHighlights}");
                            }

                            // Preserve specular color (URP and Built-in)
                            if (loadedMat.HasProperty("_SpecColor"))
                            {
                                Color specColor = loadedMat.GetColor("_SpecColor");
                                loadedMat.SetColor("_SpecColor", specColor);
                                Debug.Log($"Preserved _SpecColor for material '{matName}': {specColor}");
                            }

                            // Preserve smoothness (URP)
                            if (loadedMat.HasProperty("_Smoothness"))
                            {
                                float smoothness = loadedMat.GetFloat("_Smoothness");
                                loadedMat.SetFloat("_Smoothness", smoothness);
                                Debug.Log($"Preserved _Smoothness for material '{matName}': {smoothness}");
                            }

                            // Preserve glossiness (Built-in Standard shader)
                            if (loadedMat.HasProperty("_Glossiness"))
                            {
                                float glossiness = loadedMat.GetFloat("_Glossiness");
                                loadedMat.SetFloat("_Glossiness", glossiness);
                                Debug.Log($"Preserved _Glossiness for material '{matName}': {glossiness}");
                            }

                            // Preserve specular gloss map texture and switch to specular workflow if present
                            bool hasSpecGlossMap = false;
                            if (loadedMat.HasProperty("_SpecGlossMap"))
                            {
                                Texture specGlossMap = loadedMat.GetTexture("_SpecGlossMap");
                                if (specGlossMap != null)
                                {
                                    loadedMat.SetTexture("_SpecGlossMap", specGlossMap);
                                    hasSpecGlossMap = true;
                                    Debug.Log($"Preserved _SpecGlossMap for material '{matName}': {specGlossMap.name}");
                                }
                            }

                            // Switch to specular workflow if specular map is present
                            // _WorkflowMode: 0 = Specular, 1 = Metallic
                            if (hasSpecGlossMap && loadedMat.HasProperty("_WorkflowMode"))
                            {
                                loadedMat.SetFloat("_WorkflowMode", 0f); // Specular workflow
                                Debug.Log($"Switched to specular workflow for material '{matName}' (specular map present)");
                            }

                            // Preserve metallic value (for metallic workflow)
                            if (loadedMat.HasProperty("_Metallic"))
                            {
                                float metallic = loadedMat.GetFloat("_Metallic");
                                loadedMat.SetFloat("_Metallic", metallic);
                                Debug.Log($"Preserved _Metallic for material '{matName}': {metallic}");
                            }

                            // Preserve metallic gloss map texture
                            if (loadedMat.HasProperty("_MetallicGlossMap"))
                            {
                                Texture metallicGlossMap = loadedMat.GetTexture("_MetallicGlossMap");
                                if (metallicGlossMap != null)
                                {
                                    loadedMat.SetTexture("_MetallicGlossMap", metallicGlossMap);
                                    Debug.Log($"Preserved _MetallicGlossMap for material '{matName}': {metallicGlossMap.name}");
                                }
                            }

                            // Preserve environment reflections setting
                            if (loadedMat.HasProperty("_EnvironmentReflections"))
                            {
                                float environmentReflections = loadedMat.GetFloat("_EnvironmentReflections");
                                loadedMat.SetFloat("_EnvironmentReflections", environmentReflections);
                                Debug.Log($"Preserved _EnvironmentReflections for material '{matName}': {environmentReflections}");
                            }

                            // Also preserve GlossyReflections if it exists (some shaders use this instead)
                            if (loadedMat.HasProperty("_GlossyReflections"))
                            {
                                float glossyReflections = loadedMat.GetFloat("_GlossyReflections");
                                loadedMat.SetFloat("_GlossyReflections", glossyReflections);
                            }

                            // If material doesn't have _AlphaClip property, try to enable keyword if shader supports it
                            // This handles materials that might have the keyword set but not the property
                            if (!loadedMat.HasProperty("_AlphaClip") && loadedMat.shader != null)
                            {
                                // Try to enable keyword if shader supports it
                                try
                                {
                                    // Check if keyword exists in shader
                                    Shader shader = loadedMat.shader;
                                    if (shader != null)
                                    {
                                        // Enable keyword - if it doesn't exist, this will be a no-op
                                        loadedMat.EnableKeyword("_ALPHATEST_ON");
                                    }
                                }
                                catch
                                {
                                    // Keyword might not exist in this shader, ignore
                                }
                            }
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

                                // Preserve important material properties for correct rendering
                                // This ensures alpha clipping, specular highlights, and environment reflections work correctly

                                // Preserve alpha clipping settings - check multiple property names
                                bool alphaClipping = false;
                                float alphaClipValue = 0f;
                                float cutoffValue = 0.5f;

                                // Check for _AlphaClip property (URP)
                                if (materialToUseForPreview.HasProperty("_AlphaClip"))
                                {
                                    alphaClipValue = materialToUseForPreview.GetFloat("_AlphaClip");
                                    alphaClipping = true;//alphaClipValue >= 0.5f;
                                }
                                // Check for _AlphaTest property (some shaders)
                                else if (materialToUseForPreview.HasProperty("_AlphaTest"))
                                {
                                    alphaClipValue = materialToUseForPreview.GetFloat("_AlphaTest");
                                    alphaClipping = true;//alphaClipValue >= 0.5f;
                                }

                                // Get cutoff value if available
                                if (materialToUseForPreview.HasProperty("_Cutoff"))
                                {
                                    cutoffValue = materialToUseForPreview.GetFloat("_Cutoff");
                                }

                                // Set the _ALPHATEST_ON keyword BEFORE setting properties
                                // This ensures the shader variant is compiled with the keyword
                                if (alphaClipping)
                                {
                                    materialToUseForPreview.EnableKeyword("_ALPHATEST_ON");

                                    // Re-apply cutoff value after enabling keyword

                                    if (materialToUseForPreview.HasProperty("_Cutoff"))
                                    {
                                        materialToUseForPreview.SetFloat("_Cutoff", cutoffValue);
                                    }
                                }
                                else
                                {
                                    materialToUseForPreview.DisableKeyword("_ALPHATEST_ON");
                                }


                                // Preserve specular highlights setting
                                if (materialToUseForPreview.HasProperty("_SpecularHighlights"))
                                {
                                    float specularHighlights = materialToUseForPreview.GetFloat("_SpecularHighlights");
                                    materialToUseForPreview.SetFloat("_SpecularHighlights", specularHighlights);
                                }

                                // Preserve specular color (URP and Built-in)
                                if (materialToUseForPreview.HasProperty("_SpecColor"))
                                {
                                    Color specColor = materialToUseForPreview.GetColor("_SpecColor");
                                    materialToUseForPreview.SetColor("_SpecColor", specColor);
                                }

                                // Preserve smoothness (URP)
                                if (materialToUseForPreview.HasProperty("_Smoothness"))
                                {
                                    float smoothness = materialToUseForPreview.GetFloat("_Smoothness");
                                    materialToUseForPreview.SetFloat("_Smoothness", smoothness);
                                }

                                // Preserve glossiness (Built-in Standard shader)
                                if (materialToUseForPreview.HasProperty("_Glossiness"))
                                {
                                    float glossiness = materialToUseForPreview.GetFloat("_Glossiness");
                                    materialToUseForPreview.SetFloat("_Glossiness", glossiness);
                                }

                                // Preserve specular gloss map texture and switch to specular workflow if present
                                bool hasSpecGlossMapForPreview = false;
                                if (materialToUseForPreview.HasProperty("_SpecGlossMap"))
                                {
                                    Texture specGlossMap = materialToUseForPreview.GetTexture("_SpecGlossMap");
                                    if (specGlossMap != null)
                                    {
                                        materialToUseForPreview.SetTexture("_SpecGlossMap", specGlossMap);
                                        hasSpecGlossMapForPreview = true;
                                    }
                                }

                                // Switch to specular workflow if specular map is present
                                // _WorkflowMode: 0 = Specular, 1 = Metallic
                                if (hasSpecGlossMapForPreview && materialToUseForPreview.HasProperty("_WorkflowMode"))
                                {
                                    materialToUseForPreview.SetFloat("_WorkflowMode", 0f); // Specular workflow
                                }

                                // Preserve metallic value (for metallic workflow)
                                if (materialToUseForPreview.HasProperty("_Metallic"))
                                {
                                    float metallic = materialToUseForPreview.GetFloat("_Metallic");
                                    materialToUseForPreview.SetFloat("_Metallic", metallic);
                                }

                                // Preserve metallic gloss map texture
                                if (materialToUseForPreview.HasProperty("_MetallicGlossMap"))
                                {
                                    Texture metallicGlossMap = materialToUseForPreview.GetTexture("_MetallicGlossMap");
                                    if (metallicGlossMap != null)
                                    {
                                        materialToUseForPreview.SetTexture("_MetallicGlossMap", metallicGlossMap);
                                    }
                                }

                                // Preserve environment reflections setting
                                if (materialToUseForPreview.HasProperty("_EnvironmentReflections"))
                                {
                                    float environmentReflections = materialToUseForPreview.GetFloat("_EnvironmentReflections");
                                    materialToUseForPreview.SetFloat("_EnvironmentReflections", environmentReflections);
                                }

                                // Also preserve GlossyReflections if it exists (some shaders use this instead)
                                if (materialToUseForPreview.HasProperty("_GlossyReflections"))
                                {
                                    float glossyReflections = materialToUseForPreview.GetFloat("_GlossyReflections");
                                    materialToUseForPreview.SetFloat("_GlossyReflections", glossyReflections);
                                }

                                // If material doesn't have _AlphaClip property, try to enable keyword if shader supports it
                                if (!materialToUseForPreview.HasProperty("_AlphaClip") && !materialToUseForPreview.HasProperty("_AlphaTest") && materialToUseForPreview.shader != null)
                                {
                                    try
                                    {
                                        materialToUseForPreview.EnableKeyword("_ALPHATEST_ON");
                                    }
                                    catch
                                    {
                                        // Keyword might not exist in this shader, ignore
                                    }
                                }

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
                                            if (tex == null)
                                            {
                                                Debug.LogWarning($"Material '{materialToUseForPreview.name}' texture property '{propName}' is null. Material asset path: {AssetDatabase.GetAssetPath(materialToUseForPreview)}");
                                            }
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
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load meshes: {ex.Message}");
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
                EditorGUILayout.HelpBox("Failed to load model metadata.", MessageType.Error);
                return;
            }

            if (_meshes.Count == 0)
            {
                EditorGUILayout.HelpBox("No meshes found in this model.", MessageType.Info);
                return;
            }

            // Model info
            EditorGUILayout.LabelField(_meta.identity.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Version: {_meta.version}");

            EditorGUILayout.Space(10);

            // Mesh selection
            if (_meshes.Count > 1)
            {
                string[] meshNames = _meshes.Select(m => m.name).ToArray();
                _selectedMeshIndex = EditorGUILayout.Popup("Mesh", _selectedMeshIndex, meshNames);
            }

            EditorGUILayout.Space(10);

            // Preview controls
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rotation:", GUILayout.Width(60));
                _cameraRotation.x = EditorGUILayout.Slider(_cameraRotation.x, 0f, 360f);
                _cameraRotation.y = EditorGUILayout.Slider(_cameraRotation.y, 0f, 360f);

                if (GUILayout.Button("Reset", GUILayout.Width(60)))
                {
                    _cameraRotation = new Vector2(30f, 30f);
                    _cameraDistance = 5f;
                }
            }

            EditorGUILayout.LabelField("Distance:", _cameraDistance.ToString("F2"));
            _cameraDistance = EditorGUILayout.Slider(_cameraDistance, 1f, 20f);

            EditorGUILayout.Space(10);

            // 3D Preview area
            _previewRect = GUILayoutUtility.GetRect(400, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Always draw background first
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(_previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
            }

            if (_previewUtil != null && _selectedMeshIndex < _meshes.Count && _previewRect.width > 0 && _previewRect.height > 0)
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
                    size = 1f;
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
                    EditorGUI.DrawRect(_previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to render preview: {ex.Message}\n{ex.StackTrace}");
                EditorGUI.DrawRect(_previewRect, new Color(0.5f, 0f, 0f, 1f)); // Red background on error
            }
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
                    _cameraRotation.y += e.delta.x * 0.5f;
                    _cameraRotation.x += e.delta.y * 0.5f;
                    _cameraRotation.x = Mathf.Clamp(_cameraRotation.x, -90f, 90f);
                    Repaint();
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance + e.delta.y * 0.1f, 1f, 20f);
                    Repaint();
                    e.Use();
                }
            }
        }
    }
}


