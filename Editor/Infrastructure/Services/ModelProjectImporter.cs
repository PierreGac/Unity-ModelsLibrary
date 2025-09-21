#if UNITY_EDITOR
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEditor;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Copies a cached model version into the project under Assets/Models/<ModelName>/
    /// Flattens payload and dependencies into the named folder, places images under an images/ subfolder.
    /// </summary>
    public static class ModelProjectImporter
    {
        public static async Task<string> ImportFromCacheAsync(string cacheVersionRoot, ModelMeta meta, bool cleanDestination = true, string overrideInstallPath = null)
        {
            // Determine destination folder
            string destRel;
            if (!string.IsNullOrEmpty(overrideInstallPath))
            {
                destRel = overrideInstallPath;
            }
            else if (!string.IsNullOrEmpty(meta.relativePath))
            {
                destRel = $"Assets/{meta.relativePath}";
            }
            else
            {
                string safeName = meta.identity.Name;
                destRel = $"Assets/Models/{safeName}";
            }
            string destAbs = Path.GetFullPath(destRel);

            if (cleanDestination && Directory.Exists(destAbs))
            {
                TryCleanDirectory(destAbs);
            }
            Directory.CreateDirectory(destAbs);

            // Copy payload files into root of model folder (flatten), and images under images/
            string payloadRoot = Path.Combine(cacheVersionRoot, "payload");
            string depsRoot = Path.Combine(payloadRoot, "deps");

            // Copy top-level payload files directly into destAbs (skip shaders). Copy .meta alongside when present
            if (Directory.Exists(payloadRoot))
            {
                foreach (string file in Directory.GetFiles(payloadRoot, "*", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    string target = Path.Combine(destAbs, fileName);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".meta") { continue; }
                    if (ext == ".shader" || ext == ".shadervariants" || ext == ".shadergraph" || ext == ".shadersubgraph" || ext == ".cginc" || ext == ".hlsl" || ext == ".cs")
                    {
                        continue;
                    }
                    File.Copy(file, target, overwrite: true);
                    string srcMeta = file + ".meta";
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, target + ".meta", overwrite: true);
                    }
                }
            }

            // Copy dependency files (any depth) directly into destAbs (flatten) and skip shaders. Copy .meta alongside when present
            if (Directory.Exists(depsRoot))
            {
                foreach (string file in Directory.GetFiles(depsRoot, "*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    string target = Path.Combine(destAbs, fileName);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".meta") { continue; }
                    if (ext == ".shader" || ext == ".shadervariants" || ext == ".shadergraph" || ext == ".shadersubgraph" || ext == ".cginc" || ext == ".hlsl" || ext == ".cs")
                    {
                        continue;
                    }
                    if(string.Equals(fileName, "auto_preview.png"))
                    {
                        continue;
                    }
                    File.Copy(file, target, overwrite: true);
                    string srcMeta = file + ".meta";
                    if (File.Exists(srcMeta))
                    {
                        File.Copy(srcMeta, target + ".meta", overwrite: true);
                    }
                }
            }

            // Persist manifest for local version tracking
            string manifestPath = Path.Combine(destAbs, "modelLibrary.meta.json");
            File.WriteAllText(manifestPath, JsonUtil.ToJson(meta));

            // Refresh to register new files
            AssetDatabase.Refresh();

            // Restore per-file model importer settings captured in meta (if present)
            foreach (string file in Directory.GetFiles(destAbs, "*", SearchOption.TopDirectoryOnly))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".fbx" || ext == ".obj")
                {
                    string fileName = Path.GetFileName(file);
                    string payloadRel = $"payload/{fileName}";
                    bool hasMeta = File.Exists(file + ".meta");
                    if (!hasMeta && meta.modelImporters != null && meta.modelImporters.TryGetValue(payloadRel, out ModelImporterSettings settings) && settings != null)
                    {
                        string assetPath = PathUtils.SanitizePathSeparator(Path.Combine(destRel, fileName));
                        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                        if (importer != null)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(settings.materialImportMode) && System.Enum.TryParse(settings.materialImportMode, out ModelImporterMaterialImportMode mim))
                                {
                                    importer.materialImportMode = mim;
                                }
                                if (!string.IsNullOrEmpty(settings.materialSearch) && System.Enum.TryParse(settings.materialSearch, out ModelImporterMaterialSearch ms))
                                {
                                    importer.materialSearch = ms;
                                }
                                if (!string.IsNullOrEmpty(settings.materialName) && System.Enum.TryParse(settings.materialName, out ModelImporterMaterialName mn))
                                {
                                    importer.materialName = mn;
                                }
                                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                            }
                            catch { /* keep default if API mismatch */ }
                        }
                    }
                }
            }

            return await Task.FromResult(destRel);
        }

        private static void TryCleanDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                try
                {
                    foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }

                    foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                    {
                        Directory.Delete(dir, true);
                    }

                    Directory.Delete(path, true);
                }
                catch
                {
                    // give up, will overwrite existing files during copy
                }
            }
        }

        private static void CopyDir(string srcDir, string dstDir)
        {
            if (!Directory.Exists(srcDir))
            {
                return;
            }

            Directory.CreateDirectory(dstDir);
            foreach (string file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                string rel = file[srcDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(dstDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, overwrite: true);
            }
        }
    }
}
#endif


