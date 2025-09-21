
using System;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Minimal Semantic Versioning (SemVer) implementation supporting MAJOR.MINOR.PATCH format.
    /// This is used to compare model versions and determine which is newer.
    /// Does not implement prerelease/build metadata - just the core version numbers.
    /// 
    /// SemVer rules:
    /// - MAJOR: Increment for breaking changes (incompatible API changes)
    /// - MINOR: Increment for new features (backward compatible)
    /// - PATCH: Increment for bug fixes (backward compatible)
    /// 
    /// Examples: "1.0.0", "1.2.3", "2.0.0"
    /// </summary>
    public readonly struct SemVer : IComparable<SemVer>
    {
        /// <summary>
        /// Major version number - incremented for breaking changes.
        /// </summary>
        public readonly int Major;

        /// <summary>
        /// Minor version number - incremented for new features.
        /// </summary>
        public readonly int Minor;

        /// <summary>
        /// Patch version number - incremented for bug fixes.
        /// </summary>
        public readonly int Patch;

        /// <summary>
        /// Create a new SemVer with the specified version numbers.
        /// </summary>
        /// <param name="major">Major version number</param>
        /// <param name="minor">Minor version number</param>
        /// <param name="patch">Patch version number</param>
        public SemVer(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Try to parse a version string into a SemVer struct.
        /// </summary>
        /// <param name="s">Version string to parse (e.g., "1.2.3")</param>
        /// <param name="v">Output SemVer if parsing succeeds</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParse(string s, out SemVer v)
        {
            // Initialize with default values
            v = default;

            // Check for null or empty string
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            // Split by dots to get the three parts
            string[] parts = s.Split('.');
            if (parts.Length < 3)
            {
                return false; // Need at least 3 parts
            }

            // Try to parse each part as an integer
            if (int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int patch))
            {
                v = new SemVer(major, minor, patch);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Convert the SemVer back to a string format.
        /// </summary>
        /// <returns>Version string in MAJOR.MINOR.PATCH format</returns>
        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Compare this version with another version.
        /// Returns: -1 if this is older, 0 if equal, 1 if this is newer.
        /// </summary>
        /// <param name="other">The other version to compare with</param>
        /// <returns>Comparison result</returns>
        public int CompareTo(SemVer other)
        {
            // Compare major version first
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }

            // If major is equal, compare minor version
            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }

            // If minor is also equal, compare patch version
            return Patch.CompareTo(other.Patch);
        }
    }
}



