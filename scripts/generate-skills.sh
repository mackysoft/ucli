#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/generate-skills.sh [--output <skills-dir>]" >&2
}

output_root="$DOTNET_REPO_ROOT/skills"

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

case "$output_root" in
  /*)
    ;;
  *)
    output_root="$DOTNET_REPO_ROOT/$output_root"
    ;;
esac

definitions_root="$DOTNET_REPO_ROOT/src/Ucli.Skills/SkillDefinitions"
generator_project="$DOTNET_REPO_ROOT/tools/Ucli.SkillGenerator/Ucli.SkillGenerator.csproj"
runtime_generator_project="$generator_project"
runtime_definitions_root="$definitions_root"
runtime_output_root="$output_root"

if command -v cygpath >/dev/null 2>&1; then
  runtime_generator_project="$(cygpath -w "$generator_project")"
  runtime_definitions_root="$(cygpath -w "$definitions_root")"
  runtime_output_root="$(cygpath -w "$output_root")"
fi

dotnet run \
  --project "$runtime_generator_project" \
  --configuration Release \
  -- \
  --definitions-root "$runtime_definitions_root" \
  --output \
  "$runtime_output_root"
