using System;
using System.IO;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Safe local file writes for editor cache and repository download paths.
    /// Clears read-only attributes and replaces files atomically when possible.
    /// </summary>
    public static class SafeFileWriter
    {
        private const string TEMP_FILE_SUFFIX = ".tmp";

        /// <summary>
        /// Ensures a file path can be written by removing read-only attributes and deleting
        /// an existing file or directory at the same path.
        /// </summary>
        /// <param name="filePath">Absolute file path to prepare.</param>
        public static void PrepareWritableFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            string parentDirectory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, recursive: true);
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        /// <summary>
        /// Writes text to a file using a temporary file and atomic replace.
        /// </summary>
        /// <param name="filePath">Absolute destination file path.</param>
        /// <param name="contents">Text content to write.</param>
        public static void WriteAllText(string filePath, string contents)
        {
            WriteAllBytes(filePath, System.Text.Encoding.UTF8.GetBytes(contents ?? string.Empty));
        }

        /// <summary>
        /// Writes bytes to a file using a temporary file and atomic replace.
        /// </summary>
        /// <param name="filePath">Absolute destination file path.</param>
        /// <param name="contents">Bytes to write.</param>
        public static void WriteAllBytes(string filePath, byte[] contents)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (contents == null)
            {
                contents = Array.Empty<byte>();
            }

            string tempFilePath = filePath + TEMP_FILE_SUFFIX;
            PrepareWritableFile(tempFilePath);
            PrepareWritableFile(filePath);

            File.WriteAllBytes(tempFilePath, contents);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempFilePath, filePath);
        }

        /// <summary>
        /// Deletes a directory and its contents, clearing read-only attributes first.
        /// </summary>
        /// <param name="directoryPath">Absolute directory path to delete.</param>
        public static void DeleteDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                File.SetAttributes(files[i], FileAttributes.Normal);
            }

            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
