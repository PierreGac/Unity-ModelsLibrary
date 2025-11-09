using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Centralized error logging system for the Model Library.
    /// Logs errors to both Unity console and a persistent log file.
    /// </summary>
    public static class ErrorLogger
    {
        private const string LOG_FILE_NAME = "ModelLibrary_ErrorLog.txt";
        private const int MAX_LOG_ENTRIES = 1000; // Keep last 1000 entries
        private const int MAX_LOG_FILE_SIZE = 5 * 1024 * 1024; // 5 MB max file size

        private static string GetLogFilePath()
        {
            // Store log in Library folder (excluded from version control)
            string libraryPath = Path.Combine(Application.dataPath, "..", "Library", "ModelLibrary");
            if (!Directory.Exists(libraryPath))
            {
                Directory.CreateDirectory(libraryPath);
            }
            return Path.Combine(libraryPath, LOG_FILE_NAME);
        }

        /// <summary>
        /// Logs an error entry with full context.
        /// </summary>
        public static void LogError(string title, string message, ErrorHandler.ErrorCategory category, Exception exception = null, string context = null)
        {
            ErrorLogEntry entry = new ErrorLogEntry
            {
                Timestamp = DateTime.Now,
                Title = title ?? "Unknown Error",
                Message = message ?? "No message provided",
                Category = category,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception?.Message,
                StackTrace = exception?.StackTrace,
                Context = context
            };

            WriteLogEntry(entry);
        }

        /// <summary>
        /// Logs a simple error message.
        /// </summary>
        public static void LogError(string title, string message, Exception exception = null)
        {
            ErrorHandler.ErrorCategory category = exception != null 
                ? ErrorHandler.CategorizeException(exception) 
                : ErrorHandler.ErrorCategory.Unknown;
            LogError(title, message, category, exception);
        }

        /// <summary>
        /// Writes a log entry to the log file.
        /// </summary>
        private static void WriteLogEntry(ErrorLogEntry entry)
        {
            try
            {
                string logPath = GetLogFilePath();
                StringBuilder logLine = new StringBuilder();

                // Format: [Timestamp] [Category] Title: Message
                logLine.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] ");
                logLine.Append($"[{entry.Category}] ");
                logLine.Append($"{entry.Title}: {entry.Message}");

                if (!string.IsNullOrEmpty(entry.Context))
                {
                    logLine.Append($" | Context: {entry.Context}");
                }

                if (entry.ExceptionType != null)
                {
                    logLine.Append($" | Exception: {entry.ExceptionType}");
                }

                logLine.AppendLine();

                // Append to log file
                File.AppendAllText(logPath, logLine.ToString());

                // If file is too large, rotate it
                if (File.Exists(logPath))
                {
                    FileInfo fileInfo = new FileInfo(logPath);
                    if (fileInfo.Length > MAX_LOG_FILE_SIZE)
                    {
                        RotateLogFile(logPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to Unity console if file logging fails
                Debug.LogError($"[ModelLibrary ErrorLogger] Failed to write to log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Rotates the log file when it gets too large.
        /// </summary>
        private static void RotateLogFile(string logPath)
        {
            try
            {
                string backupPath = logPath + ".old";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(logPath, backupPath);

                // Read last N lines from backup and write to new log
                string[] allLines = File.ReadAllLines(backupPath);
                int keepLines = Math.Min(500, allLines.Length); // Keep last 500 lines
                string[] recentLines = allLines.Skip(allLines.Length - keepLines).ToArray();
                File.WriteAllLines(logPath, recentLines);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary ErrorLogger] Failed to rotate log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads all log entries from the log file.
        /// </summary>
        public static List<ErrorLogEntry> ReadLogEntries(int maxEntries = MAX_LOG_ENTRIES)
        {
            List<ErrorLogEntry> entries = new List<ErrorLogEntry>();
            string logPath = GetLogFilePath();

            if (!File.Exists(logPath))
            {
                return entries;
            }

            try
            {
                string[] lines = File.ReadAllLines(logPath);
                
                // Parse each line (simple format: [Timestamp] [Category] Title: Message | ...)
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    ErrorLogEntry entry = ParseLogLine(line);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }

                // Return most recent entries first
                return entries.OrderByDescending(e => e.Timestamp).Take(maxEntries).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary ErrorLogger] Failed to read log file: {ex.Message}");
                return entries;
            }
        }

        /// <summary>
        /// Parses a single log line into an ErrorLogEntry.
        /// </summary>
        private static ErrorLogEntry ParseLogLine(string line)
        {
            try
            {
                ErrorLogEntry entry = new ErrorLogEntry();

                // Extract timestamp [yyyy-MM-dd HH:mm:ss]
                int timestampEnd = line.IndexOf(']');
                if (timestampEnd > 0)
                {
                    string timestampStr = line.Substring(1, timestampEnd - 1);
                    if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                    {
                        entry.Timestamp = timestamp;
                    }
                }

                // Extract category [Category]
                int categoryStart = line.IndexOf('[', timestampEnd + 1);
                int categoryEnd = line.IndexOf(']', categoryStart + 1);
                if (categoryStart > 0 && categoryEnd > categoryStart)
                {
                    string categoryStr = line.Substring(categoryStart + 1, categoryEnd - categoryStart - 1);
                    if (Enum.TryParse<ErrorHandler.ErrorCategory>(categoryStr, out ErrorHandler.ErrorCategory category))
                    {
                        entry.Category = category;
                    }
                }

                // Extract title and message (everything after "] ")
                int messageStart = line.IndexOf("] ", categoryEnd) + 2;
                if (messageStart > 0 && messageStart < line.Length)
                {
                    string remainder = line.Substring(messageStart);
                    
                    // Split by " | " to get parts
                    string[] parts = remainder.Split(new[] { " | " }, StringSplitOptions.None);
                    string titleAndMessage = parts[0];

                    // Extract title (before ": ") and message (after ": ")
                    int colonIndex = titleAndMessage.IndexOf(": ");
                    if (colonIndex > 0)
                    {
                        entry.Title = titleAndMessage.Substring(0, colonIndex);
                        entry.Message = titleAndMessage.Substring(colonIndex + 2);
                    }
                    else
                    {
                        entry.Title = titleAndMessage;
                        entry.Message = "";
                    }

                    // Parse additional fields from parts
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string part = parts[i];
                        if (part.StartsWith("Context: "))
                        {
                            entry.Context = part.Substring(9);
                        }
                        else if (part.StartsWith("Exception: "))
                        {
                            entry.ExceptionType = part.Substring(11);
                        }
                    }
                }

                return entry;
            }
            catch
            {
                // If parsing fails, create a basic entry
                return new ErrorLogEntry
                {
                    Timestamp = DateTime.Now,
                    Title = "Parse Error",
                    Message = line,
                    Category = ErrorHandler.ErrorCategory.Unknown
                };
            }
        }

        /// <summary>
        /// Clears the log file.
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                string logPath = GetLogFilePath();
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary ErrorLogger] Failed to clear log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the log file path for display to the user.
        /// </summary>
        public static string GetLogFilePathForDisplay() => GetLogFilePath();
    }

    /// <summary>
    /// Represents a single error log entry.
    /// </summary>
    [Serializable]
    public class ErrorLogEntry
    {
        public DateTime Timestamp;
        public string Title;
        public string Message;
        public ErrorHandler.ErrorCategory Category;
        public string ExceptionType;
        public string ExceptionMessage;
        public string StackTrace;
        public string Context;
    }
}

