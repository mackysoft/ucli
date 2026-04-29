#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-contracts-package-version.sh --version <version>

Updates repository files that track the MackySoft.Ucli.Contracts package version.
EOF
}

package_version=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_version="$2"
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

if [[ ! "${package_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Package version must use <major>.<minor>.<patch> format. Actual: ${package_version}" >&2
  exit 1
fi

csproj_path="src/Ucli.Contracts/Ucli.Contracts.csproj"
unity_packages_config_path="src/Ucli.Unity/Assets/packages.config"
unity_package_nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"

csproj_current_version="$(sed -nE 's#.*<Version>([^<]+)</Version>.*#\1#p' "${csproj_path}" | head -n 1)"
if [[ -z "${csproj_current_version}" ]]; then
  echo "Failed to resolve contracts csproj version from ${csproj_path}." >&2
  exit 1
fi

unity_current_version="$(sed -nE 's#.*<package id="MackySoft.Ucli.Contracts" version="([^"]+)".*#\1#p' "${unity_packages_config_path}" | head -n 1)"
if [[ -z "${unity_current_version}" ]]; then
  echo "Failed to resolve Unity contracts package version from ${unity_packages_config_path}." >&2
  exit 1
fi

unity_package_dependency_version="$(sed -nE 's#.*<dependency id="MackySoft.Ucli.Contracts" version="([^"]+)".*#\1#p' "${unity_package_nuspec_path}" | head -n 1)"
if [[ -z "${unity_package_dependency_version}" ]]; then
  echo "Failed to resolve Unity package contracts dependency version from ${unity_package_nuspec_path}." >&2
  exit 1
fi

PACKAGE_VERSION="${package_version}" perl -0pi -e '
  my $version = $ENV{"PACKAGE_VERSION"};
  s{<Version>[^<]+</Version>}{<Version>$version</Version>};
' "${csproj_path}"

PACKAGE_VERSION="${package_version}" perl -0pi -e '
  my $version = $ENV{"PACKAGE_VERSION"};
  s{(<package id="MackySoft.Ucli.Contracts" version=")[^"]+(")}{$1$version$2};
' "${unity_packages_config_path}"

PACKAGE_VERSION="${package_version}" perl -0pi -e '
  my $version = $ENV{"PACKAGE_VERSION"};
  s{(<dependency id="MackySoft.Ucli.Contracts" version=")[^"]+(")}{$1$version$2};
' "${unity_package_nuspec_path}"
