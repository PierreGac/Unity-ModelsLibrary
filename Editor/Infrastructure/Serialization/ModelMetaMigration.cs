using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelLibrary.Editor.Serialization
{
    /// <summary>
    /// Handles migration of ModelMeta data between different schema versions.
    /// This ensures backward compatibility when the data structure changes.
    /// </summary>
    public static class ModelMetaMigration
    {
        /// <summary>
        /// Current schema version - increment this when making breaking changes.
        /// </summary>
        public const int CURRENT_SCHEMA_VERSION = 1;

        /// <summary>
        /// Migrates a ModelMeta object from an older schema version to the current version.
        /// </summary>
        /// <param name="modelMeta">The ModelMeta object to migrate</param>
        /// <returns>True if migration was successful, false otherwise</returns>
        public static bool MigrateToCurrentVersion(ref Data.ModelMeta modelMeta)
        {
            if (modelMeta == null)
            {
                Debug.LogError("ModelMetaMigration: Cannot migrate null ModelMeta object");
                return false;
            }

            int currentVersion = modelMeta.schemaVersion;

            // If already at current version, no migration needed
            if (currentVersion >= CURRENT_SCHEMA_VERSION)
            {
                return true;
            }

            try
            {
                // Apply migrations in sequence
                for (int version = currentVersion; version < CURRENT_SCHEMA_VERSION; version++)
                {
                    if (!ApplyMigration(modelMeta, version, version + 1))
                    {
                        Debug.LogError($"ModelMetaMigration: Failed to migrate from version {version} to {version + 1}");
                        return false;
                    }
                }

                // Update to current schema version
                modelMeta.schemaVersion = CURRENT_SCHEMA_VERSION;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ModelMetaMigration: Exception during migration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a specific migration from one version to the next.
        /// </summary>
        /// <param name="modelMeta">The ModelMeta object to migrate</param>
        /// <param name="fromVersion">Source version</param>
        /// <param name="toVersion">Target version</param>
        /// <returns>True if migration was successful</returns>
        private static bool ApplyMigration(Data.ModelMeta modelMeta, int fromVersion, int toVersion)
        {
            switch (fromVersion)
            {
                case 0: // Migration from version 0 to 1
                    return MigrateFrom0To1(modelMeta);

                // Add more migration cases here as the schema evolves
                // case 1: return MigrateFrom1To2(modelMeta);
                // case 2: return MigrateFrom2To3(modelMeta);

                default:
                    Debug.LogWarning($"ModelMetaMigration: No migration defined from version {fromVersion} to {toVersion}");
                    return true; // Assume no migration needed
            }
        }

        /// <summary>
        /// Migration from schema version 0 to 1.
        /// This handles the initial addition of schemaVersion field and any other changes.
        /// </summary>
        private static bool MigrateFrom0To1(Data.ModelMeta modelMeta)
        {
            try
            {
                // Ensure all required collections are initialized
                if (modelMeta.payloadRelativePaths == null)
                    modelMeta.payloadRelativePaths = new List<string>();

                if (modelMeta.materials == null)
                    modelMeta.materials = new List<Data.AssetRef>();

                if (modelMeta.textures == null)
                    modelMeta.textures = new List<Data.AssetRef>();

                if (modelMeta.assetGuids == null)
                    modelMeta.assetGuids = new List<string>();

                if (modelMeta.imageRelativePaths == null)
                    modelMeta.imageRelativePaths = new List<string>();

                if (modelMeta.projectTags == null)
                    modelMeta.projectTags = new List<string>();

                if (modelMeta.notes == null)
                    modelMeta.notes = new List<Data.ModelNote>();

                if (modelMeta.dependencies == null)
                    modelMeta.dependencies = new List<string>();

                if (modelMeta.dependenciesDetailed == null)
                    modelMeta.dependenciesDetailed = new List<Data.DependencyRef>();

                if (modelMeta.extra == null)
                    modelMeta.extra = new Dictionary<string, string>();

                if (modelMeta.modelImporters == null)
                    modelMeta.modelImporters = new Dictionary<string, Data.ModelImporterSettings>();

                if (modelMeta.changelog == null)
                    modelMeta.changelog = new List<Data.ModelChangelogEntry>();

                // Initialize identity if null
                if (modelMeta.identity == null)
                    modelMeta.identity = new Data.ModelIdentity();

                // Initialize tags if null
                if (modelMeta.tags == null)
                    modelMeta.tags = new Data.Tags();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ModelMetaMigration: Error in MigrateFrom0To1: {ex.Message}");
                return false;
            }
        }
    }
}
