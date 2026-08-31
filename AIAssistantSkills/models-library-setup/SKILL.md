---
name: models-library-setup
description: >
  Use when configuring Models Library: first-run wizard, user name and role,
  File System vs HTTP repository, repository path or URL, empty catalog,
  connection test, or "nothing loads".
required_editor_version: ">=6000.2"
---

# Models Library setup

## First run

1. Open `Tools > Model Library`.
2. Complete the wizard: **Welcome → Your Identity → Repository Setup → Summary**.
3. Identity: user name plus role (Developer / Artist / Admin).
4. Repository: prefer **File System** with an absolute path or UNC share. HTTP needs a base URL and is experimental.
5. Use **Test Connection** on the repository step or later in Settings → Repository.

Re-run the wizard from the main window **Settings** menu → Configuration Wizard.

## Settings

In the Model Library window: **Settings** → User Settings and Repository Settings.

- Repository kind and root live on the `ModelLibrarySettings` asset at `Assets/ModelLibrary/Resources/ModelLibrarySettings.asset`.
- User name and role are stored in `EditorPrefs` (client-side UI gating only).
- Local cache default: `Library/ModelLibraryCache`.

Index maintenance (File System only): preview/rebuild `models_index.json` from on-disk `model.json` files — only when the user asks, and confirm before rebuilding.

## Empty catalog or "nothing loads"

- Confirm the repository root exists and contains `models_index.json` (or is an empty new share the team will submit into).
- File System: path must be reachable (VPN, share permissions).
- HTTP: do not invent authentication. The built-in HTTP repository is experimental; prefer a shared folder.
- Stale cache: delete the affected folder under `Library/ModelLibraryCache` and retry.
- Play mode: the window will not open while the Editor is playing.

Do not write `models_index.json` by hand unless the user is recovering a broken index and understands the layout.
