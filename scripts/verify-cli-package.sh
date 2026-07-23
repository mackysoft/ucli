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
tool_packages_root="$(mktemp -d "${temp_root%/}/ucli-tool-packages.XXXXXX")"
tool_nuget_config="${tool_path}/NuGet.config"
provider_restore_project="${tool_path}/ProviderRestore.csproj"
canonicalization_package_version="0.1.0"
canonicalization_package_artifact="${package_dir}/MackySoft.Json.Canonicalization.${canonicalization_package_version}.nupkg"
canonicalization_local_mapping=""
canonicalization_remote_mapping='      <package pattern="MackySoft.Json.Canonicalization" />'
expected_canonicalization_source="https://api.nuget.org/v3/index.json"
if [[ -f "${canonicalization_package_artifact}" ]]; then
  canonicalization_local_mapping='      <package pattern="MackySoft.Json.Canonicalization" />'
  canonicalization_remote_mapping=""
  expected_canonicalization_source="${package_dir}"
fi
install_repo=""
trap 'rm -rf "${tool_path}" "${tool_packages_root}" "${install_repo}"' EXIT

cat > "${tool_nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="PackageArtifacts" value="${package_dir}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="PackageArtifacts">
      <package pattern="MackySoft.Ucli" />
${canonicalization_local_mapping}
    </packageSource>
    <packageSource key="nuget.org">
${canonicalization_remote_mapping}
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

cat > "${provider_restore_project}" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MackySoft.Json.Canonicalization" Version="[${canonicalization_package_version}]" />
  </ItemGroup>
</Project>
EOF

# Restore the provider into the same isolated cache used by the tool install.
# A local provider artifact is exact-mapped during pre-publication validation;
# after publication the fixed version is resolved from nuget.org.
NUGET_PACKAGES="${tool_packages_root}" dotnet restore "${provider_restore_project}" \
  --configfile "${tool_nuget_config}" \
  --no-cache \
  --force-evaluate

# Resolve the tool from the inspected package directory and an empty cache so
# another package with the same version cannot satisfy the smoke test.
NUGET_PACKAGES="${tool_packages_root}" dotnet tool install \
  --tool-path "${tool_path}" \
  --configfile "${tool_nuget_config}" \
  --no-http-cache \
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
canonicalization_notice_root="tools/net8.0/any/third-party/MackySoft.Json.Canonicalization/${canonicalization_package_version}"
canonicalization_notice_files=(
  "LICENSE"
  "THIRD-PARTY-NOTICES.md"
  "licenses/Apache-2.0.txt"
  "licenses/MPL-2.0.txt"
)
required_package_entries=(
  "README.md"
  "LICENSE"
  "tools/net8.0/any/DotnetToolSettings.xml"
  "tools/net8.0/any/MackySoft.Json.Canonicalization.dll"
  "tools/net8.0/any/MackySoft.Ucli.deps.json"
  "tools/net8.0/any/THIRD-PARTY-NOTICES"
  "tools/net8.0/any/schemas/v1/schema-manifest.json"
  "tools/net8.0/any/skills/bundle.json"
)
for relative_path in "${canonicalization_notice_files[@]}"; do
  required_package_entries+=("${canonicalization_notice_root}/${relative_path}")
done

for entry in "${required_package_entries[@]}"; do
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "CLI package is missing required entry: ${entry}" >&2
    exit 1
  fi
done

if grep -Fi "es6numberserializer" <<< "${package_entries}" >/dev/null; then
  echo "CLI package contains the legacy es6numberserializer package or assembly." >&2
  grep -Fi "es6numberserializer" <<< "${package_entries}" >&2
  exit 1
fi

if grep -Ei '(^|/)MackySoft[.]Json[.]Canonicalization[.][^/]+[.]nupkg$' <<< "${package_entries}" >/dev/null; then
  echo "CLI package must redistribute only the canonicalization runtime closure, not the standalone provider nupkg." >&2
  exit 1
fi

if unzip -p "${package_path}" tools/net8.0/any/MackySoft.Ucli.deps.json \
  | grep -Fi "es6numberserializer" >/dev/null; then
  echo "CLI package dependency manifest references the legacy es6numberserializer package or assembly." >&2
  exit 1
