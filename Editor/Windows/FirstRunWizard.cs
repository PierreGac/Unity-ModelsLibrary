using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Simple first-run wizard to collect user name and repository root.
    /// Shown automatically if either is missing when the browser opens.
    /// </summary>
    public class FirstRunWizard : EditorWindow
    {
        private string _userName;
        private string _repoRoot;
        private ModelLibrarySettings.RepositoryKind _kind;

        public static bool IsConfigured()
        {
            ModelLibrarySettings s = ModelLibrarySettings.GetOrCreate();
            if (string.IsNullOrWhiteSpace(new SimpleUserIdentityProvider().GetUserName()))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(s.repositoryRoot))
            {
                return false;
            }

            return true;
        }

        public static void MaybeShow()
        {
            if (!IsConfigured())
            {
                FirstRunWizard w = GetWindow<FirstRunWizard>(true, "Model Library Setup", true);
                w.minSize = new Vector2(420, 180);
                w.Init();
                w.ShowUtility();
            }
        }

        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            _userName = new SimpleUserIdentityProvider().GetUserName();
            _repoRoot = settings.repositoryRoot;
            _kind = settings.repositoryKind;
        }

        private void OnGUI()
        {
            GUILayout.Label("Welcome to Model Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Please configure your user name and the database location.", MessageType.Info);
            _userName = EditorGUILayout.TextField("User Name", _userName);
            _kind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Kind", _kind);
            _repoRoot = EditorGUILayout.TextField("Repository Root", _repoRoot);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save", GUILayout.Width(100)))
                {
                    SimpleUserIdentityProvider id = new SimpleUserIdentityProvider();
                    id.SetUserName(_userName);
                    ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                    settings.repositoryKind = _kind;
                    settings.repositoryRoot = _repoRoot;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    Close();
                }
            }
        }
    }
}


