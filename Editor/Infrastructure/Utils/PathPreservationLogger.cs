using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Centralized logging utility for path preservation debugging.
    /// Provides structured logging for path-related operations across the ModelLibrary system.
    /// </summary>
    public static class PathPreservationLogger
    {
        private const string __LOG_PREFIX = "[PathPreservation]";
        private const bool __ENABLE_DEBUG_LOGGING = true;

        /// <summary>
        /// Logs path resolution decisions with detailed context.
        /// </summary>
        /// <param name="operation">The operation being performed (e.g., "Import", "Deploy", "Validate")</param>
        /// <param name="modelName">Name of the model being processed</param>
        /// <param name="originalPath">Original path before processing</param>
        /// <param name="resolvedPath">Resolved path after processing</param>
        /// <param name="reason">Reason for the path resolution decision</param>
        public static void LogPathResolution(string operation, string modelName, string originalPath, string resolvedPath, string reason)
        {
            if (!__ENABLE_DEBUG_LOGGING)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} {operation} - Model: '{modelName}'");
            logMessage.AppendLine($"  Original Path: '{originalPath ?? "null"}'");
            logMessage.AppendLine($"  Resolved Path: '{resolvedPath ?? "null"}'");
            logMessage.AppendLine($"  Reason: {reason}");

            Debug.Log(logMessage.ToString());
        }

        /// <summary>
        /// Logs path validation results with detailed error information.
        /// </summary>
        /// <param name="operation">The operation being performed</param>
        /// <param name="modelName">Name of the model being processed</param>
        /// <param name="path">Path being validated</param>
        /// <param name="isValid">Whether the path is valid</param>
        /// <param name="errors">List of validation errors (if any)</param>
        public static void LogPathValidation(string operation, string modelName, string path, bool isValid, List<string> errors = null)
        {
            if (!__ENABLE_DEBUG_LOGGING)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} {operation} - Model: '{modelName}'");
            logMessage.AppendLine($"  Path: '{path ?? "null"}'");
            logMessage.AppendLine($"  Valid: {isValid}");

            if (errors != null && errors.Count > 0)
            {
                logMessage.AppendLine($"  Errors ({errors.Count}):");
                foreach (string error in errors)
                {
                    logMessage.AppendLine($"    - {error}");
                }
            }

            if (isValid)
            {
                Debug.Log(logMessage.ToString());
            }
            else
            {
                Debug.LogWarning(logMessage.ToString());
            }
        }

        /// <summary>
        /// Logs path sanitization operations.
        /// </summary>
        /// <param name="operation">The operation being performed</param>
        /// <param name="originalPath">Original path before sanitization</param>
        /// <param name="sanitizedPath">Path after sanitization</param>
        /// <param name="changes">List of changes made during sanitization</param>
        public static void LogPathSanitization(string operation, string originalPath, string sanitizedPath, List<string> changes = null)
        {
            if (!__ENABLE_DEBUG_LOGGING)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} {operation} - Path Sanitization");
            logMessage.AppendLine($"  Original: '{originalPath ?? "null"}'");
            logMessage.AppendLine($"  Sanitized: '{sanitizedPath ?? "null"}'");

            if (changes != null && changes.Count > 0)
            {
                logMessage.AppendLine($"  Changes ({changes.Count}):");
                foreach (string change in changes)
                {
                    logMessage.AppendLine($"    - {change}");
                }
            }

            Debug.Log(logMessage.ToString());
        }

        /// <summary>
        /// Logs path preservation workflow steps.
        /// </summary>
        /// <param name="workflowStep">The step in the workflow</param>
        /// <param name="modelName">Name of the model being processed</param>
        /// <param name="details">Additional details about the step</param>
        public static void LogWorkflowStep(string workflowStep, string modelName, string details = null)
        {
            if (!__ENABLE_DEBUG_LOGGING)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} Workflow - {workflowStep}");
            logMessage.AppendLine($"  Model: '{modelName}'");

            if (!string.IsNullOrEmpty(details))
            {
                logMessage.AppendLine($"  Details: {details}");
            }

            Debug.Log(logMessage.ToString());
        }

        /// <summary>
        /// Logs path preservation errors with full context.
        /// </summary>
        /// <param name="operation">The operation that failed</param>
        /// <param name="modelName">Name of the model being processed</param>
        /// <param name="error">The error that occurred</param>
        /// <param name="context">Additional context about the error</param>
        public static void LogPathError(string operation, string modelName, Exception error, string context = null)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} ERROR - {operation}");
            logMessage.AppendLine($"  Model: '{modelName}'");
            logMessage.AppendLine($"  Error: {error?.Message ?? "Unknown error"}");

            if (!string.IsNullOrEmpty(context))
            {
                logMessage.AppendLine($"  Context: {context}");
            }

            if (error != null)
            {
                logMessage.AppendLine($"  Stack Trace: {error.StackTrace}");
            }

            Debug.LogError(logMessage.ToString());
        }

        /// <summary>
        /// Logs path preservation warnings with context.
        /// </summary>
        /// <param name="operation">The operation that generated the warning</param>
        /// <param name="modelName">Name of the model being processed</param>
        /// <param name="warning">The warning message</param>
        /// <param name="context">Additional context about the warning</param>
        public static void LogPathWarning(string operation, string modelName, string warning, string context = null)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} WARNING - {operation}");
            logMessage.AppendLine($"  Model: '{modelName}'");
            logMessage.AppendLine($"  Warning: {warning}");

            if (!string.IsNullOrEmpty(context))
            {
                logMessage.AppendLine($"  Context: {context}");
            }

            Debug.LogWarning(logMessage.ToString());
        }

        /// <summary>
        /// Logs a summary of path preservation operations for a model.
        /// </summary>
        /// <param name="modelName">Name of the model</param>
        /// <param name="operations">List of operations performed</param>
        /// <param name="finalPaths">Final resolved paths</param>
        public static void LogPathSummary(string modelName, List<string> operations, Dictionary<string, string> finalPaths)
        {
            if (!__ENABLE_DEBUG_LOGGING)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{__LOG_PREFIX} SUMMARY - Model: '{modelName}'");
            logMessage.AppendLine($"  Operations Performed ({operations.Count}):");

            foreach (string operation in operations)
            {
                logMessage.AppendLine($"    - {operation}");
            }

            logMessage.AppendLine($"  Final Paths ({finalPaths.Count}):");
            foreach (KeyValuePair<string, string> kvp in finalPaths)
            {
                logMessage.AppendLine($"    {kvp.Key}: '{kvp.Value}'");
            }

            Debug.Log(logMessage.ToString());
        }
    }
}
