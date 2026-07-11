#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(git -C "${script_dir}" rev-parse --show-toplevel)"
unity_editor_path=""
unity_application_path=""
results_directory=""
keep_work_directory=false

usage() {
  echo "Usage: $0 --unity-editor <Unity.app-or-executable> [--results-dir <absolute-path>] [--keep-work-directory]" >&2
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

if [[ -d "${unity_editor_path}" ]]; then
  unity_application_path="${unity_editor_path%/}"
  unity_executable="${unity_application_path}/Contents/MacOS/Unity"
else
  unity_executable="${unity_editor_path}"
  if [[ "${unity_executable}" == */Contents/MacOS/Unity ]]; then
    candidate_application_path="${unity_executable%/Contents/MacOS/Unity}"
    if [[ -d "${candidate_application_path}" ]]; then
      unity_application_path="${candidate_application_path}"
    fi
  fi
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

mkdir -p "${results_directory}"
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
unity_launcher_pid=""
controller_started=false
next_sequence=1
overall_status="error"
failure_message="System-test runner did not reach completion."
game_view_sizes_path="${HOME}/Library/Preferences/Unity/Editor-5.x/GameViewSizes.asset"
game_view_sizes_backup="${run_directory}/GameViewSizes.asset.before"
source_provenance_path="${results_directory}/source-provenance.json"
source_snapshot="${run_directory}/source"
build_workspace="${run_directory}/build-source"
execution_input_manifest_path="${results_directory}/execution-input-manifest.json"

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
  if [[ "${controller_started}" == true && -x "${ucli_executable}" ]]; then
    "${ucli_executable}" \
      daemon stop \
      --projectPath "${unity_project}" \
      --timeout 10000 \
      > "${results_directory}/cleanup-daemon-stop.json" \
      2> "${results_directory}/cleanup-daemon-stop.stderr.log"
  fi

  if [[ "${controller_started}" == true && -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    local sequence="${next_sequence}"
    jq -n --argjson sequence "${sequence}" '{sequence:$sequence,action:"quit",nonce:"cleanup"}' \
      > "${run_directory}/control.json.tmp"
    mv "${run_directory}/control.json.tmp" "${run_directory}/control.json"
    for _ in $(seq 1 50); do
      kill -0 "${unity_pid}" 2>/dev/null || break
      sleep 0.2
    done
  fi

  if [[ -n "${unity_pid}" ]] && kill -0 "${unity_pid}" 2>/dev/null; then
    kill "${unity_pid}" 2>/dev/null
    wait "${unity_pid}" 2>/dev/null
  fi

  if [[ -n "${unity_launcher_pid}" && "${unity_launcher_pid}" != "${unity_pid}" ]]; then
    for _ in $(seq 1 50); do
      kill -0 "${unity_launcher_pid}" 2>/dev/null || break
      sleep 0.1
    done
    if kill -0 "${unity_launcher_pid}" 2>/dev/null; then
      kill "${unity_launcher_pid}" 2>/dev/null
    fi
    wait "${unity_launcher_pid}" 2>/dev/null
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
    if [[ -z "${unity_pid}" && -n "${unity_launcher_pid}" ]] \
      && ! kill -0 "${unity_launcher_pid}" 2>/dev/null; then
      fail "Unity launcher exited while waiting for ${path}. See ${results_directory}/unity.log"
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
  local waited=0
  local session_path
  while true; do
    for session_path in "${test_repository}"/.ucli/local/fingerprints/*/session.json; do
      [[ -f "${session_path}" ]] || continue
      if jq -e \
        --argjson expectedProcessId "${expected_process_id}" \
        '.editorMode == "gui" and .processId == $expectedProcessId' \
        "${session_path}" >/dev/null 2>&1; then
        gui_session_path="${session_path}"
        return
      fi
    done

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

send_control() {
  local action="$1"
  local nonce="$2"
  local sequence="${next_sequence}"
  local response_path
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
  assert_fixture_render_isolation "${response_path}" "${action}"
  control_response_path="${response_path}"
  next_sequence=$((next_sequence + 1))
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

  if [[ -z "${unity_application_path}" ]]; then
    fail "WindowServer fidelity capture requires a Unity application bundle or its Contents/MacOS/Unity executable."
  fi
  local activated=false
  for _ in $(seq 1 10); do
    if open -a "${unity_application_path}" > /dev/null 2> "${error_path}"; then
      activated=true
      break
    fi

    if ! kill -0 "${process_id}" 2>/dev/null; then
      break
    fi
    sleep 0.2
  done
  if [[ "${activated}" != true ]]; then
    fail "macOS could not bring the Unity fixture application forward before WindowServer capture. See ${error_path}."
  fi

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

assert_fixture_render_isolation() {
  local response_path="$1"
  local context="$2"
  jq -e '
    .renderIsolation as $state
    | $state.lightCount == 0
      and $state.meshRendererCount == 2
      and $state.fogEnabled == false
      and $state.skyboxAssigned == false
      and $state.ambientMode == "Flat"
      and $state.ambientLightMaximum == 0
      and $state.ambientIntensity == 0
      and $state.reflectionIntensity == 0
      and $state.customReflectionAssigned == false
      and $state.patternShaderName == "Hidden/uCLI/ScreenshotFidelityPattern"
      and $state.patternShaderSupported == true
      and $state.patternShaderMessageCount == 0
      and $state.activeRendererFeatureCount == 0
      and $state.baseCameraCullingMask == 536870912
      and $state.overlayCameraCullingMask == 1073741824
      and $state.baseCameraPostProcessing == true
      and $state.overlayCameraPostProcessing == false
      and $state.baseCameraStackCount == 1
      and $state.patternLayer == 29
      and $state.overlayLayer == 30
      and $state.volumeLayer == 28
      and $state.volumeComponentCount == 2
      and $state.canvasRenderMode == "ScreenSpaceOverlay"
      and (
        if $state.target == "Game"
        then $state.presentationCanvasActive == true and $state.runtimeImguiEnabled == true
        else $state.presentationCanvasActive == false and $state.runtimeImguiEnabled == false
        end
      )
      and (
        $state.target != "Scene"
        or (
          $state.sceneCameraMode == "Textured"
          and $state.sceneIn2DMode == false
          and $state.sceneDrawGizmos == false
          and $state.sceneLighting == false
          and $state.sceneFxEnabled == false
          and $state.sceneFogEnabled == false
          and $state.sceneSkyboxEnabled == false
          and $state.sceneImageEffectsEnabled == false
          and $state.sceneParticleSystemsEnabled == false
        )
      )' \
    "${response_path}" >/dev/null \
    || fail "${context} fixture render isolation is invalid: ${response_path}"
}

assert_unity_log_query_empty() {
  local result_path="$1"
  local context="$2"
  assert_command_success "${result_path}"
  jq -e '.payload.count == 0 and .payload.completionReason == "completed"' \
    "${result_path}" >/dev/null \
    || fail "${context} emitted Unity diagnostics; see ${result_path} and ${result_path%.json}.stderr.log"
}

assert_overlay_failure() {
  local command_result="$1"
  local condition_name="$2"
  local expected_diagnostic="$3"
  [[ "${last_ucli_exit}" -ne 0 ]] || fail "Scene capture unexpectedly succeeded with ${condition_name} displayed."
  jq -e --arg expected "${expected_diagnostic}" '
    .status == "error"
    and any(
      .errors[]?;
      .code == "SCREENSHOT_CAPTURE_UNSUPPORTED"
      and (.message | contains($expected)))
    and ((.payload? == null) or (.payload.artifact? == null))' \
    "${command_result}" >/dev/null \
    || fail "${condition_name} capture did not fail without an artifact reference using SCREENSHOT_CAPTURE_UNSUPPORTED: ${command_result}"
}

assert_no_excluded_overlays() {
  local response_path="$1"
  local context="$2"
  jq -e '
    .displayedExcludedOverlayCount == 0
    and ((.displayedExcludedOverlays // []) | length == 0)' \
    "${response_path}" >/dev/null \
    || fail "${context} contains a displayed configurable Overlay: ${response_path}"
}

displayed_excluded_overlays() {
  local response_path="$1"
  jq -c '(.displayedExcludedOverlays // []) | sort' "${response_path}"
}

assert_scene_fixture_state_unchanged() {
  local before_path="$1"
  local after_path="$2"
  local context="$3"
  local before_state="${run_directory}/scene-state-before.json"
  local after_state="${run_directory}/scene-state-after.json"

  jq '{windowInstanceId,sceneTool,sceneSelectionInstanceId}' "${before_path}" > "${before_state}"
  jq '{windowInstanceId,sceneTool,sceneSelectionInstanceId}' "${after_path}" > "${after_state}"
  cmp -s "${before_state}" "${after_state}" \
    || fail "${context} changed the SceneView tool, selection, or window identity."
}

snapshot_screenshot_artifacts() {
  find "${test_repository}/.ucli" -type f -path '*/artifacts/screenshot/*' -exec shasum -a 256 {} \; 2>/dev/null \
    | LC_ALL=C sort
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
git -C "${test_repository}" init -q
git -C "${test_repository}" config user.email "screenshot-fidelity@example.invalid"
git -C "${test_repository}" config user.name "Screenshot Fidelity Harness"

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
if [[ -n "${unity_application_path}" ]]; then
  open -n -W -a "${unity_application_path}" --args \
    -projectPath "${unity_project}" \
    -logFile "${results_directory}/unity.log" \
    -executeMethod MackySoft.Ucli.ScreenshotFidelity.ScreenshotFidelityBootstrap.Start \
    -ucliScreenshotFidelityRunDirectory "${run_directory}" &
  unity_launcher_pid=$!
else
  "${unity_executable}" \
    -projectPath "${unity_project}" \
    -logFile "${results_directory}/unity.log" \
    -executeMethod MackySoft.Ucli.ScreenshotFidelity.ScreenshotFidelityBootstrap.Start \
    -ucliScreenshotFidelityRunDirectory "${run_directory}" &
  unity_launcher_pid=$!
  unity_pid="${unity_launcher_pid}"
fi
wait_for_file "${run_directory}/bootstrap-ready.json" 360
controller_started=true
bootstrap_process_id="$(jq -r '.processId' "${run_directory}/bootstrap-ready.json")"
if [[ -n "${unity_pid}" && "${bootstrap_process_id}" != "${unity_pid}" ]]; then
  fail "Unity fixture process identity changed unexpectedly. Launched=${unity_pid}, Fixture=${bootstrap_process_id}"
fi
unity_pid="${bootstrap_process_id}"
wait_for_gui_session "${unity_pid}" 120
cp "${gui_session_path}" "${results_directory}/gui-session.json"

invoke_ucli "${results_directory}/daemon-start.json" \
  daemon start \
  --projectPath "${unity_project}" \
  --editorMode gui \
  --timeout 180000
assert_command_success "${results_directory}/daemon-start.json"

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
  cp "${control_response_path}" "${directory}/fixture-before.json"
  capture_reference \
    "${control_response_path}" \
    "${directory}/window-before.png" \
    "${directory}/window-before.json"

  if [[ "${requested}" == true ]]; then
    invoke_ucli "${directory}/command.json" \
      screenshot game \
      --projectPath "${unity_project}" \
      --mode daemon \
      --width 321 \
      --height 197 \
      --timeout 30000
  else
    invoke_ucli "${directory}/command.json" \
      screenshot game \
      --projectPath "${unity_project}" \
      --mode daemon \
      --timeout 30000
  fi
  assert_command_success "${directory}/command.json"
  copy_artifact_from_result "${directory}/command.json" "${directory}/artifact.png"

  send_control "snapshotGame" "${case_name}"
  cp "${control_response_path}" "${directory}/fixture-after.json"
  capture_reference \
    "${control_response_path}" \
    "${directory}/window-after.png" \
    "${directory}/window-after.json"

  if [[ "${requested}" == true ]]; then
    jq '{windowInstanceId,gameSelectedSizeIndex,gameSizeCount,gameTargetWidth,gameTargetHeight,backingScale,hdrActive}' \
      "${directory}/fixture-before.json" > "${directory}/state-before.json"
    jq '{windowInstanceId,gameSelectedSizeIndex,gameSizeCount,gameTargetWidth,gameTargetHeight,backingScale,hdrActive}' \
      "${directory}/fixture-after.json" > "${directory}/state-after.json"
    cmp -s "${directory}/state-before.json" "${directory}/state-after.json" \
      || fail "GameView resolution state was not restored; see ${directory}/state-before.json and state-after.json"
    "${oracle}" analyze \
      --target game \
      --artifact "${directory}/artifact.png" \
      --reference-before "${directory}/window-before.png" \
      --reference-after "${directory}/window-after.png" \
      --expected-width 321 \
      --expected-height 197 \
      --output "${directory}/analysis.json"
  else
    "${oracle}" analyze \
      --target game \
      --artifact "${directory}/artifact.png" \
      --reference-before "${directory}/window-before.png" \
      --reference-after "${directory}/window-after.png" \
      --output "${directory}/analysis.json"
  fi
}

echo "Running GameView current-surface fidelity case..." >&2
run_game_case "game-current" "prepareGameCurrent" false

echo "Running GameView requested-resolution and restoration case..." >&2
run_game_case "game-requested-321x197" "prepareGameRequested" true

echo "Running SceneView current-presentation fidelity case..." >&2
scene_directory="${case_directory}/scene-current"
mkdir -p "${scene_directory}"
send_control "prepareSceneCurrent" "scene-current"
cp "${control_response_path}" "${scene_directory}/fixture-before.json"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "false" ]] \
  || fail "Scene positive fixture unexpectedly displays the Overlay Menu."
assert_no_excluded_overlays "${control_response_path}" "Scene positive fixture"
capture_reference \
  "${control_response_path}" \
  "${scene_directory}/window-before.png" \
  "${scene_directory}/window-before.json"
invoke_ucli "${scene_directory}/command.json" \
  screenshot scene \
  --projectPath "${unity_project}" \
  --mode daemon \
  --timeout 30000
assert_command_success "${scene_directory}/command.json"
copy_artifact_from_result "${scene_directory}/command.json" "${scene_directory}/artifact.png"
send_control "snapshotScene" "scene-current"
cp "${control_response_path}" "${scene_directory}/fixture-after.json"
assert_scene_fixture_state_unchanged \
  "${scene_directory}/fixture-before.json" \
  "${scene_directory}/fixture-after.json" \
  "Scene positive capture"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "false" ]] \
  || fail "Scene Overlay Menu became visible during positive fidelity capture."
assert_no_excluded_overlays "${control_response_path}" "Scene positive fixture after capture"
capture_reference \
  "${control_response_path}" \
  "${scene_directory}/window-after.png" \
  "${scene_directory}/window-after.json"
"${oracle}" analyze \
  --target scene \
  --artifact "${scene_directory}/artifact.png" \
  --reference-before "${scene_directory}/window-before.png" \
  --reference-after "${scene_directory}/window-after.png" \
  --output "${scene_directory}/analysis.json"

echo "Running SceneView OverlayMenu fail-closed case..." >&2
overlay_directory="${case_directory}/scene-overlay-menu-fail-closed"
mkdir -p "${overlay_directory}"
send_control "prepareSceneOverlayMenu" "scene-overlay-menu"
cp "${control_response_path}" "${overlay_directory}/fixture-before.json"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "true" ]] \
  || fail "Scene negative fixture could not display the Overlay Menu."
assert_no_excluded_overlays "${control_response_path}" "Scene Overlay Menu negative fixture"
capture_reference \
  "${control_response_path}" \
  "${overlay_directory}/window.png" \
  "${overlay_directory}/window.json"
send_control "snapshotScene" "scene-overlay-menu-after-window-capture"
cp "${control_response_path}" "${overlay_directory}/fixture-after-window-capture.json"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "true" ]] \
  || fail "Scene Overlay Menu closed before the negative capture command."
assert_no_excluded_overlays "${control_response_path}" "Scene Overlay Menu fixture before command"
snapshot_screenshot_artifacts > "${overlay_directory}/artifacts-before.txt"
invoke_ucli "${overlay_directory}/command.json" \
  screenshot scene \
  --projectPath "${unity_project}" \
  --mode daemon \
  --timeout 30000
assert_overlay_failure \
  "${overlay_directory}/command.json" \
  "Overlay Menu" \
  "Displayed SceneView Overlay Menu"
send_control "snapshotScene" "scene-overlay-menu-after-command"
cp "${control_response_path}" "${overlay_directory}/fixture-after-command.json"
assert_scene_fixture_state_unchanged \
  "${overlay_directory}/fixture-before.json" \
  "${overlay_directory}/fixture-after-command.json" \
  "Scene Overlay Menu capture"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "true" ]] \
  || fail "Scene screenshot command changed the displayed Overlay Menu state."
assert_no_excluded_overlays "${control_response_path}" "Scene Overlay Menu fixture after command"
snapshot_screenshot_artifacts > "${overlay_directory}/artifacts-after.txt"
cmp -s "${overlay_directory}/artifacts-before.txt" "${overlay_directory}/artifacts-after.txt" \
  || fail "Scene OverlayMenu failure changed the screenshot artifact path/digest set."
jq -n \
  --arg status "ok" \
  --arg failureCode "SCREENSHOT_CAPTURE_UNSUPPORTED" \
  --arg artifactSetStatus "unchanged" \
  '{status:$status,failureCode:$failureCode,artifactSetStatus:$artifactSetStatus}' \
  > "${overlay_directory}/analysis.json"

echo "Running SceneView configurable Overlay panel fail-closed case..." >&2
panel_directory="${case_directory}/scene-configurable-overlay-fail-closed"
mkdir -p "${panel_directory}"
send_control "prepareSceneConfigurableOverlay" "scene-configurable-overlay"
cp "${control_response_path}" "${panel_directory}/fixture-before.json"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "false" ]] \
  || fail "Configurable Overlay fixture unexpectedly displays the Overlay Menu."
[[ "$(jq -r '.displayedExcludedOverlayCount' "${control_response_path}")" == "1" ]] \
  || fail "Scene negative fixture did not display exactly one configurable Overlay panel."
panel_overlay_state="$(displayed_excluded_overlays "${control_response_path}")"
panel_overlay_name="$(jq -r '.displayedExcludedOverlays[0]' "${control_response_path}")"
capture_reference \
  "${control_response_path}" \
  "${panel_directory}/window.png" \
  "${panel_directory}/window.json"
send_control "snapshotScene" "scene-configurable-overlay-after-window-capture"
cp "${control_response_path}" "${panel_directory}/fixture-after-window-capture.json"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "false" ]] \
  || fail "Overlay Menu appeared in the configurable Overlay negative fixture."
[[ "$(displayed_excluded_overlays "${control_response_path}")" == "${panel_overlay_state}" ]] \
  || fail "Configurable Overlay panel state changed before the negative capture command."
snapshot_screenshot_artifacts > "${panel_directory}/artifacts-before.txt"
invoke_ucli "${panel_directory}/command.json" \
  screenshot scene \
  --projectPath "${unity_project}" \
  --mode daemon \
  --timeout 30000
assert_overlay_failure \
  "${panel_directory}/command.json" \
  "configurable Overlay panel" \
  "Displayed configurable Overlay panel or toolbar"
send_control "snapshotScene" "scene-configurable-overlay-after-command"
cp "${control_response_path}" "${panel_directory}/fixture-after-command.json"
assert_scene_fixture_state_unchanged \
  "${panel_directory}/fixture-before.json" \
  "${panel_directory}/fixture-after-command.json" \
  "Scene configurable Overlay capture"
[[ "$(jq -r '.overlayMenuDisplayed' "${control_response_path}")" == "false" ]] \
  || fail "Overlay Menu appeared after the configurable Overlay negative command."
[[ "$(displayed_excluded_overlays "${control_response_path}")" == "${panel_overlay_state}" ]] \
  || fail "Scene screenshot command changed the configurable Overlay panel state."
snapshot_screenshot_artifacts > "${panel_directory}/artifacts-after.txt"
cmp -s "${panel_directory}/artifacts-before.txt" "${panel_directory}/artifacts-after.txt" \
  || fail "Configurable Overlay failure changed the screenshot artifact path/digest set."
jq -n \
  --arg status "ok" \
  --arg failureCode "SCREENSHOT_CAPTURE_UNSUPPORTED" \
  --arg artifactSetStatus "unchanged" \
  --arg displayedOverlay "${panel_overlay_name}" \
  '{status:$status,failureCode:$failureCode,artifactSetStatus:$artifactSetStatus,displayedOverlay:$displayedOverlay}' \
  > "${panel_directory}/analysis.json"

jq -s \
  '{
    game: .[0].renderIsolation,
    scene: .[1].renderIsolation
  }' \
  "${case_directory}/game-current/fixture-before.json" \
  "${case_directory}/scene-current/fixture-before.json" \
  > "${results_directory}/fixture-render-isolation.json"

invoke_ucli "${results_directory}/unity-errors.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --after "${unity_log_baseline_cursor}" \
  --level error \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_unity_log_query_empty \
  "${results_directory}/unity-errors.json" \
  "Screenshot fidelity measurement"

invoke_ucli "${results_directory}/unity-warnings.json" \
  logs unity read \
  --projectPath "${unity_project}" \
  --after "${unity_log_baseline_cursor}" \
  --level warning \
  --source all \
  --stackTrace all \
  --format json \
  --timeout 30000
assert_unity_log_query_empty \
  "${results_directory}/unity-warnings.json" \
  "Screenshot fidelity measurement"

invoke_ucli "${results_directory}/daemon-stop.json" \
  daemon stop \
  --projectPath "${unity_project}" \
  --timeout 30000
assert_command_success "${results_directory}/daemon-stop.json"
send_control "quit" "completed"
for _ in $(seq 1 100); do
  kill -0 "${unity_pid}" 2>/dev/null || break
  sleep 0.2
done
if kill -0 "${unity_pid}" 2>/dev/null; then
  fail "Unity did not exit after the fidelity fixture completed."
fi
wait "${unity_pid}" 2>/dev/null || true
controller_started=false

compiler_diagnostics_path="${results_directory}/unity-compiler-diagnostics.txt"
if grep -E '(^|[^[:alnum:]_])(warning|error) CS[0-9]{4}([^[:digit:]]|$)' \
  "${results_directory}/unity.log" > "${compiler_diagnostics_path}"; then
  fail "Unity fixture compilation emitted C# diagnostics; see ${compiler_diagnostics_path}."
fi
: > "${compiler_diagnostics_path}"

game_view_sizes_hash_after="$(if [[ -f "${game_view_sizes_path}" ]]; then shasum -a 256 "${game_view_sizes_path}" | awk '{print $1}'; else echo absent; fi)"
[[ "${game_view_sizes_hash_before}" == "${game_view_sizes_hash_after}" ]] \
  || fail "GameViewSizes.asset changed during screenshot capture. The runner did not overwrite it; the pre-run copy is ${game_view_sizes_backup}."

jq -n \
  --arg status "ok" \
  --arg size "Medium" \
  --arg gameViewSizesHashBefore "${game_view_sizes_hash_before}" \
  --arg gameViewSizesHashAfter "${game_view_sizes_hash_after}" \
  --slurpfile source "${source_provenance_path}" \
  --slurpfile macos "${results_directory}/macos-environment.json" \
  --slurpfile unity "${run_directory}/unity-environment.json" \
  --slurpfile renderIsolation "${results_directory}/fixture-render-isolation.json" \
  --slurpfile unityErrors "${results_directory}/unity-errors.json" \
  --slurpfile unityWarnings "${results_directory}/unity-warnings.json" \
  --slurpfile gameCurrent "${case_directory}/game-current/analysis.json" \
  --slurpfile gameRequested "${case_directory}/game-requested-321x197/analysis.json" \
  --slurpfile sceneCurrent "${case_directory}/scene-current/analysis.json" \
  --slurpfile sceneOverlay "${case_directory}/scene-overlay-menu-fail-closed/analysis.json" \
  --slurpfile scenePanel "${case_directory}/scene-configurable-overlay-fail-closed/analysis.json" \
  '{
    status:$status,
    size:$size,
    source:$source[0],
    environment:{macos:$macos[0],unity:$unity[0]},
    verification:{
      renderIsolation:$renderIsolation[0],
      unityDiagnostics:{
        errorCount:$unityErrors[0].payload.count,
        warningCount:$unityWarnings[0].payload.count,
        compilerDiagnosticCount:0
      }
    },
    stateRestoration:{gameViewSizesHashBefore:$gameViewSizesHashBefore,gameViewSizesHashAfter:$gameViewSizesHashAfter},
    cases:{
      gameCurrent:$gameCurrent[0],
      gameRequested321x197:$gameRequested[0],
      sceneCurrent:$sceneCurrent[0],
      sceneOverlayMenuFailClosed:$sceneOverlay[0],
      sceneConfigurableOverlayFailClosed:$scenePanel[0]
    }
  }' > "${results_directory}/fidelity-result.json"

overall_status="ok"
failure_message=""
echo "Screenshot fidelity system-test lane passed: ${results_directory}/fidelity-result.json" >&2
