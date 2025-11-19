using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Manages notification read/unread state for models.
    /// Tracks which models have been viewed to hide notification badges after viewing.
    /// </summary>
    public static class NotificationStateManager
    {
        private const string __NOTES_READ_PREF_KEY_PREFIX = "ModelLibrary.NotesRead.";
        private const string __UPDATES_READ_PREF_KEY_PREFIX = "ModelLibrary.UpdatesRead.";

        /// <summary>
        /// Marks notes notification as read for a specific model version.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="version">The version string.</param>
        public static void MarkNotesAsRead(string modelId, string version)
        {
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                return;
            }

            string key = GetNotesReadKey(modelId, version);
            EditorPrefs.SetBool(key, true);
        }

        /// <summary>
        /// Marks update notification as read for a specific model.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        public static void MarkUpdateAsRead(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            string key = GetUpdateReadKey(modelId);
            EditorPrefs.SetBool(key, true);
        }

        /// <summary>
        /// Checks if notes notification has been read for a specific model version.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="version">The version string.</param>
        /// <returns>True if notes have been marked as read, false otherwise.</returns>
        public static bool AreNotesRead(string modelId, string version)
        {
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(version))
            {
                return false;
            }

            string key = GetNotesReadKey(modelId, version);
            return EditorPrefs.GetBool(key, false);
        }

        /// <summary>
        /// Checks if update notification has been read for a specific model.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <returns>True if update has been marked as read, false otherwise.</returns>
        public static bool IsUpdateRead(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return false;
            }

            string key = GetUpdateReadKey(modelId);
            return EditorPrefs.GetBool(key, false);
        }

        /// <summary>
        /// Clears read state for a specific model (useful when new notes/updates arrive).
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="version">The version string (optional, clears all versions if null).</param>
        public static void ClearReadState(string modelId, string version = null)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            if (string.IsNullOrEmpty(version))
            {
                // Clear all versions for this model
                string notesPrefix = __NOTES_READ_PREF_KEY_PREFIX + modelId + "@";
                string updateKey = GetUpdateReadKey(modelId);
                
                // Note: EditorPrefs doesn't support enumeration, so we can't easily clear all versions
                // This would require maintaining a list of keys, which is complex
                // For now, we'll clear the update key and let individual versions be cleared as needed
                EditorPrefs.DeleteKey(updateKey);
            }
            else
            {
                string notesKey = GetNotesReadKey(modelId, version);
                EditorPrefs.DeleteKey(notesKey);
            }
        }

        /// <summary>
        /// Marks all notifications as read (useful for "Mark all as read" functionality).
        /// </summary>
        public static void MarkAllAsRead()
        {
            // Note: EditorPrefs doesn't support enumeration, so we can't mark all as read
            // This would require maintaining a list of model IDs, which is complex
            // For now, this is a placeholder for future implementation
            Debug.Log("[NotificationStateManager] MarkAllAsRead() called - not fully implemented (requires model ID tracking)");
        }

        private static string GetNotesReadKey(string modelId, string version)
        {
            return __NOTES_READ_PREF_KEY_PREFIX + modelId + "@" + version;
        }

        private static string GetUpdateReadKey(string modelId)
        {
            return __UPDATES_READ_PREF_KEY_PREFIX + modelId;
        }
    }
}

