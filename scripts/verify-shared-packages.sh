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
  echo "Shared package directory does not exist: ${package_dir}" >&2
  exit 1
fi

for required_tool in python3 unzip; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done

package_dir="$(cd "${package_dir}" && pwd)"
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

temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT
package_ids=(
  "MackySoft.Ucli.Contracts"
  "MackySoft.Ucli.Infrastructure"
)

for package_id in "${package_ids[@]}"; do
  package_path="${package_dir}/${package_id}.${expected_version}.nupkg"
  nuspec_entry="${package_id}.nuspec"

  if [[ ! -f "${package_path}" ]]; then
    echo "Shared package was not created: ${package_path}" >&2
    exit 1
  fi

  package_entries="$(unzip -Z1 "${package_path}")"
  for entry in "${nuspec_entry}" README.md LICENSE; do
    if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
      echo "Shared package ${package_id} is missing required entry: ${entry}" >&2
      exit 1
    fi
  done

  case "${package_id}" in
    MackySoft.Ucli.Contracts)
      required_library_entries=(
        "lib/netstandard2.1/MackySoft.Ucli.Contracts.dll"
      )
      ;;
    MackySoft.Ucli.Infrastructure)
      required_library_entries=(
        "lib/netstandard2.1/MackySoft.Ucli.Infrastructure.dll"
        "lib/net8.0/MackySoft.Ucli.Infrastructure.dll"
      )
      ;;
    *)
      echo "Unsupported shared package id: ${package_id}" >&2
      exit 1
      ;;
  esac

  for entry in "${required_library_entries[@]}"; do
    if ! grep -Fx "${entry}" <<< "${package_entries}" >/dev/null; then
      echo "Shared package ${package_id} is missing required library entry: ${entry}" >&2
      exit 1
    fi
  done

  nuspec_path="${temp_dir}/${nuspec_entry}"
  unzip -p "${package_path}" "${nuspec_entry}" > "${nuspec_path}"

  if ! grep -F "<id>${package_id}</id>" "${nuspec_path}" >/dev/null; then
    echo "Shared package ${package_id} has an unexpected package id." >&2
    exit 1
  fi

  if ! grep -F "<version>${expected_version}</version>" "${nuspec_path}" >/dev/null; then
    echo "Shared package ${package_id} has an unexpected version." >&2
    exit 1
  fi

  if [[ "${package_id}" == "MackySoft.Ucli.Infrastructure" ]]; then
    for dependency_id in MackySoft.FileSystem MackySoft.Ucli.Contracts; do
      case "${dependency_id}" in
        MackySoft.FileSystem)
          expected_dependency_version="[${filesystem_package_version}]"
          ;;
        MackySoft.Ucli.Contracts)
          expected_dependency_version="${expected_version}"
          ;;
        *)
          echo "Unsupported infrastructure dependency: ${dependency_id}" >&2
          exit 1
          ;;
      esac

      dependency_versions="$(
        DEPENDENCY_ID="${dependency_id}" perl -ne '
          my $dependency_id = $ENV{"DEPENDENCY_ID"};
          while (/<dependency\b([^>]*)>/g) {
            my $attributes = $1;
            next unless $attributes =~ /\bid="\Q$dependency_id\E"/;
            if ($attributes =~ /\bversion="([^"]+)"/) {
              print "$1\n";
            }
          }
        ' "${nuspec_path}"
      )"

      if [[ -z "${dependency_versions}" ]]; then
        echo "MackySoft.Ucli.Infrastructure is missing dependency: ${dependency_id}." >&2
        exit 1
      fi

      unexpected_dependency_versions="$(
        grep -Fvx "${expected_dependency_version}" <<< "${dependency_versions}" || true
      )"
      if [[ -n "${unexpected_dependency_versions}" ]]; then
        echo "MackySoft.Ucli.Infrastructure dependency ${dependency_id} is not pinned to ${expected_dependency_version}." >&2
        printf '%s\n' "${unexpected_dependency_versions}" >&2
        exit 1
      fi
    done
  fi
done

consumer_dir="${temp_dir}/filesystem-consumer"
consumer_project_path="${consumer_dir}/FileSystemPackageConsumer.csproj"
consumer_dotnet_home="${temp_dir}/dotnet-home"
consumer_nuget_packages="${temp_dir}/nuget-packages"
consumer_nuget_http_cache="${temp_dir}/http-cache"
consumer_ucli_package_source="${temp_dir}/ucli-source"
consumer_filesystem_package_source="${temp_dir}/filesystem-source"
consumer_nuget_config="${temp_dir}/NuGet.config"
dotnet_consumer_home="${consumer_dotnet_home}"
dotnet_consumer_nuget_packages="${consumer_nuget_packages}"
if command -v cygpath >/dev/null 2>&1; then
  dotnet_consumer_home="$(cygpath -m "${consumer_dotnet_home}")"
  dotnet_consumer_nuget_packages="$(cygpath -m "${consumer_nuget_packages}")"
