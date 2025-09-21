
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Tiny wrapper around PreviewRenderUtility to draw a mesh/material.
    /// </summary>
    public sealed class PreviewUtility3D : System.IDisposable
    {
        private readonly PreviewRenderUtility _preview;
        private readonly GameObject _root;
        private readonly Camera _cam;
        private Light _light;

        public PreviewUtility3D()
        {
            _preview = new PreviewRenderUtility(true);
            _preview.cameraFieldOfView = 30f;
            _cam = _preview.camera;
            _root = new GameObject("PreviewRoot");
            _light = new GameObject("Light").AddComponent<Light>();
            _light.transform.SetParent(_root.transform);
            _light.type = LightType.Directional; _light.intensity = 1.0f;
        }

        public void Dispose()
        {
            if (_root)
            {
                Object.DestroyImmediate(_root);
            }

            _preview.Cleanup();
        }

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



