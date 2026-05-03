#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat >&2 <<'EOF'
Usage:
  scripts/test-unity.sh [options]

Options:
  --repo-root <path>          Repository root. Defaults to git root.
  --project-path <path>       Unity project root. Defaults to src/Ucli.Unity.
  --mode <mode>               ucli execution mode. Defaults to oneshot.
  --unity-editor-path <path>  Unity editor executable or directory path.
  --test-platform <value>     Unity test platform. Defaults to editmode.
  --assembly-name <value>     Comma-separated assembly names.
  --test-filter <value>       Test name filter pattern.
  --test-category <value>     Comma-separated test categories.
  --test-settings-path <path> TestSettings.json path.
  --timeout <milliseconds>    Test timeout. Defaults to 1800000.
  --configuration <name>      .NET build configuration. Defaults to Release.
  --result-dir <path>         Output directory. Defaults to artifacts/unity/local.
  --no-restore                Skip dotnet restore for the ucli project build.
  --no-prune                  Do not prune restored Unity package assets.
EOF
}

repository_root=""
project_path="src/Ucli.Unity"
execution_mode="oneshot"
unity_editor_path=""
test_platform="editmode"
assembly_name="MackySoft.Ucli.Unity.Tests.Editor"
test_filter=""
test_category=""
test_settings_path=""
timeout_milliseconds="1800000"
configuration="Release"
result_dir="artifacts/unity/local"
restore=true
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
    --test-platform)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_platform="$2"
      shift 2
      ;;
    --test-platform=*)
      test_platform="${1#--test-platform=}"
      shift
      ;;
    --assembly-name)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      assembly_name="$2"
      shift 2
      ;;
    --assembly-name=*)
      assembly_name="${1#--assembly-name=}"
      shift
      ;;
    --test-filter)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_filter="$2"
      shift 2
      ;;
    --test-filter=*)
      test_filter="${1#--test-filter=}"
      shift
      ;;
    --test-category)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_category="$2"
      shift 2
      ;;
    --test-category=*)
      test_category="${1#--test-category=}"
      shift
      ;;
    --test-settings-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_settings_path="$2"
      shift 2
      ;;
    --test-settings-path=*)
      test_settings_path="${1#--test-settings-path=}"
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

if [[ ! "${timeout_milliseconds}" =~ ^[1-9][0-9]*$ ]]; then
  echo "ERROR: --timeout must be a positive integer. Actual: ${timeout_milliseconds}" >&2
  exit 2
fi

cd "${repository_root}"

to_absolute_path() {
  case "$1" in
    /*|[A-Za-z]:/*|[A-Za-z]:\\*)
      printf '%s\n' "$1"
      ;;
    *)
      printf '%s/%s\n' "${repository_root}" "$1"
      ;;
  esac
}

project_path="$(to_absolute_path "${project_path}")"
result_dir="$(to_absolute_path "${result_dir}")"
command_result_path="${result_dir}/command-result.json"
command_stderr_path="${result_dir}/command-stderr.log"
ucli_project_path="${repository_root}/src/Ucli/Ucli.csproj"
ucli_dll_path="${repository_root}/src/Ucli/bin/${configuration}/net8.0/MackySoft.Ucli.dll"

if [[ ! -d "${project_path}" ]]; then
  echo "ERROR: Unity project path not found: ${project_path}" >&2
  exit 1
fi

if [[ ! -f "${ucli_project_path}" ]]; then
  echo "ERROR: uCLI project file not found: ${ucli_project_path}" >&2
  exit 1
fi

if [[ "${restore}" == "true" ]]; then
  dotnet restore "${ucli_project_path}"
fi

dotnet build "${ucli_project_path}" --configuration "${configuration}" --no-restore

unity_package_restore_args=(--repo-root "${repository_root}")
if [[ "${prune}" == "true" ]]; then
  unity_package_restore_args+=(--prune)
fi
bash scripts/update-local-shared-packages.sh "${unity_package_restore_args[@]}"

mkdir -p "${result_dir}"

ucli_test_args=(
  test
  run
  --projectPath "${project_path}"
  --mode "${execution_mode}"
  --testPlatform "${test_platform}"
  --assemblyName "${assembly_name}"
  --timeout "${timeout_milliseconds}"
)

if [[ -n "${unity_editor_path}" ]]; then
  ucli_test_args+=(--unityEditorPath "${unity_editor_path}")
fi

if [[ -n "${test_filter}" ]]; then
  ucli_test_args+=(--testFilter "${test_filter}")
fi

if [[ -n "${test_category}" ]]; then
  ucli_test_args+=(--testCategory "${test_category}")
fi

if [[ -n "${test_settings_path}" ]]; then
  ucli_test_args+=(--testSettingsPath "${test_settings_path}")
fi

set +e
dotnet "${ucli_dll_path}" "${ucli_test_args[@]}" > "${command_result_path}" 2> "${command_stderr_path}"
exit_code=$?
set -e

if [[ -s "${command_stderr_path}" ]]; then
  cat "${command_stderr_path}"
fi

if [[ -s "${command_result_path}" ]]; then
  cat "${command_result_path}"
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "exit_code=${exit_code}"
    echo "command_result_path=${command_result_path}"
    echo "command_stderr_path=${command_stderr_path}"
  } >> "${GITHUB_OUTPUT}"
fi

exit "${exit_code}"
