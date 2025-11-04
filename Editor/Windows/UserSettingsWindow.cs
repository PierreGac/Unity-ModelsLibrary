
using ModelLibrary.Editor.Identity;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// User settings configuration window for the Model Library.
    /// Allows users to configure their username and role (Developer or Artist).
    /// Role determines which features are available (e.g., Artists can submit models).
    /// Settings are persisted using EditorPrefs.
    /// </summary>
    public class UserSettingsWindow : EditorWindow
    {
        /// <summary>User identity provider for reading/writing user settings.</summary>
        private readonly SimpleUserIdentityProvider _id = new();
        /// <summary>Current user name value from the form.</summary>
        private string _name;
        /// <summary>Current user role value from the form.</summary>
        private UserRole _role;

        [MenuItem("Tools/Model Library/User Settings")]
        public static void Open()
        {
            UserSettingsWindow w = GetWindow<UserSettingsWindow>("User Settings");
            w.Show();
        }

        private void OnEnable()
        {
            _name = _id.GetUserName();
            _role = _id.GetUserRole();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("User Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            _name = EditorGUILayout.TextField("User Name", _name);
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("User Role", EditorStyles.boldLabel);
            string[] roleOptions = System.Enum.GetNames(typeof(UserRole));
            int currentRoleIndex = System.Array.IndexOf(roleOptions, _role.ToString());
            int selectedRoleIndex = EditorGUILayout.Popup("Role", currentRoleIndex, roleOptions);
            if (selectedRoleIndex >= 0 && selectedRoleIndex < roleOptions.Length)
            {
                _role = (UserRole)System.Enum.Parse(typeof(UserRole), roleOptions[selectedRoleIndex]);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                _role == UserRole.Artist 
                    ? "Artist role: Can submit models, manage versions, and browse the library." 
                    : "Developer role: Can browse, import models, and leave feedback notes.",
                MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Save", GUILayout.Height(30)))
            {
                _id.SetUserName(_name);
                _id.SetUserRole(_role);
                Close();
            }
        }
    }
}



