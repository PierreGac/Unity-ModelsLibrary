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
    /// async methods can run on threadpool threads. Unity Editor APIs
    /// (<see cref="EditorUtility.DisplayProgressBar"/>,
    /// <see cref="EditorUtility.ClearProgressBar"/>,
    /// <see cref="AssetDatabase.Refresh"/>, <c>EditorWindow.Repaint</c>,
    /// <c>titleContent.text</c>, etc.) can only be called from the main
    /// thread and either throw <c>UnityException</c> or silently corrupt
    /// internal state when invoked from a threadpool thread.
    /// </para>
    /// <para>
    /// Unity 6 already installs <c>UnitySynchronizationContext</c> for both
    /// Edit and Play mode. Replacing it with an unbounded drain loop is
    /// harmful: <c>await Task.Yield()</c> posts a nested continuation that
    /// gets executed in the same <see cref="EditorApplication.update"/>
    /// tick, so loops that yield never return control to the Editor (Unity
    /// shows "Hold on… Waiting for user code in __ModelLibrary.Editor.dll").
    /// Opening Project Settings is one common trigger. When Unity's context
    /// is already present we leave it alone.
    /// </para>
    /// <para>
    /// The fallback pump only processes work that was queued before the
    /// current tick started (one generation per frame), matching Unity's
    /// own SyncContext behavior after the <c>Task.Yield</c> hang fix.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class UnityMainThreadSyncContext
    {
        private const string UnitySynchronizationContextTypeName = "UnitySynchronizationContext";

        private sealed class MainThreadContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
            private readonly SynchronizationContext _fallback;

            public MainThreadContext(SynchronizationContext fallback)
            {
                _fallback = fallback;
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
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
                // Process only the generation queued before this tick.
                // Continuations posted by those actions (e.g. await Task.Yield())
                // run on a later EditorApplication.update — otherwise Yield loops
                // never return and freeze the Editor.
                int pending = _queue.Count;
                for (int i = 0; i < pending; i++)
                {
                    if (!_queue.TryDequeue(out Action action))
                    {
                        break;
                    }

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
        private static readonly bool _installedFallback;

        static UnityMainThreadSyncContext()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            SynchronizationContext existing = SynchronizationContext.Current;
            if (IsUnitySynchronizationContext(existing))
            {
                // Unity already marshals await continuations to the main thread
                // and pumps one generation per player-loop tick. Do not replace.
                _context = null;
                _installedFallback = false;
                return;
            }

            _context = new MainThreadContext(existing);
            SynchronizationContext.SetSynchronizationContext(_context);
            EditorApplication.update += Pump;
            _installedFallback = true;
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

        private static void Pump()
        {
            if (_context != null)
            {
                _context.Pump();
            }
        }

        /// <summary>
        /// Enqueues an action to run on the main thread on the next editor update.
        /// Safe to call from any thread. No-op when the fallback context was not installed
        /// (Unity's own context is responsible for marshaling).
        /// </summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            if (_context != null)
            {
                _context.Post(_ => action(), null);
                return;
            }

            if (IsMainThread)
            {
                action();
            }
            else
            {
                EditorApplication.delayCall += () => action();
            }
        }

        /// <summary>
        /// Runs an action on the main thread synchronously. If called from the
        /// main thread, runs inline. Otherwise throws when no fallback context
        /// can marshal the call (Unity's main thread cannot block on itself).
        /// </summary>
        public static void Send(Action action)
        {
            if (action == null) return;
            if (_context != null)
            {
                _context.Send(_ => action(), null);
                return;
            }

            if (IsMainThread)
            {
                action();
                return;
            }

            throw new InvalidOperationException(
                "[UnityMainThreadSyncContext] Send() called from a non-main thread while using UnitySynchronizationContext.");
        }

        /// <summary>
        /// Returns whether this type installed its own fallback context (false when
        /// Unity's <c>UnitySynchronizationContext</c> was already present).
        /// </summary>
        internal static bool InstalledFallback => _installedFallback;
    }
}
