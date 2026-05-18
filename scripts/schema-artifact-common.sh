#!/usr/bin/env bash

require_python3() {
  if ! command -v python3 >/dev/null 2>&1; then
    echo "Required tool is missing: python3" >&2
    return 1
  fi
}

assert_json_manifest_package_version() {
  local manifest_path="$1"
  local expected_version="$2"
  local label="$3"

  require_python3

  python3 - "${manifest_path}" "${expected_version}" "${label}" <<'PY'
import json
import sys

manifest_path = sys.argv[1]
expected_version = sys.argv[2]
label = sys.argv[3]

try:
    with open(manifest_path, "r", encoding="utf-8") as manifest_file:
        manifest = json.load(manifest_file)
except Exception as exception:
    print(f"{label} could not be parsed as JSON: {exception}", file=sys.stderr)
    sys.exit(1)

if not isinstance(manifest, dict):
    print(f"{label} must be a JSON object.", file=sys.stderr)
    sys.exit(1)

actual_version = manifest.get("packageVersion")
if actual_version != expected_version:
    print(
        f"{label} has an unexpected packageVersion. "
        f"Expected: {expected_version}. Actual: {actual_version}",
        file=sys.stderr)
    sys.exit(1)
PY
}

reject_unsafe_schema_tree_entries() {
  local schema_root="$1"
  local label="$2"
  local entry

  if [[ ! -d "${schema_root}" ]]; then
    echo "Generated schemas ${label} does not exist: ${schema_root}" >&2
    return 1
  fi

  while IFS= read -r -d '' entry; do
    case "${entry}" in
      *$'\n'*|*$'\r'*)
        echo "Generated schemas ${label} contains a path with a newline: ${entry}" >&2
        return 1
        ;;
    esac
  done < <(find "${schema_root}" -print0)

  while IFS= read -r entry; do
    echo "Generated schemas ${label} contains unsupported non-regular path: ${entry}" >&2
    return 1
  done < <(find "${schema_root}" ! -type f ! -type d -print | sort)
}
