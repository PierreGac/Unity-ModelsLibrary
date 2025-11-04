
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Identity
{
    /// <summary>
    /// User role types for the Model Library.
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Developer role - can browse, import, and leave feedback notes.
        /// </summary>
        Developer,
        
        /// <summary>
        /// Artist/Modeler role - can browse, import, submit models, and manage versions.
        /// </summary>
        Artist
    }

    public interface IUserIdentityProvider
    {
        string GetUserName();
        void SetUserName(string name);
        UserRole GetUserRole();
        void SetUserRole(UserRole role);
    }

    /// <summary>
    /// Minimal identity provider backed by EditorPrefs.
    /// </summary>
    public class SimpleUserIdentityProvider : IUserIdentityProvider
    {
        private const string Key = "ModelLibrary.UserName";
        private const string RoleKey = "ModelLibrary.UserRole";
        private const string DefaultName = "anonymous";
        private const UserRole DefaultRole = UserRole.Developer;

        public string GetUserName() => EditorPrefs.GetString(Key, DefaultName);
        public void SetUserName(string name) => EditorPrefs.SetString(Key, string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim());
        
        public UserRole GetUserRole()
        {
            string roleString = EditorPrefs.GetString(RoleKey, DefaultRole.ToString());
            if (System.Enum.TryParse<UserRole>(roleString, out UserRole role))
            {
                return role;
            }
            return DefaultRole;
        }
        
        public void SetUserRole(UserRole role) => EditorPrefs.SetString(RoleKey, role.ToString());
    }
}



