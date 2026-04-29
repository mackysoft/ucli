#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/sync-cli-package-version.sh --version <version>

Updates the CLI package version tracked in the repository.
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

csproj_path="src/Ucli/Ucli.csproj"
current_version="$(sed -nE 's#.*<Version>([^<]+)</Version>.*#\1#p' "${csproj_path}" | head -n 1)"
if [[ -z "${current_version}" ]]; then
  echo "Failed to resolve CLI csproj version from ${csproj_path}." >&2
  exit 1
fi

if [[ "${current_version}" == "${package_version}" ]]; then
  exit 0
fi

PACKAGE_VERSION="${package_version}" perl -0pi -e '
  my $version = $ENV{"PACKAGE_VERSION"};
  s{<Version>[^<]+</Version>}{<Version>$version</Version>};
' "${csproj_path}"
