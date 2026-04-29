#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/prepare-version-sync-branch.sh --default-branch <branch> --sync-branch <branch>

Checks out a version sync branch from the latest origin default branch.
EOF
}

default_branch=""
sync_branch=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --default-branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      default_branch="$2"
      shift 2
      ;;
    --sync-branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      sync_branch="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ -z "${default_branch}" || -z "${sync_branch}" ]]; then
  usage
  exit 2
fi

git fetch origin "${default_branch}"
git checkout -B "${sync_branch}" "origin/${default_branch}"
