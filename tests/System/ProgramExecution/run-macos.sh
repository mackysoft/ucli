#!/usr/bin/env bash
set -euo pipefail

# Runs the Program Supervisor against a disposable Unity GUI project.  The
# observations are intentionally made through the public CLI result and the
# immutable artifacts referenced by that result; this runner does not inspect
# Program Supervisor implementation state.

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(git -C "${script_directory}" rev-parse --show-toplevel)"
unity_editor_path=""
results_directory=""
keep_work_directory=false

usage() {
  cat >&2 <<'EOF'
Usage: tests/System/ProgramExecution/run-macos.sh --unity-editor <Unity.app-or-executable> [--results-dir <absolute-path>] [--keep-work-directory]
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --unity-editor)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      unity_editor_path="$2"
      shift 2
      ;;
    --results-dir)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      results_directory="$2"
      shift 2
      ;;
    --keep-work-directory)
      keep_work_directory=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

[[ -n "${unity_editor_path}" ]] || { echo "--unity-editor is required." >&2; exit 2; }

if [[ -d "${unity_editor_path}" ]]; then
  unity_executable="${unity_editor_path%/}/Contents/MacOS/Unity"
else
  unity_executable="${unity_editor_path}"
fi
[[ -x "${unity_executable}" ]] || { echo "Unity Editor executable is not executable: ${unity_executable}" >&2; exit 2; }
command -v jq >/dev/null || { echo "jq is required." >&2; exit 2; }

if [[ -z "${results_directory}" ]]; then
  results_directory="${repository_root}/TestResults/ProgramExecution/$(date -u +%Y%m%dT%H%M%SZ)"
