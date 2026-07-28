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
json_schema_package_id="MackySoft.JsonSchema.Generation"
json_schema_dependency_version_range="[0.3.1]"

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
        "${json_schema_package_id}"
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
      MackySoft.JsonSchema.Generation)
        required_dependency_version="${json_schema_dependency_version_range}"
        ;;
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

consumer_dir="${temp_dir}/shared-package-consumer"
consumer_project_path="${consumer_dir}/UcliSharedPackageConsumer.csproj"
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
      <package pattern="MackySoft.Json.Canonicalization" />
      <package pattern="MackySoft.JsonSchema.Generation" />
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
  --name UcliSharedPackageConsumer \
  --output "${consumer_dir}" \
  --framework netstandard2.1 \
  --no-restore \
  >/dev/null
EXPECTED_VERSION="${expected_version}" perl -0pi -e '
  my $version = $ENV{"EXPECTED_VERSION"};
  s{<TargetFramework>netstandard2\.1</TargetFramework>}{<TargetFrameworks>net8.0;netstandard2.1</TargetFrameworks>\n    <OutputType Condition="&apos;\$(TargetFramework)&apos; == &apos;net8.0&apos;">Exe</OutputType>};
  s{</Project>}{  <ItemGroup>\n    <PackageReference Include="MackySoft.Ucli.Infrastructure" Version="$version" />\n  </ItemGroup>\n</Project>};
' "${consumer_project_path}"
cat > "${consumer_dir}/Class1.cs" <<'EOF'
using System;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace UcliSharedPackageConsumer
{
    public static class PackageConsumer
    {
        public static int Main ()
        {
            var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
            var generationResult = UcliOperationJsonContractGenerator.Generate(
                "scene.tree",
                serializerOptions.GetTypeInfo(typeof(SceneTreeArgs)),
                resultTypeInfo: null);
            var argsContract = generationResult.ArgsContract;
            var schemaUtf8 = generationResult.GetArgsJsonSchemaUtf8();
            var typeMetadataText = argsContract.TypeMetadata.GetRawText();
            if (string.IsNullOrWhiteSpace(argsContract.ContractDigest.ToString())
                || schemaUtf8.Length == 0
                || string.IsNullOrWhiteSpace(typeMetadataText))
            {
                return 1;
            }

            var publicationTime = new DateTimeOffset(
                2026,
                7,
                28,
                12,
                34,
                56,
                TimeSpan.Zero);
            ArtifactRef terminalRecord = new PathArtifactRef(
                new ArtifactKind("packageConsumer.terminalRecord"),
                new ArtifactMediaType("application/json"),
                new ArtifactPath(".ucli/local/package-consumer/terminal-record.json"),
                Sha256Digest.Compute(new byte[] { 1, 2, 3 }),
                sizeBytes: 3,
                publicationTime);
            ExecutionRef execution = new TerminalExecutionRef(
                new ExecutionKind("packageConsumer"),
                new Guid("8b8b657d-f631-4509-af40-88f6af40f53b"),
                Sha256Digest.Compute(new byte[] { 4, 5, 6 }),
                new ExecutionState("completed"),
                statusLocator: null,
                terminalRecord);
            var executionJson = JsonSerializer.Serialize(
                execution,
                IpcJsonSerializerOptions.StrictPropertyNames);
            var roundTrippedExecution = JsonSerializer.Deserialize<ExecutionRef>(
                executionJson,
                IpcJsonSerializerOptions.StrictPropertyNames);
            if (!execution.Equals(roundTrippedExecution))
            {
                return 1;
            }

            using var executionDocument = JsonDocument.Parse(executionJson);
            var serializedLifecycle = executionDocument.RootElement
                .GetProperty("lifecycle")
                .GetString();
            Console.WriteLine($"contractDigest={argsContract.ContractDigest}");
            Console.WriteLine($"schemaBytes={schemaUtf8.Length}");
            Console.WriteLine($"typeMetadataCharacters={typeMetadataText.Length}");
            Console.WriteLine($"artifactKind={terminalRecord.Kind}");
            Console.WriteLine($"executionLifecycle={serializedLifecycle}");
            return 0;
        }

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

consumer_output="$(
  dotnet run \
    --project "${consumer_project_path}" \
    --framework net8.0 \
    --configuration Release \
    --no-restore \
    --no-build
)"
for expected_output_pattern in \
  '^contractDigest=[^[:space:]]+$' \
  '^schemaBytes=[1-9][0-9]*$' \
  '^typeMetadataCharacters=[1-9][0-9]*$' \
  '^artifactKind=packageConsumer\.terminalRecord$' \
  '^executionLifecycle=terminal$'; do
  if ! grep -Eq "${expected_output_pattern}" <<< "${consumer_output}"; then
    echo "Shared package consumer did not observe the generated contract output matching ${expected_output_pattern}." >&2
    printf '%s\n' "${consumer_output}" >&2
    exit 1
  fi
done

cat > "${consumer_dir}/ExternalDerivedReferences.cs" <<'EOF'
using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;

namespace UcliSharedPackageConsumer
{
    public sealed record ExternalArtifactRef : ArtifactRef
    {
        public ExternalArtifactRef ()
            : base(
                new ArtifactKind("external"),
                new ArtifactMediaType("application/octet-stream"),
                Sha256Digest.Compute(Array.Empty<byte>()),
                sizeBytes: 0,
                DateTimeOffset.UnixEpoch)
        {
        }
    }

    public sealed record ExternalExecutionRef : ExecutionRef
    {
        public ExternalExecutionRef ()
            : base(
                new ExecutionKind("external"),
                Guid.Empty,
                Sha256Digest.Compute(Array.Empty<byte>()),
                new ExecutionState("running"),
                statusLocator: null)
        {
        }
    }
}
EOF
closed_union_build_log="${temp_dir}/closed-union-build.log"
if dotnet build "${consumer_project_path}" \
  --framework net8.0 \
  --configuration Release \
  --no-restore \
  --verbosity minimal \
  >"${closed_union_build_log}" 2>&1; then
  echo "External package consumer unexpectedly derived from the closed reference unions." >&2
  exit 1
fi
for expected_external_type in \
  'ExternalArtifactRef' \
  'ExternalExecutionRef'; do
  if ! grep -F "${expected_external_type}" "${closed_union_build_log}" >/dev/null; then
    echo "Closed reference union compile failure did not identify ${expected_external_type}." >&2
    cat "${closed_union_build_log}" >&2
    exit 1
  fi
done

printf '%s\n' "${consumer_output}"
echo "Shared package verification passed: ${package_dir}"
