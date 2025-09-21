
using System.IO;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    public static class EditorPaths
    {
        public static string projectRoot => Application.dataPath[..^"/Assets".Length];
        public static string LibraryPath(string sub) => PathUtils.SanitizePathSeparator(Path.Combine(projectRoot, sub));
    }
}



