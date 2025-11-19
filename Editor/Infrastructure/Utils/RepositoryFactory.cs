using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Settings;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Factory class for creating repository instances based on settings.
    /// Eliminates code duplication across multiple files.
    /// </summary>
    public static class RepositoryFactory
    {
        /// <summary>
        /// Creates a repository instance based on the current ModelLibrarySettings.
        /// </summary>
        /// <returns>An IModelRepository instance configured according to settings.</returns>
        public static IModelRepository CreateRepository()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            return settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? new FileSystemRepository(settings.repositoryRoot)
                : new HttpRepository(settings.repositoryRoot);
        }

        /// <summary>
        /// Creates a repository instance with a custom root path.
        /// </summary>
        /// <param name="repositoryRoot">The root path for the repository.</param>
        /// <param name="isFileSystem">True for file system repository, false for HTTP repository.</param>
        /// <returns>An IModelRepository instance.</returns>
        public static IModelRepository CreateRepository(string repositoryRoot, bool isFileSystem)
        {
            return isFileSystem
                ? new FileSystemRepository(repositoryRoot)
                : new HttpRepository(repositoryRoot);
        }
    }
}

