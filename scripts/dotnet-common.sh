#!/usr/bin/env bash

DOTNET_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

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
