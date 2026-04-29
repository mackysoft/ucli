#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-unity-package-version.sh --version <version>

Updates the MackySoft.Ucli.Unity package version tracked in the repository.
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

nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"
current_version="$(sed -nE 's#.*<version>([^<]+)</version>.*#\1#p' "${nuspec_path}" | head -n 1)"
if [[ -z "${current_version}" ]]; then
  echo "Failed to resolve Unity package version from ${nuspec_path}." >&2
  exit 1
fi

if [[ "${current_version}" == "${package_version}" ]]; then
  exit 0
fi

PACKAGE_VERSION="${package_version}" perl -0pi -e '
  my $version = $ENV{"PACKAGE_VERSION"};
  s{<version>[^<]+</version>}{<version>$version</version>};
' "${nuspec_path}"
