
using UnityEngine;
using ModelLibrary.Data;
using ModelLibrary.Editor.Serialization;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for JSON serialization/deserialization using Unity's JsonUtility.
    /// This provides a consistent interface for converting our data models to/from JSON format.
    /// Unity's JsonUtility is fast and lightweight, but has some limitations compared to other JSON libraries.
    /// 
    /// For robust deserialization that handles schema changes, use the versioned methods.
    /// </summary>
    public static class JsonUtil
    {
        /// <summary>
        /// Convert an object to JSON string format.
        /// Uses pretty printing (indented) for better readability in files.
        /// </summary>
        /// <typeparam name="t">The type of object to serialize</typeparam>
        /// <param name="obj">The object to convert to JSON</param>
        /// <returns>JSON string representation of the object</returns>
        public static string ToJson<T>(T obj) => JsonUtility.ToJson(obj, prettyPrint: true);

        /// <summary>
        /// Convert a JSON string back to an object of the specified type.
        /// This is the standard Unity JsonUtility deserialization.
        /// </summary>
        /// <typeparam name="t">The type of object to deserialize to</typeparam>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>Object of type T, or default(T) if parsing fails</returns>
        public static T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);

        /// <summary>
        /// Robust deserialization with version handling and migration support.
        /// Use this method when loading data that might have been saved with an older schema version.
        /// This method will automatically attempt migration if the standard deserialization fails.
        /// </summary>
        /// <typeparam name="t">The type of object to deserialize to</typeparam>
        /// <param name="json">The JSON string to parse</param>
        /// <param name="migrate">Whether to attempt migration if deserialization fails (default: true)</param>
        /// <returns>Object of type T, or default(T) if all attempts fail</returns>
        public static T FromJsonWithMigration<T>(string json, bool migrate = true) where T : class, new() => VersionedJsonUtil.FromJsonWithMigration<T>(json, migrate);

        /// <summary>
        /// Deserialize ModelMeta with automatic migration support.
        /// This is a convenience method specifically for ModelMeta objects.
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>ModelMeta object, or null if deserialization fails</returns>
        public static ModelMeta FromJsonModelMeta(string json) => FromJsonWithMigration<ModelMeta>(json);
    }
}



