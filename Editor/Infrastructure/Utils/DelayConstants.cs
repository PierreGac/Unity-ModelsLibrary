namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Constants for delay values used in async operations.
    /// Centralizes delay definitions to eliminate magic numbers.
    /// </summary>
    public static class DelayConstants
    {
        /// <summary>Standard delay for UI updates (100 milliseconds).</summary>
        public const int UI_UPDATE_DELAY_MS = 100;

        /// <summary>Long delay for operations (500 milliseconds).</summary>
        public const int LONG_DELAY_MS = 500;
    }
}

