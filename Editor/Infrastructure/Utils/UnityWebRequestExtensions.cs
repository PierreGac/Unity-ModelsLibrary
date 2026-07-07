using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace ModelLibrary.Editor.Utils
{
    internal static class UnityWebRequestExtensions
    {
        /// <summary>
        /// Converts a <see cref="UnityWebRequestAsyncOperation"/> to a <see cref="Task"/>
        /// that completes when the request finishes. SECURITY (audit MED-09):
        /// the previous implementation always called <c>TrySetResult(true)</c>
        /// on completion, regardless of whether the request succeeded. This
        /// meant callers had to explicitly check <c>webRequest.result</c>
        /// after awaiting — and most didn't. We now route HTTP failures
        /// through <c>TrySetException</c> so they propagate as exceptions
        /// and become visible. Callers that want to handle specific HTTP
        /// status codes (e.g. 404) should catch <see cref="UnityWebRequestException"/>.
        /// </summary>
        public static Task ToTask(this UnityWebRequestAsyncOperation operation)
        {
            if (operation == null)
            {
                return Task.FromException(new ArgumentNullException(nameof(operation)));
            }

            if (operation.isDone)
            {
                // Even if already done, route failures through exception path for consistency.
                if (operation.webRequest != null
                    && operation.webRequest.result != UnityWebRequest.Result.Success
                    && operation.webRequest.responseCode != 404 /* 404 handled by callers as "empty" */)
                {
                    return Task.FromException(new UnityWebRequestException(operation.webRequest));
                }
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            operation.completed += _ =>
            {
                UnityWebRequest req = operation.webRequest;
                if (req != null
                    && req.result != UnityWebRequest.Result.Success
                    && req.responseCode != 404 /* 404 is "not found" not "failure" — let callers handle */)
                {
                    tcs.TrySetException(new UnityWebRequestException(req));
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            };
            return tcs.Task;
        }
    }

    /// <summary>
    /// Exception thrown when a <see cref="UnityWebRequest"/> fails.
    /// SECURITY (audit MED-09).
    /// </summary>
    internal sealed class UnityWebRequestException : Exception
    {
        public long ResponseCode { get; }
        public string Url { get; }
        public UnityWebRequest.Result Result { get; }

        public UnityWebRequestException(UnityWebRequest req)
            : base($"UnityWebRequest failed: {req?.result} HTTP {req?.responseCode} - {req?.error} (URL: {req?.url})")
        {
            ResponseCode = req?.responseCode ?? 0;
            Url = req?.url;
            Result = req?.result ?? UnityWebRequest.Result.ConnectionError;
        }
    }
}
