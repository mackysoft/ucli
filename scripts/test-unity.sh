#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat >&2 <<'EOF'
Usage:
  scripts/test-unity.sh [options]

Options:
  --repoRoot <path>           Repository root. Defaults to git root.
  --projectPath <path>        Unity project root. Required unless UCLI_UNITY_PROJECT_PATH is set.
  --mode <mode>               ucli execution mode. Defaults to oneshot.
  --unityEditorPath <path>    Unity editor executable or directory path.
  --testPlatform <value>      Unity test platform. Defaults to editmode.
  --assemblyName <value>      Comma-separated assembly names. Required unless UCLI_UNITY_TEST_ASSEMBLY is set.
  --testFilter <value>        Test name filter pattern.
  --testCategory <value>      Comma-separated test categories.
  --testSettingsPath <path>   TestSettings.json path.
  --timeout <milliseconds>    Test timeout. Defaults to 1800000.
  --configuration <name>      .NET build configuration. Defaults to Release.
  --resultDir <path>          Output directory. Defaults to artifacts/unity/local.
  --noRestore                 Skip dotnet restore for the ucli project build.
  --noPrune                   Do not prune restored Unity package assets.
EOF
}

repository_root=""
project_path="${UCLI_UNITY_PROJECT_PATH:-}"
execution_mode="oneshot"
unity_editor_path=""
test_platform="editmode"
assembly_name="${UCLI_UNITY_TEST_ASSEMBLY:-}"
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
    --repoRoot|--repo-root)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      repository_root="$2"
      shift 2
      ;;
    --repoRoot=*|--repo-root=*)
      repository_root="${1#*=}"
      shift
      ;;
    --projectPath|--project-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      project_path="$2"
      shift 2
      ;;
    --projectPath=*|--project-path=*)
      project_path="${1#*=}"
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
    --unityEditorPath|--unity-editor-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      unity_editor_path="$2"
      shift 2
      ;;
    --unityEditorPath=*|--unity-editor-path=*)
      unity_editor_path="${1#*=}"
      shift
      ;;
    --testPlatform|--test-platform)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_platform="$2"
      shift 2
      ;;
    --testPlatform=*|--test-platform=*)
      test_platform="${1#*=}"
      shift
      ;;
    --assemblyName|--assembly-name)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      assembly_name="$2"
      shift 2
      ;;
    --assemblyName=*|--assembly-name=*)
      assembly_name="${1#*=}"
      shift
      ;;
    --testFilter|--test-filter)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_filter="$2"
      shift 2
      ;;
    --testFilter=*|--test-filter=*)
      test_filter="${1#*=}"
      shift
      ;;
    --testCategory|--test-category)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_category="$2"
      shift 2
      ;;
    --testCategory=*|--test-category=*)
      test_category="${1#*=}"
      shift
      ;;
    --testSettingsPath|--test-settings-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      test_settings_path="$2"
      shift 2
      ;;
    --testSettingsPath=*|--test-settings-path=*)
      test_settings_path="${1#*=}"
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
    --resultDir|--result-dir)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      result_dir="$2"
      shift 2
      ;;
    --resultDir=*|--result-dir=*)
      result_dir="${1#*=}"
      shift
      ;;
    --noRestore|--no-restore)
      restore=false
      shift
      ;;
    --noPrune|--no-prune)
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
  echo "ERROR: --projectPath or UCLI_UNITY_PROJECT_PATH is required." >&2
  exit 2
fi

if [[ -z "${assembly_name}" ]]; then
  echo "ERROR: --assemblyName or UCLI_UNITY_TEST_ASSEMBLY is required." >&2
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
