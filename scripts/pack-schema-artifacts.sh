#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/pack-schema-artifacts.sh --version <version> [--repo-root <path>] [--output <dir>]

Creates MackySoft.Ucli.Schemas.<version>.zip with schemas/v1 as the archive root.
EOF
}

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/schema-artifact-common.sh
source "${script_dir}/schema-artifact-common.sh"

repository_root="$(cd "${script_dir}/.." && pwd)"
package_version=""
output_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo-root)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      repository_root="$(cd "$2" && pwd)"
      shift 2
      ;;
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_version="$2"
      shift 2
      ;;
    --output)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      output_dir="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ -z "${package_version}" ]]; then
  usage
  exit 2
fi

if ! command -v zip >/dev/null 2>&1; then
  echo "Required tool is missing: zip" >&2
  exit 1
fi

if [[ -z "${output_dir}" ]]; then
  output_dir="${repository_root}/artifacts/packages"
fi

schema_root="${repository_root}/schemas"
schema_manifest="${schema_root}/v1/schema-manifest.json"
if [[ ! -f "${schema_manifest}" ]]; then
  echo "Schema manifest does not exist: ${schema_manifest}" >&2
  exit 1
fi

reject_unsafe_schema_tree_entries "${schema_root}" "source tree"
assert_json_manifest_package_version "${schema_manifest}" "${package_version}" "Schema manifest"

mkdir -p "${output_dir}"
output_dir="$(cd "${output_dir}" && pwd)"
archive_path="${output_dir}/MackySoft.Ucli.Schemas.${package_version}.zip"
file_list="$(mktemp)"
trap 'rm -f "${file_list}"' EXIT

while IFS= read -r schema_file; do
  printf '%s\n' "${schema_file#"${repository_root}/"}"
done < <(find "${schema_root}" -type f | sort) > "${file_list}"

rm -f "${archive_path}"
(
  cd "${repository_root}"
  zip -q -X "${archive_path}" -@ < "${file_list}"
)

echo "Packed schema artifacts: ${archive_path}"
