#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/pack-unity-plugin.sh [--repo-root <path>] [--version <version>] [--output <dir>]

Creates MackySoft.Ucli.Unity.<version>.nupkg for NuGetForUnity.
EOF
}

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

nuspec_path="${repository_root}/src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"
if [[ ! -f "${nuspec_path}" ]]; then
  echo "Unity package nuspec does not exist: ${nuspec_path}" >&2
  exit 1
fi

if [[ -z "${package_version}" ]]; then
  package_version="$(
    sed -nE 's#.*<version>([^<]+)</version>.*#\1#p' "${nuspec_path}" | head -n 1
  )"
fi

if [[ -z "${package_version}" ]]; then
  echo "Failed to resolve MackySoft.Ucli.Unity package version from ${nuspec_path}." >&2
  exit 1
fi

if [[ -z "${output_dir}" ]]; then
  output_dir="${repository_root}/artifacts/packages"
fi

mkdir -p "${output_dir}"
echo "Packing MackySoft.Ucli.Unity ${package_version}"
nuget pack "${nuspec_path}" \
  -Version "${package_version}" \
  -OutputDirectory "${output_dir}" \
  -NoDefaultExcludes \
  -NonInteractive
