
using ModelLibrary.Editor.Identity;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Minimal window to configure the username used for submissions and notes.
    /// </summary>
    public class UserSettingsWindow : EditorWindow
    {
        private readonly SimpleUserIdentityProvider _id = new();
        private string _name;

        [MenuItem("Tools/Model Library/User Settings")]
        public static void Open()
        {
            UserSettingsWindow w = GetWindow<UserSettingsWindow>("User");
            w.Show();
        }

        private void OnEnable() => _name = _id.GetUserName();

        private void OnGUI()
        {
            _name = EditorGUILayout.TextField("User Name", _name);
            if (GUILayout.Button("Save")) { _id.SetUserName(_name); Close(); }
        }
    }
}



