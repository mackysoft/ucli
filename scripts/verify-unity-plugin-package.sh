#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <package-dir> <expected-version>" >&2
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

if [[ ! -f "${package_path}" ]]; then
  echo "Unity package was not created: ${package_path}" >&2
  exit 1
fi

for required_tool in unzip nuget; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done

if [[ ! -f "${unity_packages_config}" ]]; then
  echo "Unity packages.config does not exist: ${unity_packages_config}" >&2
  exit 1
fi

canonicalization_version="$(
  sed -nE 's#.*<package id="MackySoft.Json.Canonicalization" version="([^"]+)".*#\1#p' "${unity_packages_config}" \
    | head -n 1
)"
if [[ -z "${canonicalization_version}" ]]; then
  echo "Unity packages.config does not declare MackySoft.Json.Canonicalization." >&2
  exit 1
fi
canonicalization_package_artifact="${package_dir}/MackySoft.Json.Canonicalization.${canonicalization_version}.nupkg"

package_entries="$(unzip -Z1 "${package_path}")"
if grep -Fi "es6numberserializer" <<< "${package_entries}" >/dev/null; then
  echo "Unity package contains the legacy es6numberserializer package or assembly." >&2
  grep -Fi "es6numberserializer" <<< "${package_entries}" >&2
  exit 1
fi

if grep -F "MackySoft.Json.Canonicalization.dll" <<< "${package_entries}" >/dev/null; then
  echo "Unity package must reference MackySoft.Json.Canonicalization as a dependency instead of embedding its assembly." >&2
  exit 1
fi

if grep -Ei '(^|/)MackySoft[.]Json[.]Canonicalization[.][^/]+[.]nupkg$' <<< "${package_entries}" >/dev/null; then
  echo "Unity package must reference MackySoft.Json.Canonicalization as a dependency instead of embedding its nupkg." >&2
  exit 1
fi

required_entries=(
  "${nuspec_entry}"
  "ucli-plugin.json"
  "Editor/MackySoft.Ucli.Unity.Editor.asmdef"
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

for forbidden_pattern in '^Assets/' '^Tests/' '^ProjectSettings/' '^Packages/' '^.*\.unitypackage$' '^package\.json$'; do
  if grep -E "${forbidden_pattern}" <<< "${package_entries}" >/dev/null; then
    echo "Unity package contains forbidden entry matching ${forbidden_pattern}." >&2
    grep -E "${forbidden_pattern}" <<< "${package_entries}" >&2
    exit 1
  fi
done

temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT
nuspec_path="${temp_dir}/${nuspec_entry}"
unzip -p "${package_path}" "${nuspec_entry}" > "${nuspec_path}"

if grep -Fi "es6numberserializer" "${nuspec_path}" >/dev/null; then
  echo "Unity package nuspec references the legacy es6numberserializer package or assembly." >&2
  exit 1
fi

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
  if [[ "$(tr '[:upper:]' '[:lower:]' <<< "${dependency_id}")" == "es6numberserializer" ]]; then
    echo "Unity package must not declare the legacy es6numberserializer dependency." >&2
    exit 1
  fi

  expected_dependency_version="${dependency_version}"
  if [[ "${dependency_id}" == "MackySoft.Json.Canonicalization" ]]; then
    expected_dependency_version="[${dependency_version}]"
  fi

  if ! grep -F "<dependency id=\"${dependency_id}\" version=\"${expected_dependency_version}\" />" "${nuspec_path}" >/dev/null; then
    echo "Unity package nuspec is missing dependency ${dependency_id} ${expected_dependency_version}." >&2
    exit 1
  fi
done < <(
  sed -nE 's#.*<package id="([^"]+)" version="([^"]+)".*#\1\t\2#p' "${unity_packages_config}"
)

restore_root="${temp_dir}/UnityProject"
mkdir -p "${restore_root}/Assets/Packages"
canonicalization_local_mapping=""
canonicalization_remote_mapping='      <package pattern="MackySoft.Json.Canonicalization" />'
if [[ -f "${canonicalization_package_artifact}" ]]; then
  canonicalization_local_mapping='      <package pattern="MackySoft.Json.Canonicalization" />'
  canonicalization_remote_mapping=""
fi
cat > "${temp_dir}/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="PackageArtifacts" value="${package_dir}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="PackageArtifacts">
      <package pattern="MackySoft.Ucli.*" />
${canonicalization_local_mapping}
    </packageSource>
    <packageSource key="nuget.org">
${canonicalization_remote_mapping}
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="packageSaveMode" value="nuspec;nupkg" />
  </config>
</configuration>
EOF

nuget install "${package_id}" \
  -Version "${expected_version}" \
  -OutputDirectory "${restore_root}/Assets/Packages" \
  -ConfigFile "${temp_dir}/NuGet.config" \
  -DependencyVersion Lowest \
  -NoCache \
  -NonInteractive >/dev/null

restored_marker_path="${restore_root}/Assets/Packages/${package_id}.${expected_version}/ucli-plugin.json"
if [[ ! -f "${restored_marker_path}" ]]; then
  echo "Restored Unity package marker was not found: ${restored_marker_path}" >&2
  exit 1
fi

required_dependency_files=(
  "${restore_root}/Assets/Packages/MackySoft.Json.Canonicalization.${canonicalization_version}/lib/netstandard2.1/MackySoft.Json.Canonicalization.dll"
)
for dependency_file in "${required_dependency_files[@]}"; do
  if [[ ! -f "${dependency_file}" ]]; then
    echo "Unity package dependency closure is missing required assembly: ${dependency_file}" >&2
    exit 1
  fi
done

if [[ -f "${canonicalization_package_artifact}" ]]; then
  restored_canonicalization_package="${restore_root}/Assets/Packages/MackySoft.Json.Canonicalization.${canonicalization_version}/MackySoft.Json.Canonicalization.${canonicalization_version}.nupkg"
  if [[ ! -f "${restored_canonicalization_package}" ]]; then
    echo "Unity package dependency closure is missing the restored local canonicalization nupkg." >&2
    exit 1
  fi
  if ! cmp -s "${canonicalization_package_artifact}" "${restored_canonicalization_package}"; then
    echo "Unity package dependency closure contains a canonicalization nupkg that differs from the local provider artifact." >&2
    exit 1
  fi
  if ! unzip -p "${canonicalization_package_artifact}" lib/netstandard2.1/MackySoft.Json.Canonicalization.dll \
    | cmp -s - "${required_dependency_files[0]}"; then
    echo "Unity package dependency closure contains a canonicalization assembly that differs from the local provider artifact." >&2
    exit 1
  fi
fi

for assembly_name in MackySoft.Json.Canonicalization.dll; do
  assembly_count="$(
    find "${restore_root}/Assets/Packages" -type f -name "${assembly_name}" | wc -l | tr -d '[:space:]'
  )"
  if [[ "${assembly_count}" != "1" ]]; then
    echo "Unity package dependency closure must contain exactly one ${assembly_name}; found ${assembly_count}." >&2
    exit 1
  fi
done

legacy_dependency_path="$(
  find "${restore_root}/Assets/Packages" -iname '*es6numberserializer*' -print -quit
)"
if [[ -n "${legacy_dependency_path}" ]]; then
  echo "Unity package dependency closure contains the legacy es6numberserializer package or assembly." >&2
  find "${restore_root}/Assets/Packages" -iname '*es6numberserializer*' -print >&2
  exit 1
fi

echo "Unity package verification passed: ${package_path}"
