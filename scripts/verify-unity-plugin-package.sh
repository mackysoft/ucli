#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  echo "Usage: $0 <package-dir> <expected-version>" >&2
}

if [[ "$#" -ne 2 ]]; then
  print_usage
  exit 2
fi

package_dir="$1"
expected_version="$2"

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
foundation_package_ids=(
  "${filesystem_package_id}"
  "MackySoft.Text.Vocabularies"
  "MackySoft.Text.Vocabularies.Json"
)
ucli_dependency_package_ids=(
  "MackySoft.Ucli.Contracts"
  "MackySoft.Ucli.Infrastructure"
)
ucli_dependency_package_versions=()

if [[ ! -f "${package_path}" ]]; then
  echo "Unity package was not created: ${package_path}" >&2
  exit 1
fi

for required_tool in jq nuget unzip; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done

if [[ ! -f "${unity_packages_config}" ]]; then
  echo "Unity packages.config does not exist: ${unity_packages_config}" >&2
  exit 1
fi

for foundation_package_id in "${foundation_package_ids[@]}"; do
  foundation_package_version="$(
    sed -nE "s#.*<package id=\"${foundation_package_id}\" version=\"([^\"]+)\".*#\\1#p" \
      "${unity_packages_config}" |
      head -n 1
  )"
  if [[ "${foundation_package_version}" != "${filesystem_package_version}" ]]; then
    echo "${foundation_package_id} must use exact version ${filesystem_package_version}. Actual: ${foundation_package_version}" >&2
    exit 1
  fi
done

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
  '(^|/)MackySoft\.FileSystem\.[^/]*\.nupkg$' \
  '(^|/)MackySoft\.Text\.Vocabularies(\.Json)?\.dll$' \
  '(^|/)MackySoft\.Text\.Vocabularies(\.Json)?\.[^/]*\.nupkg$'; do
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
  expected_nuspec_version="${dependency_version}"
  case "${dependency_id}" in
    MackySoft.FileSystem|MackySoft.Text.Vocabularies|MackySoft.Text.Vocabularies.Json)
      expected_nuspec_version="[${dependency_version}]"
      ;;
  esac

  if ! grep -F "<dependency id=\"${dependency_id}\" version=\"${expected_nuspec_version}\" />" "${nuspec_path}" >/dev/null; then
    echo "Unity package nuspec is missing dependency ${dependency_id} ${expected_nuspec_version}." >&2
    exit 1
  fi
done < <(
  sed -nE 's#.*<package id="([^"]+)" version="([^"]+)".*#\1\t\2#p' "${unity_packages_config}"
)

unity_package_source="${temp_dir}/unity-source"
ucli_package_source="${temp_dir}/ucli-source"
isolated_nuget_packages="${temp_dir}/global-packages"
isolated_nuget_http_cache="${temp_dir}/http-cache"
nuget_config="${temp_dir}/NuGet.config"
restore_root="${temp_dir}/UnityProject"
restore_packages_directory="${restore_root}/Assets/Packages"
restore_packages_config="${temp_dir}/packages.config"
mkdir -p \
  "${unity_package_source}" \
  "${ucli_package_source}" \
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

cat > "${nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="UnityPackage" value="./unity-source" />
    <add key="UcliPackages" value="./ucli-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="UnityPackage">
      <package pattern="${package_id}" />
    </packageSource>
    <packageSource key="UcliPackages">
      <package pattern="MackySoft.Ucli.Contracts" />
      <package pattern="MackySoft.Ucli.Infrastructure" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="MackySoft.FileSystem" />
      <package pattern="MackySoft.Text.Vocabularies" />
      <package pattern="MackySoft.Text.Vocabularies.Json" />
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="Newtonsoft.Json" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

{
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<packages>' \
    "  <package id=\"${package_id}\" version=\"${expected_version}\" targetFramework=\"netstandard2.1\" />"
  sed -nE '/^[[:space:]]*<package /p' "${unity_packages_config}"
  printf '%s\n' '</packages>'
} > "${restore_packages_config}"

