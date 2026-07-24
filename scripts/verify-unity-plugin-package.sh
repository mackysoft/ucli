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

package_entries="$(unzip -Z1 "${package_path}")"
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
    MackySoft.Text.Vocabularies|MackySoft.Text.Vocabularies.Json)
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

restore_root="${temp_dir}/UnityProject"
mkdir -p "${restore_root}/Assets"
nuget_config_path="${temp_dir}/NuGet.config"
escaped_package_dir="${package_dir//&/&amp;}"
escaped_package_dir="${escaped_package_dir//\"/&quot;}"
cat > "${nuget_config_path}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalNuGet" value="${escaped_package_dir}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="LocalNuGet">
      <package pattern="MackySoft.Ucli.*" />
    </packageSource>
    <packageSource key="nuget.org">
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
} > "${temp_dir}/packages.config"

nuget restore "${temp_dir}/packages.config" \
  -PackagesDirectory "${restore_root}/Assets/Packages" \
  -ConfigFile "${nuget_config_path}" \
  -NoCache \
  -NonInteractive >/dev/null

restored_marker_path="${restore_root}/Assets/Packages/${package_id}.${expected_version}/ucli-plugin.json"
if [[ ! -f "${restored_marker_path}" ]]; then
  echo "Restored Unity package marker was not found: ${restored_marker_path}" >&2
  exit 1
fi

while IFS=$'\t' read -r restored_package_id restored_package_version; do
  [[ -n "${restored_package_id}" ]] || continue
  restored_package_path="${restore_root}/Assets/Packages/${restored_package_id}.${restored_package_version}"
  if [[ ! -d "${restored_package_path}" ]]; then
    echo "Unity package dependency was not restored: ${restored_package_id} ${restored_package_version}" >&2
    exit 1
  fi
done < <(
  sed -nE 's#.*<package id="([^"]+)" version="([^"]+)".*#\1\t\2#p' "${temp_dir}/packages.config"
)

echo "Unity package verification passed: ${package_path}"
