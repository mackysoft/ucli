#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/generate-skills.sh [--output <generated-skills-dir>]" >&2
}

output_root="$DOTNET_REPO_ROOT/skills/generated"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --output)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      output_root="$2"
      shift 2
      ;;
    --output=*)
      output_root="${1#--output=}"
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

repo_root="$(cd "$DOTNET_REPO_ROOT" && pwd -P)"

resolve_output_root() {
  local candidate="$1"
  local parent
  local resolved_parent
  local name
  local resolved

  candidate="$(dotnet_to_bash_path "$candidate")"
  if [ -z "$candidate" ]; then
    echo "output path must not be empty." >&2
    exit 2
  fi

  if [ -n "${candidate%/}" ]; then
    candidate="${candidate%/}"
  fi

  case "$candidate" in
    /*)
      ;;
    *)
      candidate="$repo_root/$candidate"
      ;;
  esac

  parent="$(dirname "$candidate")"
  name="$(basename "$candidate")"
  if [ ! -d "$parent" ]; then
    echo "output parent directory must already exist: $parent" >&2
    exit 2
  fi

  resolved_parent="$(cd "$parent" && pwd -P)"
  resolved="$resolved_parent/$name"
  if [ "$resolved" = "$repo_root" ]; then
    echo "output path must not be the repository root." >&2
    exit 2
  fi

  case "$resolved/" in
    "$repo_root"/*)
      ;;
    *)
      echo "output path must stay under the repository root: $resolved" >&2
      exit 2
      ;;
  esac

  if [ -L "$resolved" ]; then
    echo "output path must not be a symbolic link: $resolved" >&2
    exit 2
  fi

  printf '%s\n' "$resolved"
}

output_root="$(resolve_output_root "$output_root")"

definitions_root="$repo_root/skills/definitions"
runtime_definitions_root="$definitions_root"
runtime_output_root="$output_root"

if command -v cygpath >/dev/null 2>&1; then
  runtime_definitions_root="$(cygpath -w "$definitions_root")"
  runtime_output_root="$(cygpath -w "$output_root")"
fi

cd "$repo_root"
dotnet tool restore >/dev/null
dotnet tool run agent-skills -- \
  build \
  --definitionsRoot "$runtime_definitions_root" \
  --generatedRoot \
  "$runtime_output_root"
