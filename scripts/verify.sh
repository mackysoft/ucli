#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/verify.sh [--no-restore] [--solution <path>] [--configuration <name>] [--include-unity [test-unity-options...]] [-- dotnet-test-options...]" >&2
}

restore=true
solution_arg=""
configuration="Release"
include_unity=false
test_args=()
unity_args=()

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
    --include-unity)
      include_unity=true
      shift
      unity_args=("$@")
      break
      ;;
    --)
      shift
      test_args=("$@")
      break
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [ -z "$configuration" ]; then
  usage
  exit 2
fi

solution="$(dotnet_resolve_solution "$solution_arg")"
cd "$DOTNET_REPO_ROOT"

if [ "$restore" = true ]; then
  dotnet restore "$solution"
fi

bash scripts/code-quality.sh --no-restore --solution "$solution" verify
dotnet build "$solution" --configuration "$configuration" --no-restore
test_code_args=(
  --no-restore
  --solution "$solution"
  --configuration "$configuration"
  --no-build
)
if [ "${#test_args[@]}" -gt 0 ]; then
  test_code_args+=("${test_args[@]}")
fi
bash scripts/test-code.sh "${test_code_args[@]}"

if [ "$include_unity" = true ]; then
  test_unity_args=(--configuration "$configuration")
  if [ "${#unity_args[@]}" -gt 0 ]; then
    test_unity_args+=("${unity_args[@]}")
  fi
  bash scripts/test-unity.sh "${test_unity_args[@]}"
fi
