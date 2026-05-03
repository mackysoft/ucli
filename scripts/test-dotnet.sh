#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/test-dotnet.sh [--no-restore] [--solution <path>] [--configuration <name>] [test-target] [dotnet-test-options...]" >&2
}

restore=true
solution_arg=""
configuration="Release"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-restore)
      restore=false
      shift
      ;;
    --solution)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      solution_arg="$2"
      shift 2
      ;;
    --solution=*)
      solution_arg="${1#--solution=}"
      shift
      ;;
    --configuration)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      configuration="$2"
      shift 2
      ;;
    --configuration=*)
      configuration="${1#--configuration=}"
      shift
      ;;
    *)
      break
      ;;
  esac
done

if [ -z "$configuration" ]; then
  usage
  exit 2
fi

solution="$(dotnet_resolve_solution "$solution_arg")"
cd "$DOTNET_REPO_ROOT"

target="$solution"

if [ "$#" -gt 0 ] && [[ "$1" != -* ]]; then
  target="$1"
  shift
fi

if [ "$restore" = true ]; then
  dotnet restore "$solution"
fi

dotnet test "$target" --configuration "$configuration" --no-restore --logger "console;verbosity=normal" "$@"
