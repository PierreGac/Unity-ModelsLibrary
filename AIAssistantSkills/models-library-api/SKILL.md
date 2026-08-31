---
name: models-library-api
description: >
  Use when writing C# against Models Library: IModelRepository, ModelLibraryService,
  RepositoryFactory, ModelMeta/ModelIndex, SemVer, ModelLibrarySettings, or window
  Open methods. Use for custom storage backends and Assistant-generated Editor scripts.
required_editor_version: ">=6000.2"
---

# Models Library public API

Call types by fully qualified name. Do not reference `internal` Editor types (utils, FileSystemRepository, HttpRepository, most windows, subordinate services).

For signatures and examples, read `references/public-api.md`.

## Supported surface

- Data: `ModelLibrary.Data` (`ModelMeta`, `ModelIndex`, notes, tags, changelog, rebuild report)
- `ModelLibrary.Editor.Repository.IModelRepository`
- `ModelLibrary.Editor.Utils.RepositoryFactory`
- `ModelLibrary.Editor.Services.ModelLibraryService` and `ModelUpdateInfo`
- `ModelLibrary.Editor.Settings.ModelLibrarySettings`
- `ModelLibrary.Editor.Identity.IUserIdentityProvider` and `UserRole`
- `ModelLibrary.Editor.Utils.SemVer`
- Windows: `ModelLibraryWindow.Open()`, `ModelSubmitWindow.Open` / `OpenForUpdate`, `ModelDetailsWindow.Open`

## Rules

1. Create a repository with `RepositoryFactory.CreateRepository()`, then `new ModelLibraryService(repo)`.
2. Prefer UI for import into the project. The facade downloads to cache (`DownloadModelVersionAsync`); it does not replace the Import button.
3. `PreviewIndexRebuildAsync` / `RebuildIndexFromRepositoryAsync` apply to **File System** repositories only. Confirm before rebuild.
4. Confirm with the user before `DeleteVersionAsync` or `DeleteModelAsync`.
5. Roles on `IUserIdentityProvider` are UI gating, not authorization.
6. HTTP repository is experimental; prefer File System for team use.
