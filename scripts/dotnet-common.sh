#!/usr/bin/env bash

DOTNET_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet_to_native_path() {
  local path="$1"

  if command -v cygpath >/dev/null 2>&1; then
    cygpath -m "$path"
    return
  fi

  printf '%s\n' "$path"
}

DOTNET_LOCAL_PACKAGE_SOURCE="$(
  dotnet_to_native_path "${DOTNET_REPO_ROOT}/src/Ucli.Unity/Packages/nuget-local-source"
)"
DOTNET_REPOSITORY_NUGET_PACKAGES="$(
  dotnet_to_native_path "${DOTNET_REPO_ROOT}/src/Ucli.Unity/.nuget-packages"
)"

export NUGET_PACKAGES="${DOTNET_REPOSITORY_NUGET_PACKAGES}"

dotnet_restore_with_local_packages() {
  local additional_sources="${RestoreAdditionalProjectSources:-}"

  if [ -d "${DOTNET_REPO_ROOT}/src/Ucli.Unity/Packages/nuget-local-source" ]; then
    if [ -n "${additional_sources}" ]; then
      additional_sources="${additional_sources};${DOTNET_LOCAL_PACKAGE_SOURCE}"
    else
      additional_sources="${DOTNET_LOCAL_PACKAGE_SOURCE}"
    fi
  fi

  if [ -n "${additional_sources}" ]; then
    dotnet restore "$@" "-p:RestoreAdditionalProjectSources=${additional_sources}"
  else
    dotnet restore "$@"
  fi
}

dotnet_to_bash_path() {
  local path="$1"

  if command -v cygpath >/dev/null 2>&1; then
    case "$path" in
      [A-Za-z]:*)
        cygpath -u "$path"
        return
        ;;
    esac
  fi

  printf '%s\n' "$path"
}

dotnet_resolve_solution() {
  local requested="${1:-}"
  local candidate
  local count=0
  local resolved=""

  if [ -z "$requested" ] && [ -n "${DOTNET_SOLUTION:-}" ]; then
    requested="$DOTNET_SOLUTION"
  fi

  if [ -n "$requested" ]; then
    case "$requested" in
      /*)
        resolved="$requested"
        ;;
      *)
        resolved="$DOTNET_REPO_ROOT/$requested"
        ;;
    esac

    if [ ! -f "$resolved" ]; then
      echo "solution not found: $requested" >&2
      return 2
    fi

    printf '%s\n' "$resolved"
    return
  fi

  while IFS= read -r candidate; do
    resolved="$candidate"
    count=$((count + 1))
  done < <(find "$DOTNET_REPO_ROOT" -maxdepth 1 -type f \( -name '*.slnx' -o -name '*.sln' \) | sort)

  case "$count" in
    0)
      echo "solution not found. pass --solution <path>." >&2
      return 2
      ;;
    1)
      printf '%s\n' "$resolved"
      ;;
    *)
      echo "multiple solutions found. pass --solution <path>." >&2
      return 2
      ;;
  esac
}
