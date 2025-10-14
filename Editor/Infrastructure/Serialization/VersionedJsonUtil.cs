using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelLibrary.Editor.Serialization
{
    /// <summary>
    /// Enhanced JSON utility that supports versioned deserialization with migration and fallback handling.
    /// This provides robust deserialization that can handle schema changes gracefully.
    /// </summary>
    public static class VersionedJsonUtil
    {
        /// <summary>
        /// Deserialize JSON with version handling and migration support.
        /// This method attempts to deserialize the JSON and automatically migrates it if needed.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to parse</param>
        /// <param name="migrate">Whether to attempt migration if deserialization fails</param>
        /// <returns>Deserialized object or default(T) if all attempts fail</returns>
        public static T FromJsonWithMigration<T>(string json, bool migrate = true) where T : class, new()
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("VersionedJsonUtil: Attempting to deserialize null or empty JSON");
                return default(T);
            }

            try
            {
                // First attempt: Direct deserialization
                T result = JsonUtility.FromJson<T>(json);
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: Direct deserialization failed: {ex.Message}");
            }

            if (!migrate)
            {
                return default(T);
            }

            // Second attempt: Try with migration for ModelMeta specifically
            if (typeof(T) == typeof(Data.ModelMeta))
            {
                return TryDeserializeWithMigration(json) as T;
            }

            // Third attempt: Try deserializing as generic object and manually mapping
            return TryDeserializeWithFallback<T>(json);
        }

        /// <summary>
        /// Deserialize ModelMeta with specific migration handling.
        /// </summary>
        private static Data.ModelMeta TryDeserializeWithMigration(string json)
        {
            try
            {
                // Try to deserialize as-is first
                Data.ModelMeta modelMeta = JsonUtility.FromJson<Data.ModelMeta>(json);
                if (modelMeta != null)
                {
                    // Apply migration if needed
                    if (ModelMetaMigration.MigrateToCurrentVersion(ref modelMeta))
                    {
                        return modelMeta;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: ModelMeta deserialization failed: {ex.Message}");
            }

            // Fallback: Try to extract basic information even if full deserialization fails
            return TryDeserializeModelMetaFallback(json);
        }

        /// <summary>
        /// Fallback deserialization for ModelMeta that extracts available fields even if some are missing.
        /// </summary>
        private static Data.ModelMeta TryDeserializeModelMetaFallback(string json)
        {
            try
            {
                Data.ModelMeta modelMeta = new Data.ModelMeta();

                // Try to extract basic fields using simple JSON parsing
                string jsonLower = json.ToLower();

                // Extract version if present
                if (TryExtractStringValue(json, "version", out string version))
                {
                    modelMeta.version = version;
                }

                if (TryExtractStringValue(json, "description", out string description))
                {
                    modelMeta.description = description;
                }

                if (TryExtractStringValue(json, "author", out string author))
                {
                    modelMeta.author = author;
                }

                if (TryExtractStringValue(json, "installPath", out string installPath))
                {
                    modelMeta.installPath = installPath;
                }

                if (TryExtractStringValue(json, "relativePath", out string relativePath))
                {
                    modelMeta.relativePath = relativePath;
                }

                if (TryExtractStringValue(json, "previewImagePath", out string previewImagePath))
                {
                    modelMeta.previewImagePath = previewImagePath;
                }

                // Extract numeric values
                if (TryExtractLongValue(json, "createdTimeTicks", out long createdTimeTicks))
                {
                    modelMeta.createdTimeTicks = createdTimeTicks;
                }

                if (TryExtractLongValue(json, "updatedTimeTicks", out long updatedTimeTicks))
                {
                    modelMeta.updatedTimeTicks = updatedTimeTicks;
                }

                if (TryExtractLongValue(json, "uploadTimeTicks", out long uploadTimeTicks))
                {
                    modelMeta.uploadTimeTicks = uploadTimeTicks;
                }

                if (TryExtractIntValue(json, "vertexCount", out int vertexCount))
                {
                    modelMeta.vertexCount = vertexCount;
                }

                if (TryExtractIntValue(json, "triangleCount", out int triangleCount))
                {
                    modelMeta.triangleCount = triangleCount;
                }

                // Initialize collections
                modelMeta.payloadRelativePaths = new List<string>();
                modelMeta.materials = new List<Data.AssetRef>();
                modelMeta.textures = new List<Data.AssetRef>();
                modelMeta.assetGuids = new List<string>();
                modelMeta.imageRelativePaths = new List<string>();
                modelMeta.projectTags = new List<string>();
                modelMeta.notes = new List<Data.ModelNote>();
                modelMeta.dependencies = new List<string>();
                modelMeta.dependenciesDetailed = new List<Data.DependencyRef>();
                modelMeta.extra = new Dictionary<string, string>();
                modelMeta.modelImporters = new Dictionary<string, Data.ModelImporterSettings>();
                modelMeta.changelog = new List<Data.ModelChangelogEntry>();
                modelMeta.identity = new Data.ModelIdentity();
                modelMeta.tags = new Data.Tags();

                // Set schema version to current
                modelMeta.schemaVersion = ModelMetaMigration.CURRENT_SCHEMA_VERSION;

                Debug.LogWarning("VersionedJsonUtil: Used fallback deserialization for ModelMeta - some fields may be missing");
                return modelMeta;
            }
            catch (Exception ex)
            {
                Debug.LogError($"VersionedJsonUtil: Fallback deserialization failed: {ex.Message}");
                return new Data.ModelMeta(); // Return empty instance as last resort
            }
        }

        /// <summary>
        /// Generic fallback deserialization for other types.
        /// </summary>
        private static T TryDeserializeWithFallback<T>(string json) where T : class, new()
        {
            try
            {
                // Create a new instance and try to populate it manually
                T result = new T();
                Debug.LogWarning($"VersionedJsonUtil: Using fallback deserialization for {typeof(T).Name}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"VersionedJsonUtil: Fallback deserialization failed for {typeof(T).Name}: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Try to extract a string value from JSON using simple parsing.
        /// </summary>
        private static bool TryExtractStringValue(string json, string fieldName, out string value)
        {
            value = null;
            try
            {
                string pattern = $"\"{fieldName}\"\\s*:\\s*\"([^\"]*)\"";
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                if (match.Success)
                {
                    value = match.Groups[1].Value;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: Failed to extract string value for {fieldName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Try to extract a long value from JSON using simple parsing.
        /// </summary>
        private static bool TryExtractLongValue(string json, string fieldName, out long value)
        {
            value = 0;
            try
            {
                string pattern = $"\"{fieldName}\"\\s*:\\s*(\\d+)";
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                if (match.Success && long.TryParse(match.Groups[1].Value, out value))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: Failed to extract long value for {fieldName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Try to extract an int value from JSON using simple parsing.
        /// </summary>
        private static bool TryExtractIntValue(string json, string fieldName, out int value)
        {
            value = 0;
            try
            {
                string pattern = $"\"{fieldName}\"\\s*:\\s*(\\d+)";
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                if (match.Success && int.TryParse(match.Groups[1].Value, out value))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: Failed to extract int value for {fieldName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Standard JSON serialization with pretty printing.
        /// </summary>
        public static string ToJson<T>(T obj) => JsonUtility.ToJson(obj, prettyPrint: true);

        /// <summary>
        /// Standard JSON deserialization (for backward compatibility).
        /// </summary>
        public static T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}
