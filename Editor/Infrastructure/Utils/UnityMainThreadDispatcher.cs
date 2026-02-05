using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility for ensuring Unity API calls are executed on the main thread.
    /// Unity APIs must be called from the main thread, but async operations with ConfigureAwait(false)
    /// may continue on background threads.
    /// </summary>
    internal static class UnityMainThreadDispatcher
    {
        /// <summary>
        /// Executes an action on the main thread. If already on the main thread, executes immediately.
        /// Otherwise, schedules execution via EditorApplication.delayCall.
        /// </summary>
        /// <param name="action">The action to execute on the main thread.</param>
        public static void ExecuteOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            // Check if we're on the main thread by checking if we can access Unity's main thread APIs
            // In Unity Editor, the main thread is the one that can call Unity APIs
            // We use a simple heuristic: if we can call EditorApplication.delayCall without issues,
            // we're likely on the main thread, but to be safe, we always use delayCall for async contexts
            EditorApplication.delayCall += () => action();
        }

        /// <summary>
        /// Executes an action on the main thread and returns a Task that completes when the action has executed.
        /// </summary>
        /// <param name="action">The action to execute on the main thread.</param>
        /// <returns>A Task that completes after the action has been executed.</returns>
        public static Task ExecuteOnMainThreadAsync(Action action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };
            return tcs.Task;
        }

        /// <summary>
        /// Executes a function on the main thread and returns a Task with the result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute on the main thread.</param>
        /// <returns>A Task that completes with the function's result.</returns>
        public static Task<T> ExecuteOnMainThreadAsync<T>(Func<T> func)
        {
            if (func == null)
            {
                return Task.FromResult<T>(default(T));
            }

            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    T result = func();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };
            return tcs.Task;
        }
    }
}
