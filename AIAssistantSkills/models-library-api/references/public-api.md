# Public API reference

Editor assembly: `__ModelLibrary.Editor`. Unity 6000.2+, .NET Standard 2.1.

## Open windows

```csharp
ModelLibrary.Editor.Windows.ModelLibraryWindow.Open();
ModelLibrary.Editor.Windows.ModelSubmitWindow.Open();
ModelLibrary.Editor.Windows.ModelSubmitWindow.Open(resolveMeshDependencies: true, selectedAssetGuids: null);
ModelLibrary.Editor.Windows.ModelSubmitWindow.OpenForUpdate("model-id");
ModelLibrary.Editor.Windows.ModelDetailsWindow.Open("model-id", "1.2.0");
```

Windows refuse to open in play mode. Submit also requires Artist or Admin (UI check).

## Settings and factory

```csharp
ModelLibrary.Editor.Settings.ModelLibrarySettings settings =
    ModelLibrary.Editor.Settings.ModelLibrarySettings.GetOrCreate();
// settings.repositoryKind, settings.repositoryRoot, settings.localCacheRoot
// Writable copy: ProjectSettings/ModelLibrarySettings.json
// Persist edits with settings.SaveProjectCopy();

ModelLibrary.Editor.Repository.IModelRepository repo =
    ModelLibrary.Editor.Utils.RepositoryFactory.CreateRepository();
ModelLibrary.Editor.Repository.IModelRepository custom =
    ModelLibrary.Editor.Utils.RepositoryFactory.CreateRepository(@"\\studio\ModelLibrary", isFileSystem: true);
```

## Service facade

```csharp
ModelLibrary.Editor.Services.ModelLibraryService service =
    new ModelLibrary.Editor.Services.ModelLibraryService(repo);
```

| Method | Notes |
|--------|--------|
| `GetIndexAsync()` | `Task<ModelIndex>` |
| `RefreshIndexAsync()` | Reload index from repository |
| `InvalidateIndexCache()` | Next load reads from storage |
| `GetMetaAsync(id, version)` | `Task<ModelMeta>` |
| `GetPreviewTextureAsync(id, version, relativePath)` | Preview image |
| `GetAvailableVersionsAsync(modelId)` | SemVer descending |
| `PreviewIndexRebuildAsync()` | File System only; no write |
| `RebuildIndexFromRepositoryAsync(createBackup = true)` | File System only; confirm first |
| `ScanProjectForKnownModelsAsync()` | Local installs vs catalog |
| `GetAvailableUpdatesAsync()` | `List<ModelUpdateInfo>` |
| `GetUpdateInfoAsync(modelId)` | Single model |
| `GetUpdateSnapshotAsync()` | Full cache copy |
| `HasUpdateAsync(modelId)` | |
| `GetUpdateCountAsync()` | |
| `RefreshAllUpdatesAsync()` | |
| `DownloadModelVersionAsync(id, version)` | Cache under `Library/ModelLibraryCache`; returns `(versionRoot, meta)` |
| `SubmitNewVersionAsync(meta, localVersionRoot, changeSummary)` | Uploads allowlisted files and updates the index. Throws `InvalidOperationException` when the folder has no file `AssetDependencyResolver.IsMeshAssetPath` accepts. |
| `PublishMetadataUpdateAsync(updatedMeta, baseVersion, changeSummary, author, bumpStrategy)` | Metadata-only new version; `Func<SemVer, SemVer>` optional |
| `ClearCacheForModelAsync(modelId, version)` | |
| `DeleteVersionAsync(modelId, version)` | Confirm with the user |
| `DeleteModelAsync(modelId)` | Confirm with the user; also updates index |

`ModelUpdateInfo`: `modelId`, `modelName`, `localVersion`, `remoteVersion`, `hasUpdate`, `lastChecked`, `updateDescription`.

## IModelRepository

```csharp
Task<ModelIndex> LoadIndexAsync();
Task SaveIndexAsync(ModelIndex index);
Task<bool> TrySaveIndexIfUnchangedAsync(ModelIndex index, long expectedRevision);
Task<ModelMeta> LoadMetaAsync(string modelId, string version);
Task SaveMetaAsync(string modelId, string version, ModelMeta meta);
Task<bool> DirectoryExistsAsync(string relativePath);
Task EnsureDirectoryAsync(string relativePath);
Task<List<string>> ListFilesAsync(string relativeDir);
Task UploadFileAsync(string relativePath, string localAbsolutePath);
Task DownloadFileAsync(string relativePath, string localAbsolutePath);
Task<bool> DeleteVersionAsync(string modelId, string version);
Task<bool> DeleteModelAsync(string modelId);
string Root { get; }
```

Implement this interface for a custom backend. Do not subclass the internal File System / HTTP types.

## Identity

```csharp
ModelLibrary.Editor.Identity.IUserIdentityProvider
// GetUserName, SetUserName, GetUserRole, SetUserRole
ModelLibrary.Editor.Identity.UserRole // Developer, Artist, Admin
```

Roles only gate Editor UI.

## Data contract (`ModelLibrary.Data`)

Repository JSON: `ModelMeta` (`identity`, `version`, `description`, `tags`, `author`, payload/image paths, `installPath`, `notes`, `changelog`, …), `ModelIndex` / `ModelIndex.Entry`, `ModelIdentity`, `Tags`, `ModelNote`, `ModelChangelogEntry`, `AssetRef`, `DependencyRef`, `ModelImporterSettings`, `ModelIndexRebuildReport`.

Keep DTO **fields** public for Unity `JsonUtility`.

## SemVer

`ModelLibrary.Editor.Utils.SemVer` — MAJOR.MINOR.PATCH, `IComparable<SemVer>`. Used by `PublishMetadataUpdateAsync` bump strategy.

## Not public

Do not generate code against internal utils, `FileSystemRepository`, `HttpRepository`, `ModelDeployer`, `ModelProjectImporter`, or window navigation helpers (`NavigateToView`, `Initialize*State`).
