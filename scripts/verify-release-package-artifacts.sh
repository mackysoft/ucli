#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <package-dir> <expected-version>" >&2
  exit 2
fi

package_dir="$1"
expected_version="$2"

if [[ ! -d "${package_dir}" ]]; then
  echo "Release package directory does not exist: ${package_dir}" >&2
  exit 1
fi

package_dir="$(cd "${package_dir}" && pwd)"
expected_artifacts=(
  "MackySoft.Ucli.${expected_version}.nupkg"
  "MackySoft.Ucli.Contracts.${expected_version}.nupkg"
  "MackySoft.Ucli.Infrastructure.${expected_version}.nupkg"
  "MackySoft.Ucli.Unity.${expected_version}.nupkg"
  "MackySoft.Ucli.Schemas.${expected_version}.zip"
)

for artifact_name in "${expected_artifacts[@]}"; do
  artifact_path="${package_dir}/${artifact_name}"
  if [[ ! -f "${artifact_path}" ]]; then
    echo "Release artifact was not created: ${artifact_path}" >&2
    exit 1
  fi
done

actual_artifacts=()
while IFS= read -r artifact_path; do
  actual_artifacts+=("$(basename "${artifact_path}")")
done < <(find "${package_dir}" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.zip' \) | sort)

expected_sorted="$(printf '%s\n' "${expected_artifacts[@]}" | sort)"
actual_sorted="$(printf '%s\n' "${actual_artifacts[@]}" | sort)"
if [[ "${actual_sorted}" != "${expected_sorted}" ]]; then
  echo "Release artifact set does not match the expected package list." >&2
  echo "Expected:" >&2
  printf '%s\n' "${expected_sorted}" >&2
  echo "Actual:" >&2
  printf '%s\n' "${actual_sorted}" >&2
  exit 1
fi

echo "Release artifact set verified: ${package_dir}"
