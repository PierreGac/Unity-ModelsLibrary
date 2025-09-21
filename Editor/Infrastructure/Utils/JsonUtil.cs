
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for JSON serialization/deserialization using Unity's JsonUtility.
    /// This provides a consistent interface for converting our data models to/from JSON format.
    /// Unity's JsonUtility is fast and lightweight, but has some limitations compared to other JSON libraries.
    /// </summary>
    public static class JsonUtil
    {
        /// <summary>
        /// Convert an object to JSON string format.
        /// Uses pretty printing (indented) for better readability in files.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to convert to JSON</param>
        /// <returns>JSON string representation of the object</returns>
        public static string ToJson<T>(T obj) => JsonUtility.ToJson(obj, prettyPrint: true);

        /// <summary>
        /// Convert a JSON string back to an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize to</typeparam>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>Object of type T, or default(T) if parsing fails</returns>
        public static T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}



