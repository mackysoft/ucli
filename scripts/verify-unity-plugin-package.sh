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
canonicalization_package_id="MackySoft.Json.Canonicalization"
json_schema_package_id="MackySoft.JsonSchema.Generation"
json_schema_package_version="0.3.1"
foundation_package_ids=(
  "${filesystem_package_id}"
  "${canonicalization_package_id}"
  "MackySoft.Text.Vocabularies"
  "MackySoft.Text.Vocabularies.Json"
)
ucli_dependency_package_ids=(
  "MackySoft.Ucli.Contracts"
  "MackySoft.Ucli.Infrastructure"
)
ucli_dependency_package_versions=()
schema_validation_package_ids=(
  "JsonSchema.Net"
  "JsonPointer.Net"
  "Json.More.Net"
  "Humanizer.Core"
)
schema_validation_assembly_names=(
  "JsonSchema.Net.dll"
  "JsonPointer.Net.dll"
  "Json.More.dll"
  "Humanizer.dll"
)

read_plugin_importer_platform_settings() {
  local importer_meta_path="$1"

  awk '
    function fail(message) {
      print "Invalid PluginImporter platformData: " message > "/dev/stderr"
      parse_failed = 1
      exit 1
    }

    function finish_platform_entry() {
      if (!in_platform_entry) {
        return
      }

      if (platform_name == "") {
        fail("platform name is missing")
      }
      if (!has_second) {
        fail("second mapping is missing for " platform_name)
      }
      if (enabled_count != 1) {
        fail(platform_name " must define enabled exactly once")
      }
      if (seen_platforms[platform_name]) {
        fail(platform_name " is defined more than once")
      }

      seen_platforms[platform_name] = 1
      platform_entry_count += 1
      platform_names[platform_entry_count] = platform_name
      platform_enabled_values[platform_entry_count] = enabled
      in_platform_entry = 0
      platform_name = ""
      has_second = 0
      enabled_count = 0
      enabled = ""
    }

    {
      sub(/\r$/, "")
    }

    $0 == "PluginImporter:" {
      in_plugin_importer = 1
      next
    }

    in_plugin_importer && /^[^[:space:]]/ {
      if (in_platform_data) {
        finish_platform_entry()
        in_platform_data = 0
      }
      in_plugin_importer = 0
      next
    }

    in_plugin_importer && /^  platformData:[[:space:]]*$/ {
      platform_data_count += 1
      if (platform_data_count != 1) {
        fail("platformData is defined more than once")
      }
      in_platform_data = 1
      next
    }

    in_platform_data && /^  [^[:space:]-]/ {
      finish_platform_entry()
      in_platform_data = 0
      next
    }

    in_platform_data && /^  - first:[[:space:]]*$/ {
      finish_platform_entry()
      in_platform_entry = 1
      platform_name = ""
      has_second = 0
      enabled_count = 0
      enabled = ""
      settings_seen = 0
      settings_block = 0
      next
    }

    in_platform_data && in_platform_entry && platform_name == "" {
      if ($0 !~ /^      [^:]+:/) {
        fail("platform name must immediately follow first mapping")
      }
      platform_name = substr($0, 7)
      sub(/:.*/, "", platform_name)
      next
    }

    in_platform_data && in_platform_entry && /^    second:[[:space:]]*$/ {
      if (has_second) {
        fail("second mapping is defined more than once for " platform_name)
      }
      has_second = 1
      next
    }

    in_platform_data && in_platform_entry && /^      enabled:[[:space:]]*/ {
      if (!has_second) {
        fail("enabled must belong to the second mapping for " platform_name)
      }
      enabled_count += 1
      if (enabled_count != 1) {
        fail("enabled is defined more than once for " platform_name)
      }
      enabled = $0
      sub(/^      enabled:[[:space:]]*/, "", enabled)
      sub(/[[:space:]]*$/, "", enabled)
      if (enabled != "0" && enabled != "1") {
        fail("enabled must be 0 or 1 for " platform_name)
      }
      next
    }

    in_platform_data && in_platform_entry && /^      settings:[[:space:]]*\{\}[[:space:]]*$/ {
      if (enabled_count != 1 || settings_seen) {
        fail("settings must follow enabled at most once for " platform_name)
      }
      settings_seen = 1
      settings_block = 0
      next
    }

    in_platform_data && in_platform_entry && /^      settings:[[:space:]]*$/ {
      if (enabled_count != 1 || settings_seen) {
        fail("settings must follow enabled at most once for " platform_name)
      }
      settings_seen = 1
      settings_block = 1
      next
    }

    in_platform_data && in_platform_entry && /^        / {
      if (!settings_block) {
        fail("nested platform settings must belong to a settings mapping for " platform_name)
      }
      next
    }

    in_platform_data && in_platform_entry && !has_second {
      fail("second mapping must immediately follow " platform_name)
    }

    in_platform_data && in_platform_entry && enabled_count == 0 {
      fail("enabled must immediately follow the second mapping for " platform_name)
    }

    in_platform_data && !in_platform_entry && $0 !~ /^[[:space:]]*$/ {
      fail("platformData contains content outside a platform entry")
    }

    in_platform_data {
      fail("platformData contains unexpected content for " platform_name)
    }

    END {
      if (parse_failed) {
        exit 1
      }
      if (in_platform_data) {
        finish_platform_entry()
      }
      if (platform_data_count != 1) {
        fail("platformData must be defined exactly once")
      }
      if (platform_entry_count == 0) {
        fail("platformData must contain at least one platform entry")
      }

      for (entry_index = 1; entry_index <= platform_entry_count; entry_index += 1) {
        print platform_names[entry_index] "\t" platform_enabled_values[entry_index]
      }
    }
  ' "${importer_meta_path}"
}

