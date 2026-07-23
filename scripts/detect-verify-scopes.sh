#!/usr/bin/env bash
set -euo pipefail

dotnet_matrix_pr='{"include":[{"runs_on":"ubuntu-latest","os_name":"linux"},{"runs_on":"windows-latest","os_name":"windows"},{"runs_on":"macos-latest","os_name":"macos"}]}'
dotnet_matrix_push='{"include":[{"runs_on":"ubuntu-latest","os_name":"linux"}]}'
unity_matrix_pr='{"include":[{"runs_on":"ubuntu-22.04","os_name":"linux","unity_version":"2023.2.22f1","unity_version_with_revision":"2023.2.22f1 (6b19bf4f8115)","cache_installation":true},{"runs_on":"windows-latest","os_name":"windows","unity_version":"2023.2.22f1","unity_version_with_revision":"2023.2.22f1 (6b19bf4f8115)","cache_installation":false},{"runs_on":"windows-latest","os_name":"windows-6000-3","unity_version":"6000.3.11f1","unity_version_with_revision":"6000.3.11f1 (3000ef702840)","cache_installation":false},{"runs_on":"macos-latest","os_name":"macos","unity_version":"2023.2.22f1","unity_version_with_revision":"2023.2.22f1 (6b19bf4f8115)","cache_installation":true},{"runs_on":"ubuntu-22.04","os_name":"linux-6000","unity_version":"6000.0.77f1","unity_version_with_revision":"6000.0.77f1 (88f89d0d8b31)","cache_installation":true}]}'
unity_matrix_push='{"include":[{"runs_on":"ubuntu-22.04","os_name":"linux","unity_version":"2023.2.22f1","unity_version_with_revision":"2023.2.22f1 (6b19bf4f8115)","cache_installation":true},{"runs_on":"ubuntu-22.04","os_name":"linux-6000","unity_version":"6000.0.77f1","unity_version_with_revision":"6000.0.77f1 (88f89d0d8b31)","cache_installation":true}]}'

dotnet_matrix_json="${dotnet_matrix_pr}"
unity_matrix_json="${unity_matrix_pr}"
needs_dotnet=false
needs_unity=false
needs_shared_pack=false
needs_cli_pack=false
needs_unity_pack=false
needs_release_pack=false

event_name="${EVENT_NAME:-}"
current_sha="${CURRENT_SHA:-}"
output_file="${GITHUB_OUTPUT:-}"

emit_output() {
  local name="$1"
  local value="$2"

  if [[ -n "${output_file}" ]]; then
    echo "${name}=${value}" >> "${output_file}"
  else
    echo "${name}=${value}"
  fi
}

emit_outputs() {
  emit_output needs_dotnet "${needs_dotnet}"
  emit_output needs_unity "${needs_unity}"
  emit_output needs_shared_pack "${needs_shared_pack}"
  emit_output needs_cli_pack "${needs_cli_pack}"
  emit_output needs_unity_pack "${needs_unity_pack}"
  emit_output needs_release_pack "${needs_release_pack}"
  emit_output dotnet_matrix_json "${dotnet_matrix_json}"
  emit_output unity_matrix_json "${unity_matrix_json}"
}

# Fail closed when comparison context is unavailable.
emit_full_verification() {
  needs_dotnet=true
  needs_unity=true
  needs_shared_pack=true
  needs_cli_pack=true
  needs_unity_pack=true
  needs_release_pack=true
  dotnet_matrix_json="${dotnet_matrix_pr}"
  unity_matrix_json="${unity_matrix_pr}"
  emit_outputs
  echo "$1"
  exit 0
}

