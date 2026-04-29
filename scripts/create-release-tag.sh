#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/create-release-tag.sh --tag-name <tag> --release-sha <sha> [--remote <remote>]

Creates and pushes one lightweight release tag for workflow_dispatch package publishes.
EOF
}

tag_name=""
release_sha=""
remote_name="origin"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag-name)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      tag_name="$2"
      shift 2
      ;;
    --release-sha)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      release_sha="$2"
      shift 2
      ;;
    --remote)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      remote_name="$2"
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

if [[ -z "${tag_name}" || -z "${release_sha}" || -z "${remote_name}" ]]; then
  usage
  exit 2
fi

if git ls-remote --exit-code --tags "${remote_name}" "refs/tags/${tag_name}" >/dev/null; then
  echo "Tag ${tag_name} already exists." >&2
  exit 1
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

git tag "${tag_name}" "${release_sha}"
git push "${remote_name}" "refs/tags/${tag_name}"