verify_editor_only_plugin_importer() {
  local importer_meta_path="$1"
  local assembly_name="$2"
  local platform_settings
  local any_platform_enabled
  local editor_enabled
  local enabled_platform_count

  if [[ ! -f "${importer_meta_path}" ]]; then
    echo "Restored Unity dependency is missing PluginImporter metadata: ${importer_meta_path}" >&2
    exit 1
  fi

  if [[ "$(grep -Fxc "PluginImporter:" "${importer_meta_path}")" != "1" ]]; then
    echo "Restored Unity dependency metadata does not define PluginImporter settings: ${importer_meta_path}" >&2
    exit 1
  fi

  if ! platform_settings="$(read_plugin_importer_platform_settings "${importer_meta_path}")"; then
    echo "${assembly_name} has invalid Unity PluginImporter metadata." >&2
    cat "${importer_meta_path}" >&2
    exit 1
  fi
  any_platform_enabled="$(awk -F '\t' '$1 == "Any" { print $2 }' <<<"${platform_settings}")"
  editor_enabled="$(awk -F '\t' '$1 == "Editor" { print $2 }' <<<"${platform_settings}")"
  enabled_platform_count="$(awk -F '\t' '$2 == "1" { count += 1 } END { print count + 0 }' <<<"${platform_settings}")"

  if [[ "${any_platform_enabled}" != "0" || "${editor_enabled}" != "1" || "${enabled_platform_count}" != "1" ]]; then
    echo "${assembly_name} must be compatible only with the Unity Editor." >&2
    cat "${importer_meta_path}" >&2
    exit 1
  fi
}

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

for schema_validation_package_id in "${schema_validation_package_ids[@]}"; do
  if grep -Fi "<package id=\"${schema_validation_package_id}\"" "${unity_packages_config}" >/dev/null; then
    echo "Unity packages.config must not reference ${schema_validation_package_id}." >&2
    exit 1
  fi
done

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

configured_json_schema_package_version="$(
  sed -nE "s#.*<package id=\"${json_schema_package_id}\" version=\"([^\"]+)\".*#\\1#p" \
    "${unity_packages_config}" |
    head -n 1
)"
if [[ "${configured_json_schema_package_version}" != "${json_schema_package_version}" ]]; then
  echo "${json_schema_package_id} must use exact version ${json_schema_package_version}. Actual: ${configured_json_schema_package_version}" >&2
  exit 1
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
  '(^|/)MackySoft\.FileSystem\.[^/]*\.nupkg$' \
  '(^|/)MackySoft\.Json\.Canonicalization\.dll$' \
  '(^|/)MackySoft\.Json\.Canonicalization\.[^/]*\.nupkg$' \
  '(^|/)MackySoft\.JsonSchema\.Generation\.dll$' \
  '(^|/)MackySoft\.JsonSchema\.Generation\.[^/]*\.nupkg$' \
  '(^|/)MackySoft\.Text\.Vocabularies(\.Json)?\.dll$' \
  '(^|/)MackySoft\.Text\.Vocabularies(\.Json)?\.[^/]*\.nupkg$' \
  '(^|/)JsonSchema\.Net\.dll$' \
  '(^|/)JsonSchema\.Net\.[^/]*\.nupkg$' \
  '(^|/)JsonPointer\.Net\.dll$' \
  '(^|/)JsonPointer\.Net\.[^/]*\.nupkg$' \
  '(^|/)Json\.More\.dll$' \
  '(^|/)Json\.More\.Net\.[^/]*\.nupkg$' \
  '(^|/)Humanizer\.dll$' \
  '(^|/)Humanizer\.Core\.[^/]*\.nupkg$' \
  'es6numberserializer'; do
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

