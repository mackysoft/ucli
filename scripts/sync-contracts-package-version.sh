#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/package-version-sync-common.sh"

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-contracts-package-version.sh --version <version>

Updates repository files that track the MackySoft.Ucli.Contracts package version.
EOF
}

package_version="$(parse_package_version_arg usage "$@")"
csproj_path="src/Ucli.Contracts/Ucli.Contracts.csproj"
unity_packages_config_path="src/Ucli.Unity/Assets/packages.config"
unity_package_nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"

update_xml_element_value "${csproj_path}" "Version" "${package_version}" "contracts csproj version"
update_xml_attribute_value "${unity_packages_config_path}" '<package id="MackySoft.Ucli.Contracts" version="' "${package_version}" "Unity contracts package version"
update_xml_attribute_value "${unity_package_nuspec_path}" '<dependency id="MackySoft.Ucli.Contracts" version="' "${package_version}" "Unity package contracts dependency version"
