#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

temp_root="$DOTNET_REPO_ROOT/obj"
mkdir -p "$temp_root"
work_dir="$(mktemp -d "${temp_root%/}/ucli-skills-verify.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

reject_non_regular_entries() {
  local root="$1"
  local label="$2"
  local entry

  while IFS= read -r entry; do
    echo "Generated skills ${label} contains unsupported non-regular path: ${entry}" >&2
    return 1
  done < <(find "$root" ! -type f ! -type d -print | sort)
}

expected_root="$work_dir/generated"
bash "$script_dir/generate-skills.sh" --output "$expected_root" >/dev/null

reject_non_regular_entries "$DOTNET_REPO_ROOT/skills/generated" "source tree"
reject_non_regular_entries "$expected_root" "expected tree"

if ! diff -ruN "$DOTNET_REPO_ROOT/skills/generated" "$expected_root"; then
  echo "Generated skills drift detected. Run: bash scripts/generate-skills.sh" >&2
  exit 1
fi

echo "Generated skills are up to date."
