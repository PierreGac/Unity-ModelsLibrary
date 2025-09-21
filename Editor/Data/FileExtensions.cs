
using System;

namespace ModelLibrary.Editor
{
    /// <summary>
    /// Provides common file extension constants used by the ModelLibrary editor
    /// utilities and helpers for filtering and validation. Also exposes logic to
    /// determine whether a given file extension should be considered not allowed
    /// (e.g., script/shader source files that are not importable assets).
    /// </summary>
    public static class FileExtensions
    {
        // --- 3D model formats ---
        /// <summary>File extension for Autodesk Filmbox 3D models (.fbx).</summary>
        public const string FBX = ".fbx";
        /// <summary>File extension for Wavefront Object 3D models (.obj).</summary>
        public const string OBJ = ".obj";

        // --- Texture/image formats ---
        /// <summary>File extension for Portable Network Graphics images (.png).</summary>
        public const string PNG = ".png";
        /// <summary>File extension for Truevision Targa images (.tga).</summary>
        public const string TGA = ".tga";
        /// <summary>File extension for JPEG images (.jpg).</summary>
        public const string JPG = ".jpg";
        /// <summary>File extension for JPEG images (.jpeg).</summary>
        public const string JPEG = ".jpeg";
        /// <summary>File extension for Adobe Photoshop documents (.psd).</summary>
        public const string PSD = ".psd";
        /// <summary>File extension for Unity material assets (.mat).</summary>
        public const string MAT = ".mat";

        // --- Shader and code-related formats (typically not importable as model assets) ---
        /// <summary>File extension for Unity surface/vertex fragment shaders (.shader).</summary>
        public const string SHADER = ".shader";
        /// <summary>File extension for Unity shader variants collections (.shadervariants).</summary>
        public const string SHADER_VARIANTS = ".shadervariants";
        /// <summary>File extension for Shader Graph assets (.shadergraph).</summary>
        public const string SHADER_GRAPH = ".shadergraph";
        /// <summary>File extension for Shader Graph sub-graphs (.shadersubgraph).</summary>
        public const string SHADER_SUB_GRAPH = ".shadersubgraph";
        /// <summary>File extension for Cg/HLSL include files (.cginc).</summary>
        public const string CGINC = ".cginc";
        /// <summary>File extension for HLSL source files (.hlsl).</summary>
        public const string HLSL = ".hlsl";
        /// <summary>File extension for C# source files (.cs).</summary>
        public const string CS = ".cs";

        // --- Unity asset/meta ---
        /// <summary>File extension for Unity prefab assets (.prefab).</summary>
        public const string PREFAB = ".prefab";
        /// <summary>File extension for Unity meta files (.meta).</summary>
        public const string META = ".meta";

        /// <summary>
        /// Determines whether the provided file extension is considered not allowed
        /// for the current import or processing context (e.g., script/shader files).
        /// </summary>
        /// <param name="fileExtension">
        /// The file extension to check. The value may include leading/trailing
        /// whitespace and is case-insensitive. A leading dot is expected but not required.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the normalized extension matches a known
        /// not-allowed type (shader/code related); otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This method normalizes the input via <see cref="string.Trim"/> and
        /// <see cref="string.ToLowerInvariant"/> before comparison. Empty or
        /// whitespace-only input returns <see langword="false"/>.
        /// </remarks>
        public static bool IsNotAllowedFileExtension(string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                return false;
            }

            string normalized = fileExtension.Trim().ToLowerInvariant();

            return normalized switch
            {
                SHADER or
                SHADER_VARIANTS or
                SHADER_GRAPH or
                SHADER_SUB_GRAPH or
                CGINC or
                HLSL or
                CS => true,
                _ => false
            };
        }
    }
}
