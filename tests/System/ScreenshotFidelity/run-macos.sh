#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(git -C "${script_dir}" rev-parse --show-toplevel)"
unity_editor_path=""
results_directory=""
keep_work_directory=false
color_space="linear"

usage() {
  echo "Usage: $0 --unity-editor <Unity.app-or-executable> [--color-space <linear|gamma>] [--results-dir <absolute-path>] [--keep-work-directory]" >&2
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
    --color-space)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      color_space="$2"
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
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ -z "${unity_editor_path}" ]]; then
  echo "--unity-editor is required." >&2
  exit 2
fi

case "${color_space}" in
  linear)
    project_color_space_value=1
    unity_color_space_name="Linear"
    ;;
  gamma)
    project_color_space_value=0
    unity_color_space_name="Gamma"
    ;;
  *)
    echo "--color-space must be linear or gamma: ${color_space}" >&2
    exit 2
    ;;
esac

if [[ -d "${unity_editor_path}" ]]; then
  unity_executable="${unity_editor_path%/}/Contents/MacOS/Unity"
else
  unity_executable="${unity_editor_path}"
fi
if [[ ! -x "${unity_executable}" ]]; then
  echo "Unity Editor executable is not executable: ${unity_executable}" >&2
  exit 2
fi

if [[ -z "${results_directory}" ]]; then
  results_directory="${repository_root}/TestResults/ScreenshotFidelity/$(date -u +%Y%m%dT%H%M%SZ)"
