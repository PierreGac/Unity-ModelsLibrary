using System;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for version comparison and upgrade detection.
    /// </summary>
    public static class ModelVersionUtils
    {
        /// <summary>
        /// Determines if a local model version needs to be upgraded to the remote version.
        /// Uses semantic versioning comparison when possible, falls back to string comparison.
        /// Returns false for unknown local versions to prevent false "update available" messages.
        /// </summary>
        /// <param name="localVersion">The version currently installed locally.</param>
        /// <param name="remoteVersion">The latest version available in the repository.</param>
        /// <returns>True if the remote version is newer than the local version, false otherwise.</returns>
        public static bool NeedsUpgrade(string localVersion, string remoteVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion))
            {
                return false;
            }
            if (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)")
            {
                // Don't show as needing upgrade if we can't determine the local version
                return false;
            }
            if (SemVer.TryParse(localVersion, out SemVer local) && SemVer.TryParse(remoteVersion, out SemVer remote))
            {
                return remote.CompareTo(local) > 0;
            }
            return !string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}

