using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Helps modelers assemble a version folder from selected project assets and create meta.
    /// Responsible for collecting selected assets, resolving dependencies, and materializing a local version folder for submission.
    /// </summary>
    public static class ModelDeployer
    {
        /// <summary>
        /// Builds ModelMeta from the currently selected assets in the Unity Project window.
        /// Collects all selected assets (FBX, OBJ, textures, materials), extracts their GUIDs,
        /// resolves dependencies, calculates mesh statistics, and prepares metadata for submission.
        /// </summary>
        /// <param name="identityName">Display name of the model.</param>
        /// <param name="identityId">Unique identifier (GUID) for the model. If null or empty, generates a new GUID.</param>
        /// <param name="version">Version string in SemVer format (e.g., "1.0.0").</param>
        /// <param name="description">Model description text.</param>
        /// <param name="imagePaths">List of absolute paths to preview images.</param>
        /// <param name="tags">List of tags for categorizing the model.</param>
        /// <param name="installPath">Absolute install path in the Unity project (e.g., "Assets/Models/ModelName").</param>
        /// <param name="relativePath">Relative path from Assets folder (e.g., "Models/ModelName").</param>
        /// <param name="idProvider">User identity provider for getting author information.</param>
        /// <returns>Complete ModelMeta object ready for submission.</returns>
        public static async Task<ModelMeta> BuildMetaFromSelectionAsync(string identityName, string identityId, string version, string description, IEnumerable<string> imagePaths, IEnumerable<string> tags, string installPath, string relativePath, IUserIdentityProvider idProvider)
        {
            string resolvedId = string.IsNullOrWhiteSpace(identityId) ? Guid.NewGuid().ToString("N") : identityId;
            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity { id = resolvedId, name = identityName },
                version = version,
                description = description,
                author = idProvider.GetUserName(),
                createdTimeTicks = DateTime.Now.Ticks,
                updatedTimeTicks = DateTime.Now.Ticks,
                uploadTimeTicks = DateTime.Now.Ticks,
                installPath = ResolveInstallPath(installPath, identityName),
                relativePath = ResolveRelativePath(relativePath, identityName)
            };

            // Collect selected assets (FBX, textures, materials, etc.) and their dependencies
            string[] selected = Selection.assetGUIDs;
            List<string> relPayload = new List<string>();
            List<string> guids = new List<string>();
            int totalVertices = 0;
            int totalTriangles = 0;
            HashSet<int> processedMeshIds = new HashSet<int>();

            foreach (string guid in selected)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == FileExtensions.FBX || ext == FileExtensions.OBJ ||
                    ext == FileExtensions.PNG || ext == FileExtensions.TGA || ext == FileExtensions.JPG || ext == FileExtensions.JPEG || ext == FileExtensions.PSD ||
                    ext == FileExtensions.MAT)
                {
                    // In the version folder the payload is placed under payload/<filename>
                    string fileName = Path.GetFileName(path);
                    relPayload.Add($"payload/{fileName}");
                    guids.Add(guid);
                    AccumulateMeshStats(path, processedMeshIds, ref totalVertices, ref totalTriangles);

                    // Collect details for materials and textures
                    Type t = AssetDatabase.GetMainAssetTypeAtPath(path);
                    string typeName = t != null ? t.Name : null;
                    if (typeName == nameof(Material))
                    {
                        meta.materials.Add(new AssetRef
                        {
                            guid = guid,
                            name = Path.GetFileNameWithoutExtension(path),
                            relativePath = $"payload/{fileName}",
                            type = typeName
                        });
                    }
                    else if (typeName == nameof(Texture2D))
                    {
                        meta.textures.Add(new AssetRef
                        {
                            guid = guid,
                            name = Path.GetFileNameWithoutExtension(path),
                            relativePath = $"payload/{fileName}",
                            type = typeName
                        });
                    }
                }

                // Capture model importer settings for FBX/OBJ
                if (ext == FileExtensions.FBX || ext == FileExtensions.OBJ)
                {
                    ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp != null)
                    {
                        meta.modelImporters[$"payload/{Path.GetFileName(path)}"] = new ModelImporterSettings
                        {
                            materialImportMode = imp.materialImportMode.ToString(),
                            materialSearch = imp.materialSearch.ToString(),
                            materialName = imp.materialName.ToString()
                        };
                    }
                }
            }

            meta.payloadRelativePaths = relPayload;
            meta.assetGuids = guids;

            // Log GUID collection for debugging
            Debug.Log($"[ModelDeployer] Collected {guids.Count} asset GUIDs for model '{meta.identity.name}': {string.Join(", ", guids.Take(5))}{(guids.Count > 5 ? "..." : "")}");

            // Dependencies: gather GUIDs referenced by selected assets (materials, textures)
            HashSet<string> dependencyGuids = new HashSet<string>();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string[] deps = AssetDatabase.GetDependencies(assetPath, recursive: true);
                foreach (string dep in deps)
                {
                    string dGuid = AssetDatabase.AssetPathToGUID(dep);
                    if (!string.IsNullOrEmpty(dGuid) && dGuid != guid)
                    {
                        // Filter out shader-related files
                        string dext = Path.GetExtension(dep).ToLowerInvariant();
                        if (FileExtensions.IsNotAllowedFileExtension(dext))
                        {
                            continue;
                        }
                        dependencyGuids.Add(dGuid);
                    }
                }
            }
            foreach (string d in dependencyGuids)
            {
                if (!meta.assetGuids.Contains(d))
                {
                    meta.dependencies.Add(d);
                    // Add type/name context if available in the current project
                    string depPath = AssetDatabase.GUIDToAssetPath(d);
                    Type depType = string.IsNullOrEmpty(depPath) ? null : AssetDatabase.GetMainAssetTypeAtPath(depPath);
                    meta.dependenciesDetailed.Add(new DependencyRef
                    {
                        guid = d,
                        type = depType != null ? depType.Name : string.Empty,
                        name = string.IsNullOrEmpty(depPath) ? string.Empty : Path.GetFileNameWithoutExtension(depPath)
                    });
                    // Also add to materials/textures if recognizable
                    if (!string.IsNullOrEmpty(depPath))
                    {
                        string depExt = Path.GetExtension(depPath).ToLowerInvariant();
                        if (depExt == FileExtensions.FBX || depExt == FileExtensions.OBJ)
                        {
                            AccumulateMeshStats(depPath, processedMeshIds, ref totalVertices, ref totalTriangles);
                        }

                        string typeName = depType != null ? depType.Name : null;
                        if (typeName == nameof(Material))
                        {
                            meta.materials.Add(new AssetRef
                            {
                                guid = d,
                                name = Path.GetFileNameWithoutExtension(depPath),
                                relativePath = null,
                                type = typeName
                            });
                        }
                        else if (typeName == nameof(Texture2D))
                        {
                            meta.textures.Add(new AssetRef
                            {
                                guid = d,
                                name = Path.GetFileNameWithoutExtension(depPath),
                                relativePath = null,
                                type = typeName
                            });
                        }
                    }
                }
            }

            // Add images (expected to be absolute paths or project relative under Assets)
            meta.imageRelativePaths = imagePaths != null ? imagePaths.Select(p => $"images/{Path.GetFileName(p)}").ToList() : new List<string>();

            // Add tags
            meta.tags.values = tags != null ? tags.ToList() : new List<string>();
            meta.vertexCount = totalVertices;
            meta.triangleCount = totalTriangles;

            return await Task.FromResult(meta);
        }

        /// <summary>
        /// Generates automatic preview images for model assets.
        /// Attempts to create a preview image from the primary model asset (FBX/OBJ/Prefab).
        /// The preview is saved as "auto_preview.png" in the images folder.
        /// </summary>
        /// <param name="meta">Model metadata containing asset GUIDs.</param>
        /// <param name="versionRoot">Root directory of the version folder where images should be saved.</param>
        private static void GenerateAssetPreviews(ModelMeta meta, string versionRoot)
        {
            try
            {
                if (meta == null)
                {
                    return;
                }

                string primaryGuid = SelectPrimaryAssetGuid(meta);
                if (string.IsNullOrEmpty(primaryGuid))
                {
                    return;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(primaryGuid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return;
                }

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                {
                    return;
                }

                string imagesDir = Path.Combine(versionRoot, "images");
                Directory.CreateDirectory(imagesDir);

                Texture preview = FetchAssetPreview(asset, highResolution: true);

                if (preview != null)
                {
                    string previewFile = Path.Combine(imagesDir, "auto_preview.png");
                    if (SaveTextureToPng(preview, previewFile))
                    {
                        string rel = "images/auto_preview.png";
                        meta.previewImagePath = rel;
                        AddImageReference(meta, rel);
                    }
                }
            }
            catch
            {
                // ignore preview failures
            }
        }

        /// <summary>
        /// Selects the primary asset GUID for preview generation.
        /// Prefers FBX/OBJ files or Prefabs, falling back to the first available asset GUID.
        /// </summary>
        /// <param name="meta">Model metadata containing asset GUIDs.</param>
        /// <returns>The GUID of the primary asset, or null if no suitable asset is found.</returns>
        private static string SelectPrimaryAssetGuid(ModelMeta meta)
        {
            if (meta == null || meta.assetGuids == null)
            {
                return null;
            }

            foreach (string guid in meta.assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == FileExtensions.FBX || ext == FileExtensions.OBJ || ext == FileExtensions.PREFAB)
                {
                    return guid;
                }
            }

            return meta.assetGuids.FirstOrDefault();
        }

        /// <summary>
        /// Fetches a preview texture for an asset using Unity's AssetPreview API.
        /// Waits for the preview to load if it's still being generated.
        /// Falls back to mini thumbnail if high-resolution preview is not available.
        /// </summary>
        /// <param name="asset">The Unity asset to generate a preview for.</param>
        /// <param name="highResolution">Whether to request a high-resolution preview.</param>
        /// <returns>The preview texture, or null if preview generation fails.</returns>
        private static Texture FetchAssetPreview(UnityEngine.Object asset, bool highResolution)
        {
            if (asset == null)
            {
                return null;
            }

            Texture tex = highResolution ? AssetPreview.GetAssetPreview(asset) : AssetPreview.GetMiniThumbnail(asset);
            int attempts = 0;
            while (tex == null && attempts < 30 && AssetPreview.IsLoadingAssetPreview(asset.GetEntityId()))
            {
                Thread.Sleep(50);
                tex = highResolution ? AssetPreview.GetAssetPreview(asset) : AssetPreview.GetMiniThumbnail(asset);
                attempts++;
            }

            if (tex == null && highResolution)
            {
                tex = AssetPreview.GetMiniThumbnail(asset);
            }

            return tex;
        }

        /// <summary>
        /// Saves a Unity texture to a PNG file on disk.
        /// Creates a readable copy of the texture, renders it to a RenderTexture, then encodes to PNG.
        /// </summary>
        /// <param name="texture">The texture to save.</param>
        /// <param name="absolutePath">Absolute file path where the PNG should be saved.</param>
        /// <returns>True if the texture was successfully saved, false otherwise.</returns>
        private static bool SaveTextureToPng(Texture texture, string absolutePath)
        {
            if (texture == null)
            {
                return false;
            }

            int width = texture.width;
            int height = texture.height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();
                byte[] png = readable.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, png);
                UnityEngine.Object.DestroyImmediate(readable);
                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
            }
        }

        /// <summary>
        /// Resolves and validates the install path for a model.
        /// Ensures the path starts with "Assets/" and uses forward slashes.
        /// Falls back to a default path if the provided path is invalid.
        /// </summary>
        /// <param name="installPath">The install path provided by the user (may be null or invalid).</param>
        /// <param name="identityName">Model name used for generating default paths.</param>
        /// <returns>Resolved and sanitized install path.</returns>
        private static string ResolveInstallPath(string installPath, string identityName)
        {
            string sanitizedName = SanitizeFolderName(identityName);
            string defaultPath = $"Assets/Models/{sanitizedName}";

            string path;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                path = defaultPath;
                Debug.Log($"[ModelDeployer] Using default install path for model '{identityName}': {path}");
            }
            else
            {
                path = PathUtils.SanitizePathSeparator(installPath);
                Debug.Log($"[ModelDeployer] Using provided install path for model '{identityName}': {path}");
            }

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                path = $"Assets/{path.TrimStart('/')}";
                Debug.Log($"[ModelDeployer] Added 'Assets/' prefix to install path: {path}");
            }

            string finalPath = PathUtils.SanitizePathSeparator(path);
            Debug.Log($"[ModelDeployer] Final resolved install path for model '{identityName}': {finalPath}");
            return finalPath;
        }

        /// <summary>
        /// Resolves and validates the relative path for a model.
        /// Validates the path using PathUtils, removes "Assets/" prefix if present, and ensures proper formatting.
        /// Falls back to a default path if validation fails.
        /// </summary>
        /// <param name="relativePath">The relative path provided by the user (may be null or invalid).</param>
        /// <param name="identityName">Model name used for generating default paths.</param>
        /// <returns>Resolved and validated relative path (without "Assets/" prefix).</returns>
        private static string ResolveRelativePath(string relativePath, string identityName)
        {
            string sanitizedName = SanitizeFolderName(identityName);
            string defaultPath = $"Models/{sanitizedName}";

            // Validate relative path if provided
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                List<string> pathErrors = PathUtils.ValidateRelativePath(relativePath);
                if (pathErrors.Count > 0)
                {
                    Debug.LogWarning($"[ModelDeployer] Invalid relative path '{relativePath}' for model '{identityName}': {string.Join(", ", pathErrors)}. Using fallback path.");
                    relativePath = null; // Force fallback to default
                }
                else
                {
                    Debug.Log($"[ModelDeployer] Using validated relative path '{relativePath}' for model '{identityName}'");
                }
            }

            string path = string.IsNullOrWhiteSpace(relativePath) ? defaultPath : PathUtils.SanitizePathSeparator(relativePath);
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                path = path[7..]; // Remove "Assets/" prefix
                Debug.Log($"[ModelDeployer] Removed 'Assets/' prefix from path: {path}");
            }

            string finalPath = PathUtils.SanitizePathSeparator(path);
            Debug.Log($"[ModelDeployer] Final resolved relative path for model '{identityName}': {finalPath}");
            return finalPath;
        }

        /// <summary>
        /// Sanitizes a folder name by removing invalid file system characters.
        /// Replaces invalid characters with underscores and spaces with underscores.
        /// </summary>
        /// <param name="name">Original folder name.</param>
        /// <returns>Sanitized folder name safe for use in file system paths.</returns>
        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Models";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] result = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(result).Replace(' ', '_');
        }

        /// <summary>
        /// Adds an image reference to the model metadata if it doesn't already exist.
        /// </summary>
        /// <param name="meta">Model metadata to update.</param>
        /// <param name="relativePath">Relative path to the image file.</param>
        private static void AddImageReference(ModelMeta meta, string relativePath)
        {
            if (meta == null || string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            meta.imageRelativePaths ??= new List<string>();
            if (!meta.imageRelativePaths.Contains(relativePath))
            {
                meta.imageRelativePaths.Add(relativePath);
            }
        }

        /// <summary>
        /// Accumulates mesh statistics (vertex and triangle counts) from a model asset.
        /// Processes both MeshFilter and SkinnedMeshRenderer components.
        /// Uses a set of processed mesh instance IDs to avoid counting the same mesh multiple times.
        /// </summary>
        /// <param name="assetPath">Path to the model asset (FBX/OBJ/Prefab).</param>
        /// <param name="processedMeshIds">Set of already processed mesh instance IDs to avoid duplicates.</param>
        /// <param name="vertexCount">Running total of vertices (incremented by this method).</param>
        /// <param name="triangleCount">Running total of triangles (incremented by this method).</param>
        private static void AccumulateMeshStats(string assetPath, HashSet<int> processedMeshIds, ref int vertexCount, ref int triangleCount)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root == null)
            {
                return;
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                AddMesh(filter.sharedMesh, processedMeshIds, ref vertexCount, ref triangleCount);
            }

            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                AddMesh(skinned.sharedMesh, processedMeshIds, ref vertexCount, ref triangleCount);
            }
        }

        /// <summary>
        /// Adds a single mesh's statistics to the running totals.
        /// Handles both single submesh and multi-submesh meshes.
        /// Skips meshes that have already been processed (by instance ID).
        /// </summary>
        /// <param name="mesh">The mesh to process.</param>
        /// <param name="processedMeshIds">Set of processed mesh instance IDs.</param>
        /// <param name="vertexCount">Running total of vertices (incremented by this method).</param>
        /// <param name="triangleCount">Running total of triangles (incremented by this method).</param>
        private static void AddMesh(Mesh mesh, HashSet<int> processedMeshIds, ref int vertexCount, ref int triangleCount)
        {
            if (mesh == null)
            {
                return;
            }

            if (!processedMeshIds.Add(mesh.GetInstanceID()))
            {
                return;
            }

            vertexCount += mesh.vertexCount;

            int subMeshCount = mesh.subMeshCount;
            if (subMeshCount > 0)
            {
                for (int i = 0; i < subMeshCount; i++)
                {
                    triangleCount += (int)(mesh.GetIndexCount(i) / 3);
                }
            }
            else
            {
                int[] tris = mesh.triangles;
                triangleCount += tris != null ? tris.Length / 3 : 0;
            }
        }

        /// <summary>
        /// Creates a local version folder structure and copies all model assets into it.
        /// Creates the folder structure: payload/ (for main assets), payload/deps/ (for dependencies), and images/.
        /// Copies selected assets, their .meta files, dependencies, and generates automatic previews.
        /// Writes the model metadata JSON file to the folder for debugging.
        /// </summary>
        /// <param name="meta">Model metadata containing asset paths and GUIDs.</param>
        /// <param name="destinationAbsoluteFolder">Absolute path to the destination folder where the version structure should be created.</param>
        /// <returns>The destination folder path (same as input parameter).</returns>
        public static async Task<string> MaterializeLocalVersionFolderAsync(ModelMeta meta, string destinationAbsoluteFolder)
        {
            Directory.CreateDirectory(destinationAbsoluteFolder);
            Directory.CreateDirectory(Path.Combine(destinationAbsoluteFolder, "payload"));
            Directory.CreateDirectory(Path.Combine(destinationAbsoluteFolder, "images"));

            GenerateAssetPreviews(meta, destinationAbsoluteFolder);

            // Copy payload from project selection into the folder (with .meta)
            foreach (string rel in meta.payloadRelativePaths)
            {
                string fileName = Path.GetFileName(rel);
                string guid = meta.assetGuids.FirstOrDefault(g => Path.GetFileName(AssetDatabase.GUIDToAssetPath(g)) == fileName);
                if (guid != null)
                {
                    string src = AssetDatabase.GUIDToAssetPath(guid);
                    string dst = Path.Combine(destinationAbsoluteFolder, "payload", fileName);
                    File.Copy(src, dst, overwrite: true);
                    string srcMeta = src + FileExtensions.META;
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, dst + FileExtensions.META, overwrite: true);
                    }
                }
            }

            // Optionally copy dependencies into payload/deps (non-FBX assets like textures/materials)
            string depsDir = Path.Combine(destinationAbsoluteFolder, "payload", "deps");
            Directory.CreateDirectory(depsDir);
            foreach (string dGuid in meta.dependencies)
            {
                string src = AssetDatabase.GUIDToAssetPath(dGuid);
                if (string.IsNullOrEmpty(src))
                {
                    continue;
                }

                string name = Path.GetFileName(src);
                string dst = Path.Combine(depsDir, name);
                string dext = Path.GetExtension(src).ToLowerInvariant();
                if (FileExtensions.IsNotAllowedFileExtension(dext))
                {
                    continue; // don't include shaders and scripts in the package
                }
                if (File.Exists(src))
                {
                    File.Copy(src, dst, overwrite: true);
                    string srcMeta = src + FileExtensions.META;
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, dst + FileExtensions.META, overwrite: true);
                    }
                }
            }

            // Images are not in project necessarily; skip here (handled by Submit window which copies them first)
            // Write meta.json to the folder for debugging
            // Use dot prefix to hide from Unity Project window
            string jsonPath = Path.Combine(destinationAbsoluteFolder, "." + ModelMeta.MODEL_JSON);
            File.WriteAllText(jsonPath, JsonUtility.ToJson(meta, true));

            return await Task.FromResult(destinationAbsoluteFolder);
        }
    }
}
