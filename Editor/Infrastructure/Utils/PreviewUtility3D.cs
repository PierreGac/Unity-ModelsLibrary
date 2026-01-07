using System;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Wrapper around Unity's PreviewRenderUtility for rendering 3D mesh previews.
    /// Provides a simple interface for generating preview textures from meshes and materials.
    /// Automatically handles camera positioning, lighting setup, and cleanup.
    /// </summary>
    public sealed class PreviewUtility3D : IDisposable
    {
        /// <summary>Unity's preview render utility for generating preview textures.</summary>
        private readonly PreviewRenderUtility _preview;
        /// <summary>Root GameObject for organizing preview scene objects.</summary>
        private readonly GameObject _root;
        /// <summary>Camera used for rendering the preview.</summary>
        private readonly Camera _cam;
        /// <summary>Directional light for illuminating the preview scene.</summary>
        private Light _light;
        /// <summary>Reflection probe for adding reflections to the preview.</summary>
        private ReflectionProbe _reflectionProbe;

        /// <summary>
        /// Initializes a new PreviewUtility3D instance.
        /// Sets up the preview render utility with a camera, directional light, and reflection probe.
        /// </summary>
        public PreviewUtility3D()
        {
            _preview = new PreviewRenderUtility(true) { cameraFieldOfView = 30f };
            _cam = _preview.camera;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.backgroundColor = new Color(0.1921569f, 0.3019608f, 0.4745098f, 0f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;

            // PreviewRenderUtility with 'true' creates its own scene with built-in lighting
            // We'll add additional lights directly to the preview scene using AddSingleGO
            // Create light as root GameObject (AddSingleGO requires root GameObjects)
            // CRITICAL: Use HideFlags to prevent this GameObject from appearing in the scene hierarchy
            GameObject lightObj = new GameObject("PreviewLight");
            lightObj.hideFlags = HideFlags.HideAndDontSave; // Hide from hierarchy and don't save to scene
            _light = lightObj.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 2; // Increased intensity for better specular visibility
            _light.colorTemperature = 5000;
            _light.color = Color.white;
            _light.shadows = LightShadows.Soft;
            _light.enabled = true;
            _light.bounceIntensity = 1;
            _light.renderMode = LightRenderMode.Auto;

            // Add light to preview scene - AddSingleGO handles adding it to the preview's isolated scene
            // This doesn't modify the current scene, only the preview's internal scene
            _preview.AddSingleGO(lightObj);

            // Add reflection probe for reflections
            // CRITICAL: Use HideFlags to prevent this GameObject from appearing in the scene hierarchy
            GameObject probeObj = new GameObject("PreviewReflectionProbe");
            probeObj.hideFlags = HideFlags.HideAndDontSave; // Hide from hierarchy and don't save to scene
            probeObj.transform.position = Vector3.zero;
            _reflectionProbe = probeObj.AddComponent<ReflectionProbe>();
            _reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            _reflectionProbe.resolution = 128; // Lower resolution for performance
            _reflectionProbe.boxProjection = false;
            _reflectionProbe.intensity = 1.0f;
            _reflectionProbe.size = new Vector3(100f, 100f, 100f); // Large enough to cover the preview area

            // Add probe to preview scene
            _preview.AddSingleGO(probeObj);

            // Create root for cleanup (but don't add it to preview scene)
            // CRITICAL: Use HideFlags to prevent this GameObject from appearing in the scene hierarchy
            _root = new GameObject("PreviewRoot");
            _root.hideFlags = HideFlags.HideAndDontSave; // Hide from hierarchy and don't save to scene

            // Set up skybox for reflections - use the currently active scene's skybox
            // RenderSettings.skybox reflects the skybox of the currently active scene
            Material sceneSkybox = RenderSettings.skybox;

            if (sceneSkybox != null)
            {
                // Use the scene's skybox - explicitly set it to ensure PreviewRenderUtility picks it up
                // This ensures the preview uses the same skybox as the currently opened scene
                RenderSettings.skybox = sceneSkybox;
            }
            else
            {
                // If no skybox is set in the scene, create a simple procedural skybox as fallback
                Shader skyboxShader = Shader.Find("Skybox/Procedural");
                if (skyboxShader != null)
                {
                    Material fallbackSkybox = new Material(skyboxShader);
                    fallbackSkybox.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f, 1f));
                    fallbackSkybox.SetColor("_GroundColor", new Color(0.2f, 0.2f, 0.2f, 1f));
                    fallbackSkybox.SetFloat("_SunSize", 0.04f);
                    fallbackSkybox.SetFloat("_SunSizeConvergence", 5f);
                    fallbackSkybox.SetFloat("_AtmosphereThickness", 1f);
                    fallbackSkybox.SetColor("_SunColor", Color.white);

                    // Set the fallback skybox for the preview
                    RenderSettings.skybox = fallbackSkybox;
                }
            }

            // Add ambient light settings - increased for better specular/metallic visibility
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Brighter for specular highlights
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            RenderSettings.ambientIntensity = 1.0f; // Increase overall ambient intensity
        }

        /// <summary>
        /// Disposes of the preview utility and cleans up all resources.
        /// Cleans up the preview render utility which will destroy all GameObjects in the preview scene.
        /// </summary>
        public void Dispose()
        {
            // Cleanup will destroy all GameObjects in the preview scene, including the light and probe
            // We don't need to manually destroy them
            if (_root)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            _preview.Cleanup();
        }

        /// <summary>
        /// Renders a mesh with a material to a preview texture.
        /// Automatically positions the camera to frame the mesh and sets up lighting.
        /// </summary>
        /// <param name="mesh">The mesh to render.</param>
        /// <param name="mat">The material to apply to the mesh.</param>
        /// <param name="rect">The rectangle defining the preview area.</param>
        /// <returns>The rendered preview texture.</returns>
        public Texture Render(Mesh mesh, Material mat, Rect rect)
        {
            if (mesh == null)
            {
                return null;
            }

            // Ensure material exists - use URP-compatible shader if available
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader);
            }

            Bounds bounds = mesh.bounds;
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size <= 0f)
            {
                size = 1f;
            }
            Vector3 pos = bounds.center + new Vector3(0, 0, -size * 2f);

            // Ensure rect has valid size
            if (rect.width <= 0 || rect.height <= 0)
            {
                return null;
            }

            _cam.transform.position = pos;
            _cam.transform.LookAt(bounds.center);
            _light.transform.SetPositionAndRotation(pos + Vector3.up * 2f, Quaternion.Euler(30, 30, 0));

            // Position reflection probe at mesh center
            if (_reflectionProbe != null)
            {
                _reflectionProbe.transform.position = bounds.center;
                _reflectionProbe.RenderProbe();
            }

            // Ensure camera is properly set up
            _cam.orthographic = false;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.backgroundColor = new Color(0.1921569f, 0.3019608f, 0.4745098f, 0f);

            // PreviewRenderUtility works with SRP enabled - no need to disable it
            // This allows URP materials to render correctly with their textures
            try
            {
                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
                _preview.camera.Render();
                Texture previewTexture = _preview.EndPreview();
                return previewTexture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"PreviewUtility3D.Render: Exception during rendering: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Renders a mesh with custom camera position and rotation.
        /// </summary>
        /// <param name="mesh">The mesh to render.</param>
        /// <param name="mat">The material to apply to the mesh.</param>
        /// <param name="rect">The rectangle defining the preview area.</param>
        /// <param name="cameraPosition">Custom camera position.</param>
        /// <param name="lookAt">Point for the camera to look at.</param>
        /// <returns>The rendered preview texture.</returns>
        public Texture Render(Mesh mesh, Material mat, Rect rect, Vector3 cameraPosition, Vector3 lookAt)
        {
            if (mesh == null)
            {
                Debug.LogWarning("PreviewUtility3D.Render: mesh is null");
                return null;
            }

            // Ensure material exists - use URP-compatible shader if available
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader);
                Debug.Log($"PreviewUtility3D.Render: Created default material with shader {(shader != null ? shader.name : "null")}");
            }

            // Ensure rect has valid size
            if (rect.width <= 0 || rect.height <= 0)
            {
                Debug.LogWarning($"PreviewUtility3D.Render: Invalid rect size: {rect}");
                return null;
            }

            _cam.transform.position = cameraPosition;
            _cam.transform.LookAt(lookAt);
            _light.transform.SetPositionAndRotation(cameraPosition + Vector3.up * 2f, Quaternion.Euler(30, 30, 0));

            // Position reflection probe at lookAt point (mesh center)
            if (_reflectionProbe != null)
            {
                _reflectionProbe.transform.position = lookAt;
                _reflectionProbe.RenderProbe();
            }

            // Ensure camera is properly set up
            _cam.orthographic = false;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.backgroundColor = new Color(0.1921569f, 0.3019608f, 0.4745098f, 0f);

            // PreviewRenderUtility works with SRP enabled - no need to disable it
            // This allows URP materials to render correctly with their textures
            try
            {
                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
                _preview.camera.Render();
                Texture previewTexture = _preview.EndPreview();
                return previewTexture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"PreviewUtility3D.Render: Exception during rendering: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}