fi
if [[ "${results_directory}" != /* ]]; then
  echo "--results-dir must be absolute: ${results_directory}" >&2
  exit 2
fi
mkdir -p "$(dirname "${results_directory}")"
if ! mkdir "${results_directory}" 2>/dev/null; then
  echo "--results-dir must not already exist: ${results_directory}" >&2
  exit 2
fi

results_directory="$(cd "${results_directory}" && pwd -P)"
run_directory="${results_directory}/work"
test_repository="${run_directory}/repository"
unity_project="${test_repository}/UnityProject"
tool_directory="${run_directory}/tools"
case_directory="${results_directory}/cases"
oracle_directory="${tool_directory}/oracle"
mkdir -p "${unity_project}" "${oracle_directory}" "${case_directory}" "${run_directory}/responses"

oracle="${oracle_directory}/screenshot-fidelity-oracle"
ucli_directory="${tool_directory}/ucli"
ucli_executable="${ucli_directory}/MackySoft.Ucli"
unity_pid=""
daemon_started=false
play_mode_entered=false
next_sequence=1
overall_status="error"
failure_message="System-test runner did not reach completion."
game_view_sizes_path="${HOME}/Library/Preferences/Unity/Editor-5.x/GameViewSizes.asset"
game_view_sizes_backup="${run_directory}/GameViewSizes.asset.before"
source_provenance_path="${results_directory}/source-provenance.json"
source_snapshot="${run_directory}/source"
build_workspace="${run_directory}/build-source"
execution_input_manifest_path="${results_directory}/execution-input-manifest.json"
fixture_ready_path="${run_directory}/fixture-ready.json"

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
  if [[ "${play_mode_entered}" == true && "${daemon_started}" == true ]] \
    && [[ -x "${ucli_executable}" && -n "${unity_pid}" ]] \
    && kill -0 "${unity_pid}" 2>/dev/null; then
    "${ucli_executable}" \
      play exit \
      --projectPath "${unity_project}" \
      --timeout 30000 \
      > "${results_directory}/cleanup-play-exit.json" \
      2> "${results_directory}/cleanup-play-exit.stderr.log"
  fi

  if [[ "${daemon_started}" == true && -x "${ucli_executable}" ]] \
    && [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    "${ucli_executable}" \
      daemon stop \
      --projectPath "${unity_project}" \
      --timeout 10000 \
      > "${results_directory}/cleanup-daemon-stop.json" \
      2> "${results_directory}/cleanup-daemon-stop.stderr.log"
  fi

  if [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    kill "${unity_pid}" 2>/dev/null
    wait "${unity_pid}" 2>/dev/null
  fi

  if [[ "${keep_work_directory}" != true ]]; then
    rm -rf "${unity_project}/Library" "${unity_project}/Temp" "${unity_project}/Logs"
  fi

  if [[ "${overall_status}" == "ok" ]]; then
    write_runner_status "ok" "Screenshot fidelity system-test lane completed."
  else
    write_runner_status "error" "${failure_message}"
  fi
  exit "${cleanup_exit}"
}

handle_signal() {
  local exit_code="$1"
  local signal_name="$2"
  overall_status="error"
  failure_message="Screenshot fidelity system-test runner received ${signal_name}."
  trap - INT TERM
  exit "${exit_code}"
}

trap cleanup EXIT
trap 'handle_signal 130 INT' INT
trap 'handle_signal 143 TERM' TERM

fail() {
  failure_message="$1"
  echo "screenshot-fidelity: ${failure_message}" >&2
  exit 1
}

write_source_provenance() {
  local source_index="${run_directory}/repository-source.index"
  local repository_revision
  local repository_object_format
  local repository_source_tree
  local worktree_dirty=false

  rm -f "${source_index}"
  if ! GIT_INDEX_FILE="${source_index}" git -C "${repository_root}" read-tree HEAD \
    || ! GIT_INDEX_FILE="${source_index}" git -C "${repository_root}" add -A -- .; then
    rm -f "${source_index}"
    fail "Could not construct the repository source snapshot used by the fidelity run."
  fi

  repository_revision="$(git -C "${repository_root}" rev-parse HEAD)"
  repository_object_format="$(git -C "${repository_root}" rev-parse --show-object-format)"
  repository_source_tree="$(GIT_INDEX_FILE="${source_index}" git -C "${repository_root}" write-tree)"
  rm -f "${source_index}"

  rm -rf "${source_snapshot}"
  mkdir -p "${source_snapshot}"
  if ! git -C "${repository_root}" archive --format=tar "${repository_source_tree}" \
    | tar -xf - -C "${source_snapshot}"; then
    fail "Could not materialize the repository source snapshot used by the fidelity run."
  fi

  rm -rf "${build_workspace}"
  mkdir -p "${build_workspace}"
  rsync -a --delete "${source_snapshot}/" "${build_workspace}/"

  if [[ -n "$(git -C "${repository_root}" status --porcelain=v1 --untracked-files=all)" ]]; then
    worktree_dirty=true
  fi

  jq -n \
    --arg repositoryRevision "${repository_revision}" \
    --arg repositoryObjectFormat "${repository_object_format}" \
    --arg repositorySourceTree "${repository_source_tree}" \
    --argjson worktreeDirty "${worktree_dirty}" \
    '{
      repositoryRevision:$repositoryRevision,
      repositoryObjectFormat:$repositoryObjectFormat,
      repositorySourceTree:$repositorySourceTree,
      worktreeDirty:$worktreeDirty
    }' > "${source_provenance_path}"
}

append_execution_input_entries() {
  local scope="$1"
  local root="$2"
  local output_path="$3"
  local input_path
  local relative_path
  local digest
  local size_bytes
  local link_target

  while IFS= read -r input_path; do
    relative_path="${input_path#"${root}/"}"
    if [[ -L "${input_path}" ]]; then
      link_target="$(readlink "${input_path}")"
      digest="$(printf '%s' "${link_target}" | shasum -a 256 | awk '{print $1}')"
      size_bytes="$(printf '%s' "${link_target}" | wc -c | tr -d ' ')"
      jq -cn \
        --arg scope "${scope}" \
        --arg path "${relative_path}" \
        --arg digest "${digest}" \
        --argjson sizeBytes "${size_bytes}" \
        --arg linkTarget "${link_target}" \
        '{scope:$scope,path:$path,kind:"symbolicLink",digest:$digest,sizeBytes:$sizeBytes,linkTarget:$linkTarget}' \
        >> "${output_path}"
    else
      digest="$(shasum -a 256 "${input_path}" | awk '{print $1}')"
      size_bytes="$(stat -f '%z' "${input_path}")"
      jq -cn \
        --arg scope "${scope}" \
        --arg path "${relative_path}" \
        --arg digest "${digest}" \
        --argjson sizeBytes "${size_bytes}" \
        '{scope:$scope,path:$path,kind:"file",digest:$digest,sizeBytes:$sizeBytes}' \
        >> "${output_path}"
    fi
  done < <(find "${root}" \( -type f -o -type l \) -print | LC_ALL=C sort)
}

write_execution_input_manifest() {
  local entries_path="${run_directory}/execution-input-entries.jsonl"
  local manifest_digest
  local file_count
  local source_provenance_tmp="${source_provenance_path}.tmp"

  : > "${entries_path}"
  append_execution_input_entries "unityProject" "${unity_project}" "${entries_path}"
  append_execution_input_entries "ucliConfig" "${test_repository}/.ucli" "${entries_path}"
  append_execution_input_entries "ucliHost" "${ucli_directory}" "${entries_path}"
  append_execution_input_entries "windowServerOracle" "${oracle_directory}" "${entries_path}"

  jq -s \
    '{schemaVersion:1,digestAlgorithm:"sha256",entries:.}' \
    "${entries_path}" > "${execution_input_manifest_path}"
  rm -f "${entries_path}"

  manifest_digest="$(shasum -a 256 "${execution_input_manifest_path}" | awk '{print $1}')"
  file_count="$(jq '.entries | length' "${execution_input_manifest_path}")"
  jq \
    --arg path "$(basename "${execution_input_manifest_path}")" \
    --arg digest "${manifest_digest}" \
    --argjson fileCount "${file_count}" \
    '. + {
      executionInputs:{
        manifestPath:$path,
        manifestDigest:$digest,
        digestAlgorithm:"sha256",
        fileCount:$fileCount
      }
    }' \
    "${source_provenance_path}" > "${source_provenance_tmp}"
  mv "${source_provenance_tmp}" "${source_provenance_path}"
}

wait_for_file() {
  local path="$1"
  local timeout_seconds="$2"
  local waited=0
  while [[ ! -s "${path}" ]]; do
    if [[ -n "${unity_pid}" ]] && ! kill -0 "${unity_pid}" 2>/dev/null; then
      fail "Unity exited while waiting for ${path}. See ${results_directory}/unity.log"
    fi
    if (( waited >= timeout_seconds * 10 )); then
      fail "Timed out waiting for ${path}. See ${results_directory}/unity.log"
    fi
    sleep 0.1
    waited=$((waited + 1))
  done
}

wait_for_gui_session() {
  local expected_process_id="$1"
  local timeout_seconds="$2"
  [[ -n "${expected_process_id}" ]] \
    || fail "Unity process identity is required before waiting for its uCLI GUI session."
  local waited=0
  local session_path
  local session_process_id
  local matched_path
  local matched_process_id
  local match_count
  while true; do
    matched_path=""
    matched_process_id=""
    match_count=0
    for session_path in "${test_repository}"/.ucli/local/projects/*/session.json; do
      [[ -f "${session_path}" ]] || continue
      jq -e '.editorMode == "gui" and (.processId | type == "number")' \
        "${session_path}" >/dev/null 2>&1 || continue
      session_process_id="$(jq -r '.processId' "${session_path}")"
      if [[ "${session_process_id}" != "${expected_process_id}" ]]; then
        continue
      fi
      kill -0 "${session_process_id}" 2>/dev/null || continue
      matched_path="${session_path}"
      matched_process_id="${session_process_id}"
      match_count=$((match_count + 1))
    done

    if [[ "${match_count}" -eq 1 ]]; then
      gui_session_path="${matched_path}"
      unity_pid="${matched_process_id}"
      return
    fi
    if [[ "${match_count}" -gt 1 ]]; then
      fail "Multiple live uCLI GUI sessions were registered for the disposable fixture repository."
    fi
    if ! kill -0 "${expected_process_id}" 2>/dev/null; then
      fail "Unity exited before its uCLI GUI session was registered. See ${results_directory}/unity.log"
    fi
    if (( waited >= timeout_seconds * 10 )); then
      fail "Timed out waiting for the Unity uCLI GUI session. See ${results_directory}/unity.log"
    fi
    sleep 0.1
    waited=$((waited + 1))
  done
}

invoke_ucli() {
  local output_path="$1"
  shift
  set +e
  "${ucli_executable}" "$@" > "${output_path}" 2> "${output_path%.json}.stderr.log"
  last_ucli_exit=$?
  set -e
}

start_fixture() {
  local output_path="$1"
  local play_mode="$2"
  local arguments=(
    eval
    --projectPath "${unity_project}"
    --mode daemon
    --allowDangerous
  )
  case "${play_mode}" in
    true)
      arguments+=(--allowPlayMode)
      ;;
    false)
      ;;
    *)
      fail "Fixture start play-mode selection must be true or false: ${play_mode}"
      ;;
  esac
  arguments+=(
    --source "${fixture_start_source}"
    --timeout 30000
  )
  rm -f "${fixture_ready_path}" "${run_directory}/control.json"
  invoke_ucli "${output_path}" "${arguments[@]}"
  assert_command_success "${output_path}"
  jq -e '
    .payload.opResults
    | length == 1
      and .[0].op == "ucli.cs.eval"
      and .[0].result.compile.status == "succeeded"
      and .[0].result.returnValue.kind == "json"
      and .[0].result.returnValue.value == true
  ' "${output_path}" >/dev/null \
    || fail "Screenshot fidelity fixture did not report a successful start: ${output_path}"
  wait_for_file "${fixture_ready_path}" 60
  fixture_process_id="$(jq -r '.processId' "${fixture_ready_path}")"
  [[ "${fixture_process_id}" == "${unity_pid}" ]] \
    || fail "Screenshot fidelity fixture started in an unexpected process. Session=${unity_pid}, Fixture=${fixture_process_id}"
}

