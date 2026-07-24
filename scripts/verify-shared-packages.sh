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
  echo "Shared package directory does not exist: ${package_dir}" >&2
  exit 1
fi

for required_tool in unzip; do
  if ! command -v "${required_tool}" >/dev/null 2>&1; then
    echo "Required tool is missing: ${required_tool}" >&2
    exit 1
  fi
done

package_dir="$(cd "${package_dir}" && pwd)"
filesystem_package_id="MackySoft.FileSystem"
filesystem_package_version="0.1.0"

temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT
foundation_dependency_version_range="[0.1.0]"
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
      required_dependencies=(
        "MackySoft.Text.Vocabularies"
        "MackySoft.Text.Vocabularies.Json"
      )
      ;;
    MackySoft.Ucli.Infrastructure)
      required_library_entries=(
        "lib/netstandard2.1/MackySoft.Ucli.Infrastructure.dll"
        "lib/net8.0/MackySoft.Ucli.Infrastructure.dll"
      )
      required_dependencies=(
        "MackySoft.FileSystem"
        "MackySoft.Ucli.Contracts"
        "MackySoft.Text.Vocabularies.Json"
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

  for dependency_id in "${required_dependencies[@]}"; do
    dependency_versions="$(
      DEPENDENCY_ID="${dependency_id}" perl -ne '
        my $dependency_id = $ENV{"DEPENDENCY_ID"};
        while (/<dependency\b([^>]*)>/g) {
          my $attributes = $1;
          next unless $attributes =~ /\bid="([^"]+)"/;
          next unless $1 eq $dependency_id;
          if ($attributes =~ /\bversion="([^"]+)"/) {
            print "$1\n";
          }
        }
        ' "${nuspec_path}"
      )"

    if [[ -z "${dependency_versions}" ]]; then
      echo "${package_id} is missing dependency: ${dependency_id}." >&2
      exit 1
    fi

    case "${dependency_id}" in
      MackySoft.FileSystem|MackySoft.Text.Vocabularies|MackySoft.Text.Vocabularies.Json)
        required_dependency_version="${foundation_dependency_version_range}"
        ;;
      *)
        required_dependency_version="${expected_version}"
        ;;
    esac

    unexpected_dependency_versions="$(
      grep -Fvx "${required_dependency_version}" <<< "${dependency_versions}" || true
    )"
    if [[ -n "${unexpected_dependency_versions}" ]]; then
      echo "${package_id} dependency ${dependency_id} does not match ${required_dependency_version}." >&2
      printf '%s\n' "${unexpected_dependency_versions}" >&2
      exit 1
    fi
  done
done

consumer_dir="${temp_dir}/filesystem-consumer"
consumer_project_path="${consumer_dir}/FileSystemPackageConsumer.csproj"
consumer_dotnet_home="${temp_dir}/dotnet-home"
consumer_nuget_packages="${temp_dir}/nuget-packages"
consumer_nuget_http_cache="${temp_dir}/http-cache"
consumer_ucli_package_source="${temp_dir}/ucli-source"
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

cat > "${consumer_nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="UcliPackages" value="./ucli-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
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
EXPECTED_VERSION="${expected_version}" perl -0pi -e '
  my $version = $ENV{"EXPECTED_VERSION"};
  s{<TargetFramework>netstandard2\.1</TargetFramework>}{<TargetFrameworks>net8.0;netstandard2.1</TargetFrameworks>};
  s{</Project>}{  <ItemGroup>\n    <PackageReference Include="MackySoft.Ucli.Infrastructure" Version="$version" />\n  </ItemGroup>\n</Project>};
' "${consumer_project_path}"
cat > "${consumer_dir}/Class1.cs" <<'EOF'
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Ipc;

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

        public static void UseUcliPublicTypes ()
        {
            _ = typeof(ScreenshotArtifactKind);
            _ = typeof(IpcFrameCodec);
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

dotnet build "${consumer_project_path}" \
  --configuration Release \
  --no-restore \
  --verbosity minimal

echo "Shared package verification passed: ${package_dir}"
