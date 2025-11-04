using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// First-run configuration wizard for the Model Library system.
    /// Automatically displayed when the Model Library is not yet configured.
    /// Collects essential setup information: user name and repository location.
    /// The wizard is modal and blocks other operations until configuration is complete.
    /// </summary>
    public class FirstRunWizard : EditorWindow
    {
        /// <summary>User name entered in the form.</summary>
        private string _userName;
        /// <summary>Repository root path or URL entered in the form.</summary>
        private string _repoRoot;
        /// <summary>Selected repository type (FileSystem or HTTP).</summary>
        private ModelLibrarySettings.RepositoryKind _kind;

        /// <summary>
        /// Checks if the Model Library has been properly configured.
        /// Verifies that both user name and repository root are set and not default values.
        /// </summary>
        /// <returns>True if configuration is complete, false if the wizard should be shown.</returns>
        public static bool IsConfigured()
        {
            try
            {
                // Check user identity - check if user has explicitly set a username
                if (!EditorPrefs.HasKey("ModelLibrary.UserName"))
                {
                    return false; // User has never configured username
                }

                SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                string userName = identityProvider.GetUserName();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return false;
                }

                // Check settings
                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                if (string.IsNullOrWhiteSpace(settings.repositoryRoot))
                {
                    return false;
                }

                // Check if this is just the default example value (user hasn't configured yet)
                if (settings.repositoryRoot == "\\\\SERVER\\ModelLibrary")
                {
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"FirstRunWizard.IsConfigured() failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows the first-run wizard if configuration is not complete.
        /// Called automatically when the Model Library browser opens and configuration is missing.
        /// </summary>
        public static void MaybeShow()
        {
            if (!IsConfigured())
            {
                FirstRunWizard w = GetWindow<FirstRunWizard>(true, "Model Library Setup", true);
                w.minSize = new Vector2(420, 180);
                w.Init();
                w.ShowUtility(); // Show as modal utility window
            }
        }

        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            _userName = new SimpleUserIdentityProvider().GetUserName();
            _repoRoot = settings.repositoryRoot;
            _kind = settings.repositoryKind;
        }

        private void SaveConfiguration()
        {
            // Save user identity
            SimpleUserIdentityProvider id = new SimpleUserIdentityProvider();
            id.SetUserName(_userName);

            // Save settings
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryKind = _kind;
            settings.repositoryRoot = _repoRoot;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // Notify other windows to reinitialize
            NotifyConfigurationChanged();
        }

        private void NotifyConfigurationChanged()
        {
            // Find and reinitialize any open ModelLibraryWindow instances
            ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
            foreach (ModelLibraryWindow window in windows)
            {
                if (window != null)
                {
                    window.ReinitializeAfterConfiguration();
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Welcome to Model Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Please configure your user name and the database location.", MessageType.Info);

            EditorGUILayout.Space();

            _userName = EditorGUILayout.TextField("User Name", _userName);
            if (string.IsNullOrWhiteSpace(_userName))
            {
                EditorGUILayout.HelpBox("User name is required.", MessageType.Warning);
            }

            _kind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Kind", _kind);

            _repoRoot = EditorGUILayout.TextField("Repository Root", _repoRoot);
            if (string.IsNullOrWhiteSpace(_repoRoot))
            {
                EditorGUILayout.HelpBox("Repository root is required.", MessageType.Warning);
            }
            else if (_kind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                // Validate file system path
                if (!System.IO.Path.IsPathRooted(_repoRoot) && !_repoRoot.StartsWith("\\\\"))
                {
                    EditorGUILayout.HelpBox("File system path should be absolute (e.g., C:\\Models or \\\\server\\share)", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Examples:\n• File System: C:\\Models or \\\\server\\Models\n• HTTP: https://api.example.com/models", MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                bool canSave = !string.IsNullOrWhiteSpace(_userName) && !string.IsNullOrWhiteSpace(_repoRoot);
                using (new EditorGUI.DisabledScope(!canSave))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(100)))
                    {
                        SaveConfiguration();
                        Close();
                    }
                }
            }
        }
    }
}


