
using System;

namespace ModelLibrary.Editor
{
    public static class FileExtensions
    {
        public const string FBX = ".fbx";
        public const string OBJ = ".obj";
        public const string PNG = ".png";
        public const string TGA = ".tga";
        public const string JPG = ".jpg";
        public const string JPEG = ".jpeg";
        public const string PSD = ".psd";
        public const string MAT = ".mat";

        public const string SHADER = ".shader";
        public const string SHADER_VARIANTS = ".shadervariants";
        public const string SHADER_GRAPH = ".shadergraph";
        public const string SHADER_SUB_GRAPH = ".shadersubgraph";
        public const string CGINC = ".cginc";
        public const string HLSL = ".hlsl";
        public const string CS = ".cs";

        public const string PREFAB = ".prefab";
        public const string META = ".meta";
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