fi
[[ "${results_directory}" == /* ]] || { echo "--results-dir must be absolute: ${results_directory}" >&2; exit 2; }
mkdir -p "$(dirname "${results_directory}")"
mkdir "${results_directory}" 2>/dev/null || { echo "--results-dir must not already exist: ${results_directory}" >&2; exit 2; }
results_directory="$(cd "${results_directory}" && pwd -P)"

run_directory="${results_directory}/work"
test_repository="${run_directory}/repository"
unity_project="${test_repository}/UnityProject"
ucli_directory="${run_directory}/ucli"
ucli_executable="${ucli_directory}/MackySoft.Ucli"
program_path="${run_directory}/program.json"
unity_pid=""
daemon_started=false
program_may_have_entered_play_mode=false
failure_message="ProgramExecution system runner did not reach completion."
overall_status="error"

write_runner_status() {
  jq -n \
    --arg status "$1" \
    --arg message "$2" \
    --arg observedAtUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    '{status:$status,message:$message,observedAtUtc:$observedAtUtc}' \
    > "${results_directory}/runner-status.json"
}

cleanup() {
  local cleanup_exit=$?
  set +e
  if [[ "${program_may_have_entered_play_mode}" == true && "${daemon_started}" == true ]] \
    && [[ -x "${ucli_executable}" ]] && [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    "${ucli_executable}" play exit --projectPath "${unity_project}" --timeout 30000 \
      > "${results_directory}/cleanup-play-exit.json" \
      2> "${results_directory}/cleanup-play-exit.stderr.log"
  fi
  if [[ "${daemon_started}" == true ]] && [[ -x "${ucli_executable}" ]] \
    && [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    "${ucli_executable}" daemon stop --projectPath "${unity_project}" --timeout 30000 \
      > "${results_directory}/cleanup-daemon-stop.json" \
      2> "${results_directory}/cleanup-daemon-stop.stderr.log"
  fi
  if [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    kill "${unity_pid}" 2>/dev/null
    wait "${unity_pid}" 2>/dev/null
  fi
  if [[ "${keep_work_directory}" != true ]]; then
    rm -rf "${unity_project}/Library" "${unity_project}/Logs" "${unity_project}/Temp"
  fi
  if [[ "${overall_status}" == "ok" ]]; then
    write_runner_status "ok" "Program lifecycle execution system-test lane completed."
  else
    write_runner_status "error" "${failure_message}"
  fi
  exit "${cleanup_exit}"
}

trap cleanup EXIT

fail() {
  failure_message="$1"
  echo "program-execution: ${failure_message}" >&2
  exit 1
}

wait_for_gui_session() {
  local expected_process_id="$1"
  local timeout_seconds="$2"
  local waited=0
  local session_path
  while true; do
    for session_path in "${test_repository}"/.ucli/local/projects/*/session.json; do
      [[ -f "${session_path}" ]] || continue
      if jq -e --argjson process_id "${expected_process_id}" \
        '.editorMode == "gui" and .processId == $process_id' "${session_path}" >/dev/null 2>&1; then
        cp "${session_path}" "${results_directory}/gui-session.json"
        return
      fi
    done
    if ! kill -0 "${expected_process_id}" 2>/dev/null; then
      fail "Unity exited before registering its GUI session; see ${results_directory}/unity.log."
    fi
    if (( waited >= timeout_seconds * 10 )); then
      fail "Timed out waiting for Unity GUI session registration; see ${results_directory}/unity.log."
    fi
    sleep 0.1
    waited=$((waited + 1))
  done
}

invoke_ucli() {
  local output_path="$1"
  shift
  set +e
  "${ucli_executable}" "$@" > "${output_path}" 2> "${output_path}.stderr.log"
  last_ucli_exit=$?
  set -e
}

invoke_ucli_with_terminal_input() {
  local output_path="$1"
  shift
  command -v script >/dev/null || fail "macOS script is required to give --program-path a terminal standard input stream."
  # program run treats a redirected standard input stream as Program JSON input,
  # even when --program-path is selected. script supplies a pseudo terminal to
  # the child while preserving the public stdout/stderr artifacts separately.
  set +e
  UCLI_PROGRAM_SYSTEM_STDERR="${output_path}.stderr.log" \
    script -q /dev/null sh -c 'exec "$@" 2>"$UCLI_PROGRAM_SYSTEM_STDERR"' sh \
      "${ucli_executable}" "$@" > "${output_path}.raw"
  last_ucli_exit=$?
  set -e
  # macOS script echoes its end-of-input control character as ^D followed by
  # two backspaces when its own input is redirected. Remove only that framing;
  # retain the raw transcript as diagnostic evidence.
  sed $'s/^\\^D\x08\x08//' "${output_path}.raw" > "${output_path}"
}

assert_success_result() {
  local path="$1"
  [[ "${last_ucli_exit}" -eq 0 ]] || fail "uCLI command failed (${last_ucli_exit}); see ${path} and ${path}.stderr.log."
  jq -e '.status == "ok"' "${path}" >/dev/null \
    || fail "uCLI command did not return a successful CommandResult: ${path}"
}

assert_program_result() {
  jq -e '
    .status == "ok"
    and .command == "program.run"
    and .payload.state == "completed"
    and .payload.verdict == "pass"
    and .payload.childExecutionRefs == []
    and .payload.supervisor.kind == "attachedCli"
    and .payload.supervisor.requestedMode == "daemon"
    and .payload.supervisor.resolvedMode == "daemon"
    and (.payload.supervisor.hostId | type == "string" and length > 0)
    and .payload.terminal.state == "completed"
    and .payload.terminal.verdict == "pass"
    and .payload.terminal.completedStepCount == 4
    and .payload.terminal.unstartedStepCount == 0
    and .payload.executionRef.terminalRecordRef.kind == "programRunTerminalRecord"
    and .payload.terminal.recordRef.kind == "programRunTerminalRecord"
    and .payload.executionRef.terminalRecordRef.digest == .payload.terminal.recordRef.digest
    and ([.payload.steps[].command] == ["refresh", "compile", "play.enter", "play.exit"])
    and ([.payload.steps[] | .state] == ["completed", "completed", "completed", "completed"])
    and ([.payload.steps[] | .verdict] == [null, "pass", null, null])
    and ([.payload.steps[] | .lifecycleExecutionRef.kind] == ["refresh", "compile", "play.enter", "play.exit"])
    and all(.payload.steps[];
      .generationBefore != null
      and .generationAfter != null
      and .lifecycleExecutionRef != null
      and .lifecycleExecutionRef.terminalRecordRef.kind == "lifecycleExecutionTerminalRecord"
      and .resultRef.kind == "programStepTerminalRecord"
      and .applicationState == "applied")
  ' "${results_directory}/program-run.json" >/dev/null \
    || fail "Program Run JSON did not prove the four lifecycle terminal records and fixed daemon host; see program-run.json."
}

assert_referenced_artifacts() {
  local reference_path
  local artifact_path
  local command
  local lifecycle_reference_path
  local lifecycle_artifact_path
  local fixed_host
  local expected_host=""
  while IFS=$'\t' read -r command reference_path lifecycle_reference_path; do
    artifact_path="${test_repository}/${reference_path}"
    [[ -f "${artifact_path}" ]] || fail "Program Step terminal artifact is absent: ${artifact_path}"
    jq -e --arg command "${command}" '
      .command == $command
      and .state == "completed"
      and (if $command == "compile" then .verdict == "pass" else .verdict == null end)
      and .lifecycleExecutionRef.kind == $command
      and .lifecycleExecutionRef.terminalRecordRef.kind == "lifecycleExecutionTerminalRecord"
      and .stepResult == null
    ' "${artifact_path}" >/dev/null \
      || fail "Program Step terminal artifact did not retain its lifecycle terminal reference: ${artifact_path}"
    lifecycle_artifact_path="${test_repository}/${lifecycle_reference_path}"
    [[ -f "${lifecycle_artifact_path}" ]] || fail "Lifecycle terminal artifact is absent: ${lifecycle_artifact_path}"
    fixed_host="$(jq -er '
      [
        .host.process.processId,
        .host.process.generation,
        .host.editorInstanceId
      ] | @tsv
    ' "${lifecycle_artifact_path}")" \
      || fail "Lifecycle terminal artifact did not retain its fixed host identity: ${lifecycle_artifact_path}"
    if [[ -z "${expected_host}" ]]; then
      expected_host="${fixed_host}"
    else
      [[ "${fixed_host}" == "${expected_host}" ]] \
        || fail "Lifecycle terminal artifacts did not retain one fixed process and editor host."
    fi
  done < <(jq -r '.payload.steps[] | [.command, .resultRef.path, .lifecycleExecutionRef.terminalRecordRef.path] | @tsv' "${results_directory}/program-run.json")

  local run_record_path
  run_record_path="${test_repository}/$(jq -r '.payload.terminal.recordRef.path' "${results_directory}/program-run.json")"
  [[ -f "${run_record_path}" ]] || fail "Program Run terminal artifact is absent: ${run_record_path}"
  jq -e '
    .state == "completed"
    and .verdict == "pass"
    and (.steps | length == 4)
    and ([.steps[].command] == ["refresh", "compile", "play.enter", "play.exit"])
    and all(.steps[]; .lifecycleExecutionRef.terminalRecordRef.kind == "lifecycleExecutionTerminalRecord")
  ' "${run_record_path}" >/dev/null \
    || fail "Program Run terminal artifact did not preserve lifecycle terminal records: ${run_record_path}"
  cp "${run_record_path}" "${results_directory}/program-run-terminal-record.json"
}

mkdir -p "${run_directory}" "${test_repository}" "${ucli_directory}"
jq -n \
  --arg repositoryRevision "$(git -C "${repository_root}" rev-parse HEAD)" \
  --arg unityEditor "${unity_executable}" \
  --arg capturedAtUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  '{repositoryRevision:$repositoryRevision,unityEditor:$unityEditor,capturedAtUtc:$capturedAtUtc}' \
  > "${results_directory}/execution-inputs.json"

echo "Building current uCLI and Unity shared packages..." >&2
dotnet build "${repository_root}/src/Ucli/Ucli.csproj" --configuration Debug \
  > "${results_directory}/dotnet-build.log"
bash "${repository_root}/scripts/update-local-shared-packages.sh" --repo-root "${repository_root}" --prune \
  > "${results_directory}/shared-package-build.log"

echo "Preparing disposable Unity project..." >&2
rsync -a --delete \
  --exclude /Library/ \
  --exclude /Logs/ \
  --exclude /Temp/ \
  --exclude /.ucli/ \
  "${repository_root}/src/Ucli.Unity/" "${unity_project}/"
git -C "${test_repository}" init -q
git -C "${test_repository}" config user.email "program-execution@example.invalid"
git -C "${test_repository}" config user.name "Program Execution Harness"

echo "Publishing uCLI host..." >&2
dotnet publish "${repository_root}/src/Ucli/Ucli.csproj" --configuration Debug --output "${ucli_directory}" \
  > "${results_directory}/dotnet-publish.log"
[[ -x "${ucli_executable}" ]] || fail "Published uCLI executable is missing: ${ucli_executable}"

# The Program definition is written by jq so that the script never depends on
# shell quoting for its public JSON input.
jq -n '{steps:[
  {command:"refresh",timeoutMilliseconds:180000},
  {command:"compile",timeoutMilliseconds:180000},
  {command:"play.enter",timeoutMilliseconds:180000},
  {command:"play.exit",timeoutMilliseconds:180000}
]}' > "${program_path}"

