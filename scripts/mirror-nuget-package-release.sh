#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/mirror-nuget-package-release.sh --repository <owner/repo> --tag-name <tag> --package-glob <glob> --title <title> --notes <notes>

Creates or updates the GitHub Release for a package tag and uploads matched nupkg artifacts.
EOF
}

repository=""
tag_name=""
package_glob=""
release_title=""
release_notes=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repository)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      repository="$2"
      shift 2
      ;;
    --tag-name)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      tag_name="$2"
      shift 2
      ;;
    --package-glob)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_glob="$2"
      shift 2
      ;;
    --title)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      release_title="$2"
      shift 2
      ;;
    --notes)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      release_notes="$2"
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

if [[ -z "${repository}" || -z "${tag_name}" || -z "${package_glob}" || -z "${release_title}" || -z "${release_notes}" ]]; then
  usage
  exit 2
fi

package_paths=()
while IFS= read -r package_path; do
  package_paths+=("${package_path}")
done < <(compgen -G "${package_glob}" | sort)
if [[ ${#package_paths[@]} -eq 0 ]]; then
  echo "No NuGet package artifacts matched: ${package_glob}" >&2
  exit 1
fi

if gh release view "${tag_name}" --repo "${repository}" >/dev/null 2>&1; then
  gh release upload "${tag_name}" "${package_paths[@]}" --repo "${repository}" --clobber
else
  gh release create "${tag_name}" "${package_paths[@]}" \
    --repo "${repository}" \
    --title "${release_title}" \
    --notes "${release_notes}"
fi
