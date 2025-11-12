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
                string[] textureExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.psd" };
                List<string> textureFiles = new List<string>();

                foreach (string ext in textureExtensions)
                {
                    textureFiles.AddRange(Directory.GetFiles(cacheRoot, ext, SearchOption.AllDirectories));
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

                                    Texture2D matchingTex = null;

                                    if (oldTex != null)
                                    {
                                        // Try to find matching texture by the referenced texture's name
                                        string texName = oldTex.name;
                                        // Remove common suffixes Unity adds
                                        if (texName.Contains(" (Texture2D)"))
                                        {
                                            texName = texName.Replace(" (Texture2D)", "");
                                        }

                                        loadedTextures.TryGetValue(texName, out matchingTex);
                                    }

                                    // If still not found, try matching by property name and common texture naming patterns
                                    if (matchingTex == null && loadedTextures.Count > 0)
                                    {
                                        // Common mappings: _BaseMap/_MainTex -> diffuse/albedo/base textures
                                        if (propName == "_BaseMap" || propName == "_MainTex")
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

                                        // If still not found, use first available texture as fallback
                                        if (matchingTex == null)
                                        {
                                            matchingTex = loadedTextures.Values.First();
                                            Debug.LogWarning($"Using first available texture '{matchingTex.name}' as fallback for property '{propName}' in material '{matName}'");
                                        }
                                    }

                                    if (matchingTex != null)
                                    {
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
                                        Debug.LogWarning($"Could not find texture for property '{propName}' in material '{matName}'. Available textures: {string.Join(", ", loadedTextures.Keys)}");
                                    }
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
                        Debug.Log($"Found material asset: {material.name}, shader: {material.shader?.name}");
                    }
                    else if (asset is Texture2D texture)
                    {
                        textures.Add(texture);
                        Debug.Log($"Found texture asset: {texture.name} ({texture.width}x{texture.height})");
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
                                    Debug.Log($"Matched material '{materialNameToMatch}' to loaded .mat file");
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
                                Debug.Log($"Using first loaded .mat file: {materialToUse.name}");
                            }

                            // Use the material asset directly from the imported FBX
                            // The material should reference textures that are now in the same directory
                            Material materialToUseForPreview = null;
                            if (materialToUse != null)
                            {
                                // Use the material asset directly - it should have texture references
                                materialToUseForPreview = materialToUse;

                                Debug.Log($"Using material asset: '{materialToUseForPreview.name}' from '{tempAssetPath}', shader: {materialToUseForPreview.shader?.name}");

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
                                            if (tex != null)
                                            {
                                                Debug.Log($"Material '{materialToUseForPreview.name}' has texture property '{propName}': {tex.name} ({tex.width}x{tex.height}), type: {tex.GetType().Name}");
                                            }
                                            else
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

                // Debug logging
                if (Event.current.type == EventType.Repaint)
                {
                    Debug.Log($"Rendering preview: Mesh={meshInfo.mesh.name}, Bounds={bounds}, CameraPos={cameraPos}, Rect={_previewRect}");
                }

                // Render the preview with custom camera position
                Texture preview = _previewUtil.Render(meshInfo.mesh, meshInfo.material, _previewRect, cameraPos, bounds.center);
                if (preview != null)
                {
                    GUI.DrawTexture(_previewRect, preview, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // Debug: log when preview is null
                    Debug.LogWarning($"Preview texture is null. Mesh: {meshInfo.mesh?.name}, Material: {meshInfo.material?.name}, Rect: {_previewRect}");
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


