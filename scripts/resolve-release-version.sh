#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/resolve-release-version.sh --tag-prefix <prefix> [--dispatch-version <version>] [--event-name <event>] [--ref-name <ref>]

Resolves a SemVer package version and release tag name for package publish workflows.
EOF
}

tag_prefix=""
dispatch_version="${DISPATCH_VERSION:-}"
event_name="${GITHUB_EVENT_NAME:-}"
ref_name="${GITHUB_REF_NAME:-}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag-prefix)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      tag_prefix="$2"
      shift 2
      ;;
    --dispatch-version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      dispatch_version="$2"
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

if [[ -z "${tag_prefix}" || -z "${event_name}" ]]; then
  usage
  exit 2
fi

if [[ ! "${tag_prefix}" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "Tag prefix must not contain path separators or shell pattern characters. Actual: ${tag_prefix}" >&2
  exit 2
fi

if [[ "${event_name}" == "workflow_dispatch" ]]; then
  package_version="${dispatch_version}"
else
  expected_prefix="${tag_prefix}/"
  if [[ -z "${ref_name}" || "${ref_name}" != "${expected_prefix}"* ]]; then
    echo "Release ref must start with ${expected_prefix}. Actual: ${ref_name}" >&2
    exit 1
  fi

  package_version="${ref_name#${expected_prefix}}"
fi

if [[ ! "${package_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Package version must use <major>.<minor>.<patch> format. Actual: ${package_version}" >&2
  exit 1
fi

tag_name="${tag_prefix}/${package_version}"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "package_version=${package_version}"
    echo "tag_name=${tag_name}"
  } >> "${GITHUB_OUTPUT}"
else
  echo "package_version=${package_version}"
  echo "tag_name=${tag_name}"
fi
