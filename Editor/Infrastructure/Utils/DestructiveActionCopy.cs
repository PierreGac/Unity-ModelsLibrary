namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Copy for repository deletion and local project removal.
    /// Repository deletion leaves imported project copies in place. Project removal does not change the repository.
    /// </summary>
    internal static class DestructiveActionCopy
    {
        /// <summary>States that imported project copies survive a repository delete.</summary>
        internal const string LOCAL_INSTALLS_REMAIN = "This does not remove copies that you or teammates already imported into Unity projects.";

        /// <summary>Points at the separate local-install action.</summary>
        internal const string REMOVE_FROM_PROJECT_IS_SEPARATE = "Use Remove from project to delete only this project's install.";

        /// <summary>States that project removal is local.</summary>
        internal const string REMOVE_FROM_PROJECT_SCOPE = "This deletes the install in this project's Assets folder only. The shared repository and other people's projects are not changed.";

        /// <summary>Tooltip for deleting one repository version.</summary>
        internal const string DELETE_VERSION_BUTTON_TOOLTIP = "Remove this version from the shared repository. Copies already imported into projects stay in place.";

        /// <summary>Tooltip for deleting a whole repository model.</summary>
        internal const string DELETE_MODEL_BUTTON_TOOLTIP = "Remove this model and every version from the shared repository. Copies already imported into projects stay in place.";

        /// <summary>Tooltip for deleting a local install.</summary>
        internal const string REMOVE_FROM_PROJECT_BUTTON_TOOLTIP = "Delete this model's files from this project's Assets folder. The shared repository is not changed.";

        /// <summary>
        /// Confirmation for deleting one repository version.
        /// </summary>
        /// <param name="modelName">Display name of the model.</param>
        /// <param name="version">Version being deleted.</param>
        /// <returns>Dialog body.</returns>
        internal static string BuildDeleteVersionMessage(string modelName, string version)
        {
            return "Are you sure you want to delete version " + version + " of '" + modelName + "'?\n\n"
                + "This permanently removes that version's folder, payload files, preview images, and metadata from the shared repository.\n\n"
                + LOCAL_INSTALLS_REMAIN + " " + REMOVE_FROM_PROJECT_IS_SEPARATE;
        }

        /// <summary>
        /// Second confirmation when the deleted version is the latest.
        /// </summary>
        /// <returns>Dialog body.</returns>
        internal static string BuildDeleteLatestVersionMessage()
        {
            return "This is the latest version. Deleting it will promote an older version to be the new latest version in the shared repository.\n\n"
                + LOCAL_INSTALLS_REMAIN + "\n\nContinue?";
        }

        /// <summary>
        /// First confirmation for deleting every version of a model.
        /// </summary>
        /// <param name="modelName">Display name of the model.</param>
        /// <returns>Dialog body.</returns>
        internal static string BuildDeleteModelMessage(string modelName)
        {
            return "Are you sure you want to delete the entire model '" + modelName + "' from the shared repository?\n\n"
                + "This will permanently remove:\n"
                + "• All versions of this model\n"
                + "• All payload files, metadata, and preview images\n"
                + "• The model entry from the index\n\n"
                + LOCAL_INSTALLS_REMAIN + " " + REMOVE_FROM_PROJECT_IS_SEPARATE + "\n\n"
                + "This action cannot be undone.";
        }

        /// <summary>
        /// Second confirmation for deleting every version of a model.
        /// </summary>
        /// <param name="modelName">Display name of the model.</param>
        /// <returns>Dialog body.</returns>
        internal static string BuildDeleteModelFinalMessage(string modelName)
        {
            return "You are about to permanently delete '" + modelName + "' and all its versions from the shared repository.\n\n"
                + LOCAL_INSTALLS_REMAIN + "\n\nAre you absolutely sure?";
        }

        /// <summary>
        /// Confirmation for deleting only this project's install.
        /// </summary>
        /// <param name="modelName">Display name of the model.</param>
        /// <returns>Dialog body.</returns>
        internal static string BuildRemoveFromProjectMessage(string modelName)
        {
            return "Are you sure you want to remove '" + modelName + "' from this project?\n\n" + REMOVE_FROM_PROJECT_SCOPE;
        }
    }
}
