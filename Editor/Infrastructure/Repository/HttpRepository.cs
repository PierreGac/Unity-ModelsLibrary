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
    /// Example HTTP repository (requires server API).
    /// Implement REST endpoints: GET/PUT models_index.json, model.json, and file upload/download.
    /// This is a stub with minimal examples for index/meta; file IO should use chunked endpoints.
    /// </summary>
    public class HttpRepository : IModelRepository
    {
        public string Root { get; }
        public HttpRepository(string rootBaseUrl) { Root = rootBaseUrl.TrimEnd('/'); }

        private string Url(params string[] parts) => Root + "/" + string.Join("/", parts);

        public async Task<ModelIndex> LoadIndexAsync()
        {
            using UnityWebRequest req = UnityWebRequest.Get(Url("models_index.json"));
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                return new ModelIndex();
            }

            return JsonUtil.FromJson<ModelIndex>(req.downloadHandler.text) ?? new ModelIndex();
        }

        public async Task SaveIndexAsync(ModelIndex index)
        {
            string json = JsonUtil.ToJson(index);
            using UnityWebRequest req = new UnityWebRequest(Url("models_index.json"), UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            await req.SendWebRequest();
        }

        public async Task<ModelMeta> LoadMetaAsync(string modelId, string version)
        {
            using UnityWebRequest req = UnityWebRequest.Get(Url(modelId, version, ModelMeta.MODEL_JSON));
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new IOException(req.error);
            }

            return JsonUtil.FromJson<ModelMeta>(req.downloadHandler.text);
        }

        public async Task SaveMetaAsync(string modelId, string version, ModelMeta meta)
        {
            string json = JsonUtil.ToJson(meta);
            using UnityWebRequest req = new UnityWebRequest(Url(modelId, version, ModelMeta.MODEL_JSON), UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            await req.SendWebRequest();
        }

        public Task<bool> DirectoryExistsAsync(string relativePath) => Task.FromResult(true); // server decides
        public Task EnsureDirectoryAsync(string relativePath) => Task.CompletedTask;          // server decides
        public Task<List<string>> ListFilesAsync(string relativeDir) => Task.FromResult(new List<string>()); // implement API

        public Task UploadFileAsync(string relativePath, string localAbsolutePath) => Task.CompletedTask;

        public Task DownloadFileAsync(string relativePath, string localAbsolutePath) => Task.CompletedTask;
    }
}