fi
export DOTNET_CLI_HOME="${dotnet_consumer_home}"
export NUGET_PACKAGES="${dotnet_consumer_nuget_packages}"
export NUGET_HTTP_CACHE_PATH="${consumer_nuget_http_cache}"
mkdir -p \
  "${consumer_dotnet_home}" \
  "${consumer_nuget_http_cache}" \
  "${consumer_nuget_packages}" \
  "${consumer_ucli_package_source}"
for package_id in "${package_ids[@]}"; do
  cp "${package_dir}/${package_id}.${expected_version}.nupkg" \
    "${consumer_ucli_package_source}/${package_id}.${expected_version}.nupkg"
done

filesystem_package_source_entry=""
filesystem_package_source_mapping=""
public_filesystem_package_mapping='<package pattern="MackySoft.FileSystem" />'
if [[ -n "${filesystem_package_source}" ]]; then
  mkdir -p "${consumer_filesystem_package_source}"
  cp "${filesystem_package_source}/${filesystem_package_file_name}" \
    "${consumer_filesystem_package_source}/${filesystem_package_file_name}"
  filesystem_package_source_entry='<add key="FileSystemCandidate" value="./filesystem-source" />'
  filesystem_package_source_mapping='
    <packageSource key="FileSystemCandidate">
      <package pattern="MackySoft.FileSystem" />
    </packageSource>'
  public_filesystem_package_mapping=""
fi

cat > "${consumer_nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    ${filesystem_package_source_entry}
    <add key="UcliPackages" value="./ucli-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>${filesystem_package_source_mapping}
    <packageSource key="UcliPackages">
      <package pattern="MackySoft.Ucli.Contracts" />
      <package pattern="MackySoft.Ucli.Infrastructure" />
    </packageSource>
    <packageSource key="nuget.org">
      ${public_filesystem_package_mapping}
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet new classlib \
  --name FileSystemPackageConsumer \
  --output "${consumer_dir}" \
  --framework netstandard2.1 \
  --no-restore \
  >/dev/null
EXPECTED_VERSION="${expected_version}" FILESYSTEM_VERSION="${filesystem_package_version}" perl -0pi -e '
  my $version = $ENV{"EXPECTED_VERSION"};
  my $filesystem_version = $ENV{"FILESYSTEM_VERSION"};
  s{<TargetFramework>netstandard2\.1</TargetFramework>}{<TargetFrameworks>net8.0;netstandard2.1</TargetFrameworks>};
  s{</Project>}{  <ItemGroup>\n    <PackageReference Include="MackySoft.FileSystem" Version="[$filesystem_version]" />\n    <PackageReference Include="MackySoft.Ucli.Contracts" Version="$version" />\n    <PackageReference Include="MackySoft.Ucli.Infrastructure" Version="$version" />\n  </ItemGroup>\n</Project>};
' "${consumer_project_path}"
cat > "${consumer_dir}/Class1.cs" <<'EOF'
using MackySoft.FileSystem;

namespace FileSystemPackageConsumer
{
    public static class GuardedPathConsumer
    {
        public static ContainedPath Resolve (string rootPath, string relativePath)
        {
            AbsolutePath root = AbsolutePath.Parse(rootPath);
            RootRelativePath relative = RootRelativePath.Parse(relativePath);
            return ContainedPath.Create(root, relative);
        }
    }
}
EOF
consumer_restore_args=(
  --configfile "${consumer_nuget_config}"
  --no-cache
  --force-evaluate
  --verbosity minimal
)
dotnet restore "${consumer_project_path}" "${consumer_restore_args[@]}"

if [[ -n "${filesystem_package_source}" ]]; then
  restored_filesystem_package="${consumer_nuget_packages}/mackysoft.filesystem/${filesystem_package_version}/mackysoft.filesystem.${filesystem_package_version}.nupkg"
  restored_filesystem_metadata="${consumer_nuget_packages}/mackysoft.filesystem/${filesystem_package_version}/.nupkg.metadata"
  if [[ ! -f "${restored_filesystem_package}" ]]; then
    echo "Restored filesystem package was not found in the isolated global package cache." >&2
    exit 1
  fi

  if ! cmp -s \
    "${filesystem_package_source}/${filesystem_package_file_name}" \
    "${restored_filesystem_package}"; then
    echo "Restored filesystem package does not match the supplied prepublication package." >&2
    exit 1
  fi

  FILESYSTEM_METADATA_PATH="${restored_filesystem_metadata}" \
  EXPECTED_FILESYSTEM_SOURCE="${consumer_filesystem_package_source}" \
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

dotnet build "${consumer_project_path}" \
  --configuration Release \
  --no-restore \
  --verbosity minimal

echo "Shared package verification passed: ${package_dir}"
