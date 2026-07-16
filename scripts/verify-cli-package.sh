#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <package-dir> <expected-version>" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/schema-artifact-common.sh
source "${script_dir}/schema-artifact-common.sh"
repo_root="$(cd "${script_dir}/.." && pwd)"
package_dir="$1"
expected_version="$2"

if [[ ! -d "${package_dir}" ]]; then
  echo "CLI package directory does not exist: ${package_dir}" >&2
  exit 1
fi

package_dir="$(cd "${package_dir}" && pwd)"
# Use the exact expected package file so a stale package in the directory cannot
# satisfy the smoke test by accident.
package_path="${package_dir}/MackySoft.Ucli.${expected_version}.nupkg"
if [[ ! -f "${package_path}" ]]; then
  echo "CLI package was not created: ${package_path}" >&2
  exit 1
fi

temp_root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
tool_path="$(mktemp -d "${temp_root%/}/ucli-tool.XXXXXX")"
install_repo=""
trap 'rm -rf "${tool_path}" "${install_repo}"' EXIT

# The package directory must be available for the just-built CLI nupkg, while
# configured feeds remain available for runtime package dependencies.
dotnet tool install \
  --tool-path "${tool_path}" \
  --add-source "${package_dir}" \
  MackySoft.Ucli \
  --version "${expected_version}"

actual_version="$("${tool_path}/ucli" --version)"
if [[ "${actual_version}" != "${expected_version}" ]]; then
  echo "Unexpected ucli --version. Expected: ${expected_version}. Actual: ${actual_version}" >&2
  exit 1
fi

if ! "${tool_path}/ucli" --help | grep -F "Commands:" >/dev/null; then
  echo "ucli --help did not include the public command list." >&2
  exit 1
fi

if ! "${tool_path}/ucli" query --help | grep -F "asset schema" >/dev/null; then
  echo "ucli query --help did not include the query subcommands." >&2
  exit 1
fi

package_entries="$(unzip -Z1 "${package_path}")"
for entry in README.md LICENSE tools/net8.0/any/DotnetToolSettings.xml tools/net8.0/any/schemas/v1/schema-manifest.json; do
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "CLI package is missing required entry: ${entry}" >&2
    exit 1
  fi
done

package_schema_manifest_path="${tool_path}/package-schema-manifest.json"
unzip -p "${package_path}" tools/net8.0/any/schemas/v1/schema-manifest.json > "${package_schema_manifest_path}"
assert_json_manifest_package_version "${package_schema_manifest_path}" "${expected_version}" "CLI package schema manifest"

generated_skills_root="${repo_root}/skills/generated"
while IFS= read -r skill_file; do
  relative_path="${skill_file#"${generated_skills_root}/"}"
  entry="tools/net8.0/any/skills/${relative_path}"
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "CLI package is missing required generated SKILL entry: ${entry}" >&2
    exit 1
  fi
done < <(find "${generated_skills_root}" -type f | sort)

while IFS= read -r schema_file; do
  relative_path="${schema_file#"${repo_root}/"}"
  entry="tools/net8.0/any/${relative_path}"
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "CLI package is missing required schema entry: ${entry}" >&2
    exit 1
  fi
done < <(find "${repo_root}/schemas" -type f | sort)

installed_schema_manifest="$(find "${tool_path}" -path "*/schemas/v1/schema-manifest.json" -type f | head -n 1)"
if [[ -z "${installed_schema_manifest}" ]]; then
  echo "Installed CLI tool package did not materialize schemas/v1/schema-manifest.json." >&2
  exit 1
fi

assert_json_manifest_package_version "${installed_schema_manifest}" "${expected_version}" "Installed CLI tool schema manifest"

