---
name: models-library-submit
description: >
  Use when submitting a Models Library model: new entry vs update existing,
  SemVer, changelog, FBX/OBJ and materials, preview images, install path, or
  Assets > Model Library > Submit Model.
required_editor_version: ">=6000.2"
---

# Submit models

Submission is UI-gated to **Artist** and **Admin**. Developers should switch role in Settings (advisory only) or ask an Artist.

## How to open

1. Select FBX/OBJ (and related materials/textures) in the Project window.
2. `Assets > Model Library > Submit Model`, or Actions → Submit Model in the library window.

From code (same assembly / Assistant script):

- `ModelLibrary.Editor.Windows.ModelSubmitWindow.Open()`
- `ModelLibrary.Editor.Windows.ModelSubmitWindow.Open(resolveMeshDependencies: true)`
- `ModelLibrary.Editor.Windows.ModelSubmitWindow.OpenForUpdate(modelId)` — Update Existing for that catalog id, no Project assets attached.

**New Version** on details or browser cards also opens Update Existing for that model.

Play mode blocks submit.

## Form

- **New** — name, SemVer, description, tags, assets, install path, optional preview images.
- **Update Existing** — pick a catalog model (searchable). Changelog summary is **required**.
- Default install path: `Assets/Models/<ModelName>/` (must stay under `Assets/`).
- Payload allowlist: meshes (`.fbx`, `.obj`), textures (`.png`, `.jpg`, `.jpeg`, `.tga`, `.psd`), `.mat`. Scripts and shaders are rejected on upload.

Drafts auto-save; do not treat a draft as a completed submit.

## After submit

The repository layout is `<root>/<modelId>/<version>/` with `model.json`, `payload/`, `images/`. The global `models_index.json` is updated by the tool.

Do not hand-edit those files unless recovering a broken repository. Confirm with the user before deleting a version or an entire model.
