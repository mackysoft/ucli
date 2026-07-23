#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  echo "Usage: $0 <package-dir> <expected-version> [--filesystem-package-source <dir>]" >&2
}

if [[ "$#" -lt 2 ]]; then
  print_usage
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/schema-artifact-common.sh
source "${script_dir}/schema-artifact-common.sh"
repo_root="$(cd "${script_dir}/.." && pwd)"
package_dir="$1"
expected_version="$2"
filesystem_package_source="${FILESYSTEM_PACKAGE_SOURCE:-}"
shift 2

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --filesystem-package-source)
      if [[ "$#" -lt 2 ]]; then
        print_usage
        exit 2
      fi
      filesystem_package_source="$2"
      shift 2
      ;;
    --filesystem-package-source=*)
      filesystem_package_source="${1#--filesystem-package-source=}"
      shift
      ;;
    *)
      print_usage
      exit 2
      ;;
  esac
done

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

filesystem_package_id="MackySoft.FileSystem"
filesystem_package_version="0.1.0"
filesystem_package_file_name="${filesystem_package_id}.${filesystem_package_version}.nupkg"
if [[ -n "${filesystem_package_source}" ]]; then
  if [[ ! -d "${filesystem_package_source}" ]]; then
    echo "Filesystem package source does not exist: ${filesystem_package_source}" >&2
    exit 1
  fi

  filesystem_package_source="$(cd "${filesystem_package_source}" && pwd)"
  if [[ ! -f "${filesystem_package_source}/${filesystem_package_file_name}" ]]; then
    echo "Filesystem package source is missing ${filesystem_package_file_name}: ${filesystem_package_source}" >&2
    exit 1
  fi
fi

for required_tool in cmp dotnet unzip; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done
require_python3

temp_root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
tool_path="$(mktemp -d "${temp_root%/}/ucli-tool.XXXXXX")"
verification_root="$(mktemp -d "${temp_root%/}/ucli-tool-verification.XXXXXX")"
tool_packages_root="$(mktemp -d "${temp_root%/}/ucli-tool-packages.XXXXXX")"
tool_http_cache="$(mktemp -d "${temp_root%/}/ucli-tool-http-cache.XXXXXX")"
tool_dotnet_home="$(mktemp -d "${temp_root%/}/ucli-tool-dotnet-home.XXXXXX")"
tool_package_source="${verification_root}/tool-source"
isolated_filesystem_package_source="${verification_root}/filesystem-source"
tool_nuget_config="${verification_root}/NuGet.config"
provider_restore_project="${verification_root}/ProviderRestore.csproj"
publish_path="${verification_root}/publish"
install_repo=""
source_build_directories=(
  "${repo_root}/src/Ucli.Application"
  "${repo_root}/src/Ucli.Contracts"
  "${repo_root}/src/Ucli.Infrastructure"
  "${repo_root}/src/Ucli"
)

cleanup() {
  rm -rf \
    "${tool_path}" \
    "${verification_root}" \
    "${tool_packages_root}" \
    "${tool_http_cache}" \
    "${tool_dotnet_home}"
  if [[ -n "${install_repo}" ]]; then
    rm -rf "${install_repo}"
  fi
  for project_directory in "${source_build_directories[@]}"; do
    rm -rf "${project_directory}/bin" "${project_directory}/obj"
  done
}

trap cleanup EXIT

mkdir -p "${tool_package_source}"
cp "${package_path}" "${tool_package_source}/"

filesystem_source_entry=""
filesystem_source_mapping=""
public_filesystem_mapping='<package pattern="MackySoft.FileSystem" />'
if [[ -n "${filesystem_package_source}" ]]; then
  mkdir -p "${isolated_filesystem_package_source}"
  cp "${filesystem_package_source}/${filesystem_package_file_name}" \
    "${isolated_filesystem_package_source}/${filesystem_package_file_name}"
  filesystem_source_entry='<add key="FileSystemCandidate" value="./filesystem-source" />'
  filesystem_source_mapping='
    <packageSource key="FileSystemCandidate">
      <package pattern="MackySoft.FileSystem" />
    </packageSource>'
  public_filesystem_mapping=""
fi

cat > "${tool_nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    ${filesystem_source_entry}
    <add key="ToolPackage" value="./tool-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>${filesystem_source_mapping}
    <packageSource key="ToolPackage">
      <package pattern="MackySoft.Ucli" />
    </packageSource>
    <packageSource key="nuget.org">
      ${public_filesystem_mapping}
      <package pattern="ConsoleAppFramework*" />
      <package pattern="MackySoft.AgentSkills*" />
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="Newtonsoft.Json" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
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
    <PackageReference Include="${filesystem_package_id}" Version="[${filesystem_package_version}]" />
  </ItemGroup>