echo "Launching Unity GUI host..." >&2
"${unity_executable}" -projectPath "${unity_project}" -logFile "${results_directory}/unity.log" &
unity_pid=$!
wait_for_gui_session "${unity_pid}" 360

invoke_ucli "${results_directory}/daemon-start.json" \
  daemon start --projectPath "${unity_project}" --editorMode gui --timeout 180000
assert_success_result "${results_directory}/daemon-start.json"
daemon_started=true

echo "Executing one Program Run through the daemon..." >&2
program_may_have_entered_play_mode=true
invoke_ucli_with_terminal_input "${results_directory}/program-run.stream.jsonl" \
  program run --projectPath "${unity_project}" --program-path "${program_path}" \
  --mode daemon --allowPlayMode --timeout 600000 --format json
[[ -s "${results_directory}/program-run.stream.jsonl" ]] \
  || fail "Program Run did not produce CLI output."
jq -s 'map(select(.command? == "program.run")) | last' \
  "${results_directory}/program-run.stream.jsonl" > "${results_directory}/program-run.json"
[[ "${last_ucli_exit}" -eq 0 ]] || fail "uCLI Program Run failed (${last_ucli_exit}); see ${results_directory}/program-run.json and ${results_directory}/program-run.stream.jsonl.stderr.log."
jq -e '.status == "ok"' "${results_directory}/program-run.json" >/dev/null \
  || fail "uCLI Program Run did not return a successful CommandResult: ${results_directory}/program-run.json"
program_may_have_entered_play_mode=false

assert_program_result
run_id="$(jq -r '.payload.runId' "${results_directory}/program-run.json")"
invoke_ucli "${results_directory}/program-status.json" \
  program status --projectPath "${unity_project}" --runId "${run_id}"
assert_success_result "${results_directory}/program-status.json"
jq -e --arg run_id "${run_id}" '
  .command == "program.status"
  and .payload.runId == $run_id
  and .payload.state == "completed"
  and .payload.terminal.recordRef.digest != null
' "${results_directory}/program-status.json" >/dev/null \
  || fail "Program status did not recover the same terminal Run."
assert_referenced_artifacts

invoke_ucli "${results_directory}/daemon-stop.json" \
  daemon stop --projectPath "${unity_project}" --timeout 30000
assert_success_result "${results_directory}/daemon-stop.json"
daemon_started=false

overall_status="ok"
echo "Program lifecycle System E2E succeeded: ${results_directory}" >&2
