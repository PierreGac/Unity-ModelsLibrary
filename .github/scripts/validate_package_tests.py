"""Fixture checks for the static package job.

A valid tree does not need CHANGELOG.md. A folder renamed with a trailing ~
so Unity ignores it passes when it contains no .meta files. The same tree
fails when it contains the private UNC path, a missing imported-asset .meta
file, or a .meta file left inside the renamed folder.
"""

import os
import shutil
import subprocess
import sys
import tempfile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
VALIDATOR = os.path.join(SCRIPT_DIR, "validate_package.py")
PACKAGE_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))

sys.path.insert(0, SCRIPT_DIR)
import validate_package  # noqa: E402


def main():
    failures = []
    run_case(failures, "tilde folder without meta passes", expect_pass, make_valid_tree)
    run_case(failures, "renamed skill and script folders pass", expect_pass, make_renamed_ignored_folders_tree)
    run_case(failures, "private UNC path fails", expect_unc_failure, make_unc_tree)
    run_case(failures, "missing imported-asset meta fails", expect_missing_meta_failure, make_missing_meta_tree)
    run_case(failures, "meta inside tilde folder fails", expect_tilde_meta_failure, make_tilde_meta_tree)
    run_case(failures, "current package passes", expect_pass, use_package_root)

    if failures:
        for failure in failures:
            print(failure)
        return 1

    print("Static package fixture checks passed.")
    return 0


def run_case(failures, name, assertion, factory):
    root = factory()
    try:
        completed = run_validator(root)
        assertion(name, completed, failures)
    finally:
        if root != PACKAGE_ROOT:
            shutil.rmtree(root, ignore_errors=True)


def make_valid_tree():
    root = tempfile.mkdtemp(prefix="package-check-")
    write(root, "package.json", package_json())
    write(root, "package.json.meta", "fileFormatVersion: 2\n")
    write(root, "LICENSE", "MIT\n")
    write(root, "LICENSE.meta", "fileFormatVersion: 2\n")
    os.makedirs(os.path.join(root, "Editor"))
    write(root, "Editor.meta", "fileFormatVersion: 2\n")
    write(root, os.path.join("Editor", "Keep.txt"), "keep\n")
    write(root, os.path.join("Editor", "Keep.txt.meta"), "fileFormatVersion: 2\n")
    os.makedirs(os.path.join(root, "Documentation~"))
    write(root, os.path.join("Documentation~", "notes.txt"), "docs\n")
    os.makedirs(os.path.join(root, "Samples~"))
    write(root, os.path.join("Samples~", "example.txt"), "sample\n")
    return root


def make_renamed_ignored_folders_tree():
    root = make_valid_tree()
    skill_dir = os.path.join("AIAssistantSkills~", "models-library-api")
    os.makedirs(os.path.join(root, skill_dir))
    write(root, os.path.join(skill_dir, "SKILL.md"), "skill\n")
    os.makedirs(os.path.join(root, "scripts~"))
    write(root, os.path.join("scripts~", "dotnet_format_verify.py"), "print('ok')\n")
    return root


def make_unc_tree():
    root = make_valid_tree()
    write(root, os.path.join("Editor", "Keep.txt"), validate_package.PRIVATE_UNC + "\n")
    return root


def make_missing_meta_tree():
    root = make_valid_tree()
    write(root, os.path.join("Editor", "Missing.cs"), "class Missing {}\n")
    return root


def make_tilde_meta_tree():
    root = make_valid_tree()
    write(root, os.path.join("Samples~", "example.txt.meta"), "fileFormatVersion: 2\n")
    return root


def use_package_root():
    return PACKAGE_ROOT


def expect_pass(name, completed, failures):
    if completed.returncode != 0:
        failures.append(name + " expected pass, exit " + str(completed.returncode) + "\n" + completed.stdout + completed.stderr)


def expect_unc_failure(name, completed, failures):
    if completed.returncode == 0 or "Private UNC path" not in completed.stdout:
        failures.append(name + " expected a private UNC failure\n" + completed.stdout + completed.stderr)


def expect_missing_meta_failure(name, completed, failures):
    if completed.returncode == 0 or "Missing meta:" not in completed.stdout:
        failures.append(name + " expected a missing meta failure\n" + completed.stdout + completed.stderr)


def expect_tilde_meta_failure(name, completed, failures):
    if completed.returncode == 0 or "Meta inside ignored folder:" not in completed.stdout:
        failures.append(name + " expected a tilde-folder meta failure\n" + completed.stdout + completed.stderr)


def run_validator(root):
    return subprocess.run(
        [sys.executable, VALIDATOR, root],
        capture_output=True,
        text=True,
    )


def package_json():
    return (
        "{\n"
        '  "name": "' + validate_package.PACKAGE_NAME + '",\n'
        '  "version": "' + package_version() + '",\n'
        '  "unity": "' + validate_package.UNITY_VERSION + '",\n'
        '  "unityRelease": "' + validate_package.UNITY_RELEASE + '",\n'
        '  "license": "' + validate_package.LICENSE_ID + '",\n'
        '  "repository": { "type": "git", "url": "' + validate_package.REPOSITORY_URL + '" }\n'
        "}\n"
    )


def package_version():
    manifest_path = os.path.join(PACKAGE_ROOT, "package.json")
    with open(manifest_path, "r", encoding="utf-8-sig") as handle:
        manifest = __import__("json").loads(handle.read())
    return manifest["version"]


def write(root, relative_path, contents):
    path = os.path.join(root, relative_path)
    parent = os.path.dirname(path)
    if parent and not os.path.isdir(parent):
        os.makedirs(parent)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(contents)


if __name__ == "__main__":
    sys.exit(main())
