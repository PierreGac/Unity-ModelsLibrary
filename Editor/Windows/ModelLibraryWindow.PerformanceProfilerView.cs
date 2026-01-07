using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing PerformanceProfilerWindow view implementation for ModelLibraryWindow.
    /// Handles the performance profiler view for viewing async operation performance metrics.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Hidden PerformanceProfilerWindow instance for reuse.
        /// This instance is created once and reused to maintain profiler state.
        /// </summary>
        private PerformanceProfilerWindow _profilerInstance;

        /// <summary>
        /// Initializes profiler state when navigating to the PerformanceProfiler view.
        /// Creates the hidden PerformanceProfilerWindow instance if needed.
        /// </summary>
        public void InitializePerformanceProfilerState()
        {
            // Create instance if needed - will be initialized by DrawEditorWindowView
            if (_profilerInstance == null)
            {
                _profilerInstance = CreateInstance<PerformanceProfilerWindow>();
            }
        }

        /// <summary>
        /// Cleans up profiler state when leaving the PerformanceProfiler view.
        /// Calls OnDisable on the profiler instance to unsubscribe from update events.
        /// </summary>
        private void CleanupPerformanceProfilerState()
        {
            CleanupEditorWindowInstance(_profilerInstance);
        }

        /// <summary>
        /// Draws the PerformanceProfiler view.
        /// Uses a hidden PerformanceProfilerWindow instance to render performance metrics.
        /// </summary>
        private void DrawPerformanceProfilerView()
        {
            // Use helper method to render the EditorWindow instance
            DrawEditorWindowView(ref _profilerInstance);
        }
    }
}

