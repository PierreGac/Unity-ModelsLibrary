using System.Collections.Generic;
using System.IO;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Filesystem enumeration helpers that protect against symlink / reparse-point traversal.
    /// </summary>
    /// <remarks>
    /// SECURITY (audit HIGH-04 + HIGH-09): <c>Directory.GetFiles</c> and
    /// <c>Directory.EnumerateFiles</c> with <c>SearchOption.AllDirectories</c>
    /// follow symlinks / reparse points by default. A malicious source
    /// folder containing a symlink to <c>C:\Windows\System32</c> (or
    /// <c>/etc</c>) would cause batch upload and manifest discovery to
    /// enumerate files outside the intended source — and potentially
    /// upload sensitive files into a model payload.
    ///
    /// This helper provides safe enumeration that skips reparse points.
    /// </remarks>
    public static class SafeFileEnumerator
    {
        /// <summary>
        /// Enumerates files under <paramref name="root"/> recursively,
        /// skipping any directory that has the <c>ReparsePoint</c> attribute
        /// (i.e. symlinks, junctions, and other reparse points).
        /// </summary>
        /// <param name="root">Root directory to enumerate.</param>
        /// <param name="pattern">File name pattern (e.g. <c>"*.json"</c> or <c>"*"</c>).</param>
        /// <returns>Sequence of absolute file paths that match the pattern, excluding any file reachable via a reparse point.</returns>
        public static IEnumerable<string> EnumerateFilesSafe(string root, string pattern = "*")
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                yield break;
            }

            Stack<string> stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                // SECURITY (HIGH-04): Skip directories that are themselves reparse points.
                // (We check the parent before pushing, but also check here for safety.)
                FileAttributes attrs;
                try
                {
                    attrs = File.GetAttributes(current);
                }
                catch
                {
                    continue;
                }
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                // Yield matching files in this directory (top-level only).
                string[] files;
                try
                {
                    files = Directory.GetFiles(current, pattern, SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    files = null;
                }
                if (files != null)
                {
                    foreach (string f in files)
                    {
                        yield return f;
                    }
                }

                // Push subdirectories (skipping reparse points).
                string[] subdirs;
                try
                {
                    subdirs = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    subdirs = null;
                }
                if (subdirs != null)
                {
                    foreach (string d in subdirs)
                    {
                        try
                        {
                            FileAttributes subAttrs = File.GetAttributes(d);
                            if ((subAttrs & FileAttributes.ReparsePoint) != 0)
                            {
                                // SECURITY (HIGH-04): Skip symlinked directories.
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                        stack.Push(d);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the given path has the <c>ReparsePoint</c> attribute
        /// (i.e. is a symlink, junction, or other reparse point).
        /// </summary>
        public static bool IsReparsePoint(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                FileAttributes attrs = File.GetAttributes(path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