send_control() {
  local action="$1"
  local nonce="$2"
  local sequence="${next_sequence}"
  local response_path
  next_sequence=$((next_sequence + 1))
  response_path="${run_directory}/responses/$(printf '%04d' "${sequence}").json"
  jq -n \
    --argjson sequence "${sequence}" \
    --arg action "${action}" \
    --arg nonce "${nonce}" \
    '{sequence:$sequence,action:$action,nonce:$nonce}' \
    > "${run_directory}/control.json.tmp"
  mv "${run_directory}/control.json.tmp" "${run_directory}/control.json"
  wait_for_file "${response_path}" 60
  if [[ "$(jq -r '.status' "${response_path}")" != "ready" ]]; then
    fail "Unity fixture action failed: ${action}: $(jq -r '.message // "unknown error"' "${response_path}")"
  fi
  control_response_path="${response_path}"
}

capture_reference() {
  local response_path="$1"
  local output_path="$2"
  local metadata_path="$3"
  local process_id
  local window_title
  local error_path="${metadata_path%.json}.stderr.log"
  process_id="$(jq -r '.processId' "${response_path}")"
  window_title="$(jq -r '.windowTitle' "${response_path}")"

  for _ in $(seq 1 50); do
    rm -f "${output_path}" "${metadata_path}"
    if "${oracle}" capture \
      --pid "${process_id}" \
      --title "${window_title}" \
      --output "${output_path}" \
      --metadata "${metadata_path}" \
      2> "${error_path}"; then
      rm -f "${error_path}"
      return
    fi

    if ! kill -0 "${process_id}" 2>/dev/null; then
      fail "Unity exited while waiting for its WindowServer presentation. See ${error_path}."
    fi
    sleep 0.2
  done

  fail "WindowServer did not expose exactly one on-screen Unity window titled '${window_title}' for PID ${process_id}. See ${error_path}."
}

