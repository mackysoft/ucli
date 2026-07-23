#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  echo "Usage: $0 <package-dir> <expected-version> [--filesystem-package-source <dir>]" >&2
}

if [[ "$#" -lt 2 ]]; then
  print_usage
  exit 2
fi

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
  echo "Unity package directory does not exist: ${package_dir}" >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "${script_dir}/.." && pwd)"
package_dir="$(cd "${package_dir}" && pwd)"
package_id="MackySoft.Ucli.Unity"
package_path="${package_dir}/${package_id}.${expected_version}.nupkg"
nuspec_entry="${package_id}.nuspec"
unity_packages_config="${repository_root}/src/Ucli.Unity/Assets/packages.config"
unity_editor_asmdef_entry="Editor/MackySoft.Ucli.Unity.Editor.asmdef"
filesystem_package_id="MackySoft.FileSystem"
filesystem_package_version="0.1.0"
filesystem_package_file_name="${filesystem_package_id}.${filesystem_package_version}.nupkg"
ucli_dependency_package_ids=(
  "MackySoft.Ucli.Contracts"
  "MackySoft.Ucli.Infrastructure"
)
ucli_dependency_package_versions=()

if [[ ! -f "${package_path}" ]]; then
  echo "Unity package was not created: ${package_path}" >&2
  exit 1
fi

for required_tool in cmp dotnet jq nuget python3 unzip; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done

if [[ ! -f "${unity_packages_config}" ]]; then
  echo "Unity packages.config does not exist: ${unity_packages_config}" >&2
  exit 1
fi

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

for dependency_package_id in "${ucli_dependency_package_ids[@]}"; do
  dependency_package_version="$(
    sed -nE "s#.*<package id=\"${dependency_package_id}\" version=\"([^\"]+)\".*#\\1#p" \
      "${unity_packages_config}" |
      head -n 1
  )"
  if [[ -z "${dependency_package_version}" ]]; then
    echo "Unity packages.config is missing ${dependency_package_id}." >&2
    exit 1
  fi

  ucli_dependency_package_versions+=("${dependency_package_version}")
  dependency_package_path="${package_dir}/${dependency_package_id}.${dependency_package_version}.nupkg"
  if [[ ! -f "${dependency_package_path}" ]]; then
    echo "Unity dependency package was not created: ${dependency_package_path}" >&2
    exit 1
  fi
done

package_entries="$(unzip -Z1 "${package_path}")"
required_entries=(
  "${nuspec_entry}"
  "ucli-plugin.json"
  "${unity_editor_asmdef_entry}"
  "Editor/csc.rsp"
  "Editor/csc.rsp.meta"
  "Editor/AssemblyInfo.cs"
  "Editor/Ipc/Bootstrap/UnityDaemonBootstrap.cs"
  "Editor/Execution/UnityExecutionServiceCollectionExtensions.cs"
  "README.md"
  "LICENSE"
)

for entry in "${required_entries[@]}"; do
  if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
    echo "Unity package is missing required entry: ${entry}" >&2
    exit 1
  fi
done

for forbidden_pattern in \
  '^Assets/' \
  '^Tests/' \
  '^ProjectSettings/' \
  '^Packages/' \
  '^.*\.unitypackage$' \
  '^package\.json$' \
  '(^|/)MackySoft\.FileSystem\.dll$' \
  '(^|/)MackySoft\.FileSystem\.[^/]*\.nupkg$'; do
  if grep -Ei "${forbidden_pattern}" <<< "${package_entries}" >/dev/null; then
    echo "Unity package contains forbidden entry matching ${forbidden_pattern}." >&2
    grep -Ei "${forbidden_pattern}" <<< "${package_entries}" >&2
    exit 1
  fi
done

temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT
nuspec_path="${temp_dir}/${nuspec_entry}"
unzip -p "${package_path}" "${nuspec_entry}" > "${nuspec_path}"

if ! grep -F "<id>${package_id}</id>" "${nuspec_path}" >/dev/null; then
  echo "Unity package nuspec has an unexpected package id." >&2
  exit 1
fi

if ! grep -F "<version>${expected_version}</version>" "${nuspec_path}" >/dev/null; then
  echo "Unity package nuspec has an unexpected version." >&2
  exit 1
fi