fi

canonicalization_package_root="${tool_packages_root}/mackysoft.json.canonicalization/${canonicalization_package_version}"
if [[ ! -d "${canonicalization_package_root}" ]]; then
  echo "Restored MackySoft.Json.Canonicalization package was not found: ${canonicalization_package_root}" >&2
  exit 1
fi

canonicalization_metadata_path="${canonicalization_package_root}/.nupkg.metadata"
require_python3
CANONICALIZATION_METADATA_PATH="${canonicalization_metadata_path}" \
EXPECTED_CANONICALIZATION_SOURCE="${expected_canonicalization_source}" \
python3 - <<'PY'
import json
import os
import sys

metadata_path = os.environ["CANONICALIZATION_METADATA_PATH"]
expected_source = os.environ["EXPECTED_CANONICALIZATION_SOURCE"]
if not os.path.isfile(metadata_path):
    print(
        f"Restored MackySoft.Json.Canonicalization metadata was not found: {metadata_path}",
        file=sys.stderr,
    )
    sys.exit(1)
with open(metadata_path, encoding="utf-8") as metadata_file:
    actual_source = json.load(metadata_file).get("source")
if actual_source != expected_source:
    print(
        "Restored MackySoft.Json.Canonicalization source differs from the inspected source. "
        f"Expected: {expected_source}. Actual: {actual_source}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

if [[ -f "${canonicalization_package_artifact}" ]]; then
  restored_canonicalization_package="${canonicalization_package_root}/mackysoft.json.canonicalization.${canonicalization_package_version}.nupkg"
  if ! cmp -s "${canonicalization_package_artifact}" "${restored_canonicalization_package}"; then
    echo "Restored MackySoft.Json.Canonicalization package differs from the inspected provider artifact." >&2
    exit 1
  fi
fi

canonicalization_provider_assembly="${canonicalization_package_root}/lib/netstandard2.1/MackySoft.Json.Canonicalization.dll"
if [[ ! -f "${canonicalization_provider_assembly}" ]]; then
  echo "Restored MackySoft.Json.Canonicalization package is missing its netstandard2.1 assembly." >&2
  exit 1
fi
if ! unzip -p "${package_path}" tools/net8.0/any/MackySoft.Json.Canonicalization.dll \
  | cmp -s - "${canonicalization_provider_assembly}"; then
  echo "CLI package contains a MackySoft.Json.Canonicalization assembly that differs from the restored ${canonicalization_package_version} package." >&2
  exit 1
fi

for relative_path in "${canonicalization_notice_files[@]}"; do
  provider_file="${canonicalization_package_root}/${relative_path}"
  package_entry="${canonicalization_notice_root}/${relative_path}"
  if [[ ! -f "${provider_file}" ]]; then
    echo "Restored MackySoft.Json.Canonicalization package is missing notice material: ${provider_file}" >&2
    exit 1
  fi
  if ! unzip -p "${package_path}" "${package_entry}" | cmp -s - "${provider_file}"; then
    echo "CLI package notice material differs from MackySoft.Json.Canonicalization ${canonicalization_package_version}: ${relative_path}" >&2
    exit 1
  fi
done

cli_notice="$(
  unzip -p "${package_path}" tools/net8.0/any/THIRD-PARTY-NOTICES
)"
if ! unzip -p "${package_path}" tools/net8.0/any/THIRD-PARTY-NOTICES \
  | cmp -s - "${repo_root}/src/Ucli/THIRD-PARTY-NOTICES"; then
  echo "CLI package third-party notice differs from src/Ucli/THIRD-PARTY-NOTICES." >&2
  exit 1
fi
if ! grep -F "MackySoft.Json.Canonicalization ${canonicalization_package_version}" <<< "${cli_notice}" >/dev/null \
  || ! grep -F "third-party/MackySoft.Json.Canonicalization/${canonicalization_package_version}/" <<< "${cli_notice}" >/dev/null; then
  echo "CLI package third-party notice does not identify the redistributed provider and its bundled notice directory." >&2
  exit 1
fi

