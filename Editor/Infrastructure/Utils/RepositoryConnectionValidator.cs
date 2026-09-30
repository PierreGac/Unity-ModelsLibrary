using System;
using System.IO;
using ModelLibrary.Editor.Settings;
using UnityEditor;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Decides whether a wizard repository location can be saved.
    /// A filesystem folder must exist unless the user chooses the labeled save-without-testing action.
    /// HTTP is accepted from URL shape only.
    /// </summary>
    internal static class RepositoryConnectionValidator
    {
        /// <summary>Button label for saving a repository that was not checked.</summary>
        internal const string SAVE_WITHOUT_TESTING_LABEL = "Save without testing";

        private const string ROOT_REQUIRED_MESSAGE = "Repository root is required.";
        private const string ABSOLUTE_PATH_MESSAGE = "File system paths should be absolute (for example, C:\\Models or \\\\server\\share).";
        private const string MISSING_DIRECTORY_MESSAGE = "Directory not found. Test Connection requires an existing folder.";
        private const string SAVED_WITHOUT_TESTING_MESSAGE = "Saved without testing. The folder was not checked.";
        private const string DIRECTORY_FOUND_MESSAGE = "Directory located successfully.";
        private const string HTTP_URL_MESSAGE = "Please provide a valid HTTP or HTTPS URL.";
        private const string HTTP_SHAPE_MESSAGE = "URL format looks valid. Remember to verify credentials and server availability.";
        private const string UNC_PREFIX = "\\\\";

        /// <summary>
        /// Checks a repository location. A missing filesystem directory is rejected unless <paramref name="saveWithoutTesting"/> is true.
        /// </summary>
        /// <param name="kind">Filesystem or HTTP repository.</param>
        /// <param name="root">Absolute folder or HTTP URL.</param>
        /// <param name="saveWithoutTesting">True when the user chose the labeled escape hatch.</param>
        /// <param name="message">Feedback for the wizard.</param>
        /// <param name="messageType">Severity of <paramref name="message"/>.</param>
        /// <returns>True when the location may be saved.</returns>
        internal static bool TryValidate(
            ModelLibrarySettings.RepositoryKind kind,
            string root,
            bool saveWithoutTesting,
            out string message,
            out MessageType messageType)
        {
            message = string.Empty;
            messageType = MessageType.None;

            if (string.IsNullOrWhiteSpace(root))
            {
                message = ROOT_REQUIRED_MESSAGE;
                messageType = MessageType.Error;
                return false;
            }

            if (kind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                return TryValidateFileSystem(root, saveWithoutTesting, out message, out messageType);
            }

            return TryValidateHttpShape(root, out message, out messageType);
        }

        /// <summary>
        /// Saves the repository location when <see cref="TryValidate"/> accepts it.
        /// </summary>
        /// <param name="kind">Filesystem or HTTP repository.</param>
        /// <param name="root">Absolute folder or HTTP URL.</param>
        /// <param name="saveWithoutTesting">True when the user chose the labeled escape hatch.</param>
        /// <param name="message">Feedback for the wizard.</param>
        /// <param name="messageType">Severity of <paramref name="message"/>.</param>
        /// <returns>True when the project settings were updated.</returns>
        internal static bool TrySaveRepository(
            ModelLibrarySettings.RepositoryKind kind,
            string root,
            bool saveWithoutTesting,
            out string message,
            out MessageType messageType)
        {
            if (!TryValidate(kind, root, saveWithoutTesting, out message, out messageType))
            {
                return false;
            }

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryKind = kind;
            settings.repositoryRoot = root.Trim();
            settings.SaveProjectCopy();
            return true;
        }

        private static bool TryValidateFileSystem(
            string root,
            bool saveWithoutTesting,
            out string message,
            out MessageType messageType)
        {
            if (!Path.IsPathRooted(root) && !root.StartsWith(UNC_PREFIX, StringComparison.Ordinal))
            {
                message = ABSOLUTE_PATH_MESSAGE;
                messageType = MessageType.Error;
                return false;
            }

            if (Directory.Exists(root))
            {
                message = DIRECTORY_FOUND_MESSAGE;
                messageType = MessageType.Info;
                return true;
            }

            if (saveWithoutTesting)
            {
                message = SAVED_WITHOUT_TESTING_MESSAGE;
                messageType = MessageType.Warning;
                return true;
            }

            message = MISSING_DIRECTORY_MESSAGE;
            messageType = MessageType.Error;
            return false;
        }

        private static bool TryValidateHttpShape(string root, out string message, out MessageType messageType)
        {
            Uri uriResult;
            bool hasAbsoluteUri = Uri.TryCreate(root, UriKind.Absolute, out uriResult);
            bool isHttp = hasAbsoluteUri
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (!isHttp)
            {
                message = HTTP_URL_MESSAGE;
                messageType = MessageType.Error;
                return false;
            }

            message = HTTP_SHAPE_MESSAGE;
            messageType = MessageType.Info;
            return true;
        }
    }
}
