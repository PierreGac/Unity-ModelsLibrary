using System;
using System.Threading;
using UnityEditor;

namespace ModelLibrary.Editor.Infrastructure
{
    /// <summary>
    /// Records the Unity Editor main thread and marshals an explicit callback onto it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity 6 installs <c>UnitySynchronizationContext</c> before user code runs, on
    /// both the initial domain load and every domain reload, and pumps one generation
    /// of <c>await</c> continuations per player-loop tick. That already resumes editor
    /// async work on the main thread.
    /// </para>
    /// <para>
    /// Do not replace that context and do not drain a queue from
    /// <see cref="EditorApplication.update"/>. <c>await Task.Yield()</c> posts the next
    /// continuation back onto the current context. Running those continuations inside
    /// the update callback never returns control to the editor. Unity then shows
    /// "Hold on… Waiting for user code in __ModelLibrary.Editor.dll" with
    /// <c>UnityMainThreadSyncContext.Pump</c> on the stack until the process is killed.
    /// Opening a project is enough to trigger it, because domain reload runs this
    /// static constructor and restored editor windows start async work immediately.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class UnityMainThreadSyncContext
    {
        private const string UnitySynchronizationContextTypeName = "UnitySynchronizationContext";

        /// <summary>
        /// Managed thread id captured when the editor domain loads. InitializeOnLoad
        /// runs on the editor main thread.
        /// </summary>
        private static readonly int _mainThreadId;

        static UnityMainThreadSyncContext()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Returns <c>true</c> if the current thread is the Unity Editor main thread.
        /// </summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Returns whether this type installed its own synchronization context.
        /// Always <c>false</c>: Unity's context must stay in place across domain reload.
        /// </summary>
        internal static bool InstalledFallback => false;

        /// <summary>
        /// Enqueues <paramref name="action"/> on the editor main thread.
        /// Runs inline when already on the main thread.
        /// </summary>
        /// <param name="action">Callback to run on the main thread. Null is ignored.</param>
        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread)
            {
                action();
                return;
            }

            SynchronizationContext unityContext = SynchronizationContext.Current;
            if (IsUnitySynchronizationContext(unityContext))
            {
                unityContext.Post(_ => action(), null);
                return;
            }

            EditorApplication.delayCall += () => action();
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the editor main thread and waits until it finishes.
        /// Runs inline when already on the main thread.
        /// </summary>
        /// <param name="action">Callback to run on the main thread. Null is ignored.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called off the main thread and Unity's synchronization context is not current.
        /// </exception>
        public static void Send(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread)
            {
                action();
                return;
            }

            SynchronizationContext unityContext = SynchronizationContext.Current;
            if (IsUnitySynchronizationContext(unityContext))
            {
                unityContext.Send(_ => action(), null);
                return;
            }

            throw new InvalidOperationException(
                "[UnityMainThreadSyncContext] Send() called from a non-main thread without UnitySynchronizationContext.");
        }

        private static bool IsUnitySynchronizationContext(SynchronizationContext context)
        {
            if (context == null)
            {
                return false;
            }

            string typeName = context.GetType().Name;
            return string.Equals(typeName, UnitySynchronizationContextTypeName, StringComparison.Ordinal);
        }
    }
}
