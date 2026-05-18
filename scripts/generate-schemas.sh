#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/generate-schemas.sh [--output <schemas-dir>] [--package-version <version>]" >&2
}

output_root="$DOTNET_REPO_ROOT/schemas"
package_version=""

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
    --package-version)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      package_version="$2"
      shift 2
      ;;
    --package-version=*)
      package_version="${1#--package-version=}"
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

output_root="$(dotnet_to_bash_path "$output_root")"

case "$output_root" in
  /*)
    ;;
  *)
    output_root="$DOTNET_REPO_ROOT/$output_root"
    ;;
esac

generator_project="$DOTNET_REPO_ROOT/tools/Ucli.SchemaGenerator/Ucli.SchemaGenerator.csproj"
runtime_generator_project="$generator_project"
runtime_repository_root="$DOTNET_REPO_ROOT"
runtime_output_root="$output_root"

if command -v cygpath >/dev/null 2>&1; then
  runtime_generator_project="$(cygpath -w "$generator_project")"
  runtime_repository_root="$(cygpath -w "$DOTNET_REPO_ROOT")"
  runtime_output_root="$(cygpath -w "$output_root")"
fi

generator_args=(
  --project "$runtime_generator_project"
  --configuration Release
  --
  --repository-root "$runtime_repository_root"
  --output "$runtime_output_root"
)

if [ -n "$package_version" ]; then
  generator_args+=(--package-version "$package_version")
fi

dotnet run "${generator_args[@]}"
