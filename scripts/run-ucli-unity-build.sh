#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat >&2 <<'EOF'
Usage:
  scripts/run-ucli-unity-build.sh [options]

Options:
  --repo-root <path>          Repository root. Defaults to git root.
  --project-path <path>       Unity project root. Required unless UCLI_UNITY_PROJECT_PATH is set.
  --profile-path <path>       uCLI build profile JSON path. Required unless UCLI_BUILD_PROFILE_PATH is set.
  --buildTarget <value>       BuildTarget stable name. Defaults to UCLI_BUILD_TARGET when set.
  --mode <mode>               uCLI execution mode. Defaults to oneshot.
  --unity-editor-path <path>  Unity editor executable or directory path.
  --timeout <milliseconds>    Build timeout. Defaults to 900000.
  --configuration <name>      .NET build configuration. Defaults to Release.
  --result-dir <path>         Output directory. Defaults to a temporary directory.
  --no-restore                Skip dotnet restore for the ucli project build.
  --no-build                  Skip dotnet build for the ucli project.
  --no-package-restore        Skip Unity local package restore.
  --no-prune                  Do not prune restored Unity package assets.
EOF
}

repository_root=""
project_path="${UCLI_UNITY_PROJECT_PATH:-}"
profile_path="${UCLI_BUILD_PROFILE_PATH:-}"
buildTarget="${UCLI_BUILD_TARGET:-}"
execution_mode="oneshot"
unity_editor_path=""
timeout_milliseconds="900000"
configuration="Release"
result_dir=""
restore=true
build=true
package_restore=true
prune=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo-root)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      repository_root="$2"
      shift 2
      ;;
    --repo-root=*)
      repository_root="${1#--repo-root=}"
      shift
      ;;
    --project-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      project_path="$2"
      shift 2
      ;;
    --project-path=*)
      project_path="${1#--project-path=}"
      shift
      ;;
    --profile-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      profile_path="$2"
      shift 2
      ;;
    --profile-path=*)
      profile_path="${1#--profile-path=}"
      shift
      ;;
    --buildTarget)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      buildTarget="$2"
      shift 2
      ;;
    --buildTarget=*)
      buildTarget="${1#--buildTarget=}"
      shift
      ;;
    --mode)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      execution_mode="$2"
      shift 2
      ;;
    --mode=*)
      execution_mode="${1#--mode=}"
      shift
      ;;
    --unity-editor-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      unity_editor_path="$2"
      shift 2
      ;;
    --unity-editor-path=*)
      unity_editor_path="${1#--unity-editor-path=}"
      shift
      ;;
    --timeout)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      timeout_milliseconds="$2"
      shift 2
      ;;
    --timeout=*)
      timeout_milliseconds="${1#--timeout=}"
      shift
      ;;
    --configuration)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      configuration="$2"
      shift 2
      ;;
    --configuration=*)
      configuration="${1#--configuration=}"
      shift
      ;;
    --result-dir)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      result_dir="$2"
      shift 2
      ;;
    --result-dir=*)
      result_dir="${1#--result-dir=}"
      shift
      ;;
    --no-restore)
      restore=false
      shift
      ;;
    --no-build)
      build=false
      shift
      ;;
    --no-package-restore)
      package_restore=false
      shift
      ;;
    --no-prune)
      prune=false
      shift
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      print_usage
      exit 2
      ;;
  esac
done

if [[ -z "${repository_root}" ]]; then
  repository_root="$(git rev-parse --show-toplevel)"
fi

if [[ ! -d "${repository_root}" ]]; then
  echo "ERROR: Repository root not found: ${repository_root}" >&2
  exit 1
fi

if [[ -z "${configuration}" ]]; then
  echo "ERROR: --configuration must not be empty." >&2
  exit 2
fi

if [[ -z "${project_path}" ]]; then
  echo "ERROR: --project-path or UCLI_UNITY_PROJECT_PATH is required." >&2
  exit 2
fi

