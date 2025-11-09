using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing image handling methods for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Draws a drag-and-drop area for image files.
        /// </summary>
        private void DrawImageDropArea()
        {
            Event currentEvent = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));

            bool isDragging = currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform;
            bool isHovering = dropArea.Contains(currentEvent.mousePosition);

            // Draw drop area background
            Color originalColor = GUI.color;
            if (isDragging && isHovering)
            {
                GUI.color = new Color(0.5f, 0.8f, 1f, 0.3f); // Light blue tint
            }
            GUI.Box(dropArea, "", EditorStyles.helpBox);
            GUI.color = originalColor;

            // Draw drop area content
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                string dropText = isDragging && isHovering ? "Drop images here" : "Drag and drop image files here";
                EditorGUILayout.LabelField(dropText, EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }

            // Handle drag and drop events
            if (isHovering)
            {
                if (currentEvent.type == EventType.DragUpdated)
                {
                    // Check if any dragged files are valid images
                    bool hasValidImages = false;
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (IsValidImageFile(path))
                            {
                                hasValidImages = true;
                                break;
                            }
                        }
                    }
                    else if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D)
                            {
                                hasValidImages = true;
                                break;
                            }
                        }
                    }

                    if (hasValidImages)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        currentEvent.Use();
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }
                }
                else if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    currentEvent.Use();

                    // Process dragged files
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (IsValidImageFile(path))
                            {
                                AddImageFile(path);
                            }
                        }
                    }
                    else if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D texture)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(texture);
                                if (!string.IsNullOrEmpty(assetPath))
                                {
                                    string fullPath = Path.GetFullPath(assetPath);
                                    AddImageFile(fullPath);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds an image file to the list if valid and not already present.
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        private void AddImageFile(string filePath)
        {
            if (IsValidImageFile(filePath))
            {
                if (!_imageAbsPaths.Contains(filePath))
                {
                    _imageAbsPaths.Add(filePath);
                    SaveDraft(); // Auto-save draft when images are added
                }
                else
                {
                    Debug.LogWarning($"Image '{Path.GetFileName(filePath)}' is already selected.");
                }
            }
            else
            {
                Debug.LogError($"Invalid image file: {Path.GetFileName(filePath)}");
            }
        }

        /// <summary>
        /// Loads a preview thumbnail for an image file.
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <returns>Texture2D preview or null if loading fails</returns>
        private Texture2D LoadImagePreview(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }

                // Try to load as Unity asset first
                string fullDataPath = Path.GetFullPath(Application.dataPath);
                if (filePath.StartsWith(fullDataPath))
                {
                    string assetPath = "Assets" + filePath.Substring(fullDataPath.Length).Replace('\\', '/');
                    Texture2D assetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (assetTexture != null)
                    {
                        return assetTexture;
                    }
                }

                // Fallback: load from file system (only for small previews)
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 10 * 1024 * 1024) // Only load if less than 10MB
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(fileData))
                    {
                        return texture;
                    }
                }
            }
            catch
            {
                // Ignore errors loading preview
            }

            return null;
        }

        /// <summary>
        /// Validates if the selected file is a valid image file.
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if the file is a valid image, false otherwise</returns>
        private static bool IsValidImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            // Check file extension
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };
            if (!validExtensions.Contains(extension))
            {
                return false;
            }

            // Check file size (max 50MB)
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > __MAX_IMAGE_FILE_SIZE_BYTES)
            {
                Debug.LogWarning($"Image file '{Path.GetFileName(filePath)}' is too large ({GetFileSizeString(filePath)}). Maximum size is 50MB.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a human-readable file size string.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Formatted file size string</returns>
        private static string GetFileSizeString(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return "Unknown";
            }

            long bytes = new FileInfo(filePath).Length;
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= __BYTES_PER_KILOBYTE && order < sizes.Length - 1)
            {
                order++;
                len /= __BYTES_PER_KILOBYTE;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}


