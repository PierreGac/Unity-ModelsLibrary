---
name: models-library
description: >
  Use when the user works with Models Library: browse, import, update, or
  submit 3D models from a shared repository. Activate for Tools > Model Library,
  the catalog, tags, versions, repository setup, or Model Library API questions.
required_editor_version: ">=6000.2"
---

# Models Library

Editor tools for browsing, versioning, submitting, and importing 3D models from a shared repository. Package: `com.models-library`. Unity **6000.2+**, Editor only.

Prefer the Editor UI over writing repository files by hand. Do not invent windows or menus that are not listed here.

## Menus

- `Tools > Model Library` — main window (browser, settings, submit, details, help)
- `Assets > Model Library > Submit Model`
- `Assets > Model Library > Open in Model Library`
- `Assets > Model Library > Check for Updates`
- `Assets > Model Library > View Details`

Settings and secondary views (Help, shortcuts, analytics, batch upload, error log, profiler) open **inside** this window, not as separate Unity menus.

## Facts the user must not be wrong about

- **File System** (folder / UNC) is the supported repository. **HTTP** is experimental.
- Roles (**Developer**, **Artist**, **Admin**) only hide or show Editor UI. They are not a security boundary. Real access control is folder ACLs or the HTTP server.
- Default install destination is `Assets/Models/<ModelName>/` (overridable per model).
- After import, each install folder has `.modelLibrary.meta.json` (legacy `modelLibrary.meta.json` is still detected).
- Local cache: `Library/ModelLibraryCache`.
- On Unity GUID import conflicts, prefer **Keep** when the user needs to preserve references.

## Other skills

- Setup, first-run, empty catalog, connection issues → `models-library-setup`
- Search, tags, Installed / Update Available, import, 3D preview → `models-library-browse-import`
- New model vs update, SemVer, changelog, FBX/OBJ → `models-library-submit`
- Custom `IModelRepository`, `ModelLibraryService`, window `Open` methods → `models-library-api`
