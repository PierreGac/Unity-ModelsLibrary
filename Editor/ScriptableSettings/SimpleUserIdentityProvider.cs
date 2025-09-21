#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Identity
{
    public interface IUserIdentityProvider
    {
        string GetUserName();
        void SetUserName(string name);
    }

    /// <summary>
    /// Minimal identity provider backed by EditorPrefs.
    /// </summary>
    public class SimpleUserIdentityProvider : IUserIdentityProvider
    {
        private const string Key = "ModelLibrary.UserName";
        private const string DefaultName = "anonymous";

        public string GetUserName() => EditorPrefs.GetString(Key, DefaultName);
        public void SetUserName(string name) => EditorPrefs.SetString(Key, string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim());
    }
}
#endif


