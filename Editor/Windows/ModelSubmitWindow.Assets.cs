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
        private void InitializeSelectedAssetsFromProjectSelection()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < Selection.assetGUIDs.Length; i++)
            {
                TryAddAssetGuid(Selection.assetGUIDs[i], saveDraft: false);
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
                    if (HasMeshExtension(folderAssetPath))
                    {
                        return true;
                    }
                }

                return false;
            }

            return HasMeshExtension(path);
        }

        /// <summary>
        /// Returns whether the asset path has a mesh file extension.
        /// </summary>
        /// <param name="assetPath">Unity asset path.</param>
        /// <returns>True for FBX or OBJ files.</returns>
        private static bool HasMeshExtension(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            return extension == FileExtensions.FBX || extension == FileExtensions.OBJ;
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
