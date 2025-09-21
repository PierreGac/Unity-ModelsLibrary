namespace ModelLibrary.Editor.Utils
{
    public static class PathUtils
    {
        public static string SanitizePathSeparator(string path) => path.Replace('\\', '/');
    }
}
