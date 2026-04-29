#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/package-version-sync-common.sh"

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-unity-package-version.sh --version <version>

Updates the MackySoft.Ucli.Unity package version tracked in the repository.
EOF
}

package_version="$(parse_package_version_arg usage "$@")"
nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"

update_xml_element_value "${nuspec_path}" "version" "${package_version}" "Unity package version"
