using System.Threading.Tasks;
using UnityEngine.Networking;

namespace ModelLibrary.Editor.Utils
{
    internal static class UnityWebRequestExtensions
    {
        public static Task ToTask(this UnityWebRequestAsyncOperation operation)
        {
            if (operation.isDone)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
