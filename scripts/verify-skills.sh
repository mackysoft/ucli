#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

temp_root="$(dotnet_to_bash_path "${RUNNER_TEMP:-${TMPDIR:-/tmp}}")"
mkdir -p "$temp_root"
work_dir="$(mktemp -d "${temp_root%/}/ucli-skills-verify.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

expected_root="$work_dir/skills"
bash "$script_dir/generate-skills.sh" --output "$expected_root" >/dev/null

if ! diff -ruN "$DOTNET_REPO_ROOT/skills" "$expected_root"; then
  echo "Generated skills drift detected. Run: bash scripts/generate-skills.sh" >&2
  exit 1
fi

echo "Generated skills are up to date."
