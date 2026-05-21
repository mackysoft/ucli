#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/resolve-release-version.sh [--dispatch-tag <tag>] [--event-name <event>] [--ref-name <ref>]

Resolves a SemVer package version and release tag name for package publish workflows.
EOF
}

dispatch_tag="${DISPATCH_TAG:-}"
event_name="${GITHUB_EVENT_NAME:-}"
ref_name="${GITHUB_REF_NAME:-}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dispatch-tag)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      dispatch_tag="$2"
      shift 2
      ;;
    --event-name)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      event_name="$2"
      shift 2
      ;;
    --ref-name)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      ref_name="$2"
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

if [[ -z "${event_name}" ]]; then
  usage
  exit 2
fi

if [[ "${event_name}" == "workflow_dispatch" ]]; then
  release_tag="${dispatch_tag}"
else
  if [[ -z "${ref_name}" ]]; then
    echo "Release ref name is required for ${event_name}." >&2
    exit 1
  fi

  release_tag="${ref_name}"
fi

if [[ "${release_tag}" == */* ]]; then
  echo "Release tag must not contain path separators. Use <major>.<minor>.<patch> without release/ prefix. Actual: ${release_tag}" >&2
  exit 1
fi

if [[ ! "${release_tag}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Release tag must use <major>.<minor>.<patch> format without a prefix. Actual: ${release_tag}" >&2
  exit 1
fi

package_version="${release_tag}"
tag_name="${release_tag}"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "package_version=${package_version}"
    echo "tag_name=${tag_name}"
  } >> "${GITHUB_OUTPUT}"
else
  echo "package_version=${package_version}"
  echo "tag_name=${tag_name}"
fi
