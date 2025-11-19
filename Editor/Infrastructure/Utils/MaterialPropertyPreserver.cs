using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for preserving material properties when copying or instantiating materials.
    /// Ensures consistent material rendering properties across different contexts.
    /// </summary>
    public static class MaterialPropertyPreserver
    {
        // Material property names
        private const string PROPERTY_ALPHA_CLIP = "_AlphaClip";
        private const string PROPERTY_ALPHA_TEST = "_AlphaTest";
        private const string PROPERTY_CUTOFF = "_Cutoff";
        private const string PROPERTY_SPECULAR_HIGHLIGHTS = "_SpecularHighlights";
        private const string PROPERTY_SPEC_COLOR = "_SpecColor";
        private const string PROPERTY_SMOOTHNESS = "_Smoothness";
        private const string PROPERTY_GLOSSINESS = "_Glossiness";
        private const string PROPERTY_SPEC_GLOSS_MAP = "_SpecGlossMap";
        private const string PROPERTY_WORKFLOW_MODE = "_WorkflowMode";
        private const string PROPERTY_METALLIC = "_Metallic";
        private const string PROPERTY_METALLIC_GLOSS_MAP = "_MetallicGlossMap";
        private const string PROPERTY_ENVIRONMENT_REFLECTIONS = "_EnvironmentReflections";
        private const string PROPERTY_GLOSSY_REFLECTIONS = "_GlossyReflections";
        
        // Shader keywords
        private const string KEYWORD_ALPHATEST_ON = "_ALPHATEST_ON";
        
        // Workflow mode values
        private const float WORKFLOW_MODE_SPECULAR = 0f;
        private const float WORKFLOW_MODE_METALLIC = 1f;

        /// <summary>
        /// Preserves all important material properties for correct rendering.
        /// This ensures alpha clipping, specular highlights, and environment reflections work correctly.
        /// </summary>
        /// <param name="material">The material to preserve properties for.</param>
        /// <param name="logMaterialName">Optional material name for logging purposes.</param>
        /// <param name="enableLogging">Whether to log property preservation operations.</param>
        public static void PreserveMaterialProperties(Material material, string logMaterialName = null, bool enableLogging = false)
        {
            if (material == null)
            {
                return;
            }

            // Preserve alpha clipping settings
            PreserveAlphaClipping(material, logMaterialName, enableLogging);

            // Preserve specular highlights setting
            if (material.HasProperty(PROPERTY_SPECULAR_HIGHLIGHTS))
            {
                float specularHighlights = material.GetFloat(PROPERTY_SPECULAR_HIGHLIGHTS);
                material.SetFloat(PROPERTY_SPECULAR_HIGHLIGHTS, specularHighlights);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _SpecularHighlights for material '{logMaterialName ?? material.name}': {specularHighlights}");
                }
            }

            // Preserve specular color (URP and Built-in)
            if (material.HasProperty(PROPERTY_SPEC_COLOR))
            {
                Color specColor = material.GetColor(PROPERTY_SPEC_COLOR);
                material.SetColor(PROPERTY_SPEC_COLOR, specColor);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _SpecColor for material '{logMaterialName ?? material.name}': {specColor}");
                }
            }

            // Preserve smoothness (URP)
            if (material.HasProperty(PROPERTY_SMOOTHNESS))
            {
                float smoothness = material.GetFloat(PROPERTY_SMOOTHNESS);
                material.SetFloat(PROPERTY_SMOOTHNESS, smoothness);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _Smoothness for material '{logMaterialName ?? material.name}': {smoothness}");
                }
            }

            // Preserve glossiness (Built-in Standard shader)
            if (material.HasProperty(PROPERTY_GLOSSINESS))
            {
                float glossiness = material.GetFloat(PROPERTY_GLOSSINESS);
                material.SetFloat(PROPERTY_GLOSSINESS, glossiness);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _Glossiness for material '{logMaterialName ?? material.name}': {glossiness}");
                }
            }

            // Preserve specular gloss map texture and switch to specular workflow if present
            bool hasSpecGlossMap = false;
            if (material.HasProperty(PROPERTY_SPEC_GLOSS_MAP))
            {
                Texture specGlossMap = material.GetTexture(PROPERTY_SPEC_GLOSS_MAP);
                if (specGlossMap != null)
                {
                    material.SetTexture(PROPERTY_SPEC_GLOSS_MAP, specGlossMap);
                    hasSpecGlossMap = true;
                    if (enableLogging)
                    {
                        Debug.Log($"Preserved _SpecGlossMap for material '{logMaterialName ?? material.name}': {specGlossMap.name}");
                    }
                }
            }

            // Switch to specular workflow if specular map is present
            if (hasSpecGlossMap && material.HasProperty(PROPERTY_WORKFLOW_MODE))
            {
                material.SetFloat(PROPERTY_WORKFLOW_MODE, WORKFLOW_MODE_SPECULAR);
                if (enableLogging)
                {
                    Debug.Log($"Switched to specular workflow for material '{logMaterialName ?? material.name}' (specular map present)");
                }
            }

            // Preserve metallic value (for metallic workflow)
            if (material.HasProperty(PROPERTY_METALLIC))
            {
                float metallic = material.GetFloat(PROPERTY_METALLIC);
                material.SetFloat(PROPERTY_METALLIC, metallic);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _Metallic for material '{logMaterialName ?? material.name}': {metallic}");
                }
            }

            // Preserve metallic gloss map texture
            if (material.HasProperty(PROPERTY_METALLIC_GLOSS_MAP))
            {
                Texture metallicGlossMap = material.GetTexture(PROPERTY_METALLIC_GLOSS_MAP);
                if (metallicGlossMap != null)
                {
                    material.SetTexture(PROPERTY_METALLIC_GLOSS_MAP, metallicGlossMap);
                    if (enableLogging)
                    {
                        Debug.Log($"Preserved _MetallicGlossMap for material '{logMaterialName ?? material.name}': {metallicGlossMap.name}");
                    }
                }
            }

            // Preserve environment reflections setting
            if (material.HasProperty(PROPERTY_ENVIRONMENT_REFLECTIONS))
            {
                float environmentReflections = material.GetFloat(PROPERTY_ENVIRONMENT_REFLECTIONS);
                material.SetFloat(PROPERTY_ENVIRONMENT_REFLECTIONS, environmentReflections);
                if (enableLogging)
                {
                    Debug.Log($"Preserved _EnvironmentReflections for material '{logMaterialName ?? material.name}': {environmentReflections}");
                }
            }

            // Also preserve GlossyReflections if it exists (some shaders use this instead)
            if (material.HasProperty(PROPERTY_GLOSSY_REFLECTIONS))
            {
                float glossyReflections = material.GetFloat(PROPERTY_GLOSSY_REFLECTIONS);
                material.SetFloat(PROPERTY_GLOSSY_REFLECTIONS, glossyReflections);
            }

            // If material doesn't have _AlphaClip property, try to enable keyword if shader supports it
            if (!material.HasProperty(PROPERTY_ALPHA_CLIP) && !material.HasProperty(PROPERTY_ALPHA_TEST) && material.shader != null)
            {
                try
                {
                    material.EnableKeyword(KEYWORD_ALPHATEST_ON);
                }
                catch
                {
                    // Keyword might not exist in this shader, ignore
                }
            }
        }

        /// <summary>
        /// Preserves alpha clipping settings for a material.
        /// </summary>
        /// <param name="material">The material to preserve alpha clipping for.</param>
        /// <param name="logMaterialName">Optional material name for logging purposes.</param>
        /// <param name="enableLogging">Whether to log property preservation operations.</param>
        private static void PreserveAlphaClipping(Material material, string logMaterialName = null, bool enableLogging = false)
        {
            if (material == null)
            {
                return;
            }

            bool alphaClipping = false;
            float alphaClipValue = 0f;
            float cutoffValue = 0.5f;

            // Check for _AlphaClip property (URP)
            if (material.HasProperty(PROPERTY_ALPHA_CLIP))
            {
                alphaClipValue = material.GetFloat(PROPERTY_ALPHA_CLIP);
                alphaClipping = true;
            }
            // Check for _AlphaTest property (some shaders)
            else if (material.HasProperty(PROPERTY_ALPHA_TEST))
            {
                alphaClipValue = material.GetFloat(PROPERTY_ALPHA_TEST);
                alphaClipping = true;
            }

            // Get cutoff value if available
            if (material.HasProperty(PROPERTY_CUTOFF))
            {
                cutoffValue = material.GetFloat(PROPERTY_CUTOFF);
            }

            // Set the _ALPHATEST_ON keyword BEFORE setting properties
            // This ensures the shader variant is compiled with the keyword
            if (alphaClipping)
            {
                material.EnableKeyword(KEYWORD_ALPHATEST_ON);

                // Re-apply cutoff value after enabling keyword
                if (material.HasProperty(PROPERTY_CUTOFF))
                {
                    material.SetFloat(PROPERTY_CUTOFF, cutoffValue);
                }

                if (enableLogging)
                {
                    Debug.Log($"Enabled _ALPHATEST_ON keyword for material '{logMaterialName ?? material.name}' (AlphaClip: {alphaClipValue})");
                    Debug.Log($"Set _Cutoff value for material '{logMaterialName ?? material.name}': {cutoffValue}");
                    Debug.Log($"Configured alpha clipping for material '{logMaterialName ?? material.name}': {alphaClipping} (AlphaClip value: {alphaClipValue})");
                }
            }
            else
            {
                material.DisableKeyword(KEYWORD_ALPHATEST_ON);
            }
        }
    }
}