</Project>
EOF

for project_directory in "${source_build_directories[@]}"; do
  rm -rf "${project_directory}/bin" "${project_directory}/obj"
done

export DOTNET_CLI_HOME="${tool_dotnet_home}"
export NUGET_HTTP_CACHE_PATH="${tool_http_cache}"
export NUGET_PACKAGES="${tool_packages_root}"

dotnet restore "${provider_restore_project}" \
  --configfile "${tool_nuget_config}" \
  --no-cache \
  --force-evaluate \
  --verbosity minimal
dotnet restore "${repo_root}/src/Ucli/Ucli.csproj" \
  --configfile "${tool_nuget_config}" \
  --no-cache \
  --force-evaluate \
  --verbosity minimal
dotnet publish "${repo_root}/src/Ucli/Ucli.csproj" \
  --configuration Release \
  --no-restore \
  --output "${publish_path}" \
  -p:Version="${expected_version}" \
  -p:PackageVersion="${expected_version}" \
  --verbosity minimal

filesystem_package_root="${tool_packages_root}/mackysoft.filesystem/${filesystem_package_version}"
restored_filesystem_package="${filesystem_package_root}/mackysoft.filesystem.${filesystem_package_version}.nupkg"
restored_filesystem_metadata="${filesystem_package_root}/.nupkg.metadata"
filesystem_provider_assembly="${filesystem_package_root}/lib/net8.0/${filesystem_package_id}.dll"
filesystem_provider_license="${filesystem_package_root}/LICENSE"
for restored_entry in \
  "${restored_filesystem_metadata}" \
  "${filesystem_provider_assembly}" \
  "${filesystem_provider_license}"; do
  if [[ ! -f "${restored_entry}" ]]; then
    echo "Restored filesystem package is missing required entry: ${restored_entry}" >&2
    exit 1
  fi
done

if [[ -n "${filesystem_package_source}" ]]; then
  if [[ ! -f "${restored_filesystem_package}" ]] \
    || ! cmp -s \
      "${filesystem_package_source}/${filesystem_package_file_name}" \
      "${restored_filesystem_package}"; then
    echo "Restored filesystem package does not match the supplied prepublication package." >&2
    exit 1
  fi

  FILESYSTEM_METADATA_PATH="${restored_filesystem_metadata}" \
  EXPECTED_FILESYSTEM_SOURCE="${isolated_filesystem_package_source}" \
    python3 - <<'PY'
import json
import os
import sys

metadata_path = os.environ["FILESYSTEM_METADATA_PATH"]
expected_source = os.path.normcase(os.path.realpath(os.environ["EXPECTED_FILESYSTEM_SOURCE"]))
with open(metadata_path, encoding="utf-8") as metadata_file:
    actual_source_value = json.load(metadata_file).get("source")

if not isinstance(actual_source_value, str):
    print(
        "Restored MackySoft.FileSystem metadata does not contain a source string.",
        file=sys.stderr,
    )
    sys.exit(1)

actual_source = os.path.normcase(os.path.realpath(actual_source_value))
if actual_source != expected_source:
    print(
        "Restored MackySoft.FileSystem source differs from the isolated candidate source. "
        f"Expected: {expected_source}. Actual: {actual_source}",
        file=sys.stderr,
    )
    sys.exit(1)
PY
fi

FILESYSTEM_PROJECT_ASSETS="${repo_root}/src/Ucli/obj/project.assets.json" \
FILESYSTEM_PACKAGE_ROOT="${filesystem_package_root}" \
  python3 - <<'PY'
import json
import os
import sys

assets_path = os.environ["FILESYSTEM_PROJECT_ASSETS"]
package_root = os.environ["FILESYSTEM_PACKAGE_ROOT"]
with open(assets_path, encoding="utf-8") as assets_file:
    library = json.load(assets_file).get("libraries", {}).get("MackySoft.FileSystem/0.1.0")
if not isinstance(library, dict):
    print(
        f"CLI project assets do not contain MackySoft.FileSystem/0.1.0: {assets_path}",
        file=sys.stderr,
    )
    sys.exit(1)

with open(
    os.path.join(package_root, "mackysoft.filesystem.0.1.0.nupkg.sha512"),
    encoding="utf-8",
) as hash_file:
    expected_hash = hash_file.read().strip()
actual_hash = library.get("sha512")
if actual_hash != expected_hash:
    print(
        "CLI project assets content hash differs from the restored MackySoft.FileSystem package. "
        f"Expected: {expected_hash}. Actual: {actual_hash}",
        file=sys.stderr,
    )
    sys.exit(1)
PY