if find "${tool_path}" -type f -iname '*es6numberserializer*' -print -quit | grep -q .; then
  echo "Installed CLI tool contains the legacy es6numberserializer package or assembly." >&2
  find "${tool_path}" -type f -iname '*es6numberserializer*' -print >&2
  exit 1
fi

installed_canonicalization_assembly_count="$(
  find "${tool_path}" -path '*/tools/net8.0/any/MackySoft.Json.Canonicalization.dll' -type f \
    | wc -l \
    | tr -d '[:space:]'
)"
if [[ "${installed_canonicalization_assembly_count}" != "1" ]]; then
  echo "Installed CLI tool must contain exactly one MackySoft.Json.Canonicalization.dll; found ${installed_canonicalization_assembly_count}." >&2
  exit 1
fi
installed_canonicalization_assembly="$(
  find "${tool_path}" -path '*/tools/net8.0/any/MackySoft.Json.Canonicalization.dll' -type f -print -quit
)"
if ! cmp -s "${canonicalization_provider_assembly}" "${installed_canonicalization_assembly}"; then
  echo "Installed CLI tool canonicalization assembly differs from the restored ${canonicalization_package_version} package." >&2
  exit 1
fi

while IFS= read -r dependency_manifest; do
  if grep -Fi "es6numberserializer" "${dependency_manifest}" >/dev/null; then
    echo "Installed CLI tool dependency manifest references the legacy es6numberserializer package or assembly: ${dependency_manifest}" >&2
    exit 1
  fi
done < <(find "${tool_path}" -type f -name '*.deps.json' -print)

installed_cli_notice_count="$(
  find "${tool_path}" -path '*/tools/net8.0/any/THIRD-PARTY-NOTICES' -type f | wc -l | tr -d '[:space:]'
)"
if [[ "${installed_cli_notice_count}" != "1" ]]; then
  echo "Installed CLI tool must contain exactly one top-level THIRD-PARTY-NOTICES file; found ${installed_cli_notice_count}." >&2
  exit 1
fi
installed_cli_notice="$(
  find "${tool_path}" -path '*/tools/net8.0/any/THIRD-PARTY-NOTICES' -type f -print -quit
)"
if ! cmp -s "${repo_root}/src/Ucli/THIRD-PARTY-NOTICES" "${installed_cli_notice}"; then
  echo "Installed CLI tool third-party notice differs from src/Ucli/THIRD-PARTY-NOTICES." >&2
  exit 1
fi

for relative_path in "${canonicalization_notice_files[@]}"; do
  installed_file_count="$(
    find "${tool_path}" \
      -path "*/third-party/MackySoft.Json.Canonicalization/${canonicalization_package_version}/${relative_path}" \
      -type f \
      | wc -l \
      | tr -d '[:space:]'
  )"
  if [[ "${installed_file_count}" != "1" ]]; then
    echo "Installed CLI tool must contain exactly one provider notice file ${relative_path}; found ${installed_file_count}." >&2
    exit 1
  fi
  installed_file="$(
    find "${tool_path}" \
      -path "*/third-party/MackySoft.Json.Canonicalization/${canonicalization_package_version}/${relative_path}" \
      -type f \
      -print \
      -quit
  )"
  if [[ -z "${installed_file}" ]]; then
    echo "Installed CLI tool is missing provider notice material: ${relative_path}" >&2
    exit 1
  fi
  if ! cmp -s "${canonicalization_package_root}/${relative_path}" "${installed_file}"; then
    echo "Installed CLI tool notice material differs from MackySoft.Json.Canonicalization ${canonicalization_package_version}: ${relative_path}" >&2
    exit 1
  fi
done

package_schema_manifest_path="${tool_path}/package-schema-manifest.json"
unzip -p "${package_path}" tools/net8.0/any/schemas/v1/schema-manifest.json > "${package_schema_manifest_path}"
assert_json_manifest_package_version "${package_schema_manifest_path}" "${expected_version}" "CLI package schema manifest"

