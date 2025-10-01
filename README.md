# Model Library for Unity

A Unity Editor tool that lets teams version, browse, submit, and import 3D models (FBX, materials, textures) from a shared repository. It keeps a lightweight index for fast search and tag filtering, supports per‑project visibility, and preserves Unity GUIDs to enable reliable update detection.

## Features

- Browser window with search and tag filters
- One‑click download, import, and update
- Submit new models or update existing ones from current selection
- Caching of model versions locally for fast re‑use
- High‑res preview thumbnail loading and display
- Per‑project visibility via project tags/scopes
- Repository‑agnostic: filesystem or HTTP backends

## Quick Start

1) Open the Browser window
- Unity menu: `Tools > Model Library > Browser`.
- On first run, the setup wizard helps configure the repository location.

2) Configure the repository
- Choose repository kind:
  - File System: a folder on disk accessible to the team
  - HTTP: a web endpoint implementing the repository interface
- Set the repository root (path or base URL).

3) Browse and filter
- Use the search bar to filter by model name or tags.
- Open the tag foldout to select specific tags to include.

4) Download / Import / Update
- Download caches a version locally.
- Import copies the model into your project at the recommended install path (or a custom folder) and preserves Unity GUIDs.
- If a local version is detected, you’ll see the current and latest versions and can update in place.

5) Submit models
- Unity menu: `Tools > Model Library > Submit Model`.
- Select assets in the Project (FBX, materials, textures), set name/version/description/tags.
- Submit as a new model or update an existing one; the tool builds metadata and uploads the new version.

## Screenshots

> Add screenshots or GIFs here:
- Browser with search and tag filter
- Model details with preview thumbnail
- Submit window

## How It Works

The library keeps a small, fast “index” for browsing and a full metadata file per model version.

- Index (`models_index.json`):
  - A list of entries: id, name, latest version, description, tags, update timestamps, optional project scopes.
  - Loaded once and cached for fast UI.

- Version metadata (`model.json` under `<modelId>/<version>/`):
  - Identity (id/name), version, description, author, timestamps
  - Payload paths, extracted assets (materials, textures)
  - Asset GUIDs (preserved `.meta` files) for reliable update detection
  - Images/previews and optional importer settings for FBX/OBJ
  - Changelog entries with summary/author/timestamps

### Repository Layout

```
<repository-root>/
  models_index.json
  <modelId>/
    <version>/
      model.json
      payload/
        <files and deps>
      images/
        <screenshots and previews>
```

### Local Cache

- Downloads are cached under a library path configured in settings.
- Import copies from cache into your `Assets/` while keeping GUIDs so the system can detect updates accurately.

## Configuration

- Settings are stored as a ScriptableObject; the first‑run wizard sets it up.
- You can refresh the index from the Browser toolbar.
- Preferred install path and a relative path can be set during submission; these are stored in metadata and suggested on import.

## Workflows

- Browsing & Filtering
  - Search by name or tag; combine with tag selection foldout.
  - Only models visible to the current project (by project name) are listed if scoped.

- Import / Update
  - The Browser detects local installs (via marker files and matching asset GUIDs).
  - If a newer remote version exists, the UI offers an Update.

- Submitting Models
  - Select assets and open the Submit window.
  - For new models, a duplicate‑name check avoids accidental re‑submission.
  - For updates, the version must be greater than the latest; a change summary is required.

## Extensibility

- Repository interface
  - Swappable backends (filesystem, HTTP). Implement the repository interface to add new storage backends.

- Metadata
  - `model.json` includes flexible `extra` and dependency fields to extend without schema changes.

## Development

- Open the Unity project and locate the Editor code under `Assets/ModelLibrary/Editor`.
- Key editor windows
  - Browser: `Editor/Windows/ModelLibraryWindow.cs`
  - Submit: `Editor/Windows/ModelSubmitWindow.cs`
  - Details: `Editor/Windows/ModelDetailsWindow.cs`
- Core services
  - Library service: `Editor/Infrastructure/Services/ModelLibraryService.cs`
  - Deployer (build metadata from selection): `Editor/Infrastructure/Services/ModelDeployer.cs`

### Building/Running

- No build step is required. Open the project in Unity and use the menu commands.
- The tool targets the Unity Editor only; no runtime components are required in player builds.

## Contributing

Contributions are welcome! Suggested areas:

- New repository backends (e.g., cloud object stores)
- Preview generation and richer details view
- Bulk operations (batch import/update)
- CI helpers for validating repository integrity

Please open an issue to discuss significant changes before submitting a PR.

## Roadmap (Ideas)

- Smarter duplicate detection and conflict resolution on submit
- Optional permissions/roles layer for HTTP backend
- Advanced search (e.g., AND/OR on tags, saved searches)

## License

MIT License

## Acknowledgements

- Unity and the Editor ecosystem
- Contributors and teams using this workflow

## Todo

- [x] Manage updates (with version increment)
- [x] Edit description (increments version)
- [x] Investigate automatic images/thumbnails
- [x] Test in remote mode
- [ ] Fix async usage (performance)
- [x] Add comments
- [ ] Auto-upload function for the database
- [ ] Function to check for model updates
- [x] Filter by project name: add a project tag so only models for project "XXX" are visible; if opened from project "YYY" the model is not visible
- [x] Add changelog to models
- [x] Show model triangle/vertex counts
- [x] Improve the browser by adding model images
- [x] Notes system: a model's notes are shared across versions (and add a note on each new submission)
- [x] Asset upload date
- [x] Add the asset path (set a default if unspecified) with the ability to change it later

- [ ] Ensure the relative path is not the Materials folder, e.g., `"RelativePath": "Models/Benne/Materials"`
- [ ] Validate changelogs thoroughly
- [ ] Ensure JSON files are not serialized (store elsewhere, e.g., Library)
- [ ] Detect if an object already exists in the database and prevent saving if so
- [ ] Issue with project tag: convert it into a normal tag
- [ ] Differentiate account roles: "artist" vs "dev"
- [ ] If a model has a note, show a notification in the tool
- [ ] Notify on model updates (Meta Quest-like notifications)
- [ ] Explore notifications without launching the tool
- [ ] Major performance issue (likely await/async)
- [ ] Create an image-only grid; clicking an image opens the current bundle description window (details view)
- [ ] First-run wizard not working
- [ ] For tags, ensure OrdinalIgnoreCase is used
- [ ] Investigate duplicate GUID error issues