while IFS=$'\t' read -r dependency_id dependency_version; do
  [[ -n "${dependency_id}" ]] || continue
  if [[ "${dependency_id}" == "MackySoft.FileSystem" ]]; then
    dependency_version="[${dependency_version}]"
  fi
  if ! grep -F "<dependency id=\"${dependency_id}\" version=\"${dependency_version}\" />" "${nuspec_path}" >/dev/null; then
    echo "Unity package nuspec is missing dependency ${dependency_id} ${dependency_version}." >&2
    exit 1
  fi
done < <(
  sed -nE 's#.*<package id="([^"]+)" version="([^"]+)".*#\1\t\2#p' "${unity_packages_config}"
)

unity_package_source="${temp_dir}/unity-source"
ucli_package_source="${temp_dir}/ucli-source"
isolated_filesystem_package_source="${temp_dir}/filesystem-source"
isolated_nuget_packages="${temp_dir}/global-packages"
isolated_nuget_http_cache="${temp_dir}/http-cache"
isolated_dotnet_home="${temp_dir}/dotnet-home"
nuget_config="${temp_dir}/NuGet.config"
filesystem_restore_project="${temp_dir}/FileSystemRestore.csproj"
restore_root="${temp_dir}/UnityProject"
restore_packages_directory="${restore_root}/Assets/Packages"
mkdir -p \
  "${unity_package_source}" \
  "${ucli_package_source}" \
  "${isolated_dotnet_home}" \
  "${isolated_nuget_http_cache}" \
  "${isolated_nuget_packages}" \
  "${restore_packages_directory}"
cp "${package_path}" "${unity_package_source}/"
for dependency_index in "${!ucli_dependency_package_ids[@]}"; do
  dependency_package_id="${ucli_dependency_package_ids[${dependency_index}]}"
  dependency_package_version="${ucli_dependency_package_versions[${dependency_index}]}"
  cp "${package_dir}/${dependency_package_id}.${dependency_package_version}.nupkg" \
    "${ucli_package_source}/"
done

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

cat > "${nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    ${filesystem_source_entry}
    <add key="UnityPackage" value="./unity-source" />
    <add key="UcliPackages" value="./ucli-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>${filesystem_source_mapping}
    <packageSource key="UnityPackage">
      <package pattern="${package_id}" />
    </packageSource>
    <packageSource key="UcliPackages">
      <package pattern="MackySoft.Ucli.Contracts" />
      <package pattern="MackySoft.Ucli.Infrastructure" />
    </packageSource>
    <packageSource key="nuget.org">
      ${public_filesystem_mapping}
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

if [[ -n "${filesystem_package_source}" ]]; then
  cat > "${filesystem_restore_project}" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="${filesystem_package_id}" Version="[${filesystem_package_version}]" />
  </ItemGroup>
</Project>
EOF

  DOTNET_CLI_HOME="${isolated_dotnet_home}" \
    NUGET_HTTP_CACHE_PATH="${isolated_nuget_http_cache}" \
    NUGET_PACKAGES="${isolated_nuget_packages}" \
    dotnet restore "${filesystem_restore_project}" \
    --configfile "${nuget_config}" \
    --no-cache \
    --force-evaluate \
    --verbosity minimal

  restored_global_filesystem_root="${isolated_nuget_packages}/mackysoft.filesystem/${filesystem_package_version}"
  restored_global_filesystem_package="${restored_global_filesystem_root}/mackysoft.filesystem.${filesystem_package_version}.nupkg"
  restored_global_filesystem_metadata="${restored_global_filesystem_root}/.nupkg.metadata"
  if [[ ! -f "${restored_global_filesystem_package}" ]] \
    || ! cmp -s \
      "${filesystem_package_source}/${filesystem_package_file_name}" \
      "${restored_global_filesystem_package}"; then
    echo "Restored global filesystem package does not match the supplied prepublication package." >&2
    exit 1
  fi

  FILESYSTEM_METADATA_PATH="${restored_global_filesystem_metadata}" \
  EXPECTED_FILESYSTEM_SOURCE="${isolated_filesystem_package_source}" \
    python3 - <<'PY'
import json
import os
import sys

metadata_path = os.environ["FILESYSTEM_METADATA_PATH"]
expected_source = os.path.normcase(os.path.realpath(os.environ["EXPECTED_FILESYSTEM_SOURCE"]))
if not os.path.isfile(metadata_path):
    print(
        f"Restored MackySoft.FileSystem metadata was not found: {metadata_path}",
        file=sys.stderr,
    )
    sys.exit(1)

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

