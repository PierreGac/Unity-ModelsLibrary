using System;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Centralized error handling utility for the Model Library.
    /// Provides consistent error reporting with actionable guidance and retry options.
    /// </summary>
    public static class ErrorHandler
    {
        /// <summary>
        /// Error categories for better error classification and user guidance.
        /// </summary>
        public enum ErrorCategory
        {
            /// <summary>Network or repository connection issues.</summary>
            Connection,
            /// <summary>File system or I/O errors.</summary>
            FileSystem,
            /// <summary>Data validation or format errors.</summary>
            Validation,
            /// <summary>Permission or access errors.</summary>
            Permission,
            /// <summary>Configuration or setup errors.</summary>
            Configuration,
            /// <summary>Unknown or unexpected errors.</summary>
            Unknown
        }

        /// <summary>
        /// Displays a user-friendly error dialog with actionable guidance.
        /// </summary>
        /// <param name="title">Title of the error dialog.</param>
        /// <param name="message">Main error message.</param>
        /// <param name="category">Error category for context-specific guidance.</param>
        /// <param name="exception">Optional exception for detailed logging.</param>
        /// <param name="showRetry">Whether to show a retry option (requires retryAction).</param>
        /// <param name="retryAction">Optional action to execute if user chooses to retry.</param>
        /// <returns>True if user chose to retry, false otherwise.</returns>
        public static bool ShowErrorDialog(string title, string message, ErrorCategory category = ErrorCategory.Unknown, Exception exception = null, bool showRetry = false, Action retryAction = null)
        {
            // Log the full exception details for debugging
            if (exception != null)
            {
                Debug.LogError($"[ModelLibrary] {title}: {message}\nException: {exception}");
            }
            else
            {
                Debug.LogError($"[ModelLibrary] {title}: {message}");
            }

            // Build the full message with guidance
            string fullMessage = BuildErrorMessage(message, category, exception);

            // Show dialog with or without retry option
            if (showRetry && retryAction != null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    title,
                    fullMessage,
                    "Retry",
                    "OK",
                    "Details");
                
                if (choice == 0) // Retry
                {
                    try
                    {
                        retryAction();
                        return true;
                    }
                    catch (Exception retryEx)
                    {
                        ShowErrorDialog(title, $"Retry failed: {retryEx.Message}", category, retryEx, false, null);
                    }
                }
                else if (choice == 2) // Details
                {
                    ShowDetailedErrorDialog(title, message, exception);
                }
                
                return false;
            }
            else
            {
                bool showDetails = EditorUtility.DisplayDialog(title, fullMessage, "OK", "Details");
                if (showDetails)
                {
                    ShowDetailedErrorDialog(title, message, exception);
                }
                return false;
            }
        }

        /// <summary>
        /// Builds a user-friendly error message with actionable guidance based on error category.
        /// </summary>
        private static string BuildErrorMessage(string message, ErrorCategory category, Exception exception)
        {
            string guidance = GetCategoryGuidance(category);
            
            string fullMessage = message;
            if (!string.IsNullOrEmpty(guidance))
            {
                fullMessage += $"\n\n{guidance}";
            }

            // Add exception details if available and relevant
            if (exception != null && ShouldIncludeExceptionDetails(category))
            {
                fullMessage += $"\n\nTechnical details: {exception.GetType().Name}";
                if (!string.IsNullOrEmpty(exception.Message) && exception.Message != message)
                {
                    fullMessage += $"\n{exception.Message}";
                }
            }

            return fullMessage;
        }

        /// <summary>
        /// Gets category-specific guidance for the user.
        /// </summary>
        private static string GetCategoryGuidance(ErrorCategory category)
        {
            return category switch
            {
                ErrorCategory.Connection => "• Check your network connection\n• Verify the repository location in settings\n• Ensure the repository server is accessible",
                ErrorCategory.FileSystem => "• Check file permissions\n• Ensure sufficient disk space\n• Verify the file paths are valid",
                ErrorCategory.Validation => "• Review the input data for errors\n• Check required fields are filled\n• Verify data format is correct",
                ErrorCategory.Permission => "• Check file/folder permissions\n• Verify user role has required access\n• Contact administrator if needed",
                ErrorCategory.Configuration => "• Run the configuration wizard\n• Check repository settings\n• Verify user settings are correct",
                _ => "• Check the Unity Console for more details\n• Try refreshing or restarting the operation"
            };
        }

        /// <summary>
        /// Determines if exception details should be included in the user-facing message.
        /// </summary>
        private static bool ShouldIncludeExceptionDetails(ErrorCategory category)
        {
            // Include details for technical errors that might be actionable
            return category == ErrorCategory.FileSystem || category == ErrorCategory.Validation;
        }

        /// <summary>
        /// Shows a detailed error dialog with full exception information.
        /// </summary>
        private static void ShowDetailedErrorDialog(string title, string message, Exception exception)
        {
            string details = $"Error: {message}";
            
            if (exception != null)
            {
                details += $"\n\nException Type: {exception.GetType().FullName}";
                details += $"\nMessage: {exception.Message}";
                if (exception.StackTrace != null)
                {
                    details += $"\n\nStack Trace:\n{exception.StackTrace}";
                }
                if (exception.InnerException != null)
                {
                    details += $"\n\nInner Exception:\n{exception.InnerException}";
                }
            }

            EditorUtility.DisplayDialog($"{title} - Details", details, "OK");
        }

        /// <summary>
        /// Categorizes an exception into an ErrorCategory.
        /// </summary>
        public static ErrorCategory CategorizeException(Exception exception)
        {
            if (exception == null)
            {
                return ErrorCategory.Unknown;
            }

            string exceptionType = exception.GetType().Name;
            string message = exception.Message != null ? exception.Message.ToLowerInvariant() : "";

            if (exceptionType.Contains("FileNotFound") || exceptionType.Contains("DirectoryNotFound") || 
                exceptionType.Contains("IOException") || exceptionType.Contains("UnauthorizedAccess"))
            {
                return ErrorCategory.FileSystem;
            }

            if (exceptionType.Contains("Http") || exceptionType.Contains("Web") || 
                exceptionType.Contains("Network") || message.Contains("connection") || 
                message.Contains("timeout") || message.Contains("unreachable"))
            {
                return ErrorCategory.Connection;
            }

            if (exceptionType.Contains("Argument") || exceptionType.Contains("Invalid") || 
                exceptionType.Contains("Validation") || message.Contains("invalid") || 
                message.Contains("required"))
            {
                return ErrorCategory.Validation;
            }

            if (exceptionType.Contains("Unauthorized") || exceptionType.Contains("Forbidden") || 
                message.Contains("permission") || message.Contains("access denied"))
            {
                return ErrorCategory.Permission;
            }

            if (message.Contains("configuration") || message.Contains("settings") || 
                message.Contains("not configured"))
            {
                return ErrorCategory.Configuration;
            }

            return ErrorCategory.Unknown;
        }

        /// <summary>
        /// Shows a simple error message with automatic categorization.
        /// </summary>
        public static void ShowError(string title, string message, Exception exception = null)
        {
            ErrorCategory category = exception != null ? CategorizeException(exception) : ErrorCategory.Unknown;
            ShowErrorDialog(title, message, category, exception, false, null);
        }

        /// <summary>
        /// Shows an error with retry option.
        /// </summary>
        public static bool ShowErrorWithRetry(string title, string message, Action retryAction, Exception exception = null)
        {
            ErrorCategory category = exception != null ? CategorizeException(exception) : ErrorCategory.Unknown;
            return ShowErrorDialog(title, message, category, exception, true, retryAction);
        }
    }
}

