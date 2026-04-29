#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/create-version-sync-pr.sh --package <cli|contracts|unity> --version <version> --release-tag <tag> --repository <owner/repo> --default-branch <branch> [--verify-workflow <workflow>]

Creates or updates the post-publish version sync pull request and dispatches verification.
EOF
}

package_name=""
package_version=""
release_tag=""
repository=""
default_branch=""
verify_workflow="verify.yaml"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_name="$2"
      shift 2
      ;;
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_version="$2"
      shift 2
      ;;
    --release-tag)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      release_tag="$2"
      shift 2
      ;;
    --repository)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      repository="$2"
      shift 2
      ;;
    --default-branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      default_branch="$2"
      shift 2
      ;;
    --verify-workflow)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      verify_workflow="$2"
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

if [[ -z "${package_name}" || -z "${package_version}" || -z "${release_tag}" || -z "${repository}" || -z "${default_branch}" ]]; then
  usage
  exit 2
fi

if [[ ! "${package_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Package version must use <major>.<minor>.<patch> format. Actual: ${package_version}" >&2
  exit 1
fi

changed_paths=()
sync_branch=""
commit_message=""
pr_title=""
pr_body=""

replace_first_xml_element() {
  local path="$1"
  local element_name="$2"

  PACKAGE_VERSION="${package_version}" ELEMENT_NAME="${element_name}" perl -0pi -e '
    my $element = $ENV{"ELEMENT_NAME"};
    my $version = $ENV{"PACKAGE_VERSION"};
    s{<\Q$element\E>[^<]+</\Q$element\E>}{<$element>$version</$element>};
  ' "${path}"
}

replace_xml_attribute_version() {
  local path="$1"
  local id="$2"

  PACKAGE_VERSION="${package_version}" PACKAGE_ID="${id}" perl -0pi -e '
    my $id = quotemeta($ENV{"PACKAGE_ID"});
    my $version = $ENV{"PACKAGE_VERSION"};
    s{(<(?:package|dependency) id="$id" version=")[^"]+(")}{$1$version$2};
  ' "${path}"
}

require_value() {
  local value="$1"
  local message="$2"

  if [[ -z "${value}" ]]; then
    echo "${message}" >&2
    exit 1
  fi
}

sync_cli_version() {
  local csproj_path="src/Ucli/Ucli.csproj"
  local current_version
  current_version="$(sed -nE 's#.*<Version>([^<]+)</Version>.*#\1#p' "${csproj_path}" | head -n 1)"
  require_value "${current_version}" "Failed to resolve CLI csproj version from ${csproj_path}."

  if [[ "${current_version}" != "${package_version}" ]]; then
    replace_first_xml_element "${csproj_path}" "Version"
  fi

  changed_paths=("${csproj_path}")
  sync_branch="chore/cli-sync-${package_version}"
  commit_message="chore(cli): sync package version to ${package_version}"
  pr_title="chore(cli): sync package version to ${package_version}"
  pr_body="Sync \`MackySoft.Ucli\` package version to \`${package_version}\` after \`${release_tag}\` publish. The \`cli-package-publish\` workflow packed, smoke-tested, published, and released the package before creating this PR, then dispatched \`${verify_workflow}\` for \`${sync_branch}\`."
}

sync_contracts_version() {
  local csproj_path="src/Ucli.Contracts/Ucli.Contracts.csproj"
  local unity_packages_config_path="src/Ucli.Unity/Assets/packages.config"
  local unity_package_nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"
  local csproj_current_version
  local unity_current_version
  local unity_package_dependency_version

  csproj_current_version="$(sed -nE 's#.*<Version>([^<]+)</Version>.*#\1#p' "${csproj_path}" | head -n 1)"
  require_value "${csproj_current_version}" "Failed to resolve contracts csproj version from ${csproj_path}."

  unity_current_version="$(sed -nE 's#.*<package id="MackySoft.Ucli.Contracts" version="([^"]+)".*#\1#p' "${unity_packages_config_path}" | head -n 1)"
  require_value "${unity_current_version}" "Failed to resolve Unity contracts package version from ${unity_packages_config_path}."

  unity_package_dependency_version="$(sed -nE 's#.*<dependency id="MackySoft.Ucli.Contracts" version="([^"]+)".*#\1#p' "${unity_package_nuspec_path}" | head -n 1)"
  require_value "${unity_package_dependency_version}" "Failed to resolve Unity package contracts dependency version from ${unity_package_nuspec_path}."

  if [[ "${csproj_current_version}" != "${package_version}" ]]; then
    replace_first_xml_element "${csproj_path}" "Version"
  fi

  if [[ "${unity_current_version}" != "${package_version}" ]]; then
    replace_xml_attribute_version "${unity_packages_config_path}" "MackySoft.Ucli.Contracts"
  fi

  if [[ "${unity_package_dependency_version}" != "${package_version}" ]]; then
    replace_xml_attribute_version "${unity_package_nuspec_path}" "MackySoft.Ucli.Contracts"
  fi

  changed_paths=("${csproj_path}" "${unity_packages_config_path}" "${unity_package_nuspec_path}")
  sync_branch="chore/contracts-sync-${package_version}"
  commit_message="chore(contracts): sync package version to ${package_version}"
  pr_title="chore(contracts): sync package version to ${package_version}"
  pr_body="$(cat <<EOF
## Summary
- Sync \`MackySoft.Ucli.Contracts\` package version to \`${package_version}\` after \`${release_tag}\` publish.
- Update \`${csproj_path}\`, \`${unity_packages_config_path}\`, and \`${unity_package_nuspec_path}\`.

## Verification
- \`contracts-package-publish\` packed and published \`${release_tag}\` before creating this PR.
- \`${verify_workflow}\` workflow was dispatched for \`${sync_branch}\`.
EOF
)"
}

sync_unity_version() {
  local nuspec_path="src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"
  local current_version
  current_version="$(sed -nE 's#.*<version>([^<]+)</version>.*#\1#p' "${nuspec_path}" | head -n 1)"
  require_value "${current_version}" "Failed to resolve Unity package version from ${nuspec_path}."

  if [[ "${current_version}" != "${package_version}" ]]; then
    replace_first_xml_element "${nuspec_path}" "version"
  fi

  changed_paths=("${nuspec_path}")
  sync_branch="chore/unity-sync-${package_version}"
  commit_message="chore(unity): sync package version to ${package_version}"
  pr_title="chore(unity): sync package version to ${package_version}"
  pr_body="Sync \`MackySoft.Ucli.Unity\` package version to \`${package_version}\` after \`${release_tag}\` publish. The \`unity-package-publish\` workflow packed, verified, and published the package before creating this PR, then dispatched \`${verify_workflow}\` for \`${sync_branch}\`."
}

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

git fetch origin "${default_branch}"

case "${package_name}" in
  cli)
    sync_branch="chore/cli-sync-${package_version}"
    ;;
  contracts)
    sync_branch="chore/contracts-sync-${package_version}"
    ;;
  unity)
    sync_branch="chore/unity-sync-${package_version}"
    ;;
  *)
    echo "Unsupported package sync target: ${package_name}" >&2
    exit 2
    ;;
esac

git checkout -B "${sync_branch}" "origin/${default_branch}"

case "${package_name}" in
  cli)
    sync_cli_version
    ;;
  contracts)
    sync_contracts_version
    ;;
  unity)
    sync_unity_version
    ;;
esac

if git diff --quiet -- "${changed_paths[@]}"; then
  echo "No repository version sync required."
  exit 0
fi

git add "${changed_paths[@]}"
git commit -m "${commit_message}"

if git ls-remote --exit-code --heads origin "${sync_branch}" >/dev/null; then
  git fetch origin "${sync_branch}:refs/remotes/origin/${sync_branch}"
  remote_sync_sha="$(git rev-parse "refs/remotes/origin/${sync_branch}")"
  git push --force-with-lease="refs/heads/${sync_branch}:${remote_sync_sha}" origin "HEAD:${sync_branch}"
else
  git push origin "HEAD:${sync_branch}"
fi

existing_pr_url="$(gh pr list \
  --repo "${repository}" \
  --state open \
  --head "${sync_branch}" \
  --json url \
  --jq '.[0].url // empty')"
if [[ -n "${existing_pr_url}" ]]; then
  echo "Version sync pull request already exists: ${existing_pr_url}"
else
  gh pr create \
    --repo "${repository}" \
    --base "${default_branch}" \
    --head "${sync_branch}" \
    --title "${pr_title}" \
    --body "${pr_body}"
fi

gh workflow run "${verify_workflow}" \
  --repo "${repository}" \
  --ref "${sync_branch}"
