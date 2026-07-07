
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
        Artist,
        
        /// <summary>
        /// Administrator role - full access including analytics, version deletion, and system management.
        /// </summary>
        Admin
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
    /// <remarks>
    /// SECURITY (audit CRIT-03): This provider is CLIENT-ONLY and ADVISORY.
    /// The role is stored in plaintext in EditorPrefs (registry on Windows,
    /// plist on macOS) and can be trivially modified by any user with access
    /// to the machine. This is acceptable for UX gating (hiding/showing
    /// buttons) but MUST NOT be relied on as a security boundary.
    ///
    /// Real authorization MUST be enforced server-side by the HTTP repository
    /// backend (HttpRepository) using an authenticated user identity (e.g. a
    /// short-lived JWT). The server must reject unauthorized operations
    /// regardless of what role the client claims to have.
    ///
    /// Until a server-side enforcement layer exists, treat all role checks in
    /// the editor as advisory: a determined user can bypass any of them.
    /// </remarks>
    public class SimpleUserIdentityProvider : IUserIdentityProvider
    {
        private const string Key = "ModelLibrary.UserName";
        private const string RoleKey = "ModelLibrary.UserRole";
        private const string DefaultName = "anonymous";
        private const UserRole DefaultRole = UserRole.Developer;

        // SECURITY (CRIT-03): Single-frame cache to avoid the per-OnGUI-frame
        // EditorPrefs.GetInt registry hit (audit MED-14). Invalidated by
        // SetUserRole and by domain reload (static field).
        private static UserRole? _cachedRole;
        private static string _cachedRoleRaw;

        public string GetUserName() => EditorPrefs.GetString(Key, DefaultName);
        public void SetUserName(string name) => EditorPrefs.SetString(Key, string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim());

        public UserRole GetUserRole()
        {
            string roleString = EditorPrefs.GetString(RoleKey, DefaultRole.ToString());

            // Return cached value if EditorPrefs hasn't changed.
            if (_cachedRole.HasValue && _cachedRoleRaw == roleString)
            {
                return _cachedRole.Value;
            }

            if (System.Enum.TryParse<UserRole>(roleString, out UserRole role))
            {
                _cachedRole = role;
                _cachedRoleRaw = roleString;
                return role;
            }
            _cachedRole = DefaultRole;
            _cachedRoleRaw = roleString;
            return DefaultRole;
        }

        public void SetUserRole(UserRole role)
        {
            EditorPrefs.SetString(RoleKey, role.ToString());
            _cachedRole = role;
            _cachedRoleRaw = role.ToString();
        }

        /// <summary>
        /// Clears the cached role so the next <see cref="GetUserRole"/> call
        /// re-reads from EditorPrefs. Useful for tests and after external
        /// changes to the EditorPrefs value.
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedRole = null;
            _cachedRoleRaw = null;
        }
    }
}