NUGET_HTTP_CACHE_PATH="${isolated_nuget_http_cache}" \
  NUGET_PACKAGES="${isolated_nuget_packages}" \
  nuget install "${package_id}" \
  -Version "${expected_version}" \
  -Framework netstandard2.1 \
  -ConfigFile "${nuget_config}" \
  -OutputDirectory "${restore_packages_directory}" \
  -NoCache \
  -DirectDownload \
  -NonInteractive

restored_plugin_root="${restore_packages_directory}/${package_id}.${expected_version}"
restored_marker_path="${restored_plugin_root}/ucli-plugin.json"
if [[ ! -f "${restored_marker_path}" ]]; then
  echo "Restored Unity package marker was not found: ${restored_marker_path}" >&2
  exit 1
fi

restored_editor_asmdef="${restored_plugin_root}/${unity_editor_asmdef_entry}"
if [[ ! -f "${restored_editor_asmdef}" ]] \
  || ! jq -e \
    --arg dependency_assembly "${filesystem_package_id}" \
    '(.references | type == "array") and (.references | index($dependency_assembly) != null)' \
    "${restored_editor_asmdef}" >/dev/null; then
  echo "Restored Unity Editor asmdef does not reference ${filesystem_package_id}." >&2
  exit 1
fi

restored_plugin_package="${restored_plugin_root}/${package_id}.${expected_version}.nupkg"
if [[ ! -f "${restored_plugin_package}" ]] \
  || ! cmp -s "${package_path}" "${restored_plugin_package}"; then
  echo "Restored Unity plugin package does not match the verified package artifact." >&2
  exit 1
fi

restored_filesystem_root="${restore_packages_directory}/${filesystem_package_id}.${filesystem_package_version}"
restored_filesystem_dll="${restored_filesystem_root}/lib/netstandard2.1/${filesystem_package_id}.dll"
restored_filesystem_package="${restored_filesystem_root}/${filesystem_package_file_name}"
for restored_filesystem_entry in \
  "${restored_filesystem_dll}" \
  "${restored_filesystem_package}"; do
  if [[ ! -f "${restored_filesystem_entry}" ]]; then
    echo "Restored Unity dependency layout is missing: ${restored_filesystem_entry}" >&2
    exit 1
  fi
done

if find "${restored_plugin_root}" -type f \
  \( -iname "${filesystem_package_id}.dll" -o -iname "${filesystem_package_id}.*.nupkg" \) \
  -print -quit |
  grep -q .; then
  echo "Restored Unity plugin directory contains the external filesystem provider." >&2
  find "${restored_plugin_root}" -type f \
    \( -iname "${filesystem_package_id}.dll" -o -iname "${filesystem_package_id}.*.nupkg" \) \
    -print >&2
  exit 1
fi

if [[ -n "${filesystem_package_source}" ]] \
  && ! cmp -s \
    "${filesystem_package_source}/${filesystem_package_file_name}" \
    "${restored_filesystem_package}"; then
  echo "Restored Unity filesystem package does not match the supplied prepublication package." >&2
  exit 1
fi

if [[ -n "${filesystem_package_source}" ]] \
  && ! unzip -p \
    "${filesystem_package_source}/${filesystem_package_file_name}" \
    "lib/netstandard2.1/${filesystem_package_id}.dll" \
    | cmp -s - "${restored_filesystem_dll}"; then
  echo "Restored Unity filesystem assembly does not match the supplied prepublication package." >&2
  exit 1
fi

for dependency_index in "${!ucli_dependency_package_ids[@]}"; do
  dependency_package_id="${ucli_dependency_package_ids[${dependency_index}]}"
  dependency_package_version="${ucli_dependency_package_versions[${dependency_index}]}"
  dependency_root="${restore_packages_directory}/${dependency_package_id}.${dependency_package_version}"
  dependency_dll="${dependency_root}/lib/netstandard2.1/${dependency_package_id}.dll"
  restored_dependency_package="${dependency_root}/${dependency_package_id}.${dependency_package_version}.nupkg"
  source_dependency_package="${package_dir}/${dependency_package_id}.${dependency_package_version}.nupkg"
  if [[ ! -f "${dependency_dll}" ]] \
    || [[ ! -f "${restored_dependency_package}" ]] \
    || ! cmp -s "${source_dependency_package}" "${restored_dependency_package}"; then
    echo "Restored Unity dependency layout does not match ${source_dependency_package}." >&2
    exit 1
  fi
done

echo "Unity package verification passed: ${package_path}"
