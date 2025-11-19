namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Constants for progress bar values used throughout the editor.
    /// Centralizes progress value definitions to eliminate magic numbers.
    /// </summary>
    public static class ProgressBarConstants
    {
        /// <summary>Initial/connecting progress (10%).</summary>
        public const float INITIAL = 0.1f;

        /// <summary>Preparing/downloading progress (30%).</summary>
        public const float PREPARING = 0.3f;

        /// <summary>Mid-operation progress (40%).</summary>
        public const float MID_OPERATION = 0.4f;

        /// <summary>Copying/processing progress (50%).</summary>
        public const float COPYING = 0.5f;

        /// <summary>Copying images progress (60%).</summary>
        public const float COPYING_IMAGES = 0.6f;

        /// <summary>Upload progress (70%).</summary>
        public const float UPLOADING = 0.7f;

        /// <summary>Finalizing progress (90%).</summary>
        public const float FINALIZING = 0.9f;

        /// <summary>Complete progress (100%).</summary>
        public const float COMPLETE = 1.0f;
    }
}

