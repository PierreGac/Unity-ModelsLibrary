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
        /// Build the ModelMeta from the current selection, including payload file list, GUIDs and dependency GUIDs.
        /// </summary>
        public static async Task<ModelMeta> BuildMetaFromSelectionAsync(string identityName, string identityId, string version, string description, IEnumerable<string> imagePaths, IEnumerable<string> tags, IEnumerable<string> projectTags, string installPath, string relativePath, IUserIdentityProvider idProvider)
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
                projectTags = NormalizeProjectTags(projectTags),
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
                    string typeName = t?.Name;
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

                        string typeName = depType?.Name;
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
            meta.imageRelativePaths = imagePaths?.Select(p => $"images/{Path.GetFileName(p)}").ToList() ?? new List<string>();

            // Add tags
            meta.tags.values = tags?.ToList() ?? new List<string>();
            meta.vertexCount = totalVertices;
            meta.triangleCount = totalTriangles;

            return await Task.FromResult(meta);
        }

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

        private static string SelectPrimaryAssetGuid(ModelMeta meta)
        {
            if (meta?.assetGuids == null)
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
        }        private static List<string> NormalizeProjectTags(IEnumerable<string> projectTags)
        {
            if (projectTags == null)
            {
                return new List<string>();
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in projectTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }
                unique.Add(tag.Trim());
            }
            return new List<string>(unique);
        }

        private static string ResolveInstallPath(string installPath, string identityName)
        {
            string sanitizedName = SanitizeFolderName(identityName);
            string defaultPath = $"Assets/Models/{sanitizedName}";
            string path = string.IsNullOrWhiteSpace(installPath) ? defaultPath : PathUtils.SanitizePathSeparator(installPath);
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                path = $"Assets/{path.TrimStart('/')}";
            }
            return PathUtils.SanitizePathSeparator(path);
        }

        private static string ResolveRelativePath(string relativePath, string identityName)
        {
            string sanitizedName = SanitizeFolderName(identityName);
            string defaultPath = $"Models/{sanitizedName}";
            string path = string.IsNullOrWhiteSpace(relativePath) ? defaultPath : PathUtils.SanitizePathSeparator(relativePath);
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                path = path[7..]; // Remove "Assets/" prefix
            }
            return PathUtils.SanitizePathSeparator(path);
        }

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
        /// Create a local version folder structure (payload/images) and copy selected assets there.
        /// Returns the destination path.
        /// </summary>
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
            File.WriteAllText(Path.Combine(destinationAbsoluteFolder, ModelMeta.MODEL_JSON), JsonUtility.ToJson(meta, true));
            return await Task.FromResult(destinationAbsoluteFolder);
        }
    }
}