published_filesystem_assembly="${publish_path}/${filesystem_package_id}.dll"
published_filesystem_license="${publish_path}/third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE"
if [[ ! -f "${published_filesystem_assembly}" ]] \
  || ! cmp -s "${filesystem_provider_assembly}" "${published_filesystem_assembly}"; then
  echo "CLI publish contains a filesystem assembly that differs from the restored package." >&2
  exit 1
fi
if [[ ! -f "${published_filesystem_license}" ]] \
  || ! cmp -s "${filesystem_provider_license}" "${published_filesystem_license}"; then
  echo "CLI publish filesystem license differs from the restored package." >&2
  exit 1
fi
if find "${publish_path}" -type f -iname "${filesystem_package_id}.*.nupkg" -print -quit | grep -q .; then
  echo "CLI publish contains the standalone filesystem provider nupkg." >&2
  exit 1
fi

dotnet tool install \
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
filesystem_license_entry="tools/net8.0/any/third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE"
for entry in \
  README.md \
  LICENSE \
  tools/net8.0/any/DotnetToolSettings.xml \
  "tools/net8.0/any/${filesystem_package_id}.dll" \
  tools/net8.0/any/THIRD-PARTY-NOTICES \
  "${filesystem_license_entry}" \
  tools/net8.0/any/schemas/v1/schema-manifest.json \
  tools/net8.0/any/skills/bundle.json; do
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "CLI package is missing required entry: ${entry}" >&2
    exit 1
  fi
done

if grep -Ei '(^|/)MackySoft[.]FileSystem[.][^/]+[.]nupkg$' <<< "${package_entries}" >/dev/null; then
  echo "CLI package must redistribute only the filesystem runtime closure, not the standalone provider nupkg." >&2
  exit 1
fi

if ! unzip -p "${package_path}" "tools/net8.0/any/${filesystem_package_id}.dll" \
  | cmp -s - "${filesystem_provider_assembly}"; then
  echo "CLI package filesystem assembly differs from the restored ${filesystem_package_version} package." >&2
  exit 1
fi
if ! unzip -p "${package_path}" "${filesystem_license_entry}" \
  | cmp -s - "${filesystem_provider_license}"; then
  echo "CLI package filesystem license differs from the restored ${filesystem_package_version} package." >&2
  exit 1
fi

cli_notice="$(
  unzip -p "${package_path}" tools/net8.0/any/THIRD-PARTY-NOTICES
)"
if ! unzip -p "${package_path}" tools/net8.0/any/THIRD-PARTY-NOTICES \
  | cmp -s - "${repo_root}/src/Ucli/THIRD-PARTY-NOTICES"; then
  echo "CLI package third-party notice differs from src/Ucli/THIRD-PARTY-NOTICES." >&2
  exit 1
fi
if ! grep -F "${filesystem_package_id} ${filesystem_package_version}" <<< "${cli_notice}" >/dev/null \
  || ! grep -F "third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE" <<< "${cli_notice}" >/dev/null; then
  echo "CLI package third-party notice does not identify the redistributed filesystem provider license." >&2
  exit 1
fi

installed_filesystem_assembly_count="$(
  find "${tool_path}" -path "*/tools/net8.0/any/${filesystem_package_id}.dll" -type f \
    | wc -l \
    | tr -d '[:space:]'
)"
if [[ "${installed_filesystem_assembly_count}" != "1" ]]; then
  echo "Installed CLI tool must contain exactly one ${filesystem_package_id}.dll; found ${installed_filesystem_assembly_count}." >&2
  exit 1
fi
installed_filesystem_assembly="$(
  find "${tool_path}" -path "*/tools/net8.0/any/${filesystem_package_id}.dll" -type f -print -quit
)"
if ! cmp -s "${filesystem_provider_assembly}" "${installed_filesystem_assembly}"; then
  echo "Installed CLI tool filesystem assembly differs from the restored ${filesystem_package_version} package." >&2
  exit 1
fi

installed_filesystem_license_count="$(
  find "${tool_path}" \
    -path "*/third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE" \
    -type f \
    | wc -l \
    | tr -d '[:space:]'
)"
if [[ "${installed_filesystem_license_count}" != "1" ]]; then
  echo "Installed CLI tool must contain exactly one filesystem provider license; found ${installed_filesystem_license_count}." >&2
  exit 1
fi
installed_filesystem_license="$(
  find "${tool_path}" \
    -path "*/third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE" \
    -type f \
    -print \
    -quit
)"
if ! cmp -s "${filesystem_provider_license}" "${installed_filesystem_license}"; then
  echo "Installed CLI tool filesystem license differs from the restored ${filesystem_package_version} package." >&2
  exit 1
fi

if find "${tool_path}" -type f -iname "${filesystem_package_id}.*.nupkg" -print -quit | grep -q .; then
  echo "Installed CLI tool contains the standalone filesystem provider nupkg." >&2
  exit 1
fi

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

require_python3
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