list_host_independent_skill_files() {
  local relative_path

  while IFS= read -r skill_file; do
    relative_path="${skill_file#"${generated_skills_root}/"}"
    case "${relative_path}" in
      */SKILL.md|*/agent-skill.json|*/references/*)
        printf '%s\n' "${relative_path}"
        ;;
    esac
  done < <(find "${generated_skills_root}" -type f | sort)
}

skills_list="$("${tool_path}/ucli" skills list)"
if ! grep -F '"command": "skills.list"' <<< "${skills_list}" >/dev/null; then
  echo "ucli skills list did not report the skills.list command." >&2
  exit 1
fi

if ! grep -F '"skillName": "ucli-plan-apply"' <<< "${skills_list}" >/dev/null; then
  echo "ucli skills list did not include bundled official SKILL packages." >&2
  exit 1
fi

require_python3
SKILLS_LIST_JSON="${skills_list}" python3 - <<'PY'
import json
import os
import sys

root = json.loads(os.environ["SKILLS_LIST_JSON"])
payload = root.get("payload") or {}
skill_names = payload.get("skillNames")
if skill_names != []:
    print(
        f"ucli skills list did not report an empty skillNames selection. Actual: {skill_names}",
        file=sys.stderr,
    )
    sys.exit(1)
actual = [
    (tier.get("tier"), tier.get("skillCount"))
    for tier in payload.get("availableTiers", [])
]
expected = [
    ("basic", 4),
    ("advanced", 0),
    ("developer", 0),
]
if actual != expected:
    print(
        f"ucli skills list did not report expected availableTiers. Expected: {expected}. Actual: {actual}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

single_skill_list="$("${tool_path}/ucli" skills list --skill ucli-read-project)"
SINGLE_SKILL_LIST_JSON="${single_skill_list}" python3 - <<'PY'
import json
import os
import sys

root = json.loads(os.environ["SINGLE_SKILL_LIST_JSON"])
payload = root.get("payload") or {}
skill_names = payload.get("skillNames")
skills = [
    skill.get("skillName")
    for skill in payload.get("skills", [])
]
if skill_names != ["ucli-read-project"] or skills != ["ucli-read-project"]:
    print(
        f"ucli skills list --skill did not select only ucli-read-project. skillNames={skill_names}; skills={skills}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

export_path="${tool_path}/exported-skills"
"${tool_path}/ucli" skills export --host openai --tier basic --output "${export_path}" >/dev/null
while IFS= read -r relative_path; do
  exported_file="${export_path}/${relative_path}"
  if [[ ! -f "${exported_file}" ]]; then
    echo "ucli skills export did not materialize required SKILL content file: ${exported_file}" >&2
    exit 1
  fi
done < <(list_host_independent_skill_files)

single_export_path="${tool_path}/exported-single-skill"
"${tool_path}/ucli" skills export --host openai --skill ucli-read-project --output "${single_export_path}" >/dev/null
if [[ ! -f "${single_export_path}/ucli-read-project/SKILL.md" ]]; then
  echo "ucli skills export --skill did not materialize the selected SKILL." >&2
  exit 1
fi
if [[ -e "${single_export_path}/ucli-plan-apply" ]]; then
  echo "ucli skills export --skill materialized an unselected SKILL." >&2
  exit 1
fi

install_repo="$(mktemp -d "${temp_root%/}/ucli-skills-install.XXXXXX")"
"${tool_path}/ucli" skills install --host openai --tier basic --scope project --repoRoot "${install_repo}" >/dev/null
"${tool_path}/ucli" skills install --host openai --tier basic --scope project --repoRoot "${install_repo}" >/dev/null
"${tool_path}/ucli" skills doctor --host openai --tier basic --scope project --repoRoot "${install_repo}" >/dev/null
installed_path="${install_repo}/.agents/skills"
if [[ ! -d "${installed_path}" ]]; then
  echo "ucli skills install did not create the project skills directory: ${installed_path}" >&2
  exit 1
fi

if ! diff -ruN "${export_path}" "${installed_path}"; then
  echo "ucli skills install output differs from ucli skills export output for the same host." >&2
  exit 1
fi
