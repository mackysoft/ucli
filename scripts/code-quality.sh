#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/code-quality.sh [--no-restore] [--solution <path>] <format|verify>" >&2
}

if [ "$#" -lt 1 ]; then
  usage
  exit 2
fi

restore=true
mode=""
solution_arg=""

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
    format|verify)
      mode="$1"
      shift
      break
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [ -z "$mode" ]; then
  usage
  exit 2
fi

solution="$(dotnet_resolve_solution "$solution_arg")"
cd "$DOTNET_REPO_ROOT"

diagnostics=(
  IDE0011
  IDE0036
  IDE0048
  IDE0049
  IDE0062
  IDE1006
)

run_restore() {
  if [ "$restore" = true ]; then
    dotnet restore "$solution"
  fi
}

run_format() {
  dotnet format whitespace "$solution" --verbosity minimal --no-restore
  dotnet format style "$solution" --diagnostics "${diagnostics[@]}" --verbosity minimal --no-restore
  dotnet format whitespace "$solution" --verbosity minimal --no-restore
}

run_format_verify() {
  dotnet format whitespace "$solution" --verify-no-changes --verbosity minimal --no-restore
  dotnet format style "$solution" --diagnostics "${diagnostics[@]}" --verify-no-changes --verbosity minimal --no-restore
}

run_restore

case "$mode" in
  format)
    if [ "$#" -ne 0 ]; then
      usage
      exit 2
    fi
    run_format
    ;;
  verify)
    if [ "$#" -ne 0 ]; then
      usage
      exit 2
    fi
    run_format_verify
    ;;
  *)
    usage
    exit 2
    ;;
esac
