using System;
using System.IO;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Replaces a file only after a same-directory temporary file has been written and flushed.
    /// The temporary path is the destination plus <c>.tmp</c>, so both files are on the same volume
    /// for a local directory. An existing destination is then passed to
    /// <see cref="File.Replace(string, string, string, bool)"/>. That Windows call is not a
    /// documented atomic rename on every filesystem.
    /// On this machine, that replace succeeded for a local NTFS file and for
    /// <c>\\localhost\C$</c>. No separate file server was available, so a restricted SMB share
    /// remains unverified. If replace throws, the previous file is left in place and the write fails.
    /// </summary>
    internal static class SafeFileWriter
    {
        private const string TEMP_FILE_SUFFIX = ".tmp";
        private const int DISK_WRITE_BUFFER_BYTES = 4096;
        private const string EMPTY_PATH_MESSAGE = "File path cannot be null or empty.";

        /// <summary>
        /// Invoked after the temporary file is flushed and before the destination is changed.
        /// Tests use this to stop in that window. Production leaves it unset, and tests clear it.
        /// </summary>
        internal static Action BeforeDestinationReplace;

        /// <summary>
        /// Ensures a file path can be written by removing read-only attributes and deleting
        /// an existing file or directory at the same path.
        /// </summary>
        /// <param name="filePath">Absolute file path to prepare.</param>
        public static void PrepareWritableFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(EMPTY_PATH_MESSAGE, nameof(filePath));
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
        /// Writes text to a file after flushing a same-directory temporary file.
        /// </summary>
        /// <param name="filePath">Absolute destination file path.</param>
        /// <param name="contents">Text content to write.</param>
        public static void WriteAllText(string filePath, string contents)
        {
            WriteAllBytes(filePath, System.Text.Encoding.UTF8.GetBytes(contents ?? string.Empty));
        }

        /// <summary>
        /// Writes bytes to a file after flushing a same-directory temporary file.
        /// The destination is replaced only after that flush succeeds.
        /// </summary>
        /// <param name="filePath">Absolute destination file path.</param>
        /// <param name="contents">Bytes to write.</param>
        public static void WriteAllBytes(string filePath, byte[] contents)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(EMPTY_PATH_MESSAGE, nameof(filePath));
            }

            if (contents == null)
            {
                contents = Array.Empty<byte>();
            }

            string tempFilePath = filePath + TEMP_FILE_SUFFIX;
            PrepareWritableFile(tempFilePath);
            try
            {
                WriteAndFlush(tempFilePath, contents);
                Action beforeReplace = BeforeDestinationReplace;
                if (beforeReplace != null)
                {
                    beforeReplace();
                }

                CommitTemporaryFile(tempFilePath, filePath);
            }
            catch
            {
                DeleteFileIfPresent(tempFilePath);
                throw;
            }
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

        private static void WriteAndFlush(string tempFilePath, byte[] contents)
        {
            FileStream stream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                DISK_WRITE_BUFFER_BYTES,
                FileOptions.WriteThrough);
            try
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static void CommitTemporaryFile(string tempFilePath, string filePath)
        {
            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, recursive: true);
                File.Move(tempFilePath, filePath);
                return;
            }

            if (!File.Exists(filePath))
            {
                File.Move(tempFilePath, filePath);
                return;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Replace(tempFilePath, filePath, null, ignoreMetadataErrors: true);
        }

        private static void DeleteFileIfPresent(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }
    }
}
