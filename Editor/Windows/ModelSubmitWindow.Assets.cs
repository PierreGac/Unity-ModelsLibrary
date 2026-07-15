using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing selected asset list management for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Populates the submission asset list from the current Project window selection.
        /// </summary>
        /// <param name="assetGuids">
        /// Optional GUIDs captured at entry time. When null or empty, uses <see cref="Selection.assetGUIDs"/>.
        /// </param>
        private void InitializeSelectedAssetsFromProjectSelection(string[] assetGuids = null)
        {
            string[] guidsToUse = assetGuids;
            if (guidsToUse == null || guidsToUse.Length == 0)
            {
                guidsToUse = Selection.assetGUIDs;
            }

            if (guidsToUse == null || guidsToUse.Length == 0)
            {
                return;
            }

            for (int i = 0; i < guidsToUse.Length; i++)
            {
                TryAddAssetGuid(guidsToUse[i], saveDraft: false);
            }
        }

        /// <summary>
        /// Adds all valid assets from the current Project window selection to the submission list.
        /// </summary>
        private void AddAssetsFromProjectSelection()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Select one or more assets or folders in the Project window first.", "OK");
                return;
            }

            int addedCount = 0;
            for (int i = 0; i < Selection.assetGUIDs.Length; i++)
            {
                if (TryAddAssetGuid(Selection.assetGUIDs[i], saveDraft: false))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                SaveDraft();
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "No Assets Added",
                    "The current selection does not contain any supported model assets (FBX, OBJ, materials, textures, or folders).",
                    "OK");
            }
        }

        /// <summary>
        /// Attempts to add a Unity object from the object picker to the submission asset list.
        /// </summary>
        /// <param name="assetObject">Project asset or folder to add.</param>
        /// <returns>True when the asset was added.</returns>
        private bool TryAddUnityObject(UnityEngine.Object assetObject)
        {
            if (assetObject == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(assetObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Only assets from this Unity project can be added.", "OK");
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            return TryAddAssetGuid(guid, saveDraft: true);
        }

        /// <summary>
        /// Attempts to add an asset GUID to the submission list.
        /// </summary>
        /// <param name="guid">Asset GUID to add.</param>
        /// <param name="saveDraft">When true, persists the updated list as a draft.</param>
        /// <returns>True when the asset was added.</returns>
        private bool TryAddAssetGuid(string guid, bool saveDraft)
        {
            if (string.IsNullOrEmpty(guid) || _selectedAssetGuidLookup.Contains(guid))
            {
                return false;
            }

            if (!CanAddAssetGuid(guid))
            {
                return false;
            }

            _selectedAssetGuids.Add(guid);
            _selectedAssetGuidLookup.Add(guid);

            if (saveDraft)
            {
                SaveDraft();
            }

            return true;
        }

        /// <summary>
        /// Adds Unity-referenced dependencies for every mesh asset already in the submission list.
        /// </summary>
        private void AddDependenciesForAllMeshAssets()
        {
            List<string> meshGuids = new List<string>();
            for (int i = 0; i < _selectedAssetGuids.Count; i++)
            {
                string guid = _selectedAssetGuids[i];
                string meshPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDependencyResolver.IsMeshAssetPath(meshPath))
                {
                    meshGuids.Add(guid);
                }
            }

            for (int i = 0; i < meshGuids.Count; i++)
            {
                AddDependenciesForMeshAsset(meshGuids[i]);
            }
        }

        /// <summary>
        /// Adds Unity-referenced dependencies for a mesh asset to the submission list.
        /// </summary>
        /// <param name="meshGuid">GUID of the FBX or OBJ mesh asset.</param>
        /// <returns>Number of assets added to the list.</returns>
        private int AddDependenciesForMeshAsset(string meshGuid)
        {
            string meshPath = AssetDatabase.GUIDToAssetPath(meshGuid);
            if (!AssetDependencyResolver.IsMeshAssetPath(meshPath))
            {
                return 0;
            }

            List<string> dependencyGuids = AssetDependencyResolver.CollectReferencedGuids(
                new[] { meshGuid },
                excludeGuids: _selectedAssetGuidLookup);

            int addedCount = 0;
            for (int i = 0; i < dependencyGuids.Count; i++)
            {
                if (TryAddAssetGuid(dependencyGuids[i], saveDraft: false))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                SaveDraft();
            }

            return addedCount;
        }

        /// <summary>
        /// Removes an asset from the submission list at the given index.
        /// </summary>
        /// <param name="index">Index of the asset to remove.</param>
        private void RemoveSelectedAssetAt(int index)
        {
            if (index < 0 || index >= _selectedAssetGuids.Count)
            {
                return;
            }

            string guid = _selectedAssetGuids[index];
            _selectedAssetGuids.RemoveAt(index);
            _selectedAssetGuidLookup.Remove(guid);
            SaveDraft();
        }

        /// <summary>
        /// Replaces the submission asset list with the provided GUIDs.
        /// </summary>
        /// <param name="assetGuids">Asset GUIDs to restore.</param>
        private void RestoreSelectedAssetGuids(List<string> assetGuids)
        {
            _selectedAssetGuids.Clear();
            _selectedAssetGuidLookup.Clear();

            if (assetGuids == null)
            {
                return;
            }

            for (int i = 0; i < assetGuids.Count; i++)
            {
                TryAddAssetGuid(assetGuids[i], saveDraft: false);
            }
        }

        /// <summary>
        /// Returns whether the asset GUID points to a supported submission asset or folder.
        /// </summary>
        /// <param name="guid">Asset GUID to inspect.</param>
        /// <returns>True when the asset can be added to the submission list.</returns>
        private static bool CanAddAssetGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return true;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            return FileExtensions.IsAcceptablePayloadExtension(extension);
        }

        /// <summary>
        /// Returns whether the submission asset list contains at least one mesh file.
        /// </summary>
        /// <returns>True when an FBX or OBJ asset is included directly or inside a folder.</returns>
        private bool SelectedAssetsContainMesh()
        {
            for (int i = 0; i < _selectedAssetGuids.Count; i++)
            {
                if (AssetGuidContainsMesh(_selectedAssetGuids[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the given asset GUID references a mesh file directly or within a folder.
        /// </summary>
        /// <param name="guid">Asset GUID to inspect.</param>
        /// <returns>True when a mesh file is found.</returns>
        private static bool AssetGuidContainsMesh(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                string[] folderGuids = AssetDatabase.FindAssets(string.Empty, new[] { path });
                for (int i = 0; i < folderGuids.Length; i++)
                {
                    string folderAssetPath = AssetDatabase.GUIDToAssetPath(folderGuids[i]);
                    if (AssetDependencyResolver.IsMeshAssetPath(folderAssetPath))
                    {
                        return true;
                    }
                }

                return false;
            }

            return AssetDependencyResolver.IsMeshAssetPath(path);
        }

        /// <summary>
        /// Pings an asset in the Project window and selects it.
        /// </summary>
        /// <param name="asset">Project asset to highlight.</param>
        private static void PingAssetInProject(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        /// <summary>
        /// Handles click-to-ping interaction for an asset row content area.
        /// </summary>
        /// <param name="clickRect">Rect covering the clickable asset info region.</param>
        /// <param name="asset">Project asset to ping when clicked.</param>
        private static void TryHandleAssetRowClick(Rect clickRect, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                EditorGUIUtility.AddCursorRect(clickRect, MouseCursor.Link);
                return;
            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {
                return;
            }

            if (!clickRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            PingAssetInProject(asset);
            currentEvent.Use();
        }

        /// <summary>
        /// Returns the cached map of dependency asset GUIDs to referencing mesh display names.
        /// </summary>
        /// <returns>Dependency source map for the current asset selection.</returns>
        private IReadOnlyDictionary<string, List<string>> GetAssetDependencySourceNames()
        {
            string cacheKey = BuildSelectedAssetGuidsCacheKey();
            if (!string.Equals(cacheKey, _assetDependencyMapCacheKey, StringComparison.Ordinal))
            {
                _cachedDependencySourceNames = AssetDependencyResolver.BuildDependencySourceNamesByGuid(_selectedAssetGuids);
                _assetDependencyMapCacheKey = cacheKey;
            }

            return _cachedDependencySourceNames;
        }

        /// <summary>
        /// Returns a dependency label for an asset when it is referenced by meshes in the list.
        /// </summary>
        /// <param name="assetGuid">Asset GUID to describe.</param>
        /// <param name="dependencySources">Map of dependency GUIDs to referencing mesh names.</param>
        /// <returns>Formatted dependency label, or empty when the asset is not a listed dependency.</returns>
        private static string GetAssetDependencyLabel(
            string assetGuid,
            IReadOnlyDictionary<string, List<string>> dependencySources)
        {
            if (string.IsNullOrEmpty(assetGuid)
                || dependencySources == null
                || !dependencySources.TryGetValue(assetGuid, out List<string> sourceMeshNames))
            {
                return string.Empty;
            }

            return AssetDependencyResolver.FormatDependencySourceLabel(sourceMeshNames);
        }

        /// <summary>
        /// Builds a stable cache key from the selected submission asset GUIDs.
        /// </summary>
        /// <returns>Cache key string.</returns>
        private string BuildSelectedAssetGuidsCacheKey()
        {
            if (_selectedAssetGuids.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", _selectedAssetGuids);
        }

        /// <summary>
        /// Returns a short display label describing the asset type.
        /// </summary>
        /// <param name="guid">Asset GUID to describe.</param>
        /// <returns>Human-readable asset type label.</returns>
        private static string GetAssetTypeLabel(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return "Missing";
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return "Folder";
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == FileExtensions.FBX || extension == FileExtensions.OBJ)
            {
                return "Mesh";
            }

            if (extension == FileExtensions.MAT)
            {
                return "Material";
            }

            if (extension == FileExtensions.PNG || extension == FileExtensions.JPG ||
                extension == FileExtensions.JPEG || extension == FileExtensions.TGA ||
                extension == FileExtensions.PSD)
            {
                return "Texture";
            }

            return "Asset";
        }
    }
}
