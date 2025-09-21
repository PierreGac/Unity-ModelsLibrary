
using System.Threading.Tasks;
using ModelLibrary.Editor.Repository;

namespace ModelLibrary.Editor.Services
{
    public class ModelDownloader
    {
        private readonly ModelLibraryService _service;
        public ModelDownloader(ModelLibraryService service) { _service = service; }
        public Task<(string versionRoot, Data.ModelMeta meta)> DownloadAsync(string id, string version) => _service.DownloadModelVersionAsync(id, version);
    }
}