generated_skills_root="${repo_root}/skills/generated"
expected_bundle_descriptor_path="${generated_skills_root}/bundle.json"
package_bundle_descriptor_path="${tool_path}/package-skills-bundle.json"
unzip -p "${package_path}" tools/net8.0/any/skills/bundle.json > "${package_bundle_descriptor_path}"
if ! cmp -s "${expected_bundle_descriptor_path}" "${package_bundle_descriptor_path}"; then
  diff -u "${expected_bundle_descriptor_path}" "${package_bundle_descriptor_path}" || true
  echo "CLI package Agent Skills bundle descriptor differs from skills/generated/bundle.json." >&2
  exit 1
fi

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

EXPECTED_BUNDLE_DESCRIPTOR_PATH="${expected_bundle_descriptor_path}" SKILLS_LIST_JSON="${skills_list}" python3 - <<'PY'
import json
import os
import sys

root = json.loads(os.environ["SKILLS_LIST_JSON"])
payload = root.get("payload") or {}
with open(os.environ["EXPECTED_BUNDLE_DESCRIPTOR_PATH"], encoding="utf-8") as descriptor_file:
    expected_bundle_version = json.load(descriptor_file)["skillBundleVersion"]

skill_names = payload.get("skillNames")
if skill_names != []:
    print(
        f"ucli skills list did not report an empty skillNames selection. Actual: {skill_names}",
        file=sys.stderr,
    )
    sys.exit(1)
actual = [
    (category.get("category"), category.get("skillCount"))
    for category in payload.get("availableCategories", [])
]
expected = [
    ("basic", 4),
]
if actual != expected:
    print(
        f"ucli skills list did not report expected availableCategories. Expected: {expected}. Actual: {actual}",
        file=sys.stderr,
    )
    sys.exit(1)

unexpected_bundle_versions = [
    (skill.get("skillName"), skill.get("skillBundleVersion"))
    for skill in payload.get("skills", [])
    if skill.get("skillBundleVersion") != expected_bundle_version
]
if unexpected_bundle_versions:
    print(
        "ucli skills list reported skillBundleVersion values that differ from "
        f"skills/generated/bundle.json. Expected: {expected_bundle_version}. "
        f"Actual mismatches: {unexpected_bundle_versions}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

single_skill_list="$("${tool_path}/ucli" skills list --skill ucli-read-project)"
EXPECTED_BUNDLE_DESCRIPTOR_PATH="${expected_bundle_descriptor_path}" SINGLE_SKILL_LIST_JSON="${single_skill_list}" python3 - <<'PY'
import json
import os
import sys

root = json.loads(os.environ["SINGLE_SKILL_LIST_JSON"])
payload = root.get("payload") or {}
with open(os.environ["EXPECTED_BUNDLE_DESCRIPTOR_PATH"], encoding="utf-8") as descriptor_file:
    expected_bundle_version = json.load(descriptor_file)["skillBundleVersion"]

skill_names = payload.get("skillNames")
skills = [
    (skill.get("skillName"), skill.get("skillBundleVersion"))
    for skill in payload.get("skills", [])
]
expected_skills = [("ucli-read-project", expected_bundle_version)]
if skill_names != ["ucli-read-project"] or skills != expected_skills:
    print(
        "ucli skills list --skill did not select only ucli-read-project with the "
        f"generated bundle version. skillNames={skill_names}; skills={skills}; "
        f"expectedSkills={expected_skills}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

export_path="${tool_path}/exported-skills"
"${tool_path}/ucli" skills export --host openai --category basic --output "${export_path}" >/dev/null
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
"${tool_path}/ucli" skills install --host openai --category basic --scope project --repoRoot "${install_repo}" >/dev/null
"${tool_path}/ucli" skills install --host openai --category basic --scope project --repoRoot "${install_repo}" >/dev/null
"${tool_path}/ucli" skills doctor --host openai --category basic --scope project --repoRoot "${install_repo}" >/dev/null
installed_path="${install_repo}/.agents/skills"
if [[ ! -d "${installed_path}" ]]; then
  echo "ucli skills install did not create the project skills directory: ${installed_path}" >&2
  exit 1
fi

if ! diff -ruN "${export_path}" "${installed_path}"; then
  echo "ucli skills install output differs from ucli skills export output for the same host." >&2
  exit 1
fi
