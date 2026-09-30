using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// A discarded task is observed, so a failure reaches the console and cancellation does not.
    /// </summary>
    public class DiscardedTaskTests
    {
        private const string OPERATION_NAME = "DiscardedTask";
        private const string FAILURE_MESSAGE = "boom";

        /// <summary>
        /// A faulted task started through the observer writes an error that names the operation.
        /// </summary>
        [Test]
        public void FailedDiscardedTask_IsLogged()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex("\\[ModelLibrary\\] Fire-and-forget operation '" + OPERATION_NAME + "' failed: " + FAILURE_MESSAGE));
            Task faulted = Task.FromException(new InvalidOperationException(FAILURE_MESSAGE));
            faulted.FireAndForget(OPERATION_NAME);
        }

        /// <summary>
        /// Cancelling a discarded task is not reported as a failure.
        /// </summary>
        [Test]
        public void CancelledDiscardedTask_IsNotLogged()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Task cancelled = Task.FromCanceled(cancellation.Token);
            cancelled.FireAndForget(OPERATION_NAME);
            cancellation.Dispose();
        }
    }
}
