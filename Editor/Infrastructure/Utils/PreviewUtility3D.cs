
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Wrapper around Unity's PreviewRenderUtility for rendering 3D mesh previews.
    /// Provides a simple interface for generating preview textures from meshes and materials.
    /// Automatically handles camera positioning, lighting setup, and cleanup.
    /// </summary>
    public sealed class PreviewUtility3D : System.IDisposable
    {
        /// <summary>Unity's preview render utility for generating preview textures.</summary>
        private readonly PreviewRenderUtility _preview;
        /// <summary>Root GameObject for organizing preview scene objects.</summary>
        private readonly GameObject _root;
        /// <summary>Camera used for rendering the preview.</summary>
        private readonly Camera _cam;
        /// <summary>Directional light for illuminating the preview scene.</summary>
        private Light _light;

        /// <summary>
        /// Initializes a new PreviewUtility3D instance.
        /// Sets up the preview render utility with a camera and directional light.
        /// </summary>
        public PreviewUtility3D()
        {
            _preview = new PreviewRenderUtility(true);
            _preview.cameraFieldOfView = 30f;
            _cam = _preview.camera;
            _root = new GameObject("PreviewRoot");
            _light = new GameObject("Light").AddComponent<Light>();
            _light.transform.SetParent(_root.transform);
            _light.type = LightType.Directional; 
            _light.intensity = 1.0f;
        }

        /// <summary>
        /// Disposes of the preview utility and cleans up all resources.
        /// Destroys the root GameObject and cleans up the preview render utility.
        /// </summary>
        public void Dispose()
        {
            if (_root)
            {
                Object.DestroyImmediate(_root);
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
            Bounds bounds = mesh.bounds;
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            Vector3 pos = bounds.center + new Vector3(0, 0, -size * 2f);

            _cam.transform.position = pos;
            _cam.transform.LookAt(bounds.center);
            _light.transform.rotation = Quaternion.Euler(30, 30, 0);

            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
            _preview.camera.Render();
            return _preview.EndPreview();
        }
    }
}



