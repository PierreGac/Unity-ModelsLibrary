# Contributing to Models Library

Thanks for contributing. This package follows the coding standards in `.editorconfig` and `AGENTS.md`.

## Development setup

1. Open the package inside a Unity **6000.2+** project (or add it via Package Manager).
2. Let Unity generate `__ModelLibrary.Editor.csproj` / `ModelLibrary.Tests.csproj` at the Unity project root.
3. Install [pre-commit](https://pre-commit.com/) and enable hooks:

```bash
pip install pre-commit
python -m pre_commit install
```

On Windows, use `python -m pre_commit …` if the `pre-commit` command is not found (Scripts folder often missing from PATH after `pip --user`). To add it permanently, put your Python Scripts directory on PATH (for example `%APPDATA%\Python\Python314\Scripts`), then reopen the terminal.

Optional: point at a specific Unity project root if the `.csproj` files are not found by walking upward:

```bash
# PowerShell
$env:MODELLIBRARY_UNITY_PROJECT = "C:\path\to\YourUnityProject"

# bash
export MODELLIBRARY_UNITY_PROJECT=/path/to/YourUnityProject
```

**Standalone package clone:** if this repo is not under a Unity project (no generated `.csproj`), `dotnet-format` **skips with a warning** and the commit still proceeds. Other hooks (`editorconfig-checker`, etc.) still run. To fail instead when projects are missing (e.g. CI that embeds the package in Unity):

```bash
# PowerShell
$env:MODELLIBRARY_DOTNET_FORMAT_REQUIRED = "1"

# bash
export MODELLIBRARY_DOTNET_FORMAT_REQUIRED=1
```

## Pre-commit hooks

On every commit, hooks run:

| Hook | What it enforces |
|------|------------------|
| `trailing-whitespace` / `end-of-file-fixer` | Clean text files (skips Unity `.meta`) |
| `check-merge-conflict` / `check-yaml` / `check-json` | Basic integrity |
| `check-added-large-files` | Blocks files larger than 1 MB |
| `editorconfig-checker` | Indentation, charset (UTF-8 BOM for `.cs`), final newlines from `.editorconfig` |
| `dotnet-format` | C# whitespace + error-severity style from `.editorconfig` on **staged** `.cs` when Unity `.csproj` files are available; otherwise skips |

Run manually:

```bash
python -m pre_commit run --all-files
python -m pre_commit run dotnet-format --files Editor/Infrastructure/Utils/SemVer.cs
```

Skip a hook temporarily (escape hatch only):

```bash
# bash / Git Bash
SKIP=dotnet-format git commit -m "..."

# PowerShell
$env:SKIP = "dotnet-format"; git commit -m "..."
```

### Fixing `dotnet format` failures

From the Unity project root (where `__ModelLibrary.Editor.csproj` lives):

```bash
dotnet format whitespace __ModelLibrary.Editor.csproj --include Assets/ModelLibrary/Editor/Path/To/File.cs
dotnet format style __ModelLibrary.Editor.csproj --include Assets/ModelLibrary/Editor/Path/To/File.cs --severity error
```

The hook only checks files in the commit, so older style debt in untouched files does not block you.

## Coding standards

- Prefer explicit types (no `var`) — enforced by `.editorconfig` / `dotnet format`
- Follow naming rules in `.editorconfig` (`_privateField`, `IInterface`, `CONST` / `__PRIVATE_CONST`)
- Keep editor code under `Editor/`
- Target **.NET Standard 2.1** (no `IReadOnlySet<T>` or other .NET 5+ APIs)
- See `AGENTS.md` for architecture and Unity-specific rules

## Pull requests

- Use conventional commits: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`
- Keep changes focused; update `CHANGELOG.md` when user-facing behavior changes
- Add or update Edit Mode tests under `Editor/Tests/` when fixing logic

## License

By contributing, you agree that your contributions are licensed under the MIT License (see `LICENSE`).
