---
name: models-library-browse-import
description: >
  Use when browsing or importing Models Library assets: search, tags, favorites,
  Installed or Update Available badges, Import to Project, GUID Keep, 3D preview,
  version compare, or notes.
required_editor_version: ">=6000.2"
---

# Browse and import

## Browser

Open `Tools > Model Library`. Toolbar: search, All / Favorites / Recent, tag filter, sort, list/grid/image view.

- Open a card or **Details** for metadata, tags, notes, versions.
- Installed models show **Installed**. Newer repository versions show **Update Available**.
- Artists/Admins can start **New Version** from details or cards (opens submit in Update Existing).

## Import and update

From details (or list actions):

- **Import to Project** — copies the version into the project (default `Assets/Models/<ModelName>/`).
- **Update** — when a newer SemVer is available. GUIDs are preserved where possible.

Do this in the UI. Do not copy payload folders into `Assets` yourself.

When Unity reports a GUID conflict, advise **Keep** if the user needs existing scene/prefab references.

## Preview and compare

- **3D Preview** from details.
- **Compare Versions** to diff two versions of the same model.

## Troubleshooting

- Import failed: use the error dialog retry path; Error Log is under the window Settings menu.
- Access-denied or stale cache after VCS discard / version delete / preview before import: delete `Library/ModelLibraryCache/<modelId>/<version>` and retry.
- Texture relink issues: 3D Preview logs details.

Notes: anyone can add notes on details. Description/tags/version management are UI-gated to Artist/Admin.
