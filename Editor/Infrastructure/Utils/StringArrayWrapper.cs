using System;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// A <c>[Serializable]</c> wrapper around <c>string[]</c> for use with
    /// Unity's <c>JsonUtility</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// STABILITY (audit CRIT-10): <c>JsonUtility</c> cannot serialize or
    /// deserialize a top-level <c>string[]</c> — it silently returns
    /// <c>null</c> and writes <c>"[]"</c> regardless of contents. This
    /// caused favorites, recently-used, and search-history persistence to
    /// be 100% broken (data was lost on every editor restart).
    /// </para>
    /// <para>
    /// Wrapping the array in a <c>[Serializable]</c> class works around the
    /// limitation: <c>JsonUtility</c> serializes this class as
    /// <c>{"values":["id1","id2",...]}</c>, which round-trips correctly.
    /// </para>
    /// <para>
    /// All persistence code that previously called
    /// <c>JsonUtility.FromJson&lt;string[]&gt;</c> or
    /// <c>JsonUtility.ToJson(stringArr)</c> should use this wrapper instead.
    /// </para>
    /// </remarks>
    [Serializable]
    public class StringArrayWrapper
    {
        /// <summary>The string array to serialize.</summary>
        public string[] values;
    }
}