has_managed_input_extension() {
  case "$1" in
    *.cs|*.csproj|*.props|*.targets)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_dotnet_input() {
  local file="$1"

  case "${file}" in
    # Changes to this detector can alter every downstream job decision.
    .config/dotnet-tools.json|.editorconfig|.gitattributes|Directory.Build.props|Ucli.slnx|.github/workflows/agent-skills-sync.yaml|.github/workflows/verify.yaml|.github/workflows/package-publish.yaml|scripts/code-quality.sh|scripts/detect-verify-scopes.sh|scripts/dotnet-common.sh|scripts/generate-schemas.sh|scripts/test-dotnet.sh|scripts/verify-schemas.sh|scripts/verify-skills.sh|scripts/verify.sh|schemas/*|skills/*|skills/definitions/*|skills/generated/*|tools/*)
      return 0
      ;;
  esac

  if ! has_managed_input_extension "${file}"; then
    return 1
  fi

  case "${file}" in
    src/Ucli/*|src/Ucli.Application/*|src/Ucli.Contracts/*|src/Ucli.Infrastructure/*|tests/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_unity_input() {
  local file="$1"

  case "${file}" in
    .editorconfig|.gitattributes|.github/workflows/verify.yaml|.ucli/build/*|scripts/detect-verify-scopes.sh|scripts/run-ucli-unity-build.sh|scripts/setup-nuget-cli.sh|scripts/test-unity.sh|scripts/update-local-shared-packages.sh|scripts/verify.sh|src/Ucli.Unity/Ucli.Unity.slnx|src/Ucli.Unity/*.csproj|src/Ucli.Unity/Assets/*|src/Ucli.Unity/Packages/*|src/Ucli.Unity/ProjectSettings/*)
      return 0
      ;;
  esac

  if ! has_managed_input_extension "${file}"; then
    return 1
  fi

  case "${file}" in
    src/Ucli/*|src/Ucli.Application/*|src/Ucli.Contracts/*|src/Ucli.Infrastructure/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_package_publish_shared_input() {
  local file="$1"

  case "${file}" in
    .github/workflows/package-publish.yaml|scripts/create-release-tag.sh|scripts/mirror-nuget-package-release.sh|scripts/package-version-sync-common.sh|scripts/resolve-release-version.sh|scripts/sync-package-version.sh|scripts/verify-release-package-artifacts.sh)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_release_pack_input() {
  local file="$1"

  if is_package_publish_shared_input "${file}"; then
    return 0
  fi

  case "${file}" in
    .gitattributes|Directory.Build.props|README.md|LICENSE|.github/workflows/verify.yaml|scripts/detect-verify-scopes.sh|scripts/generate-schemas.sh|scripts/pack-schema-artifacts.sh|scripts/pack-unity-plugin.sh|scripts/schema-artifact-common.sh|scripts/setup-nuget-cli.sh|scripts/verify-cli-package.sh|scripts/verify-schema-artifacts.sh|scripts/verify-shared-packages.sh|scripts/verify-skills.sh|scripts/verify-unity-plugin-package.sh|schemas/*|src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec|src/Ucli.Unity/Assets/packages.config)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_shared_pack_input() {
  local file="$1"

  if is_package_publish_shared_input "${file}"; then
    return 0
  fi

  case "${file}" in
    .gitattributes|Directory.Build.props|.github/workflows/verify.yaml|scripts/detect-verify-scopes.sh|scripts/verify-shared-packages.sh)
      return 0
      ;;
  esac

  if ! has_managed_input_extension "${file}"; then
    return 1
  fi

  case "${file}" in
    src/Ucli.Contracts/*|src/Ucli.Infrastructure/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_cli_pack_input() {
  local file="$1"

  if is_package_publish_shared_input "${file}"; then
    return 0
  fi

  case "${file}" in
    README.md|LICENSE|.config/dotnet-tools.json|.gitattributes|Directory.Build.props|.github/workflows/verify.yaml|scripts/detect-verify-scopes.sh|scripts/generate-schemas.sh|scripts/pack-schema-artifacts.sh|scripts/schema-artifact-common.sh|scripts/verify-cli-package.sh|scripts/verify-schema-artifacts.sh|scripts/verify-schemas.sh|scripts/verify-skills.sh|schemas/*|skills/*|skills/definitions/*|skills/generated/*|src/Ucli/*|src/Ucli.Application/*|src/Ucli.Contracts/*|src/Ucli.Infrastructure/*|tools/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_unity_pack_input() {
  local file="$1"

  if is_package_publish_shared_input "${file}"; then
    return 0
  fi

  case "${file}" in
    README.md|LICENSE|.gitattributes|.github/workflows/verify.yaml|scripts/detect-verify-scopes.sh|scripts/pack-unity-plugin.sh|scripts/setup-nuget-cli.sh|scripts/update-local-shared-packages.sh|scripts/verify-unity-plugin-package.sh|src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec|src/Ucli.Unity/Assets/packages.config|src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

if [[ -z "${event_name}" ]]; then
  echo "EVENT_NAME is required." >&2
  exit 2
fi

if [[ "${event_name}" == "workflow_dispatch" ]]; then
  emit_full_verification "Full verification requested for ${event_name}."
fi

base_sha=""
head_sha=""
compare_head_sha=""
if [[ "${event_name}" == "pull_request" ]]; then
  base_sha="${PR_BASE_SHA:-}"
  head_sha="${PR_HEAD_SHA:-}"
elif [[ "${event_name}" == "push" ]]; then
  base_sha="${PUSH_BEFORE_SHA:-}"
  head_sha="${current_sha}"
  if [[ -z "${base_sha}" || "${base_sha}" =~ ^0+$ ]]; then
    emit_full_verification "Initial push detected. Verification required for all job types."
  fi
else
  emit_full_verification "Unsupported event '${event_name}' fell back to full verification."
fi

if ! git cat-file -e "${base_sha}^{commit}" 2>/dev/null; then
  emit_full_verification "Base commit '${base_sha}' is unavailable. Full verification required."
fi

compare_head_sha="${head_sha}"
if ! git cat-file -e "${compare_head_sha}^{commit}" 2>/dev/null; then
  if [[ "${event_name}" == "pull_request" ]]; then
    compare_head_sha="HEAD"
  else
    emit_full_verification "Head commit '${head_sha}' is unavailable. Full verification required."
  fi
fi

diff_base_sha="${base_sha}"
if [[ "${event_name}" == "pull_request" ]]; then
  if ! diff_base_sha="$(git merge-base "${base_sha}" "${compare_head_sha}")"; then
    emit_full_verification "Merge base could not be resolved for '${base_sha}' and '${compare_head_sha}'. Full verification required."
  fi
fi

if [[ "${event_name}" == "push" ]]; then
  dotnet_matrix_json="${dotnet_matrix_push}"
  unity_matrix_json="${unity_matrix_push}"
fi

changed_files="$(
  git diff --name-only "${diff_base_sha}" "${compare_head_sha}"
)"

while IFS= read -r file; do
  [[ -z "${file}" ]] && continue

  if is_release_pack_input "${file}"; then
    needs_release_pack=true
  fi

  if is_dotnet_input "${file}"; then
    needs_dotnet=true
  fi

  if is_unity_input "${file}"; then
    needs_unity=true
  fi

  if is_shared_pack_input "${file}"; then
    needs_shared_pack=true
  fi

  if is_cli_pack_input "${file}"; then
    needs_cli_pack=true
  fi

  if is_unity_pack_input "${file}"; then
    needs_unity_pack=true
  fi
done <<< "${changed_files}"

echo "Changed files:"
if [[ -n "${changed_files}" ]]; then
  printf '%s\n' "${changed_files}"
else
  echo "(none)"
fi

emit_outputs
