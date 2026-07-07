using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using UnityEngine.Networking;
using UnityEngine;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Repository
{
    /// <summary>
    /// HTTP repository backed by a REST API.
    /// </summary>
    /// <remarks>
    /// SECURITY (audit CRIT-03 + CRIT-06): This repository is the only path
    /// through which real server-side RBAC can be enforced. The server must
    /// authenticate every request (e.g. via a Bearer token set via
    /// <see cref="SetAuthToken"/>) and authorize each operation against the
    /// authenticated user's role.
    ///
    /// Until the server side is implemented, the file upload/download methods
    /// perform real HTTP transfers (rather than silently returning success,
    /// which was the original behavior — see CRIT-06). Silent no-ops caused
    /// the index to be updated as if a submission had succeeded, while the
    /// payload was never actually transferred.
    /// </remarks>
    public class HttpRepository : IModelRepository
    {
        public string Root { get; }

        /// <summary>
        /// Optional bearer token sent as <c>Authorization: Bearer &lt;token&gt;</c>
        /// on every request. Set via <see cref="SetAuthToken"/>.
        /// </summary>
        private string _authToken;

        public HttpRepository(string rootBaseUrl) { Root = rootBaseUrl.TrimEnd('/'); }

        /// <summary>
        /// Sets the bearer token used for authenticating all subsequent requests.
        /// Leave null/empty to disable auth.
        /// </summary>
        public void SetAuthToken(string token) => _authToken = string.IsNullOrEmpty(token) ? null : token;

        /// <summary>
        /// Builds a URL by URL-encoding each path segment.
        /// SECURITY (CRIT-06 fix): previously <c>string.Join("/", parts)</c>
        /// with no encoding, which allowed a modelId containing "/" to escape
        /// the path segment.
        /// </summary>
        private string Url(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return Root;
            }
            string[] encoded = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                encoded[i] = Uri.EscapeDataString(parts[i] ?? string.Empty);
            }
            return Root + "/" + string.Join("/", encoded);
        }

        /// <summary>
        /// Applies the auth header (if set) to a request.
        /// </summary>
        private void ApplyAuth(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                req.SetRequestHeader("Authorization", "Bearer " + _authToken);
            }
        }

        /// <summary>
        /// Throws <see cref="IOException"/> on HTTP failure. SECURITY (CRIT-06):
        /// the previous implementation returned <c>new ModelIndex()</c> on any
        /// error, masking real failures (network down, 404, 500) as "empty repo".
        /// </summary>
        private static void EnsureSuccess(UnityWebRequest req, string operation)
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new IOException(
                    $"[HttpRepository] {operation} failed: HTTP {req.responseCode} - {req.error}");
            }
        }

        public async Task<ModelIndex> LoadIndexAsync()
        {
            using UnityWebRequest req = UnityWebRequest.Get(Url("models_index.json"));
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.LoadIndex", () => req.SendWebRequest().ToTask());

            // 404 is a legitimate "empty repository" signal — return empty index.
            // Any other failure (network, 5xx) must throw, not silently return empty.
            if (req.responseCode == 404)
            {
                return new ModelIndex();
            }
            EnsureSuccess(req, "LoadIndex");

            return JsonUtil.FromJson<ModelIndex>(req.downloadHandler.text) ?? new ModelIndex();
        }

        public async Task SaveIndexAsync(ModelIndex index)
        {
            string json = JsonUtil.ToJson(index);
            using UnityWebRequest req = new UnityWebRequest(Url("models_index.json"), UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.SaveIndex", () => req.SendWebRequest().ToTask());
            EnsureSuccess(req, "SaveIndex");
        }

        public async Task<ModelMeta> LoadMetaAsync(string modelId, string version)
        {
            using UnityWebRequest req = UnityWebRequest.Get(Url(modelId, version, ModelMeta.MODEL_JSON));
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.LoadMeta", () => req.SendWebRequest().ToTask());
            EnsureSuccess(req, "LoadMeta");

            return JsonUtil.FromJson<ModelMeta>(req.downloadHandler.text);
        }

        public async Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
        {
            string json = JsonUtil.ToJson(meta);
            using UnityWebRequest req = new UnityWebRequest(Url(modelId, version, ModelMeta.MODEL_JSON), UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.SaveMeta", () => req.SendWebRequest().ToTask());
            EnsureSuccess(req, "SaveMeta");
        }

        public Task<bool> DirectoryExistsAsync(string relativePath) => Task.FromResult(true); // server decides
        public Task EnsureDirectoryAsync(string relativePath) => Task.CompletedTask;          // server decides

        public async Task<List<string>> ListFilesAsync(string relativeDir)
        {
            // SECURITY (CRIT-06): no longer returns an empty list silently.
            // The server must implement a listing endpoint; until then we
            // throw so callers don't silently treat "couldn't list" as "empty".
            using UnityWebRequest req = UnityWebRequest.Get(Url("list", relativeDir));
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.ListFiles", () => req.SendWebRequest().ToTask());
            if (req.responseCode == 404)
            {
                return new List<string>();
            }
            EnsureSuccess(req, "ListFiles");
            // Expected format: one relative path per line.
            string text = req.downloadHandler.text ?? string.Empty;
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new List<string>(lines);
        }

        /// <summary>
        /// Uploads a file to the repository via HTTP PUT.
        /// </summary>
        /// <remarks>
        /// SECURITY (CRIT-06): the original implementation returned
        /// <c>Task.CompletedTask</c> without uploading anything — silently
        /// discarding the file while callers updated the index as if the
        /// upload had succeeded. This implementation streams the file via
        /// <see cref="UploadHandlerFile"/>.
        /// </remarks>
        public async Task UploadFileAsync(string relativePath, string localAbsolutePath)
        {
            if (string.IsNullOrEmpty(localAbsolutePath) || !File.Exists(localAbsolutePath))
            {
                throw new FileNotFoundException($"[HttpRepository] UploadFileAsync: local file not found: {localAbsolutePath}");
            }

            using UnityWebRequest req = new UnityWebRequest(Url(relativePath), UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerFile(localAbsolutePath);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/octet-stream");
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.UploadFile", () => req.SendWebRequest().ToTask());
            EnsureSuccess(req, "UploadFile(" + relativePath + ")");
        }

        /// <summary>
        /// Downloads a file from the repository via HTTP GET.
        /// </summary>
        /// <remarks>
        /// SECURITY (CRIT-06): the original implementation returned
        /// <c>Task.CompletedTask</c> without downloading anything — silently
        /// leaving the local cache empty while callers proceeded to mark the
        /// model as installed.
        /// </remarks>
        public async Task DownloadFileAsync(string relativePath, string localAbsolutePath)
        {
            string dir = Path.GetDirectoryName(localAbsolutePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using UnityWebRequest req = UnityWebRequest.Get(Url(relativePath));
            ApplyAuth(req);
            await AsyncProfiler.MeasureAsync("HttpRepository.DownloadFile", () => req.SendWebRequest().ToTask());
            EnsureSuccess(req, "DownloadFile(" + relativePath + ")");

            await File.WriteAllBytesAsync(localAbsolutePath, req.downloadHandler.data);
        }

        public Task<bool> DeleteVersionAsync(string modelId, string version)
        {
            // HTTP repository deletion requires a server-side DELETE endpoint.
            // Log a warning and return false so callers know deletion didn't happen.
            Debug.LogWarning("[HttpRepository] DeleteVersionAsync requires a server-side DELETE /models/{modelId}/{version} endpoint. Returning false.");
            return Task.FromResult(false);
        }

        public Task<bool> DeleteModelAsync(string modelId)
        {
            Debug.LogWarning("[HttpRepository] DeleteModelAsync requires a server-side DELETE /models/{modelId} endpoint. Returning false.");
            return Task.FromResult(false);
        }
    }
}