NUGET_HTTP_CACHE_PATH="${isolated_nuget_http_cache}" \
  NUGET_PACKAGES="${isolated_nuget_packages}" \
  nuget restore "${restore_packages_config}" \
  -PackagesDirectory "${restore_packages_directory}" \
  -ConfigFile "${nuget_config}" \
  -NoCache \
  -NonInteractive >/dev/null

restored_plugin_root="${restore_packages_directory}/${package_id}.${expected_version}"
restored_marker_path="${restored_plugin_root}/ucli-plugin.json"
if [[ ! -f "${restored_marker_path}" ]]; then
  echo "Restored Unity package marker was not found: ${restored_marker_path}" >&2
  exit 1
fi

restored_editor_asmdef="${restored_plugin_root}/${unity_editor_asmdef_entry}"
if [[ ! -f "${restored_editor_asmdef}" ]] \
  || ! jq -e \
    --arg filesystem_assembly "${filesystem_package_id}" \
    --arg text_assembly "MackySoft.Text.Vocabularies" \
    '(.references | type == "array")
      and (.references | index($filesystem_assembly) != null)
      and (.references | index($text_assembly) != null)' \
    "${restored_editor_asmdef}" >/dev/null; then
  echo "Restored Unity Editor asmdef does not reference the required foundation assemblies." >&2
  exit 1
fi

for foundation_package_id in "${foundation_package_ids[@]}"; do
  foundation_package_root="${restore_packages_directory}/${foundation_package_id}.${filesystem_package_version}"
  foundation_runtime_assembly="${foundation_package_root}/lib/netstandard2.1/${foundation_package_id}.dll"
  if [[ ! -f "${foundation_runtime_assembly}" ]]; then
    echo "Restored Unity dependency layout is missing: ${foundation_runtime_assembly}" >&2
    exit 1
  fi
done

if find "${restored_plugin_root}" -type f \
  \( \
    -iname "${filesystem_package_id}.dll" \
    -o -iname "${filesystem_package_id}.*.nupkg" \
    -o -iname "MackySoft.Text.Vocabularies.dll" \
    -o -iname "MackySoft.Text.Vocabularies.Json.dll" \
    -o -iname "MackySoft.Text.Vocabularies.*.nupkg" \
    -o -iname "MackySoft.Text.Vocabularies.Json.*.nupkg" \
  \) \
  -print -quit |
  grep -q .; then
  echo "Restored Unity plugin directory contains an external foundation provider." >&2
  find "${restored_plugin_root}" -type f \
    \( \
      -iname "${filesystem_package_id}.dll" \
      -o -iname "${filesystem_package_id}.*.nupkg" \
      -o -iname "MackySoft.Text.Vocabularies.dll" \
      -o -iname "MackySoft.Text.Vocabularies.Json.dll" \
      -o -iname "MackySoft.Text.Vocabularies.*.nupkg" \
      -o -iname "MackySoft.Text.Vocabularies.Json.*.nupkg" \
    \) \
    -print >&2
  exit 1
fi

for dependency_index in "${!ucli_dependency_package_ids[@]}"; do
  dependency_package_id="${ucli_dependency_package_ids[${dependency_index}]}"
  dependency_package_version="${ucli_dependency_package_versions[${dependency_index}]}"
  dependency_root="${restore_packages_directory}/${dependency_package_id}.${dependency_package_version}"
  dependency_dll="${dependency_root}/lib/netstandard2.1/${dependency_package_id}.dll"
  if [[ ! -f "${dependency_dll}" ]]; then
    echo "Restored Unity dependency layout is missing: ${dependency_dll}" >&2
    exit 1
  fi
done

while IFS=$'\t' read -r restored_package_id restored_package_version; do
  [[ -n "${restored_package_id}" ]] || continue
  restored_package_path="${restore_root}/Assets/Packages/${restored_package_id}.${restored_package_version}"
  if [[ ! -d "${restored_package_path}" ]]; then
    echo "Unity package dependency was not restored: ${restored_package_id} ${restored_package_version}" >&2
    exit 1
  fi
done < <(
  sed -nE 's#.*<package id="([^"]+)" version="([^"]+)".*#\1\t\2#p' "${restore_packages_config}"
)

echo "Unity package verification passed: ${package_path}"
