using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;

namespace ModelLibrary.Editor.Infrastructure
{
    /// <summary>
    /// Installs a <see cref="SynchronizationContext"/> that marshals
    /// continuations back to the Unity Editor main thread, and pumps them
    /// from <see cref="EditorApplication.update"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SECURITY/STABILITY (audit CRIT-08 + CRIT-09): without a main-thread
    /// <see cref="SynchronizationContext"/>, <c>await</c> continuations in
    /// async methods run on threadpool threads. Unity Editor APIs
    /// (<see cref="EditorUtility.DisplayProgressBar"/>,
    /// <see cref="EditorUtility.ClearProgressBar"/>,
    /// <see cref="AssetDatabase.Refresh"/>, <c>EditorWindow.Repaint</c>,
    /// <c>titleContent.text</c>, etc.) can only be called from the main
    /// thread and either throw <c>UnityException</c> or silently corrupt
    /// internal state when invoked from a threadpool thread.
    /// </para>
    /// <para>
    /// After this class is loaded (via <c>[InitializeOnLoad]</c>), every
    /// <c>await</c> in the editor will resume on the main thread, which
    /// also eliminates the race conditions on caches read from <c>OnGUI</c>
    /// (CRIT-09).
    /// </para>
    /// <para>
    /// This also makes <c>await Task.Yield()</c> a valid "return to main
    /// thread" pattern (which the codebase already uses extensively but
    /// was not actually working).
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class UnityMainThreadSyncContext
    {
        private sealed class MainThreadContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
            private readonly SynchronizationContext _fallback;

            public MainThreadContext(SynchronizationContext fallback)
            {
                _fallback = fallback;
            }

            public override void Post(SendOrPostCallback callback, object state)
            {
                if (callback == null) return;
                _queue.Enqueue(() =>
                {
                    try { callback(state); }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                });
            }

            public override void Send(SendOrPostCallback callback, object state)
            {
                // Synchronous send — if we're already on the main thread, run inline.
                // Otherwise we cannot block the calling thread (Unity's main thread
                // is the only one that can pump the queue), so fall back to the
                // previous context's Send.
                if (IsMainThread)
                {
                    try { callback(state); }
                    catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
                }
                else if (_fallback != null)
                {
                    _fallback.Send(callback, state);
                }
                else
                {
                    throw new InvalidOperationException(
                        "[UnityMainThreadSyncContext] Send() called from a non-main thread with no fallback context.");
                }
            }

            public void Pump()
            {
                while (_queue.TryDequeue(out Action action))
                {
                    action();
                }
            }
        }

        private static readonly MainThreadContext _context;

        /// <summary>
        /// Returns <c>true</c> if the current thread is the Unity Editor main thread.
        /// </summary>
        public static bool IsMainThread => System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private static readonly int _mainThreadId;

        static UnityMainThreadSyncContext()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _context = new MainThreadContext(SynchronizationContext.Current);
            SynchronizationContext.SetSynchronizationContext(_context);
            EditorApplication.update += Pump;
            UnityEngine.Debug.Log("[ModelLibrary] UnityMainThreadSyncContext installed.");
        }

        private static void Pump()
        {
            _context.Pump();
        }

        /// <summary>
        /// Enqueues an action to run on the main thread on the next editor update.
        /// Safe to call from any thread.
        /// </summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            _context.Post(_ => action(), null);
        }

        /// <summary>
        /// Runs an action on the main thread synchronously. If called from the
        /// main thread, runs inline. Otherwise throws (Unity's main thread
        /// cannot block on itself).
        /// </summary>
        public static void Send(Action action)
        {
            if (action == null) return;
            _context.Send(_ => action(), null);
        }
    }
}
