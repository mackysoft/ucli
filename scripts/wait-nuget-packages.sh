#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/wait-nuget-packages.sh --version <version> --package-id <id> [--package-id <id>...] [--attempts <count>] [--sleep-seconds <seconds>]

Waits until the expected NuGet packages are available from the flat container feed.
EOF
}

package_version=""
package_ids=()
attempt_count=30
sleep_seconds=10

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
    --attempts)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      attempt_count="$2"
      shift 2
      ;;
    --sleep-seconds)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      sleep_seconds="$2"
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

if [[ ! "${attempt_count}" =~ ^[1-9][0-9]*$ || ! "${sleep_seconds}" =~ ^[0-9]+$ ]]; then
  echo "Attempts must be positive and sleep seconds must be non-negative." >&2
  exit 2
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
  curl --fail --silent --show-error --head "$1" >/dev/null
}

missing_package_ids=("${package_ids[@]}")
for ((attempt = 1; attempt <= attempt_count; attempt++)); do
  next_missing_package_ids=()
  for package_id in "${missing_package_ids[@]}"; do
    url="$(package_url "${package_id}" "${package_version}")"
    if package_exists "${url}"; then
      echo "NuGet package is available: ${package_id} ${package_version}"
    else
      next_missing_package_ids+=("${package_id}")
    fi
  done

  if [[ "${#next_missing_package_ids[@]}" -eq 0 ]]; then
    exit 0
  fi

  missing_package_ids=("${next_missing_package_ids[@]}")
  if [[ "${attempt}" -lt "${attempt_count}" ]]; then
    echo "Waiting for NuGet packages to become available on nuget.org (${attempt}/${attempt_count}): ${missing_package_ids[*]}"
    sleep "${sleep_seconds}"
  fi
done

echo "NuGet packages did not become available for ${package_version}:" >&2
printf '%s\n' "${missing_package_ids[@]}" >&2
exit 1
