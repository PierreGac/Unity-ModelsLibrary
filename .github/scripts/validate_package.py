"""Static package checks for the Models Library UPM root.

Checks manifest metadata, imported-asset .meta pairing, and forbidden release
content. Changelog alignment is not checked: the published repository does not
ship CHANGELOG.md. Folders whose names end in ~, including Documentation~,
Samples~, AIAssistantSkills~, and scripts~, are Unity-ignored: files there do
not need .meta siblings, and a .meta file inside them fails the check. The
.git directory is version-control metadata and is not release content.
"""

import json
import os
import re
import sys

PACKAGE_NAME = "com.models-library"
UNITY_VERSION = "6000.2"
UNITY_RELEASE = "6f2"
LICENSE_ID = "MIT"
REPOSITORY_URL = "https://github.com/PierreGac/Unity-ModelsLibrary.git"
SUBFOLDER_GIT_QUERY = "?path=Assets/ModelLibrary"
LICENSE_FILE_NAME = "LICENSE"
LICENSE_MARKDOWN_FILE_NAME = "LICENSE.md"
META_EXTENSION = ".meta"
GIT_DIRECTORY_NAME = ".git"
TILDE_SUFFIX = "~"
MAX_CONTENT_SCAN_BYTES = 5 * 1024 * 1024

# Assembled at runtime so this source file does not contain the historical path.
PRIVATE_HOST = ".".join(["10", "72", "100", "18"])
PRIVATE_SHARE = "\\".join(["Ressources", "3D", "Assets3D", "_ModelLibrary"])
PRIVATE_UNC = "\\\\" + PRIVATE_HOST + "\\" + PRIVATE_SHARE

FORBIDDEN_FILE_NAMES = (
    "test.html",
    "test.html.txt",
    "ModelLibrary.zip.txt",
    "Todo.txt",
)

RFC1918_UNC_PATTERN = re.compile(
    r"\\\\(?:10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[01])(?:\.\d{1,3}){2})\\",
    re.IGNORECASE,
)


def main():
    package_root = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
    package_root = os.path.abspath(package_root)
    problems = []
    check_manifest(package_root, problems)
    collect_pairing_problems(package_root, package_root, problems)
    check_forbidden_release_content(package_root, problems)
    if problems:
        for problem in problems:
            print(problem)
        return 1
    print("Package checks passed: " + package_root)
    return 0


def check_manifest(package_root, problems):
    manifest_path = os.path.join(package_root, "package.json")
    if not os.path.isfile(manifest_path):
        problems.append("Missing package.json")
        return ""

    raw = read_text(manifest_path)
    if SUBFOLDER_GIT_QUERY in raw:
        problems.append("package.json still contains " + SUBFOLDER_GIT_QUERY)

    try:
        manifest = json.loads(raw)
    except json.JSONDecodeError as error:
        problems.append("package.json is not valid JSON: " + str(error))
        return ""

    expect_equal(problems, "package.json name", manifest.get("name"), PACKAGE_NAME)
    expect_equal(problems, "package.json unity", manifest.get("unity"), UNITY_VERSION)
    expect_equal(problems, "package.json unityRelease", manifest.get("unityRelease"), UNITY_RELEASE)
    expect_equal(problems, "package.json license", manifest.get("license"), LICENSE_ID)

    version = manifest.get("version")
    if not isinstance(version, str) or version.strip() == "":
        problems.append("package.json version is missing")
        version = ""

    repository = manifest.get("repository")
    repository_url = ""
    if isinstance(repository, dict):
        repository_url = repository.get("url") or ""
    elif isinstance(repository, str):
        repository_url = repository
    if repository_url != REPOSITORY_URL:
        problems.append("package.json repository url is " + repr(repository_url) + ", expected " + REPOSITORY_URL)

    license_path = os.path.join(package_root, LICENSE_FILE_NAME)
    if not os.path.isfile(license_path):
        problems.append("Missing LICENSE")
    if os.path.isfile(os.path.join(package_root, LICENSE_MARKDOWN_FILE_NAME)):
        problems.append("LICENSE.md competes with the manifest license; keep LICENSE only")

    return version


def collect_pairing_problems(package_root, directory, problems):
    child_names = sorted(os.listdir(directory))
    for name in child_names:
        child_path = os.path.join(directory, name)
        if not os.path.isdir(child_path):
            continue
        if name == GIT_DIRECTORY_NAME:
            continue
        if is_ignored_by_unity(name):
            assert_no_meta_files(child_path, problems)
            continue

        relative_directory = relative_to_root(package_root, child_path)
        if not os.path.isfile(child_path + META_EXTENSION):
            problems.append("Missing folder meta: " + relative_directory)
        collect_pairing_problems(package_root, child_path, problems)

    for name in child_names:
        child_path = os.path.join(directory, name)
        if not os.path.isfile(child_path):
            continue
        if is_ignored_by_unity(name):
            continue

        relative_file = relative_to_root(package_root, child_path)
        if name.lower().endswith(META_EXTENSION):
            asset_path = child_path[: -len(META_EXTENSION)]
            if not os.path.isfile(asset_path) and not os.path.isdir(asset_path):
                problems.append("Orphan meta: " + relative_file)
        elif not os.path.isfile(child_path + META_EXTENSION):
            problems.append("Missing meta: " + relative_file)


def assert_no_meta_files(directory, problems):
    for dirpath, dirnames, filenames in os.walk(directory):
        dirnames[:] = [name for name in dirnames if name != GIT_DIRECTORY_NAME]
        for filename in filenames:
            if filename.lower().endswith(META_EXTENSION):
                problems.append("Meta inside ignored folder: " + os.path.join(dirpath, filename))


def check_forbidden_release_content(package_root, problems):
    for dirpath, dirnames, filenames in os.walk(package_root):
        dirnames[:] = [name for name in dirnames if name != GIT_DIRECTORY_NAME]
        for filename in filenames:
            if filename.lower() in forbidden_name_set():
                problems.append("Forbidden release file: " + relative_to_root(package_root, os.path.join(dirpath, filename)))
            file_path = os.path.join(dirpath, filename)
            scan_file_for_private_unc(package_root, file_path, problems)


def scan_file_for_private_unc(package_root, file_path, problems):
    try:
        size = os.path.getsize(file_path)
    except OSError:
        return
    if size > MAX_CONTENT_SCAN_BYTES:
        problems.append("Skipped oversized file during content scan: " + relative_to_root(package_root, file_path))
        return

    try:
        with open(file_path, "rb") as handle:
            payload = handle.read()
    except OSError:
        return

    text = payload.decode("utf-8", errors="ignore")
    if PRIVATE_UNC in text or RFC1918_UNC_PATTERN.search(text):
        problems.append("Private UNC path: " + relative_to_root(package_root, file_path))


def forbidden_name_set():
    names = set()
    for name in FORBIDDEN_FILE_NAMES:
        names.add(name.lower())
    return names


def is_ignored_by_unity(name):
    return name.endswith(TILDE_SUFFIX) or name.startswith(".")


def expect_equal(problems, label, actual, expected):
    if actual != expected:
        problems.append(label + " is " + repr(actual) + ", expected " + repr(expected))


def relative_to_root(package_root, path):
    return os.path.relpath(path, package_root)


def read_text(path):
    with open(path, "r", encoding="utf-8-sig") as handle:
        return handle.read()


if __name__ == "__main__":
    sys.exit(main())
