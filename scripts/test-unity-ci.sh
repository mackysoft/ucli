#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat >&2 <<'EOF'
Usage:
  scripts/test-unity-ci.sh --result-dir <path> [scripts/test-unity.sh options]

Runs scripts/test-unity.sh with CI-oriented timeout and retry policy.
macOS timeout failures are retried once after clearing the Unity Library.
EOF
}

repository_root="$(git rev-parse --show-toplevel)"
result_dir=""
project_path="${UCLI_UNITY_PROJECT_PATH:-}"
timeout_seen=false
test_unity_args=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --result-dir)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      result_dir="$2"
      shift 2
      ;;
    --result-dir=*)
      result_dir="${1#--result-dir=}"
      shift
      ;;
    --project-path)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      project_path="$2"
      test_unity_args+=("$1" "$2")
      shift 2
      ;;
    --project-path=*)
      project_path="${1#--project-path=}"
      test_unity_args+=("$1")
      shift
      ;;
    --timeout)
      [[ $# -ge 2 ]] || { print_usage; exit 2; }
      timeout_seen=true
      test_unity_args+=("$1" "$2")
      shift 2
      ;;
    --timeout=*)
      timeout_seen=true
      test_unity_args+=("$1")
      shift
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      test_unity_args+=("$1")
      shift
      ;;
  esac
done

if [[ -z "${result_dir}" ]]; then
  echo "ERROR: --result-dir is required for CI Unity test runs." >&2
  exit 2
fi

if [[ -z "${project_path}" ]]; then
  echo "ERROR: --project-path or UCLI_UNITY_PROJECT_PATH is required." >&2
  exit 2
fi

if [[ "${timeout_seen}" == "false" ]]; then
  test_unity_args+=(--timeout "${UCLI_UNITY_CI_TIMEOUT_MILLISECONDS:-720000}")
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

is_macos_runner() {
  local runner_os="${RUNNER_OS:-}"
  if [[ "${runner_os}" == "macOS" ]]; then
    return 0
  fi

  [[ "$(uname -s)" == "Darwin" ]]
}

is_timeout_result() {
  local command_result_path="$1"

  [[ -f "${command_result_path}" ]] \
    && grep -q '"UNITY_TEST_EXECUTION_TIMEOUT"' "${command_result_path}"
}

mirror_attempt_result() {
  local attempt_dir="$1"
  local attempt_name="$2"

  mkdir -p "${result_dir}"
  if [[ -f "${attempt_dir}/command-result.json" ]]; then
    cp "${attempt_dir}/command-result.json" "${result_dir}/command-result.json"
  fi

  if [[ -f "${attempt_dir}/command-stderr.log" ]]; then
    cp "${attempt_dir}/command-stderr.log" "${result_dir}/command-stderr.log"
  fi

  printf '%s\n' "${attempt_name}" > "${result_dir}/final-attempt.txt"
}

write_step_outputs() {
  local exit_code="$1"

  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "exit_code=${exit_code}"
      echo "command_result_path=${result_dir}/command-result.json"
      echo "command_stderr_path=${result_dir}/command-stderr.log"
    } >> "${GITHUB_OUTPUT}"
  fi
}

run_attempt() {
  local attempt_name="$1"
  local attempt_dir="${result_dir}/${attempt_name}"

  rm -rf "${attempt_dir}"
  mkdir -p "${attempt_dir}"

  echo "Running Unity EditMode tests (${attempt_name})."
  set +e
  GITHUB_OUTPUT= bash scripts/test-unity.sh "${test_unity_args[@]}" --result-dir "${attempt_dir}"
  local exit_code=$?
  set -e

  mirror_attempt_result "${attempt_dir}" "${attempt_name}"
  return "${exit_code}"
}

set +e
run_attempt "attempt-1"
exit_code=$?
set -e

if [[ "${exit_code}" -ne 0 ]] \
  && is_macos_runner \
  && is_timeout_result "${result_dir}/attempt-1/command-result.json"; then
  echo "macOS Unity test run timed out. Clearing Unity Library and retrying once."
  project_path="$(to_absolute_path "${project_path}")"
  rm -rf "${project_path}/Library"

  set +e
  run_attempt "attempt-2"
  exit_code=$?
  set -e
fi

write_step_outputs "${exit_code}"
exit "${exit_code}"
