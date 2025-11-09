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

            // Also log to persistent error log
            ErrorLogger.LogError(title, message, category, exception);

            // Build the full message with guidance
            string fullMessage = BuildErrorMessage(message, category, exception);

            // Show dialog with or without retry option
            if (showRetry && retryAction != null)
            {
                // Use custom dialog window for better UX with retry and "don't show again"
                Windows.ErrorDialogWindow.Show(title, fullMessage, category, exception, retryAction);
                return false; // Custom window handles retry internally
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
            // Translate technical error messages to user-friendly language
            string userFriendlyMessage = TranslateErrorMessage(message, exception);
            
            string guidance = GetCategoryGuidance(category, exception);
            
            string fullMessage = userFriendlyMessage;
            if (!string.IsNullOrEmpty(guidance))
            {
                fullMessage += $"\n\nWhat you can try:\n{guidance}";
            }

            return fullMessage;
        }

        /// <summary>
        /// Translates technical error messages into user-friendly language.
        /// </summary>
        private static string TranslateErrorMessage(string message, Exception exception)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "An unexpected error occurred.";
            }

            string lowerMessage = message.ToLowerInvariant();
            string exceptionType = exception?.GetType().Name ?? "";

            // Connection errors
            if (lowerMessage.Contains("connection") || lowerMessage.Contains("timeout") || 
                lowerMessage.Contains("unreachable") || lowerMessage.Contains("network") ||
                exceptionType.Contains("Http") || exceptionType.Contains("Web"))
            {
                if (lowerMessage.Contains("timeout"))
                {
                    return "The connection to the repository timed out. The server may be slow or unavailable.";
                }
                if (lowerMessage.Contains("unreachable") || lowerMessage.Contains("could not be reached"))
                {
                    return "Unable to reach the repository server. Please check your network connection and repository settings.";
                }
                return "Unable to connect to the repository. Please verify your network connection and repository location.";
            }

            // File system errors
            if (lowerMessage.Contains("file not found") || lowerMessage.Contains("directory not found") ||
                exceptionType.Contains("FileNotFound") || exceptionType.Contains("DirectoryNotFound"))
            {
                if (lowerMessage.Contains("model") || lowerMessage.Contains("meta"))
                {
                    return "The model file or metadata could not be found. It may have been moved or deleted from the repository.";
                }
                return "A required file or folder could not be found. Please check that all files are in their expected locations.";
            }

            if (lowerMessage.Contains("access denied") || lowerMessage.Contains("permission") ||
                exceptionType.Contains("UnauthorizedAccess"))
            {
                return "You don't have permission to access this file or folder. Please check file permissions or contact your administrator.";
            }

            if (lowerMessage.Contains("disk") || lowerMessage.Contains("space") || lowerMessage.Contains("full"))
            {
                return "There isn't enough disk space to complete this operation. Please free up some space and try again.";
            }

            if (lowerMessage.Contains("path") && (lowerMessage.Contains("invalid") || lowerMessage.Contains("too long")))
            {
                return "The file path is invalid or too long. Please choose a different location or shorten the path.";
            }

            // Validation errors
            if (lowerMessage.Contains("invalid") || lowerMessage.Contains("validation") ||
                lowerMessage.Contains("required") || lowerMessage.Contains("missing"))
            {
                if (lowerMessage.Contains("version") || lowerMessage.Contains("semver"))
                {
                    return "The version format is invalid. Please use semantic versioning (e.g., 1.0.0).";
                }
                if (lowerMessage.Contains("name") || lowerMessage.Contains("model name"))
                {
                    return "The model name is invalid or already exists. Please choose a different name.";
                }
                return "Some required information is missing or invalid. Please check all fields and try again.";
            }

            // Import/Download specific errors
            if (lowerMessage.Contains("failed to download") || lowerMessage.Contains("download failed"))
            {
                return "The model could not be downloaded from the repository. Please check your connection and try again.";
            }

            if (lowerMessage.Contains("failed to import") || lowerMessage.Contains("import failed"))
            {
                return "The model could not be imported into your project. Please check for file conflicts or permission issues.";
            }

            if (lowerMessage.Contains("failed to update") || lowerMessage.Contains("update failed"))
            {
                return "The model could not be updated. Please check for file conflicts or permission issues.";
            }

            // Configuration errors
            if (lowerMessage.Contains("configuration") || lowerMessage.Contains("not configured") ||
                lowerMessage.Contains("settings"))
            {
                return "The Model Library is not properly configured. Please run the Configuration Wizard from the Settings menu.";
            }

            // If we can't translate it, return the original but make it more user-friendly
            // Remove technical jargon and make it clearer
            string cleaned = message;
            if (cleaned.Contains("Exception:"))
            {
                cleaned = cleaned.Substring(0, cleaned.IndexOf("Exception:")).Trim();
            }
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = "An unexpected error occurred while performing this operation.";
            }

            return cleaned;
        }

        /// <summary>
        /// Gets category-specific guidance for the user.
        /// </summary>
        private static string GetCategoryGuidance(ErrorCategory category, Exception exception = null)
        {
            string baseGuidance = category switch
            {
                ErrorCategory.Connection => "• Check your internet connection\n• Open Settings (Ctrl+,) and verify the Repository Root path or URL\n• Try using the 'Test Connection' button in Settings\n• If using a network drive, ensure it's accessible",
                ErrorCategory.FileSystem => "• Check that you have write permissions for the target folder\n• Ensure you have enough free disk space\n• Verify the file paths don't contain invalid characters\n• Try choosing a different installation location",
                ErrorCategory.Validation => "• Review all form fields for errors (highlighted in red)\n• Check that required fields are filled\n• Verify data formats match the requirements (e.g., version numbers)\n• Look for inline error messages below each field",
                ErrorCategory.Permission => "• Check file and folder permissions in Windows\n• Ensure you're not trying to write to a read-only location\n• Verify your user account has the necessary access\n• Contact your system administrator if needed",
                ErrorCategory.Configuration => "• Open Settings (Ctrl+,) and check Repository Settings\n• Run the Configuration Wizard from the Settings menu\n• Verify the Repository Root path or URL is correct\n• Test the connection using the 'Test Connection' button",
                _ => "• Check the Unity Console (Window > General > Console) for more details\n• Try refreshing the window (F5)\n• Restart Unity if the problem persists"
            };

            // Add specific guidance based on exception details
            if (exception != null)
            {
                string exceptionMessage = exception.Message?.ToLowerInvariant() ?? "";
                
                if (exceptionMessage.Contains("timeout"))
                {
                    baseGuidance += "\n• The server may be slow or overloaded - try again in a few moments";
                }
                else if (exceptionMessage.Contains("404") || exceptionMessage.Contains("not found"))
                {
                    baseGuidance += "\n• The model or file may have been removed from the repository";
                }
                else if (exceptionMessage.Contains("unauthorized") || exceptionMessage.Contains("401") || exceptionMessage.Contains("403"))
                {
                    baseGuidance += "\n• Your credentials may be incorrect or expired";
                    baseGuidance += "\n• Check your user role settings in Settings > User Settings";
                }
            }

            return baseGuidance;
        }

        /// <summary>
        /// Determines if exception details should be included in the user-facing message.
        /// Technical details are now only shown in the "Details" dialog, not in the main message.
        /// </summary>
        private static bool ShouldIncludeExceptionDetails(ErrorCategory category) =>
            // Don't include technical details in the main message - they're available in the Details dialog
            false;

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

