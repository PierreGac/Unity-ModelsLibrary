# Models Library for Unity

Editor tools for browsing, versioning, submitting, and importing 3D models from a shared repository.

![Unity Version](https://img.shields.io/badge/Unity-6000.2%2B-blue)
![.NET Standard](https://img.shields.io/badge/.NET-Standard%202.1-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

Package: `com.models-library` · Display name: **Models Library** · Version: **1.0.1**

## Features

- Browse models with search, tags, grid/list views, favorites, and recent
- Import into the project with GUID preservation and install-status badges
- Detect updates via SemVer and changelog entries
- Submit new models or new versions (name prefilling, drafts, preview images)
- Notes, bulk operations, batch upload, analytics, and 3D preview
- File-system repository (supported) and HTTP repository (experimental)
- Hidden install manifests (`.modelLibrary.meta.json`)

## Requirements

- Unity **6000.2** or later (developed/tested on 6000.2.6f2)
- .NET Standard 2.1
- Unity Editor only (no runtime / player dependencies)
- Disk space for the local model cache
- Network access only if using the HTTP repository

## Install

### Package Manager (Git URL)

1. `Window > Package Manager`
2. `+` → `Add package from git URL...`
3. Enter:

```
https://github.com/PierreGac/Unity-ModelsLibrary.git?path=Assets/ModelLibrary
```

Pin a tag or branch with `#v1.0.1` or `#main` if needed.

### Package Manager (local)

1. Clone the repository
2. `+` → `Add package from disk...`
3. Select `Assets/ModelLibrary/package.json`

### Manual copy

Copy `Assets/ModelLibrary` into your project's `Assets` folder.

### After install

1. Open **Tools > Model Library**
2. Complete the first-run wizard (identity, repository type/root)
3. Browse or submit models

Settings live in the main window (not separate Unity menus). Secondary views (Help, shortcuts, analytics, batch upload, error log, profiler) are also opened from there.

## Quick start

1. **Open** `Tools > Model Library`
2. **Configure** repository (File System path/UNC, or HTTP base URL) and your name/role
3. **Browse / import** — search or filter, open a model, use Import
4. **Submit** — select assets in the Project window, then:

   - `Assets > Model Library > Submit Model`, or
   - the Submit action inside the Model Library window

Default import destination is `Assets/Models/<ModelName>/` (overridable per model).

## Configuration

| Setting | Where | Notes |
|--------|--------|--------|
| Repository kind / root | `ModelLibrarySettings` asset | Created at `Assets/ModelLibrary/Resources/ModelLibrarySettings.asset` |
| Local cache | Same settings asset | Default: `Library/ModelLibraryCache` |
| User name / role | `EditorPrefs` | Set in the wizard or settings UI |

Roles: **Developer**, **Artist**, **Admin**. These only gate Editor UI. They are not a security boundary—any real authorization must be enforced by your shared folder ACLs or HTTP server.

**File System** is the tested workflow. **HTTP** implements `IModelRepository` but needs more validation in production.

## Workflows

### Import

Open a model from the browser → **Import to Project**. Installed models show an Installed badge; newer repo versions show Update Available.

### Update

Open details for a model with an update → **Update**. GUIDs are preserved where possible; resolve conflicts carefully (prefer Keep when Unity prompts).

### Submit new / update existing

1. Select FBX/OBJ (and related materials/textures) in the Project window
2. `Assets > Model Library > Submit Model`
3. Choose New or Update Existing, fill SemVer, description, tags, optional images
4. Updates require a changelog summary

Other asset context actions:

- `Assets > Model Library > Open in Model Library`
- `Assets > Model Library > Check for Updates`
- `Assets > Model Library > View Details`

### Notes and metadata

From Model Details: add notes; Artists/Admins can edit description/tags and manage versions (UI-gated).

## Repository layout

```text
<repository-root>/
  models_index.json
  <modelId>/
    <version>/
      model.json
      payload/          # mesh, materials, textures, …
      images/           # preview images
```

After import, each install folder gets a hidden manifest:

- `.modelLibrary.meta.json` (current)
- `modelLibrary.meta.json` (legacy, still detected)

## Extending

Implement `IModelRepository` for custom storage backends:

```csharp
Task<ModelIndex> LoadIndexAsync();
Task SaveIndexAsync(ModelIndex index);
Task<ModelMeta> LoadMetaAsync(string modelId, string version);
Task SaveMetaAsync(string modelId, string version, ModelMeta meta);
// plus file/directory helpers, DeleteVersionAsync, DeleteModelAsync
```

See `Editor/Infrastructure/Repository/` for the File System and HTTP implementations.

## Known limitations

- HTTP repository is experimental; prefer a shared folder / UNC for team use
- Roles are client-side UI gating only
- GUID conflicts on import are common; use Keep when you need to preserve references
- Cache folders can leave access-denied or stale entries after VCS discard / version delete / 3D preview before import — delete the affected cache folder under `Library/ModelLibraryCache` and retry
- Submitting models without usable assets is not fully blocked yet

## License and links

- [LICENSE](LICENSE) (MIT)
- [CHANGELOG.md](CHANGELOG.md)
- Issues: https://github.com/PierreGac/Unity-ModelsLibrary/issues
