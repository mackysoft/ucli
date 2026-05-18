#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <package-dir> <expected-version>" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/schema-artifact-common.sh
source "${script_dir}/schema-artifact-common.sh"

repository_root="$(cd "${script_dir}/.." && pwd)"
package_dir="$1"
expected_version="$2"

if [[ ! -d "${package_dir}" ]]; then
  echo "Schema artifact package directory does not exist: ${package_dir}" >&2
  exit 1
fi

require_python3
reject_unsafe_schema_tree_entries "${repository_root}/schemas" "source tree"

package_dir="$(cd "${package_dir}" && pwd)"
archive_path="${package_dir}/MackySoft.Ucli.Schemas.${expected_version}.zip"
if [[ ! -f "${archive_path}" ]]; then
  echo "Schema artifact archive was not created: ${archive_path}" >&2
  exit 1
fi

python3 - "${repository_root}" "${archive_path}" "${expected_version}" <<'PY'
import json
import os
import posixpath
import stat
import sys
import zipfile

repository_root = sys.argv[1]
archive_path = sys.argv[2]
expected_version = sys.argv[3]
schema_root = os.path.join(repository_root, "schemas")

expected_entries = []
for root, _, files in os.walk(schema_root):
    for file_name in files:
        full_path = os.path.join(root, file_name)
        relative_path = os.path.relpath(full_path, repository_root).replace(os.sep, "/")
        expected_entries.append(relative_path)

expected_entries.sort()
expected_entry_set = set(expected_entries)

with zipfile.ZipFile(archive_path) as archive:
    infos = archive.infolist()
    actual_entries = [info.filename for info in infos]

    if not actual_entries:
        print(f"Schema artifact archive is empty: {archive_path}", file=sys.stderr)
        sys.exit(1)

    actual_entry_set = set(actual_entries)
    if actual_entry_set != expected_entry_set:
        missing = sorted(expected_entry_set - actual_entry_set)
        unexpected = sorted(actual_entry_set - expected_entry_set)
        if missing:
            print("Schema artifact archive is missing entries:", file=sys.stderr)
            for entry in missing:
                print(entry, file=sys.stderr)
        if unexpected:
            print("Schema artifact archive contains unexpected entries:", file=sys.stderr)
            for entry in unexpected:
                print(entry, file=sys.stderr)
        sys.exit(1)

    for info in infos:
        name = info.filename
        if "\n" in name or "\r" in name:
            print(f"Schema artifact archive contains a path with a newline: {name!r}", file=sys.stderr)
            sys.exit(1)

        if name.startswith("/") or name.endswith("/") or not name.startswith("schemas/v1/"):
            print(f"Schema artifact archive contains an invalid entry path: {name}", file=sys.stderr)
            sys.exit(1)

        normalized_name = posixpath.normpath(name)
        if normalized_name != name or normalized_name.startswith("../") or "/../" in normalized_name:
            print(f"Schema artifact archive contains a traversing entry path: {name}", file=sys.stderr)
            sys.exit(1)

        file_type = (info.external_attr >> 16) & 0o170000
        if file_type and file_type != stat.S_IFREG:
            print(f"Schema artifact archive contains a non-regular entry: {name}", file=sys.stderr)
            sys.exit(1)

    manifest_entry = "schemas/v1/schema-manifest.json"
    if manifest_entry not in actual_entry_set:
        print(f"Schema artifact archive is missing required entry: {manifest_entry}", file=sys.stderr)
        sys.exit(1)

    manifest = json.loads(archive.read(manifest_entry))
    if not isinstance(manifest, dict):
        print("Schema artifact manifest must be a JSON object.", file=sys.stderr)
        sys.exit(1)

    actual_version = manifest.get("packageVersion")
    if actual_version != expected_version:
        print(
            f"Schema artifact manifest has an unexpected packageVersion. "
            f"Expected: {expected_version}. Actual: {actual_version}",
            file=sys.stderr)
        sys.exit(1)
PY

echo "Schema artifact archive verified: ${archive_path}"