if [[ -z "${profile_path}" ]]; then
  echo "ERROR: --profile-path or UCLI_BUILD_PROFILE_PATH is required." >&2
  exit 2
fi

if [[ ! "${timeout_milliseconds}" =~ ^[1-9][0-9]*$ ]]; then
  echo "ERROR: --timeout must be a positive integer. Actual: ${timeout_milliseconds}" >&2
  exit 2
fi

cd "${repository_root}"

to_absolute_path() {
  case "$1" in
    /*|[A-Za-z]:/*|[A-Za-z]:\\*)
      printf '%s\n' "${1//\\//}"
      ;;
    *)
      printf '%s/%s\n' "${repository_root}" "$1"
      ;;
  esac
}

is_supported_unity_editor_executable_file_name() {
  local file_name
  file_name="$(basename "${1//\\//}")"
  [[ "${file_name}" == "Unity" || "${file_name}" == "Unity.exe" ]]
}

resolve_unity_editor_executable_path() {
  local preferred_path="$1"
  local normalized_path
  normalized_path="$(to_absolute_path "${preferred_path}")"

  if [[ -f "${normalized_path}" ]]; then
    if ! is_supported_unity_editor_executable_file_name "${normalized_path}"; then
      echo "ERROR: --unity-editor-path must point to a Unity executable (Unity or Unity.exe): ${normalized_path}" >&2
      return 2
    fi

    printf '%s\n' "${normalized_path}"
    return 0
  fi

  if [[ ! -d "${normalized_path}" ]]; then
    echo "ERROR: --unity-editor-path does not exist: ${normalized_path}" >&2
    return 2
  fi

  local executable_relative_paths=(
    "Contents/MacOS/Unity"
    "Unity.app/Contents/MacOS/Unity"
    "Editor/Unity.exe"
    "Editor/Unity"
    "Unity.exe"
    "Unity"
  )

  local relative_path
  local executable_path
  for relative_path in "${executable_relative_paths[@]}"; do
    executable_path="${normalized_path}/${relative_path}"
    if [[ ! -f "${executable_path}" ]]; then
      continue
    fi

    if ! is_supported_unity_editor_executable_file_name "${executable_path}"; then
      continue
    fi

    printf '%s\n' "${executable_path}"
    return 0
  done

  echo "ERROR: --unity-editor-path does not contain a Unity executable: ${normalized_path}" >&2
  return 2
}

resolve_unity_editor_root_path() {
  local executable_path="$1"

  case "${executable_path}" in
    */Editor/Unity)
      printf '%s\n' "${executable_path%/Editor/Unity}"
      ;;
    */Editor/Unity.exe)
      printf '%s\n' "${executable_path%/Editor/Unity.exe}"
      ;;
    */Unity.app/Contents/MacOS/Unity)
      printf '%s\n' "${executable_path%/Unity.app/Contents/MacOS/Unity}"
      ;;
    *)
      echo "ERROR: Unsupported Unity editor executable path: ${executable_path}" >&2
      return 2
      ;;
  esac
}

ensure_unity_hub_editor_path() {
  local editor_path="$1"
  local unity_version
  unity_version="$(
    sed -nE 's/^m_EditorVersion: (.+)$/\1/p' \
      "${project_path}/ProjectSettings/ProjectVersion.txt" \
      | head -n 1
  )"
  if [[ -z "${unity_version}" ]]; then
    echo "ERROR: Failed to resolve Unity version from ProjectVersion.txt." >&2
    return 1
  fi

  local executable_path
  executable_path="$(resolve_unity_editor_executable_path "${editor_path}")" || return $?

  local editor_root
  editor_root="$(resolve_unity_editor_root_path "${executable_path}")" || return $?

  local hub_root="${HOME}/Unity/Hub/Editor"
  local expected_editor_root="${hub_root}/${unity_version}"
  mkdir -p "${hub_root}"

  if [[ ! -e "${expected_editor_root}" ]]; then
    ln -s "${editor_root}" "${expected_editor_root}"
  fi
}

project_path="$(to_absolute_path "${project_path}")"
profile_path="$(to_absolute_path "${profile_path}")"

