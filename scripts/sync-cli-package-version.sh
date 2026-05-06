#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/package-version-sync-common.sh"

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-cli-package-version.sh --version <version>

Updates the CLI package version tracked in the repository.
EOF
}

package_version="$(parse_package_version_arg usage "$@")"
props_path="Directory.Build.props"

update_xml_element_value "${props_path}" "Version" "${package_version}" "central package version"
