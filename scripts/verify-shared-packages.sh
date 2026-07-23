#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <package-dir> <expected-version>" >&2
  exit 2
fi

package_dir="$1"
expected_version="$2"

if [[ ! -d "${package_dir}" ]]; then
  echo "Shared package directory does not exist: ${package_dir}" >&2
  exit 1
fi

if ! command -v unzip >/dev/null 2>&1; then
  echo "Required tool is missing: unzip" >&2
  exit 1
fi

package_dir="$(cd "${package_dir}" && pwd)"
temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT
external_vocabulary_version="0.1.0"
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
      MackySoft.Text.Vocabularies|MackySoft.Text.Vocabularies.Json)
        required_dependency_version="${external_vocabulary_version}"
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

consumer_dir="${temp_dir}/consumer"
export DOTNET_CLI_HOME="${temp_dir}/dotnet-home"
export NUGET_PACKAGES="${temp_dir}/nuget-packages"
mkdir -p "${DOTNET_CLI_HOME}" "${NUGET_PACKAGES}"

dotnet new classlib --output "${consumer_dir}" --no-restore >/dev/null
EXPECTED_VERSION="${expected_version}" perl -0pi -e '
  my $version = $ENV{"EXPECTED_VERSION"};
  s{</Project>}{  <ItemGroup>\n    <PackageReference Include="MackySoft.Ucli.Contracts" Version="$version" />\n    <PackageReference Include="MackySoft.Ucli.Infrastructure" Version="$version" />\n  </ItemGroup>\n</Project>};
' "${consumer_dir}/consumer.csproj"
cat > "${consumer_dir}/UcliSharedPackageConsumer.cs" <<'CS'
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Ipc;

public static class UcliSharedPackageConsumer
{
    public static void UsePublicTypes ()
    {
        _ = typeof(ScreenshotArtifactKind);
        _ = typeof(IpcFrameCodec);
    }
}
CS
dotnet restore "${consumer_dir}/consumer.csproj" \
  --source "${package_dir}" \
  --source https://api.nuget.org/v3/index.json
dotnet build "${consumer_dir}/consumer.csproj" --configuration Release --no-restore

echo "Shared package verification passed: ${package_dir}"
