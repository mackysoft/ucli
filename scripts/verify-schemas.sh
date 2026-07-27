#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

temp_root="$(dotnet_to_bash_path "${RUNNER_TEMP:-${TMPDIR:-/tmp}}")"
mkdir -p "$temp_root"
work_dir="$(mktemp -d "${temp_root%/}/ucli-schemas-verify.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

reject_non_regular_entries() {
  local root="$1"
  local label="$2"
  local entry

  while IFS= read -r entry; do
    echo "Generated schemas ${label} contains unsupported non-regular path: ${entry}" >&2
    return 1
  done < <(find "$root" ! -type f ! -type d -print | sort)
}

expected_root="$work_dir/schemas-first"
repeated_root="$work_dir/schemas-second"
bash "$script_dir/generate-schemas.sh" --output "$expected_root" >/dev/null
bash "$script_dir/generate-schemas.sh" --output "$repeated_root" >/dev/null

reject_non_regular_entries "$DOTNET_REPO_ROOT/schemas" "source tree"
reject_non_regular_entries "$expected_root" "expected tree"
reject_non_regular_entries "$repeated_root" "repeated tree"

if ! diff -ruN "$expected_root" "$repeated_root"; then
  echo "Repeated schema generation produced different artifacts." >&2
  exit 1
fi

if ! diff -ruN "$DOTNET_REPO_ROOT/schemas" "$expected_root"; then
  echo "Generated schema drift detected. Run: bash scripts/generate-schemas.sh" >&2
  exit 1
fi

echo "Generated schemas are up to date."
