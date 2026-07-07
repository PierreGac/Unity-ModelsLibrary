using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Helpers for safely executing fire-and-forget async operations.
    /// </summary>
    /// <remarks>
    /// STABILITY (audit LOW-01): The codebase has 30+ sites where async methods
    /// are started with <c>_ = SomeAsync()</c> and the returned <see cref="Task"/>
    /// is discarded. Any exception thrown after the first <c>await</c> becomes
    /// <c>UnobservedTaskException</c> — which Unity may or may not surface.
    /// These helpers wrap fire-and-forget calls so exceptions are at least
    /// logged to the Unity console (and <see cref="ErrorLogger"/> if available).
    /// </remarks>
    public static class FireAndForgetExtensions
    {
        /// <summary>
        /// Awaits the given task and logs any exception to the Unity console
        /// and <see cref="ErrorLogger"/>. Safe to call as
        /// <c>_ = someTask.FireAndForget()</c>.
        /// </summary>
        /// <param name="task">The task to observe. May be null.</param>
        /// <param name="operationName">Name to use in the error log. Defaults to the caller member name.</param>
        public static async void FireAndForget(this Task task, [CallerMemberName] string operationName = null)
        {
            if (task == null) return;
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an error — ignore.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary] Fire-and-forget operation '{operationName}' failed: {ex.Message}\n{ex.StackTrace}");
                try
                {
                    ErrorLogger.LogError(
                        operationName ?? "FireAndForget",
                        ex.Message,
                        ErrorHandler.CategorizeException(ex),
                        ex);
                }
                catch
                {
                    // ErrorLogger may not be available — ignore.
                }
            }
        }

        /// <summary>
        /// Awaits the given task and logs any exception. Variant of
        /// <see cref="FireAndForget(Task, string)"/> that takes a lambda
        /// for the operation name (useful when the caller wants a custom
        /// context string).
        /// </summary>
        public static async void FireAndForget(this Task task, string operationName, string context)
        {
            if (task == null) return;
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an error.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelLibrary] '{operationName}' failed: {ex.Message} | Context: {context}");
                try
                {
                    ErrorLogger.LogError(
                        operationName,
                        ex.Message + $" | Context: {context}",
                        ErrorHandler.CategorizeException(ex),
                        ex,
                        context);
                }
                catch
                {
                    // Ignore.
                }
            }
        }
    }
}