temporary_result_dir=""
if [[ -z "${result_dir}" ]]; then
  temporary_result_dir="$(mktemp -d "${TMPDIR:-/tmp}/ucli-unity-build.XXXXXX")"
  result_dir="${temporary_result_dir}"
else
  result_dir="$(to_absolute_path "${result_dir}")"
fi

command_result_path="${result_dir}/command-result.json"
command_stderr_path="${result_dir}/command-stderr.ndjson"
ucli_project_path="${repository_root}/src/Ucli/Ucli.csproj"
ucli_dll_path="${repository_root}/src/Ucli/bin/${configuration}/net8.0/MackySoft.Ucli.dll"

cleanup_temporary_result_dir() {
  if [[ -n "${temporary_result_dir}" && -d "${temporary_result_dir}" ]]; then
    rm -rf "${temporary_result_dir}"
  fi
}

trap cleanup_temporary_result_dir EXIT

if [[ ! -d "${project_path}" ]]; then
  echo "ERROR: Unity project path not found: ${project_path}" >&2
  exit 1
fi

if [[ ! -f "${profile_path}" ]]; then
  echo "ERROR: uCLI build profile not found: ${profile_path}" >&2
  exit 1
fi

if [[ ! -f "${ucli_project_path}" ]]; then
  echo "ERROR: uCLI project file not found: ${ucli_project_path}" >&2
  exit 1
fi

if [[ -n "${unity_editor_path}" ]]; then
  ensure_unity_hub_editor_path "${unity_editor_path}"
fi

if [[ "${restore}" == "true" ]]; then
  dotnet restore "${ucli_project_path}"
fi

if [[ "${build}" == "true" ]]; then
  dotnet build "${ucli_project_path}" --configuration "${configuration}" --no-restore
fi

if [[ "${package_restore}" == "true" ]]; then
  unity_package_restore_args=(--repo-root "${repository_root}")
  if [[ "${prune}" == "true" ]]; then
    unity_package_restore_args+=(--prune)
  fi

  bash scripts/update-local-shared-packages.sh "${unity_package_restore_args[@]}"
fi

if [[ ! -f "${ucli_dll_path}" ]]; then
  echo "ERROR: uCLI executable assembly not found: ${ucli_dll_path}" >&2
  exit 1
fi

mkdir -p "${result_dir}"

ucli_build_args=(
  build
  run
  --projectPath "${project_path}"
  --profilePath "${profile_path}"
  --mode "${execution_mode}"
  --timeout "${timeout_milliseconds}"
  --format json
)

if [[ -n "${buildTarget}" ]]; then
  ucli_build_args+=(--buildTarget "${buildTarget}")
fi

set +e
dotnet "${ucli_dll_path}" "${ucli_build_args[@]}" > "${command_result_path}" 2> >(tee "${command_stderr_path}" >&2)
exit_code=$?
set -e

if [[ -s "${command_result_path}" ]]; then
  cat "${command_result_path}"
else
  echo "ERROR: uCLI build run did not write a command result." >&2
  if [[ "${exit_code}" -ne 0 ]]; then
    exit "${exit_code}"
  fi

  exit 1
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "exit_code=${exit_code}"
    if [[ -z "${temporary_result_dir}" ]]; then
      echo "command_result_path=${command_result_path}"
      echo "command_stderr_path=${command_stderr_path}"
    fi
  } >> "${GITHUB_OUTPUT}"
fi

if ! jq -e '
  .status == "ok"
  and .exitCode == 0
  and .payload.verdict == "pass"
  and .payload.build.summary.result == "succeeded"
  and .payload.build.output.fileCount > 0
' "${command_result_path}" > /dev/null; then
  if [[ "${exit_code}" -ne 0 ]]; then
    exit "${exit_code}"
  fi

  exit 1
fi

if [[ "${exit_code}" -ne 0 ]]; then
  echo "ERROR: uCLI build run exited with ${exit_code}." >&2
  exit "${exit_code}"
fi
