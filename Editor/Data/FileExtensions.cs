
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
        ///
        /// SECURITY (audit HIGH-06): This is a DENYLIST and is NOT sufficient
        /// as a security boundary. It misses .dll, .asmdef, .asmref, .rsp,
        /// .boo, .js, .unity, and many others. Always combine with
        /// <see cref="IsAllowedPayloadExtension"/> (an ALLOWLIST) when
        /// accepting files from untrusted sources (submit, deploy, import).
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

        // =====================================================================
        // SECURITY (CRIT-05 + HIGH-06): EXPLICIT ALLOWLIST
        // =====================================================================
        //
        // The allowlist is the primary defense against RCE via malicious
        // payload files (.cs EditorScripts, .dll managed assemblies, etc.).
        //
        // The denylist above is kept as defense-in-depth but MUST NOT be the
        // only filter. Always use IsAllowedPayloadExtension when accepting
        // files from untrusted sources.
        // =====================================================================

        /// <summary>
        /// Extensions permitted in model payloads (3D model formats, textures,
        /// materials, prefabs). Any file with an extension NOT in this set
        /// must be rejected at submit, deploy, and import time.
        /// </summary>
        public static readonly System.Collections.Generic.HashSet<string> AllowedPayloadExtensions =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                FBX,
                OBJ,
                PNG,
                TGA,
                JPG,
                JPEG,
                PSD,
                MAT,
                PREFAB,
            };

        /// <summary>
        /// Returns <see langword="true"/> if the given extension is on the
        /// explicit allowlist for model payload files. Use this as the
        /// PRIMARY filter when accepting files from untrusted sources.
        /// </summary>
        /// <param name="fileExtension">File extension including the leading dot (case-insensitive).</param>
        /// <returns><see langword="true"/> if allowed; <see langword="false"/> otherwise.</returns>
        public static bool IsAllowedPayloadExtension(string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                return false;
            }
            return AllowedPayloadExtensions.Contains(fileExtension.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Combined check: returns <see langword="true"/> if the extension is
        /// on the allowlist AND not on the denylist. Use this anywhere a file
        /// from an untrusted source is being considered for inclusion in a
        /// model payload.
        /// </summary>
        public static bool IsAcceptablePayloadExtension(string fileExtension)
        {
            return IsAllowedPayloadExtension(fileExtension)
                   && !IsNotAllowedFileExtension(fileExtension);
        }
    }
}
