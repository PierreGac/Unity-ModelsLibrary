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
            _cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;

            // Create light - PreviewRenderUtility handles lighting automatically
            // but we can add a light for better control
            _root = new GameObject("PreviewRoot");
            _light = new GameObject("Light").AddComponent<Light>();
            _light.transform.SetParent(_root.transform);
            _light.type = LightType.Directional;
            _light.intensity = 1.0f;
            _light.color = Color.white;
            _light.shadows = LightShadows.None;

            // Add reflection probe for reflections
            GameObject probeObj = new GameObject("ReflectionProbe");
            probeObj.transform.SetParent(_root.transform);
            probeObj.transform.position = Vector3.zero;
            _reflectionProbe = probeObj.AddComponent<ReflectionProbe>();
            _reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            _reflectionProbe.resolution = 128; // Lower resolution for performance
            _reflectionProbe.boxProjection = false;
            _reflectionProbe.intensity = 1.0f;
            _reflectionProbe.size = new Vector3(100f, 100f, 100f); // Large enough to cover the preview area

            // Set up a simple skybox for reflections
            // Use Unity's default skybox or create a simple gradient skybox
            Material skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial == null)
            {
                // Try to find a default skybox material
                Shader skyboxShader = Shader.Find("Skybox/Procedural");
                if (skyboxShader != null)
                {
                    skyboxMaterial = new Material(skyboxShader);
                    skyboxMaterial.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f, 1f));
                    skyboxMaterial.SetColor("_GroundColor", new Color(0.2f, 0.2f, 0.2f, 1f));
                    skyboxMaterial.SetFloat("_SunSize", 0.04f);
                    skyboxMaterial.SetFloat("_SunSizeConvergence", 5f);
                    skyboxMaterial.SetFloat("_AtmosphereThickness", 1f);
                    skyboxMaterial.SetColor("_SunColor", Color.white);
                }
            }

            // Set skybox for reflections
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }

            // Add ambient light settings
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        /// <summary>
        /// Disposes of the preview utility and cleans up all resources.
        /// Destroys the root GameObject and cleans up the preview render utility.
        /// </summary>
        public void Dispose()
        {
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
            _light.transform.rotation = Quaternion.Euler(30, 30, 0);
            _light.transform.position = pos + Vector3.up * 2f;

            // Position reflection probe at mesh center
            if (_reflectionProbe != null)
            {
                _reflectionProbe.transform.position = bounds.center;
                _reflectionProbe.RenderProbe();
            }

            // Ensure camera is properly set up
            _cam.orthographic = false;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

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
            Debug.Log($"PreviewUtility3D.Render called: mesh={(mesh != null ? mesh.name : "null")}, mat={(mat != null ? mat.name : "null")}, rect={rect}, cameraPos={cameraPosition}, lookAt={lookAt}");

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
            _light.transform.rotation = Quaternion.Euler(30, 30, 0);
            _light.transform.position = cameraPosition + Vector3.up * 2f;

            // Position reflection probe at lookAt point (mesh center)
            if (_reflectionProbe != null)
            {
                _reflectionProbe.transform.position = lookAt;
                _reflectionProbe.RenderProbe();
            }

            // Ensure camera is properly set up
            _cam.orthographic = false;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

            // PreviewRenderUtility works with SRP enabled - no need to disable it
            // This allows URP materials to render correctly with their textures
            try
            {
                Debug.Log($"PreviewUtility3D.Render: Starting preview render. Using mat: {(mat.shader != null ? mat.shader.name : "null")}, SRP enabled: {Unsupported.useScriptableRenderPipeline}");
                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
                _preview.camera.Render();
                Texture previewTexture = _preview.EndPreview();
                Debug.Log($"PreviewUtility3D.Render: Preview texture created: {previewTexture != null}, Size: {(previewTexture != null ? $"{previewTexture.width}x{previewTexture.height}" : "null")}");
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