copy_artifact_from_result() {
  local command_result="$1"
  local destination="$2"
  local relative_path
  relative_path="$(jq -r '.payload.artifact.path // empty' "${command_result}")"
  [[ -n "${relative_path}" ]] || fail "Screenshot command did not return payload.artifact.path: ${command_result}"
  [[ "${relative_path}" != /* ]] || fail "Screenshot artifact path must be repository-relative: ${relative_path}"
  local source_path="${test_repository}/${relative_path}"
  [[ -f "${source_path}" ]] || fail "Screenshot artifact does not exist: ${source_path}"
  cp "${source_path}" "${destination}"
}

assert_command_success() {
  local command_result="$1"
  [[ "${last_ucli_exit}" -eq 0 ]] || fail "Command failed with exit ${last_ucli_exit}: ${command_result}"
  [[ "$(jq -r '.status' "${command_result}")" == "ok" ]] \
    || fail "Command did not return status=ok: ${command_result}"
}

assert_capture_color_space() {
  local command_result="$1"
  jq -e --arg expected "${color_space}" '.payload.capture.colorSpace == $expected' \
    "${command_result}" >/dev/null \
    || fail "Screenshot result did not report the configured ${color_space} color space: ${command_result}"
}

assert_unity_log_query_empty() {
  local result_path="$1"
  local context="$2"
  assert_command_success "${result_path}"
  jq -e '.payload.count == 0 and .payload.completionReason == "completed"' \
    "${result_path}" >/dev/null \
    || fail "${context} emitted Unity diagnostics; see ${result_path} and ${result_path%.json}.stderr.log"
}

assert_unity_compile_import_log_clean() {
  local log_path="$1"
  local diagnostics_path="$2"
  local context="$3"
  local diagnostic_pattern='(^|[^[:alnum:]_])(warning|error) CS[0-9]{4}([^[:digit:]]|$)|The scripted importer .+ Registration rejected\.|Shader (warning|error) in|Scripts have compiler errors|Compilation failed'
  local grep_exit=0

  grep -E "${diagnostic_pattern}" "${log_path}" > "${diagnostics_path}" || grep_exit=$?
  if [[ "${grep_exit}" -eq 0 ]]; then
    fail "${context} emitted compiler or importer diagnostics; see ${diagnostics_path}."
  fi
  [[ "${grep_exit}" -eq 1 ]] \
    || fail "${context} log inspection failed with exit ${grep_exit}: ${log_path}."
  : > "${diagnostics_path}"
}

write_source_provenance

echo "Compiling independent macOS WindowServer oracle..." >&2
swiftc \
  -warnings-as-errors \
  "${source_snapshot}/tests/System/ScreenshotFidelity/Oracle/ScreenshotFidelityOracle.swift" \
  -o "${oracle}"
"${oracle}" self-check --output "${results_directory}/oracle-self-check.json"
"${oracle}" environment --output "${results_directory}/macos-environment.json"
if [[ "$(jq -r '.screenCapturePermissionGranted' "${results_directory}/macos-environment.json")" != "true" ]]; then
  fail "macOS Screen Recording permission is not granted to this runner."
fi
if [[ "$(jq -r '.screenLocked' "${results_directory}/macos-environment.json")" == "true" ]]; then
  fail "The macOS interactive desktop session is locked; WindowServer fidelity capture requires an unlocked on-screen Unity window."
fi
system_profiler -json SPDisplaysDataType > "${results_directory}/display-environment.json"

echo "Building Unity shared packages from the recorded source snapshot..." >&2
bash "${build_workspace}/scripts/update-local-shared-packages.sh" \
  --repo-root "${build_workspace}" \
  --prune \
  > "${results_directory}/shared-package-build.log"

echo "Preparing disposable Unity fixture repository..." >&2
rsync -a --delete \
  --exclude /Library/ \
  --exclude /Temp/ \
  --exclude /Logs/ \
  --exclude /.ucli/ \
  "${build_workspace}/src/Ucli.Unity/" \
  "${unity_project}/"
rsync -a \
  "${source_snapshot}/tests/System/ScreenshotFidelity/UnityFixture/Assets/" \
  "${unity_project}/Assets/"
project_settings_path="${unity_project}/ProjectSettings/ProjectSettings.asset"
project_settings_tmp="${project_settings_path}.tmp"
active_color_space_count="$(grep -c '^  m_ActiveColorSpace: [01]$' "${project_settings_path}" || true)"
[[ "${active_color_space_count}" -eq 1 ]] \
  || fail "Disposable Unity project must contain exactly one supported m_ActiveColorSpace setting."
awk \
  -v value="${project_color_space_value}" \
  '/^  m_ActiveColorSpace: [01]$/ { print "  m_ActiveColorSpace: " value; next } { print }' \
  "${project_settings_path}" > "${project_settings_tmp}"
mv "${project_settings_tmp}" "${project_settings_path}"
grep -q "^  m_ActiveColorSpace: ${project_color_space_value}$" "${project_settings_path}" \
  || fail "Could not configure the disposable Unity project color space."
git -C "${test_repository}" init -q
git -C "${test_repository}" config user.email "screenshot-fidelity@example.invalid"
git -C "${test_repository}" config user.name "Screenshot Fidelity Harness"
mkdir -p "${test_repository}/.ucli"
jq -n \
  '{
    schemaVersion:1,
    operationPolicy:"dangerous",
    planTokenMode:"optional",
    operationAllowlist:["^ucli\\.cs\\.eval$"]
  }' > "${test_repository}/.ucli/config.json"

if [[ -f "${game_view_sizes_path}" ]]; then
  cp -p "${game_view_sizes_path}" "${game_view_sizes_backup}"
fi
game_view_sizes_hash_before="$(if [[ -f "${game_view_sizes_path}" ]]; then shasum -a 256 "${game_view_sizes_path}" | awk '{print $1}'; else echo absent; fi)"

echo "Publishing current uCLI host..." >&2
dotnet publish \
  "${build_workspace}/src/Ucli/Ucli.csproj" \
  --configuration Debug \
  --output "${ucli_directory}" \
  > "${results_directory}/dotnet-publish.log"
[[ -x "${ucli_executable}" ]] || fail "Published uCLI executable is missing: ${ucli_executable}"

write_execution_input_manifest

echo "Launching Unity GUI fixture..." >&2
"${unity_executable}" \
  -projectPath "${unity_project}" \
  -logFile "${results_directory}/unity.log" &
unity_pid=$!
wait_for_gui_session "${unity_pid}" 360
cp "${gui_session_path}" "${results_directory}/gui-session.json"
assert_unity_compile_import_log_clean \
  "${results_directory}/unity.log" \
  "${results_directory}/unity-bootstrap-diagnostics.txt" \
  "Unity GUI bootstrap"

invoke_ucli "${results_directory}/daemon-start.json" \
  daemon start \
  --projectPath "${unity_project}" \
  --editorMode gui \
  --timeout 180000
assert_command_success "${results_directory}/daemon-start.json"
daemon_started=true

fixture_start_source="$(jq -nr \
  --arg directory "${run_directory}" \
  '$directory | @json | "return MackySoft.Ucli.ScreenshotFidelity.ScreenshotFidelityFixture.Start(" + . + ");"')"
start_fixture "${results_directory}/fixture-start-edit.json" false
jq -e --arg expected "${unity_color_space_name}" '.colorSpace == $expected' \
  "${run_directory}/unity-environment.json" >/dev/null \
  || fail "Unity did not activate the configured ${color_space} color space."

invoke_ucli "${results_directory}/unity-console-clear.json" \
  logs unity clear \
  --projectPath "${unity_project}" \
  --timeout 30000
assert_command_success "${results_directory}/unity-console-clear.json"

invoke_ucli "${results_directory}/unity-log-baseline.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --tail 1 \
  --level all \
  --source all \
  --stackTrace none \
  --format json \
  --timeout 30000
assert_command_success "${results_directory}/unity-log-baseline.json"
unity_log_baseline_cursor="$(jq -r '.payload.nextCursor // empty' "${results_directory}/unity-log-baseline.json")"
[[ -n "${unity_log_baseline_cursor}" ]] \
  || fail "Unity log baseline did not return an incremental cursor."

run_game_case() {
  local case_name="$1"
  local action="$2"
  local requested="$3"
  local directory="${case_directory}/${case_name}"
  mkdir -p "${directory}"
  send_control "${action}" "${case_name}"
  if [[ "${requested}" == true ]]; then
    cp "${control_response_path}" "${directory}/fixture-before.json"
  fi

  if [[ "${requested}" == true ]]; then
    invoke_ucli "${directory}/command.json" \
      screenshot game \
      --projectPath "${unity_project}" \
      --width 321 \
      --height 197 \
      --timeout 30000
  else
    invoke_ucli "${directory}/command.json" \
      screenshot game \
      --projectPath "${unity_project}" \
      --timeout 30000
  fi
  assert_command_success "${directory}/command.json"
  assert_capture_color_space "${directory}/command.json"
  copy_artifact_from_result "${directory}/command.json" "${directory}/artifact.png"

  send_control "snapshotGame" "${case_name}"
  cp "${control_response_path}" "${directory}/fixture.json"

  if [[ "${requested}" == true ]]; then
    jq '{windowInstanceId,gameSelectedSizeIndex,gameSizeCount,gameTargetWidth,gameTargetHeight}' \
      "${directory}/fixture-before.json" > "${directory}/state-before.json"
    jq '{windowInstanceId,gameSelectedSizeIndex,gameSizeCount,gameTargetWidth,gameTargetHeight}' \
      "${directory}/fixture.json" > "${directory}/state-after.json"
    cmp -s "${directory}/state-before.json" "${directory}/state-after.json" \
      || fail "GameView resolution state was not restored; see ${directory}/state-before.json and state-after.json"
    "${oracle}" analyze \
      --target game \
      --artifact "${directory}/artifact.png" \
      --expected-width 321 \
      --expected-height 197 \
      --output "${directory}/analysis.json"
  else
    capture_reference \
      "${control_response_path}" \
      "${directory}/window.png" \
      "${directory}/window.json"
    "${oracle}" analyze \
      --target game \
      --artifact "${directory}/artifact.png" \
      --reference "${directory}/window.png" \
      --output "${directory}/analysis.json"
  fi
}

run_scene_case() {
  local case_name="$1"
  local action="$2"
  local directory="${case_directory}/${case_name}"
  mkdir -p "${directory}"
  send_control "${action}" "${case_name}"
  invoke_ucli "${directory}/command.json" \
    screenshot scene \
    --projectPath "${unity_project}" \
    --timeout 30000
  assert_command_success "${directory}/command.json"
  assert_capture_color_space "${directory}/command.json"
  copy_artifact_from_result "${directory}/command.json" "${directory}/artifact.png"
  send_control "snapshotScene" "${case_name}-after-command"
  cp "${control_response_path}" "${directory}/fixture.json"
  capture_reference \
    "${control_response_path}" \
    "${directory}/window.png" \
    "${directory}/window.json"
  "${oracle}" analyze \
    --target scene \
    --artifact "${directory}/artifact.png" \
    --reference "${directory}/window.png" \
    --output "${directory}/analysis.json"
}

echo "Running SceneView current-presentation fidelity case..." >&2
run_scene_case "scene-current" "prepareSceneCurrent"

echo "Running GameView current-surface fidelity case..." >&2
run_game_case "game-current" "prepareGameCurrent" false

echo "Running GameView requested-resolution and restoration case..." >&2
run_game_case "game-requested-321x197" "prepareGameRequested" true

invoke_ucli "${results_directory}/unity-errors-edit.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --after "${unity_log_baseline_cursor}" \
  --level error \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_unity_log_query_empty \
  "${results_directory}/unity-errors-edit.json" \
  "Screenshot fidelity Edit Mode measurement"

invoke_ucli "${results_directory}/unity-warnings-edit.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --after "${unity_log_baseline_cursor}" \
  --level warning \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_command_success "${results_directory}/unity-warnings-edit.json"

echo "Entering Play Mode for production-backend screenshot cases..." >&2
invoke_ucli "${results_directory}/play-enter.json" \
  play enter \
  --projectPath "${unity_project}" \
  --timeout 60000
assert_command_success "${results_directory}/play-enter.json"
play_mode_entered=true
jq -e '
  .payload.lifecycleState == "playmode"
  and .payload.playMode.state == "playing"
  and .payload.playMode.transition == "none"
  and .payload.playMode.isPlaying == true
  and .payload.playMode.isPlayingOrWillChangePlaymode == true' \
  "${results_directory}/play-enter.json" >/dev/null \
  || fail "Play Mode enter did not report a stable playing state."
start_fixture "${results_directory}/fixture-start-play.json" true

echo "Running SceneView current-presentation fidelity case in Play Mode..." >&2
run_scene_case "scene-current-play" "prepareSceneCurrent"

echo "Running GameView current-surface fidelity case in Play Mode..." >&2
run_game_case "game-current-play" "prepareGameCurrent" false

echo "Running GameView requested-resolution and restoration case in Play Mode..." >&2
run_game_case "game-requested-321x197-play" "prepareGameRequested" true

invoke_ucli "${results_directory}/play-exit.json" \
  play exit \
  --projectPath "${unity_project}" \
  --timeout 60000
assert_command_success "${results_directory}/play-exit.json"
jq -e '
  .payload.lifecycleState == "ready"
  and .payload.playMode.state == "stopped"
  and .payload.playMode.transition == "none"
  and .payload.playMode.isPlaying == false
  and .payload.playMode.isPlayingOrWillChangePlaymode == false' \
  "${results_directory}/play-exit.json" >/dev/null \
  || fail "Play Mode exit did not report a stable stopped state."
play_mode_entered=false

invoke_ucli "${results_directory}/unity-errors-play.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --level error \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_unity_log_query_empty \
  "${results_directory}/unity-errors-play.json" \
  "Screenshot fidelity Play Mode measurement"

invoke_ucli "${results_directory}/unity-warnings-play.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --level warning \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_command_success "${results_directory}/unity-warnings-play.json"

invoke_ucli "${results_directory}/daemon-stop.json" \
  daemon stop \
  --projectPath "${unity_project}" \
  --timeout 30000
assert_command_success "${results_directory}/daemon-stop.json"
daemon_started=false
if kill -0 "${unity_pid}" 2>/dev/null; then
  kill "${unity_pid}" 2>/dev/null
fi
wait "${unity_pid}" 2>/dev/null || true

assert_unity_compile_import_log_clean \
  "${results_directory}/unity.log" \
  "${results_directory}/unity-complete-diagnostics.txt" \
  "Unity GUI fixture"

game_view_sizes_hash_after="$(if [[ -f "${game_view_sizes_path}" ]]; then shasum -a 256 "${game_view_sizes_path}" | awk '{print $1}'; else echo absent; fi)"
[[ "${game_view_sizes_hash_before}" == "${game_view_sizes_hash_after}" ]] \
  || fail "GameViewSizes.asset changed during screenshot capture. The runner did not overwrite it; the pre-run copy is ${game_view_sizes_backup}."

jq -n \
  --arg status "ok" \
  --arg size "Medium" \
  --arg colorSpace "${color_space}" \
  --arg gameViewSizesHashBefore "${game_view_sizes_hash_before}" \
  --arg gameViewSizesHashAfter "${game_view_sizes_hash_after}" \
  --slurpfile source "${source_provenance_path}" \
  --slurpfile macos "${results_directory}/macos-environment.json" \
  --slurpfile unity "${run_directory}/unity-environment.json" \
  --slurpfile unityErrorsEdit "${results_directory}/unity-errors-edit.json" \
  --slurpfile unityWarningsEdit "${results_directory}/unity-warnings-edit.json" \
  --slurpfile unityErrorsPlay "${results_directory}/unity-errors-play.json" \
  --slurpfile unityWarningsPlay "${results_directory}/unity-warnings-play.json" \
  --slurpfile gameCurrent "${case_directory}/game-current/analysis.json" \
  --slurpfile gameRequested "${case_directory}/game-requested-321x197/analysis.json" \
  --slurpfile gameCurrentPlay "${case_directory}/game-current-play/analysis.json" \
  --slurpfile gameRequestedPlay "${case_directory}/game-requested-321x197-play/analysis.json" \
  --slurpfile sceneCurrent "${case_directory}/scene-current/analysis.json" \
  --slurpfile sceneCurrentPlay "${case_directory}/scene-current-play/analysis.json" \
  '{
    status:$status,
    size:$size,
    configuration:{colorSpace:$colorSpace},
    source:$source[0],
    environment:{macos:$macos[0],unity:$unity[0]},
    verification:{
      unityDiagnostics:{
        errorCount:($unityErrorsEdit[0].payload.count + $unityErrorsPlay[0].payload.count),
        warningCount:($unityWarningsEdit[0].payload.count + $unityWarningsPlay[0].payload.count),
        compilerDiagnosticCount:0,
        importerRegistrationDiagnosticCount:0
      }
    },
    stateRestoration:{gameViewSizesHashBefore:$gameViewSizesHashBefore,gameViewSizesHashAfter:$gameViewSizesHashAfter},
    cases:{
      gameCurrent:$gameCurrent[0],
      gameRequested321x197:$gameRequested[0],
      gameCurrentPlay:$gameCurrentPlay[0],
      gameRequested321x197Play:$gameRequestedPlay[0],
      sceneCurrent:$sceneCurrent[0],
      sceneCurrentPlay:$sceneCurrentPlay[0]
    }
  }' > "${results_directory}/fidelity-result.json"

overall_status="ok"
failure_message=""
echo "Screenshot fidelity system-test lane passed: ${results_directory}/fidelity-result.json" >&2
