#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/code-quality.sh [--no-restore] [--solution <path>] <format|verify> [--include <path>...]" >&2
}

if [ "$#" -lt 1 ]; then
  usage
  exit 2
fi

restore=true
mode=""
solution_arg=""
include_paths=()

append_include_path() {
  local include_path="$1"

  if [ -z "$include_path" ]; then
    usage
    exit 2
  fi

  include_paths+=("$include_path")
}

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

while [ "$#" -gt 0 ]; do
  case "$1" in
    --include)
      shift
      if [ "$#" -lt 1 ]; then
        usage
        exit 2
      fi

      while [ "$#" -gt 0 ]; do
        case "$1" in
          --include|--include=*)
            break
            ;;
          --*)
            usage
            exit 2
            ;;
          *)
            append_include_path "$1"
            shift
            ;;
        esac
      done
      ;;
    --include=*)
      append_include_path "${1#--include=}"
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

solution="$(dotnet_resolve_solution "$solution_arg")"
cd "$DOTNET_REPO_ROOT"

diagnostics=(
  IDE0005
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

run_dotnet_format() {
  local command="$1"
  shift

  if [ "${#include_paths[@]}" -gt 0 ]; then
    dotnet format "$command" "$solution" --include "${include_paths[@]}" "$@"
  else
    dotnet format "$command" "$solution" "$@"
  fi
}

run_format() {
  run_dotnet_format style --diagnostics "${diagnostics[@]}" --verbosity minimal --no-restore
  run_dotnet_format whitespace --verbosity minimal --no-restore
}

run_format_verify() {
  run_dotnet_format whitespace --verify-no-changes --verbosity minimal --no-restore
  run_dotnet_format style --diagnostics "${diagnostics[@]}" --verify-no-changes --verbosity minimal --no-restore
}

run_restore

case "$mode" in
  format)
    run_format
    ;;
  verify)
    run_format_verify
    ;;
  *)
    usage
    exit 2
    ;;
esac
