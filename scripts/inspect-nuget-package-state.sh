#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/inspect-nuget-package-state.sh --version <version> --package-id <id> [--package-id <id>...]

Checks whether the expected NuGet packages already exist for one release version.
EOF
}

package_version=""
package_ids=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_version="$2"
      shift 2
      ;;
    --package-id)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_ids+=("$2")
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

if [[ -z "${package_version}" || "${#package_ids[@]}" -eq 0 ]]; then
  usage
  exit 2
fi

if [[ ! "${package_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Package version must use <major>.<minor>.<patch> format. Actual: ${package_version}" >&2
  exit 1
fi

package_url() {
  local package_id="$1"
  local version="$2"
  local lower_package_id
  local lower_version

  lower_package_id="$(printf '%s' "${package_id}" | tr '[:upper:]' '[:lower:]')"
  lower_version="$(printf '%s' "${version}" | tr '[:upper:]' '[:lower:]')"
  printf 'https://api.nuget.org/v3-flatcontainer/%s/%s/%s.%s.nupkg' \
    "${lower_package_id}" \
    "${lower_version}" \
    "${lower_package_id}" \
    "${lower_version}"
}

package_exists() {
  curl --fail --silent --head "$1" >/dev/null 2>&1
}

existing_package_ids=()
missing_package_ids=()
for package_id in "${package_ids[@]}"; do
  url="$(package_url "${package_id}" "${package_version}")"
  if package_exists "${url}"; then
    existing_package_ids+=("${package_id}")
  else
    missing_package_ids+=("${package_id}")
  fi
done

all_packages_exist=false
publish_required=true
if [[ "${#existing_package_ids[@]}" -eq "${#package_ids[@]}" ]]; then
  all_packages_exist=true
  publish_required=false
elif [[ "${#existing_package_ids[@]}" -ne 0 ]]; then
  echo "NuGet release state is inconsistent for ${package_version}." >&2
  echo "Existing packages:" >&2
  printf '%s\n' "${existing_package_ids[@]}" >&2
  echo "Missing packages:" >&2
  printf '%s\n' "${missing_package_ids[@]}" >&2
  exit 1
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "all_packages_exist=${all_packages_exist}"
    echo "publish_required=${publish_required}"
  } >> "${GITHUB_OUTPUT}"
else
  echo "all_packages_exist=${all_packages_exist}"
  echo "publish_required=${publish_required}"
fi

if [[ "${publish_required}" == "true" ]]; then
  echo "NuGet packages are not published for ${package_version}."
else
  echo "NuGet packages already exist for ${package_version}."
fi
