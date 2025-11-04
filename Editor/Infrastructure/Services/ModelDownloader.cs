using System.Threading.Tasks;
using ModelLibrary.Editor.Repository;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Simple wrapper service for downloading model versions from the repository.
    /// Provides a clean interface for downloading models to the local cache.
    /// This is a lightweight facade that delegates to ModelLibraryService.
    /// </summary>
    public class ModelDownloader
    {
        /// <summary>The underlying model library service that handles repository operations.</summary>
        private readonly ModelLibraryService _service;

        /// <summary>
        /// Initializes a new instance of the ModelDownloader with the specified service.
        /// </summary>
        /// <param name="service">The model library service to use for downloading.</param>
        public ModelDownloader(ModelLibraryService service) => _service = service;

        /// <summary>
        /// Downloads a model version from the repository to the local cache.
        /// Returns both the cache root path and the model metadata.
        /// </summary>
        /// <param name="id">The unique identifier of the model to download.</param>
        /// <param name="version">The version of the model to download.</param>
        /// <returns>A tuple containing the cache root path and the model metadata.</returns>
        public Task<(string versionRoot, Data.ModelMeta meta)> DownloadAsync(string id, string version)
            => _service.DownloadModelVersionAsync(id, version);
    }
}



