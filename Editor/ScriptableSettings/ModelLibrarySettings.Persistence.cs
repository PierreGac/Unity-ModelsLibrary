using System;
using System.IO;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Settings
{
    /// <summary>
    /// Loads and saves <see cref="ModelLibrarySettings"/> in project-owned storage.
    /// </summary>
    public partial class ModelLibrarySettings
    {
        private const string PROJECT_RELATIVE_PATH = "ProjectSettings/ModelLibrarySettings.json";
        private const string LEGACY_ASSET_PATH = "Assets/ModelLibrary/Resources/ModelLibrarySettings.asset";
        private const string LEGACY_RESOURCE_NAME = "ModelLibrarySettings";

        private static ModelLibrarySettings _projectCopy;

        /// <summary>
        /// Returns the writable project settings object, creating it from the package asset on first use.
        /// </summary>
        /// <returns>The project-owned settings instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the project settings file exists but cannot be read.</exception>
        public static ModelLibrarySettings GetOrCreate()
        {
            if (_projectCopy != null)
            {
                return _projectCopy;
            }

            _projectCopy = CreateInstance<ModelLibrarySettings>();
            _projectCopy.hideFlags = HideFlags.HideAndDontSave;

            string absolutePath = GetProjectSettingsAbsolutePath();
            if (File.Exists(absolutePath))
            {
                ReadProjectCopy(_projectCopy, absolutePath);
            }
            else
            {
                ApplyLegacyOrDefaults(_projectCopy);
                _projectCopy.SaveProjectCopy();
                Debug.Log("[ModelLibrarySettings] Copied library settings into ProjectSettings/ModelLibrarySettings.json.");
            }

            return _projectCopy;
        }

        /// <summary>
        /// Writes this instance to project-owned storage.
        /// </summary>
        public void SaveProjectCopy()
        {
            string absolutePath = GetProjectSettingsAbsolutePath();
            string json = EditorJsonUtility.ToJson(this, true);
            SafeFileWriter.WriteAllText(absolutePath, json);
        }

        /// <summary>
        /// Drops the in-memory settings object so the next load reads project storage again.
        /// </summary>
        internal static void ReloadProjectCopy()
        {
            if (_projectCopy != null)
            {
                DestroyImmediate(_projectCopy);
                _projectCopy = null;
            }
        }

        private static void ReadProjectCopy(ModelLibrarySettings target, string absolutePath)
        {
            try
            {
                string json = File.ReadAllText(absolutePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidOperationException("Project settings file is empty.");
                }

                EditorJsonUtility.FromJsonOverwrite(json, target);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrarySettings] Failed to read project settings '{absolutePath}': {ex.Message}");
                if (ReferenceEquals(_projectCopy, target))
                {
                    DestroyImmediate(target);
                    _projectCopy = null;
                }

                throw;
            }
        }

        private static void ApplyLegacyOrDefaults(ModelLibrarySettings target)
        {
            ModelLibrarySettings legacy = AssetDatabase.LoadAssetAtPath<ModelLibrarySettings>(LEGACY_ASSET_PATH);
            if (legacy == null)
            {
                legacy = Resources.Load<ModelLibrarySettings>(LEGACY_RESOURCE_NAME);
            }

            if (legacy == null || ReferenceEquals(legacy, target))
            {
                return;
            }

            target.repositoryKind = legacy.repositoryKind;
            target.repositoryRoot = legacy.repositoryRoot;
            target.localCacheRoot = legacy.localCacheRoot;
        }

        private static string GetProjectSettingsAbsolutePath()
        {
            string projectRoot = EditorPaths.projectRoot;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, PROJECT_RELATIVE_PATH));
            PathUtils.AssertInsideRoot(absolutePath, projectRoot);
            return absolutePath;
        }
    }
}
