#!/usr/bin/env python3
"""Verify staged C# files against .editorconfig via dotnet format.

Looks for Unity-generated projects:
  - __ModelLibrary.Editor.csproj
  - ModelLibrary.Tests.csproj

Search order:
  1. MODELLIBRARY_UNITY_PROJECT environment variable (Unity project root)
  2. Walk upward from this package root
  3. Current working directory parents

When this package is cloned as a standalone git repo (not embedded under a
Unity project), those .csproj files do not exist. In that case the hook
skips with a warning and exits 0 so commits are not blocked. Other hooks
(editorconfig-checker, etc.) still run.

Set MODELLIBRARY_DOTNET_FORMAT_REQUIRED=1 to fail instead of skipping when
projects are missing (useful for CI that opens the package in Unity).

Only changed files passed by pre-commit are checked, so legacy style debt
outside the staged set does not block unrelated commits.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple


EDITOR_PROJECT_NAME = "__ModelLibrary.Editor.csproj"
TESTS_PROJECT_NAME = "ModelLibrary.Tests.csproj"
PACKAGE_RELATIVE_PREFIX = Path("Assets") / "ModelLibrary"
REQUIRED_ENV = "MODELLIBRARY_DOTNET_FORMAT_REQUIRED"


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def _env_truthy(name: str) -> bool:
    value = os.environ.get(name, "").strip().lower()
    return value in ("1", "true", "yes", "on")


def _is_test_file(relative_path: Path) -> bool:
    parts = [part.lower() for part in relative_path.parts]
    return "tests" in parts or relative_path.name.endswith("Tests.cs")


def _candidate_roots(repo_root: Path) -> List[Path]:
    roots: List[Path] = []
    env_root = os.environ.get("MODELLIBRARY_UNITY_PROJECT", "").strip()
    if env_root:
        roots.append(Path(env_root).expanduser().resolve())

    current = repo_root.resolve()
    for _ in range(8):
        roots.append(current)
        if current.parent == current:
            break
        current = current.parent

    cwd = Path.cwd().resolve()
    if cwd not in roots:
        roots.append(cwd)

    unique: List[Path] = []
    seen = set()
    for root in roots:
        key = str(root)
        if key in seen:
            continue
        seen.add(key)
        unique.append(root)
    return unique


def _find_project(project_name: str, repo_root: Path) -> Optional[Path]:
    for root in _candidate_roots(repo_root):
        candidate = root / project_name
        if candidate.is_file():
            return candidate
    return None


def _to_unity_relative(path: Path, repo_root: Path, unity_root: Path) -> Optional[str]:
    absolute = path if path.is_absolute() else (repo_root / path)
    absolute = absolute.resolve()
    try:
        relative_to_repo = absolute.relative_to(repo_root.resolve())
    except ValueError:
        return None

    unity_relative = PACKAGE_RELATIVE_PREFIX / relative_to_repo
    full = (unity_root / unity_relative).resolve()
    if not full.exists():
        # Fallback: path may already be under the Unity project.
        try:
            return str(absolute.relative_to(unity_root.resolve())).replace("\\", "/")
        except ValueError:
            return None
    return str(unity_relative).replace("\\", "/")


def _group_files(
    filenames: Sequence[str],
    repo_root: Path,
) -> Tuple[Dict[str, List[str]], List[str], bool]:
    """Group staged files by project.

    Returns (grouped, unmapped_files, projects_available).
    projects_available is False when no Unity-generated .csproj was found.
    """
    editor_project = _find_project(EDITOR_PROJECT_NAME, repo_root)
    tests_project = _find_project(TESTS_PROJECT_NAME, repo_root)
    projects_available = editor_project is not None or tests_project is not None

    grouped: Dict[str, List[str]] = {}
    unmapped: List[str] = []

    for filename in filenames:
        relative = Path(filename)
        if relative.suffix.lower() != ".cs":
            continue

        wants_tests = _is_test_file(relative)
        project = tests_project if wants_tests and tests_project is not None else editor_project
        if project is None:
            unmapped.append(filename)
            continue

        unity_root = project.parent
        include_path = _to_unity_relative(relative, repo_root, unity_root)
        if include_path is None:
            print(
                f"dotnet-format: skipping path outside package: {filename}",
                file=sys.stderr,
            )
            continue

        key = str(project)
        if key not in grouped:
            grouped[key] = []
        if include_path not in grouped[key]:
            grouped[key].append(include_path)

    return grouped, unmapped, projects_available


def _run_dotnet_format(project: Path, include_paths: Sequence[str]) -> int:
    include_arg = ",".join(include_paths)
    common = [
        "dotnet",
        "format",
        str(project),
        "--include",
        include_arg,
        "--no-restore",
    ]

    commands = [
        common[:2]
        + ["whitespace"]
        + common[2:]
        + ["--verify-no-changes"],
        common[:2]
        + ["style"]
        + common[2:]
        + ["--severity", "error", "--verify-no-changes"],
    ]

    for command in commands:
        print(" ".join(command))
        completed = subprocess.run(command, check=False)
        if completed.returncode != 0:
            return completed.returncode
    return 0


def _print_missing_projects_help(unmapped: Sequence[str]) -> None:
    searched = os.environ.get("MODELLIBRARY_UNITY_PROJECT", "").strip()
    search_hint = (
        f"  MODELLIBRARY_UNITY_PROJECT={searched}\n"
        if searched
        else "  (MODELLIBRARY_UNITY_PROJECT is unset; walked package/cwd parents)\n"
    )
    print(
        "dotnet-format: Unity-generated projects were not found.\n"
        f"  Expected: {EDITOR_PROJECT_NAME} (and optionally {TESTS_PROJECT_NAME})\n"
        f"{search_hint}"
        "  This is normal when the package repo is cloned standalone (no parent\n"
        "  Unity project). Open the package in Unity once to generate .csproj\n"
        "  files, or set MODELLIBRARY_UNITY_PROJECT to a Unity project root that\n"
        "  already contains them.\n"
        "  Staged C# files that were not checked:\n"
        + "\n".join(f"    - {path}" for path in unmapped),
        file=sys.stderr,
    )


def main(argv: Sequence[str]) -> int:
    filenames = [arg for arg in argv if arg.endswith(".cs")]
    if not filenames:
        return 0

    repo_root = _repo_root()
    grouped, unmapped, projects_available = _group_files(filenames, repo_root)

    if not projects_available:
        _print_missing_projects_help(unmapped)
        if _env_truthy(REQUIRED_ENV):
            print(
                f"dotnet-format: failing because {REQUIRED_ENV} is set.\n"
                "  Unset it to allow commits without a Unity .csproj, or point\n"
                "  MODELLIBRARY_UNITY_PROJECT at a Unity project that has generated\n"
                "  the expected projects.",
                file=sys.stderr,
            )
            return 1

        print(
            "dotnet-format: skipping style check (no Unity project).\n"
            "  editorconfig-checker and other hooks still apply.\n"
            f"  To require this check: set {REQUIRED_ENV}=1",
            file=sys.stderr,
        )
        return 0

    if unmapped:
        # Projects exist, but some staged files could not be mapped (unexpected).
        print(
            "dotnet-format: some staged C# files could not be mapped to a project:\n"
            + "\n".join(f"    - {path}" for path in unmapped),
            file=sys.stderr,
        )
        return 1

    if not grouped:
        return 0

    # Restore once per project before --no-restore format passes.
    for project_path in grouped:
        restore = subprocess.run(
            ["dotnet", "restore", project_path],
            check=False,
        )
        if restore.returncode != 0:
            print(
                f"dotnet-format: restore failed for {project_path}",
                file=sys.stderr,
            )
            return restore.returncode

    for project_path, include_paths in grouped.items():
        exit_code = _run_dotnet_format(Path(project_path), include_paths)
        if exit_code != 0:
            print(
                "dotnet-format: formatting/style check failed.\n"
                "  Fix with (from the Unity project root):\n"
                f"    dotnet format whitespace \"{project_path}\" --include {','.join(include_paths)}\n"
                f"    dotnet format style \"{project_path}\" --include {','.join(include_paths)} --severity error\n"
                "  Or skip temporarily: SKIP=dotnet-format git commit ...",
                file=sys.stderr,
            )
            return exit_code

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
