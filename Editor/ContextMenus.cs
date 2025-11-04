using System.IO;
using System.Linq;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor
{
    /// <summary>
    /// Context menu items for the Unity Editor Project view.
    /// Provides right-click functionality to submit models directly from selected assets.
    /// </summary>
    public static class ContextMenus
    {
        /// <summary>
        /// Right-click context menu item to submit selected model assets.
        /// This menu item appears in the Project view when right-clicking on assets.
        /// </summary>
        [MenuItem("Assets/Model Library/Submit Model", false, 1000)]
        public static void SubmitModelFromSelection()
        {
            // Only allow Artists to submit models
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                EditorUtility.DisplayDialog("Access Denied", 
                    "Model submission is only available for Artists. Please switch to Artist role in User Settings.", 
                    "OK");
                return;
            }

            // Open the ModelSubmitWindow - it will automatically use the current selection
            ModelSubmitWindow.Open();
        }

        /// <summary>
        /// Validation method for the submit model menu item.
        /// The menu item will only be enabled if valid model assets are selected.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if at least one valid model asset is selected;
        /// otherwise <see langword="false"/>.
        /// </returns>
        [MenuItem("Assets/Model Library/Submit Model", true, 1000)]
        public static bool ValidateSubmitModelFromSelection()
        {
            // Only allow Artists to submit models
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            if (identityProvider.GetUserRole() != UserRole.Artist)
            {
                return false;
            }

            // Check if any assets are selected
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                return false;
            }

            // Check if at least one selected asset is a valid model file type
            string[] validExtensions = {
                FileExtensions.FBX,
                FileExtensions.OBJ,
                FileExtensions.PNG,
                FileExtensions.TGA,
                FileExtensions.JPG,
                FileExtensions.JPEG,
                FileExtensions.PSD,
                FileExtensions.MAT
            };

            foreach (string guid in Selection.assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                // Skip directories/folders
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (validExtensions.Contains(extension))
                {
                    return true; // Found at least one valid model asset
                }
            }

            return false; // No valid model assets found
        }
    }
}