if grep -Fi "es6numberserializer" "${nuspec_path}" >/dev/null; then
  echo "Unity package nuspec references the retired es6numberserializer dependency." >&2
  exit 1
fi

for schema_validation_package_id in "${schema_validation_package_ids[@]}"; do
  if grep -Fi "<dependency id=\"${schema_validation_package_id}\"" "${nuspec_path}" >/dev/null; then
    echo "Unity package nuspec must not depend on ${schema_validation_package_id}." >&2
    exit 1
  fi
done

while IFS=$'\t' read -r dependency_id dependency_version; do
  [[ -n "${dependency_id}" ]] || continue
  expected_nuspec_version="${dependency_version}"
  case "${dependency_id}" in
    MackySoft.FileSystem|MackySoft.Json.Canonicalization|MackySoft.JsonSchema.Generation|MackySoft.Text.Vocabularies|MackySoft.Text.Vocabularies.Json)
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
      <package pattern="MackySoft.Json.Canonicalization" />
      <package pattern="MackySoft.JsonSchema.Generation" />
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

if find "${restore_packages_directory}" -iname "*es6numberserializer*" -print -quit | grep -q .; then
  echo "Restored Unity dependency closure contains the retired es6numberserializer dependency." >&2
  exit 1
fi

for schema_validation_package_id in "${schema_validation_package_ids[@]}"; do
  if find "${restore_packages_directory}" \
    -mindepth 1 \
    -maxdepth 1 \
    -type d \
    -iname "${schema_validation_package_id}.*" \
    -print -quit |
    grep -q .; then
    echo "Restored Unity dependency closure contains ${schema_validation_package_id}." >&2
    exit 1
  fi
done
for schema_validation_assembly_name in "${schema_validation_assembly_names[@]}"; do
  if find "${restore_packages_directory}" \
    -type f \
    -iname "${schema_validation_assembly_name}" \
    -print -quit |
    grep -q .; then
    echo "Restored Unity dependency closure contains ${schema_validation_assembly_name}." >&2
    exit 1
  fi
done

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
    --arg json_schema_assembly "${json_schema_package_id}" \
    --arg text_assembly "MackySoft.Text.Vocabularies" \
    '(.references | type == "array")
      and (.references | index($filesystem_assembly) != null)
      and (.references | index($json_schema_assembly) != null)
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

json_schema_package_root="${restore_packages_directory}/${json_schema_package_id}.${json_schema_package_version}"
json_schema_runtime_assembly="${json_schema_package_root}/lib/netstandard2.1/${json_schema_package_id}.dll"
if [[ ! -f "${json_schema_runtime_assembly}" ]]; then
  echo "Restored Unity dependency layout is missing: ${json_schema_runtime_assembly}" >&2
  exit 1
fi

if find "${restored_plugin_root}" -type f \
  \( \
    -iname "${filesystem_package_id}.dll" \
    -o -iname "${filesystem_package_id}.*.nupkg" \
    -o -iname "${canonicalization_package_id}.dll" \
    -o -iname "${canonicalization_package_id}.*.nupkg" \
    -o -iname "${json_schema_package_id}.dll" \
    -o -iname "${json_schema_package_id}.*.nupkg" \
    -o -iname "MackySoft.Text.Vocabularies.dll" \
    -o -iname "MackySoft.Text.Vocabularies.Json.dll" \
    -o -iname "MackySoft.Text.Vocabularies.*.nupkg" \
    -o -iname "MackySoft.Text.Vocabularies.Json.*.nupkg" \
    -o -iname "*es6numberserializer*" \
  \) \
  -print -quit |
  grep -q .; then
  echo "Restored Unity plugin directory contains an external foundation provider." >&2
  find "${restored_plugin_root}" -type f \
    \( \
      -iname "${filesystem_package_id}.dll" \
      -o -iname "${filesystem_package_id}.*.nupkg" \
      -o -iname "${canonicalization_package_id}.dll" \
      -o -iname "${canonicalization_package_id}.*.nupkg" \
      -o -iname "${json_schema_package_id}.dll" \
      -o -iname "${json_schema_package_id}.*.nupkg" \
      -o -iname "MackySoft.Text.Vocabularies.dll" \
      -o -iname "MackySoft.Text.Vocabularies.Json.dll" \
      -o -iname "MackySoft.Text.Vocabularies.*.nupkg" \
      -o -iname "MackySoft.Text.Vocabularies.Json.*.nupkg" \
      -o -iname "*es6numberserializer*" \
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

  verify_editor_only_plugin_importer "${dependency_dll}.meta" "${dependency_package_id}"
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
