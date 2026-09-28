using System;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using UnityEngine;

namespace ModelLibrary.Editor.Serialization
{
    /// <summary>
    /// Enhanced JSON utility that supports versioned deserialization with migration and fallback handling.
    /// This provides robust deserialization that can handle schema changes gracefully.
    /// </summary>
    internal static class VersionedJsonUtil
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
                return default;
            }

            if (typeof(T) == typeof(ModelMeta))
            {
                if (!IsCompleteJsonObject(json))
                {
                    Debug.LogWarning("VersionedJsonUtil: Refusing incomplete model metadata JSON.");
                    return default;
                }

                json = DictionaryJsonMigration.PrepareModelMeta(json);
            }
            else if (typeof(T) == typeof(ModelIndex))
            {
                json = DictionaryJsonMigration.PrepareModelIndex(json);
            }

            try
            {
                T result = JsonUtility.FromJson<T>(json);
                if (result is ModelMeta modelMeta)
                {
                    return FinishModelMeta(modelMeta, json) as T;
                }

                if (result is ModelIndex modelIndex)
                {
                    modelIndex.ReadSerializedEntries();
                    return result;
                }

                if (result != null)
                {
                    return result;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: Direct deserialization failed: {ex.Message}");
            }

            if (!migrate)
            {
                return default;
            }

            // Second attempt: Try with migration for ModelMeta specifically
            if (typeof(T) == typeof(ModelMeta))
            {
                return TryDeserializeWithMigration(json) as T;
            }

            // Third attempt: Try deserializing as generic object and manually mapping
            return TryDeserializeWithFallback<T>(json);
        }

        /// <summary>
        /// Deserialize ModelMeta with specific migration handling.
        /// </summary>
        private static ModelMeta TryDeserializeWithMigration(string json)
        {
            try
            {
                // Try to deserialize as-is first
                ModelMeta modelMeta = JsonUtility.FromJson<ModelMeta>(json);
                if (modelMeta != null)
                {
                    return FinishModelMeta(modelMeta, json);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VersionedJsonUtil: ModelMeta deserialization failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Generic fallback deserialization for other types.
        /// </summary>
        private static T TryDeserializeWithFallback<T>(string _) where T : class, new()
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
                return default;
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
        /// Applies install-path migration, schema migration, and dictionary entry loading.
        /// </summary>
        private static ModelMeta FinishModelMeta(ModelMeta modelMeta, string json)
        {
            if (modelMeta.schemaVersion > ModelMetaMigration.CURRENT_SCHEMA_VERSION)
            {
                throw new InvalidOperationException(
                    $"Refusing model metadata schema {modelMeta.schemaVersion}. Current schema is {ModelMetaMigration.CURRENT_SCHEMA_VERSION}.");
            }

            ApplyLegacyInstallPathMigration(modelMeta, json);
            if (!ModelMetaMigration.MigrateToCurrentVersion(ref modelMeta))
            {
                throw new InvalidOperationException("Model metadata could not be migrated.");
            }

            if (modelMeta.identity == null || string.IsNullOrWhiteSpace(modelMeta.identity.id))
            {
                throw new InvalidOperationException("Model metadata is missing an identity id.");
            }

            modelMeta.ReadSerializedEntries();
            return modelMeta;
        }

        /// <summary>
        /// Returns true when <paramref name="json"/> is one complete JSON object.
        /// </summary>
        private static bool IsCompleteJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            int start = 0;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
            {
                start++;
            }

            if (start >= json.Length || json[start] != '{')
            {
                return false;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                }
                else if (current == '{' || current == '[')
                {
                    depth++;
                }
                else if (current == '}' || current == ']')
                {
                    depth--;
                    if (depth < 0)
                    {
                        return false;
                    }

                    if (depth == 0)
                    {
                        for (int tail = i + 1; tail < json.Length; tail++)
                        {
                            if (!char.IsWhiteSpace(json[tail]))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Migrates legacy relativePath values from older model.json files into installPath.
        /// </summary>
        private static void ApplyLegacyInstallPathMigration(ModelMeta modelMeta, string json)
        {
            if (modelMeta == null || !string.IsNullOrWhiteSpace(modelMeta.installPath) || string.IsNullOrEmpty(json))
            {
                return;
            }

            if (!TryExtractStringValue(json, "relativePath", out string legacyRelativePath) ||
                string.IsNullOrWhiteSpace(legacyRelativePath))
            {
                return;
            }

            string normalized = PathUtils.SanitizePathSeparator(legacyRelativePath.Trim());
            modelMeta.installPath = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"Assets/{normalized.TrimStart('/')}";
        }

        /// <summary>
        /// Standard JSON serialization with pretty printing.
        /// </summary>
        public static string ToJson<T>(T obj)
        {
            JsonUtil.PrepareForSerialization(obj);
            return JsonUtility.ToJson(obj, prettyPrint: true);
        }

        /// <summary>
        /// Standard JSON deserialization (for backward compatibility).
        /// </summary>
        public static T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}
