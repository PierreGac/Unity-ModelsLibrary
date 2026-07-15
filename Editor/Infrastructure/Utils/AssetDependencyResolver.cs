using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Resolves Unity asset dependency graphs for model payloads.
    /// Single source of truth for <see cref="AssetDatabase.GetDependencies"/> usage
    /// when collecting materials, textures, and related assets for model submission.
    /// </summary>
    public static class AssetDependencyResolver
    {
        /// <summary>
        /// Callback invoked when a dependency mesh path is encountered during meta enrichment.
        /// </summary>
        /// <param name="assetPath">Unity asset path of the dependency mesh.</param>
        public delegate void MeshStatsAccumulator(string assetPath);

        /// <summary>
        /// Returns whether the asset path refers to an FBX or OBJ mesh file.
        /// </summary>
        /// <param name="assetPath">Unity asset path to inspect.</param>
        /// <returns>True for FBX or OBJ files; false for folders, empty paths, and other types.</returns>
        public static bool IsMeshAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            return extension == FileExtensions.FBX || extension == FileExtensions.OBJ;
        }

        /// <summary>
        /// Returns whether a dependency path should be included in model dependency resolution.
        /// Pure path-level filter suitable for unit testing without AssetDatabase.
        /// </summary>
        /// <param name="dependencyPath">Candidate dependency asset path.</param>
        /// <param name="sourcePath">Source asset path whose dependencies are being collected.</param>
        /// <returns>True when the dependency path is eligible for inclusion.</returns>
        public static bool IsEligibleDependencyPath(string dependencyPath, string sourcePath)
        {
            if (string.IsNullOrEmpty(dependencyPath))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(sourcePath)
                && string.Equals(dependencyPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string extension = Path.GetExtension(dependencyPath).ToLowerInvariant();
            return !FileExtensions.IsNotAllowedFileExtension(extension);
        }

        /// <summary>
        /// Collects GUIDs referenced by the given source assets via Unity's dependency graph.
        /// </summary>
        /// <param name="sourceGuids">Source asset GUIDs to resolve dependencies for.</param>
        /// <param name="excludeGuids">GUIDs to omit from the result (e.g. already-selected assets).</param>
        /// <returns>Deduplicated list of dependency GUIDs.</returns>
        public static List<string> CollectReferencedGuids(
            IEnumerable<string> sourceGuids,
            IReadOnlyCollection<string> excludeGuids = null)
        {
            HashSet<string> excludedLookup = BuildExcludedGuidLookup(excludeGuids);
            HashSet<string> dependencyGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (sourceGuids == null)
            {
                return new List<string>();
            }

            foreach (string guid in sourceGuids)
            {
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, recursive: true);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependencyPath = dependencies[i];
                    if (!IsEligibleDependencyPath(dependencyPath, assetPath))
                    {
                        continue;
                    }

                    string dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                    if (string.IsNullOrEmpty(dependencyGuid))
                    {
                        continue;
                    }

                    if (string.Equals(dependencyGuid, guid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (excludedLookup.Contains(dependencyGuid))
                    {
                        continue;
                    }

                    dependencyGuids.Add(dependencyGuid);
                }
            }

            return new List<string>(dependencyGuids);
        }

        /// <summary>
        /// Prefix for dependency source labels shown in the submit asset list.
        /// </summary>
        private const string DEPENDENCY_SOURCE_LABEL_PREFIX = "Dependency of ";

        /// <summary>
        /// Builds a map from selected asset GUIDs to the display names of mesh assets in the
        /// same selection that reference them via Unity's dependency graph.
        /// </summary>
        /// <param name="selectedAssetGuids">GUIDs currently included in the submission asset list.</param>
        /// <returns>
        /// Map of dependency asset GUID to sorted mesh display names that reference each asset.
        /// Meshes themselves are not included as keys.
        /// </returns>
        public static Dictionary<string, List<string>> BuildDependencySourceNamesByGuid(
            IReadOnlyList<string> selectedAssetGuids)
        {
            Dictionary<string, List<string>> dependencySources =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (selectedAssetGuids == null || selectedAssetGuids.Count == 0)
            {
                return dependencySources;
            }

            HashSet<string> selectedLookup = BuildExcludedGuidLookup(selectedAssetGuids);
            List<string> meshGuids = new List<string>();
            for (int i = 0; i < selectedAssetGuids.Count; i++)
            {
                string selectedGuid = selectedAssetGuids[i];
                string selectedPath = AssetDatabase.GUIDToAssetPath(selectedGuid);
                if (IsMeshAssetPath(selectedPath))
                {
                    meshGuids.Add(selectedGuid);
                }
            }

            for (int i = 0; i < meshGuids.Count; i++)
            {
                string meshGuid = meshGuids[i];
                string meshPath = AssetDatabase.GUIDToAssetPath(meshGuid);
                string meshDisplayName = GetMeshDisplayName(meshPath);
                if (string.IsNullOrEmpty(meshDisplayName))
                {
                    continue;
                }

                List<string> dependencyGuids = CollectReferencedGuids(new[] { meshGuid }, excludeGuids: null);
                for (int j = 0; j < dependencyGuids.Count; j++)
                {
                    string dependencyGuid = dependencyGuids[j];
                    if (!selectedLookup.Contains(dependencyGuid))
                    {
                        continue;
                    }

                    if (string.Equals(dependencyGuid, meshGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!dependencySources.TryGetValue(dependencyGuid, out List<string> sourceMeshNames))
                    {
                        sourceMeshNames = new List<string>();
                        dependencySources[dependencyGuid] = sourceMeshNames;
                    }

                    bool alreadyListed = false;
                    for (int k = 0; k < sourceMeshNames.Count; k++)
                    {
                        if (string.Equals(sourceMeshNames[k], meshDisplayName, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyListed = true;
                            break;
                        }
                    }

                    if (!alreadyListed)
                    {
                        sourceMeshNames.Add(meshDisplayName);
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in dependencySources)
            {
                entry.Value.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return dependencySources;
        }

        /// <summary>
        /// Formats a human-readable label naming the mesh assets that reference a dependency.
        /// </summary>
        /// <param name="sourceMeshNames">Display names of referencing mesh assets.</param>
        /// <returns>Formatted label, or empty when no source names are provided.</returns>
        public static string FormatDependencySourceLabel(IReadOnlyList<string> sourceMeshNames)
        {
            if (sourceMeshNames == null || sourceMeshNames.Count == 0)
            {
                return string.Empty;
            }

            return DEPENDENCY_SOURCE_LABEL_PREFIX + string.Join(", ", sourceMeshNames);
        }

        /// <summary>
        /// Returns the display name for a mesh asset path (filename without extension).
        /// </summary>
        /// <param name="meshAssetPath">Unity asset path of the mesh.</param>
        /// <returns>Mesh display name, or empty when the path is invalid.</returns>
        public static string GetMeshDisplayName(string meshAssetPath)
        {
            if (string.IsNullOrEmpty(meshAssetPath))
            {
                return string.Empty;
            }

            return Path.GetFileNameWithoutExtension(meshAssetPath);
        }

        /// <summary>
        /// Populates model metadata with resolved dependency GUIDs and typed asset references.
        /// </summary>
        /// <param name="meta">Model metadata to enrich.</param>
        /// <param name="dependencyGuids">Dependency GUIDs to add when not already in <see cref="ModelMeta.assetGuids"/>.</param>
        /// <param name="accumulateMeshStats">Optional callback for mesh statistics on dependency meshes.</param>
        public static void EnrichModelMetaWithDependencies(
            ModelMeta meta,
            IEnumerable<string> dependencyGuids,
            MeshStatsAccumulator accumulateMeshStats = null)
        {
            if (meta == null || dependencyGuids == null)
            {
                return;
            }

            if (meta.assetGuids == null)
            {
                meta.assetGuids = new List<string>();
            }

            if (meta.dependencies == null)
            {
                meta.dependencies = new List<string>();
            }

            if (meta.dependenciesDetailed == null)
            {
                meta.dependenciesDetailed = new List<DependencyRef>();
            }

            if (meta.materials == null)
            {
                meta.materials = new List<AssetRef>();
            }

            if (meta.textures == null)
            {
                meta.textures = new List<AssetRef>();
            }

            foreach (string dependencyGuid in dependencyGuids)
            {
                if (string.IsNullOrEmpty(dependencyGuid) || meta.assetGuids.Contains(dependencyGuid))
                {
                    continue;
                }

                meta.dependencies.Add(dependencyGuid);

                string dependencyPath = AssetDatabase.GUIDToAssetPath(dependencyGuid);
                Type dependencyType = string.IsNullOrEmpty(dependencyPath)
                    ? null
                    : AssetDatabase.GetMainAssetTypeAtPath(dependencyPath);

                meta.dependenciesDetailed.Add(new DependencyRef
                {
                    guid = dependencyGuid,
                    type = dependencyType != null ? dependencyType.Name : string.Empty,
                    name = string.IsNullOrEmpty(dependencyPath)
                        ? string.Empty
                        : Path.GetFileNameWithoutExtension(dependencyPath)
                });

                if (string.IsNullOrEmpty(dependencyPath))
                {
                    continue;
                }

                string dependencyExtension = Path.GetExtension(dependencyPath).ToLowerInvariant();
                if (dependencyExtension == FileExtensions.FBX || dependencyExtension == FileExtensions.OBJ)
                {
                    if (accumulateMeshStats != null)
                    {
                        accumulateMeshStats(dependencyPath);
                    }
                }

                string typeName = dependencyType != null ? dependencyType.Name : null;
                if (typeName == nameof(Material))
                {
                    meta.materials.Add(new AssetRef
                    {
                        guid = dependencyGuid,
                        name = Path.GetFileNameWithoutExtension(dependencyPath),
                        relativePath = null,
                        type = typeName
                    });
                }
                else if (typeName == nameof(Texture2D))
                {
                    meta.textures.Add(new AssetRef
                    {
                        guid = dependencyGuid,
                        name = Path.GetFileNameWithoutExtension(dependencyPath),
                        relativePath = null,
                        type = typeName
                    });
                }
            }
        }

        /// <summary>
        /// Builds a case-insensitive lookup set from excluded GUIDs.
        /// </summary>
        /// <param name="excludeGuids">GUIDs to exclude from dependency collection.</param>
        /// <returns>Lookup set, or empty set when none provided.</returns>
        private static HashSet<string> BuildExcludedGuidLookup(IReadOnlyCollection<string> excludeGuids)
        {
            HashSet<string> excludedLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excludeGuids == null)
            {
                return excludedLookup;
            }

            foreach (string excludedGuid in excludeGuids)
            {
                if (!string.IsNullOrEmpty(excludedGuid))
                {
                    excludedLookup.Add(excludedGuid);
                }
            }

            return excludedLookup;
        }
    }
}
