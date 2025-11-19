using System;
using System.IO;
using System.Linq;
using ModelLibrary.Editor.Utils;
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
                    int addedCount = 0;
                    int skippedCount = 0;
                    
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (IsValidImageFile(path))
                            {
                                int countBefore = _imageAbsPaths.Count;
                                AddImageFile(path);
                                if (_imageAbsPaths.Count > countBefore)
                                {
                                    addedCount++;
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                    }
                    
                    // Provide user feedback
                    if (addedCount > 0)
                    {
                        Debug.Log($"[ModelSubmitWindow] Added {addedCount} image{(addedCount == 1 ? string.Empty : "s")} via drag-and-drop");
                    }
                    if (skippedCount > 0 && addedCount == 0)
                    {
                        EditorUtility.DisplayDialog("No Images Added",
                            $"None of the {skippedCount} dropped file{(skippedCount == 1 ? string.Empty : "s")} could be added.\n\n" +
                            "Please ensure files are valid image formats (PNG, JPG, JPEG, TGA, PSD) and exist.",
                            "OK");
                    }
                    else if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        int textureAddedCount = 0;
                        int textureSkippedCount = 0;
                        
                        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D texture)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(texture);
                                if (!string.IsNullOrEmpty(assetPath))
                                {
                                    try
                                    {
                                        // Convert Unity asset path to full file system path
                                        string fullDataPath = Path.GetFullPath(Application.dataPath);
                                        string fullPath;
                                        
                                        if (assetPath.StartsWith("Assets/"))
                                        {
                                            // Remove "Assets/" prefix and combine with project root
                                            string relativePath = assetPath.Substring(7);
                                            fullPath = Path.GetFullPath(Path.Combine(fullDataPath, "..", relativePath));
                                        }
                                        else
                                        {
                                            // Fallback: try direct conversion
                                            fullPath = Path.GetFullPath(assetPath);
                                        }
                                        
                                        int countBefore = _imageAbsPaths.Count;
                                        AddImageFile(fullPath);
                                        if (_imageAbsPaths.Count > countBefore)
                                        {
                                            textureAddedCount++;
                                        }
                                        else
                                        {
                                            textureSkippedCount++;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        ErrorLogger.LogError("Convert Asset Path Failed",
                                            $"Failed to convert asset path '{assetPath}' to full path: {ex.Message}",
                                            ErrorHandler.CategorizeException(ex), ex, $"AssetPath: {assetPath}");
                                        textureSkippedCount++;
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[ModelSubmitWindow] Texture2D '{texture.name}' has no asset path");
                                    textureSkippedCount++;
                                }
                            }
                        }
                        
                        // Provide user feedback for texture drag-and-drop
                        if (textureAddedCount > 0)
                        {
                            Debug.Log($"[ModelSubmitWindow] Added {textureAddedCount} texture{(textureAddedCount == 1 ? string.Empty : "s")} via drag-and-drop");
                        }
                        if (textureSkippedCount > 0 && textureAddedCount == 0)
                        {
                            EditorUtility.DisplayDialog("No Images Added",
                                $"None of the {textureSkippedCount} dropped texture{(textureSkippedCount == 1 ? string.Empty : "s")} could be added.\n\n" +
                                "Please ensure textures are valid image formats.",
                                "OK");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds an image file to the list if valid and not already present.
        /// Normalizes the path to ensure consistent comparison and handles both relative and absolute paths.
        /// </summary>
        /// <param name="filePath">Path to the image file (can be relative or absolute)</param>
        private void AddImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[ModelSubmitWindow] Cannot add image: file path is empty");
                return;
            }

            // Normalize path - convert to absolute path for consistent comparison
            string normalizedPath = filePath;
            try
            {
                // If path is relative, make it absolute
                if (!Path.IsPathRooted(filePath))
                {
                    // Try to resolve relative to project root
                    string projectRoot = Path.GetFullPath(Application.dataPath + "/..");
                    normalizedPath = Path.GetFullPath(Path.Combine(projectRoot, filePath));
                }
                else
                {
                    normalizedPath = Path.GetFullPath(filePath);
                }

                // Normalize path separators
                normalizedPath = normalizedPath.Replace('\\', '/');
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Normalize Image Path Failed",
                    $"Failed to normalize image path '{filePath}': {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex, $"FilePath: {filePath}");
                return;
            }

            // Validate the normalized path
            if (!IsValidImageFile(normalizedPath))
            {
                string fileName = Path.GetFileName(filePath);
                ErrorLogger.LogError("Invalid Image File",
                    $"Cannot add image '{fileName}': File does not exist or is not a valid image format.",
                    ErrorHandler.ErrorCategory.Validation, null, $"FilePath: {normalizedPath}");
                
                // Show user-friendly error message
                EditorUtility.DisplayDialog("Invalid Image",
                    $"Cannot add '{fileName}':\n\n" +
                    "• File does not exist\n" +
                    "• Or file is not a supported image format (PNG, JPG, JPEG, TGA, PSD)\n" +
                    "• Or file exceeds maximum size (50MB)",
                    "OK");
                return;
            }

            // Check if already added (case-insensitive, normalized comparison)
            bool alreadyAdded = _imageAbsPaths.Any(existingPath =>
            {
                try
                {
                    string normalizedExisting = Path.GetFullPath(existingPath).Replace('\\', '/');
                    return string.Equals(normalizedExisting, normalizedPath, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

            if (alreadyAdded)
            {
                string fileName = Path.GetFileName(normalizedPath);
                Debug.LogWarning($"[ModelSubmitWindow] Image '{fileName}' is already selected.");
                EditorUtility.DisplayDialog("Image Already Added",
                    $"The image '{fileName}' is already in the list.",
                    "OK");
                return;
            }

            // Add the normalized path
            _imageAbsPaths.Add(normalizedPath);
            SaveDraft(); // Auto-save draft when images are added
            
            Debug.Log($"[ModelSubmitWindow] Added image: {Path.GetFileName(normalizedPath)}");
            Repaint(); // Refresh UI to show new image
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


