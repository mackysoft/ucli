#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/verify.sh [--no-restore] [--solution <path>] [--configuration <name>] [--filesystem-package-source <dir>] [--include-unity --project-path <path> --assembly-name <name> [test-unity-options...]] [-- dotnet-test-options...]" >&2
}

restore=true
solution_arg=""
configuration="Release"
filesystem_package_source="${FILESYSTEM_PACKAGE_SOURCE:-}"
include_unity=false
test_args=()
unity_args=()

while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-restore)
      restore=false
      shift
      ;;
    --solution)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      solution_arg="$2"
      shift 2
      ;;
    --solution=*)
      solution_arg="${1#--solution=}"
      shift
      ;;
    --configuration)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      configuration="$2"
      shift 2
      ;;
    --configuration=*)
      configuration="${1#--configuration=}"
      shift
      ;;
    --filesystem-package-source)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      filesystem_package_source="$2"
      shift 2
      ;;
    --filesystem-package-source=*)
      filesystem_package_source="${1#--filesystem-package-source=}"
      shift
      ;;
    --include-unity)
      include_unity=true
      shift
      unity_args=("$@")
      break
      ;;
    --)
      shift
      test_args=("$@")
      break
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [ -z "$configuration" ]; then
  usage
  exit 2
fi

if [ "$include_unity" = true ]; then
  unity_arg_index=0
  while [ "$unity_arg_index" -lt "${#unity_args[@]}" ]; do
    unity_arg="${unity_args[$unity_arg_index]}"
    case "$unity_arg" in
      --filesystem-package-source)
        unity_arg_index=$((unity_arg_index + 1))
        if [ "$unity_arg_index" -ge "${#unity_args[@]}" ]; then
          usage
          exit 2
        fi
        filesystem_package_source="${unity_args[$unity_arg_index]}"
        ;;
      --filesystem-package-source=*)
        filesystem_package_source="${unity_arg#--filesystem-package-source=}"
        ;;
    esac
    unity_arg_index=$((unity_arg_index + 1))
  done
fi

if [ -n "$filesystem_package_source" ] && [ "$restore" != true ]; then
  echo "--filesystem-package-source requires restore; remove --no-restore." >&2
  exit 2
fi

solution="$(dotnet_resolve_solution "$solution_arg")"
cd "$DOTNET_REPO_ROOT"

if [ "$restore" = true ]; then
  if [ -n "$filesystem_package_source" ]; then
    if [ ! -d "$filesystem_package_source" ]; then
      echo "filesystem package source not found: $filesystem_package_source" >&2
      exit 1
    fi

    filesystem_package_source="$(cd "$filesystem_package_source" && pwd)"
    filesystem_package_name="MackySoft.FileSystem.0.1.0.nupkg"
    filesystem_package_path="${filesystem_package_source}/${filesystem_package_name}"
    if [ ! -f "$filesystem_package_path" ]; then
      echo "filesystem package not found: $filesystem_package_path" >&2
      exit 1
    fi

    prepublication_restore_root="$(mktemp -d "${TMPDIR:-/tmp}/ucli-verify-filesystem.XXXXXX")"
    trap 'rm -rf "${prepublication_restore_root}"' EXIT
    isolated_filesystem_source="${prepublication_restore_root}/filesystem-source"
    isolated_nuget_packages="${prepublication_restore_root}/global-packages"
    isolated_nuget_http_cache="${prepublication_restore_root}/http-cache"
    isolated_nuget_config="${prepublication_restore_root}/NuGet.config"
    mkdir -p \
      "$isolated_filesystem_source" \
      "$isolated_nuget_packages" \
      "$isolated_nuget_http_cache"
    cp "$filesystem_package_path" "${isolated_filesystem_source}/${filesystem_package_name}"

    cat > "$isolated_nuget_config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="FileSystemCandidate" value="./filesystem-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="FileSystemCandidate">
      <package pattern="MackySoft.FileSystem" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="ConsoleAppFramework*" />
      <package pattern="MackySoft.AgentSkills*" />
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="Newtonsoft.Json" />
      <package pattern="System.*" />
      <package pattern="coverlet.*" />
      <package pattern="runtime.*" />
      <package pattern="xunit*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

    export NUGET_PACKAGES
    NUGET_PACKAGES="$(dotnet_to_native_path "$isolated_nuget_packages")"
    export NUGET_HTTP_CACHE_PATH="$isolated_nuget_http_cache"

    while IFS= read -r -d '' build_output_directory; do
      rm -rf "$build_output_directory"
    done < <(
      find src tests \
        -type d \
        \( -name bin -o -name obj \) \
        -print0
    )
    dotnet restore "$solution" \
      --configfile "$isolated_nuget_config" \
      --no-cache \
      --force-evaluate

    restored_filesystem_package="${isolated_nuget_packages}/mackysoft.filesystem/0.1.0/mackysoft.filesystem.0.1.0.nupkg"
    if [ ! -f "$restored_filesystem_package" ] \
      || ! cmp -s "$filesystem_package_path" "$restored_filesystem_package"; then
      echo "solution restore did not resolve the supplied ${filesystem_package_name}." >&2
      exit 1
    fi
  else
    dotnet_restore_with_local_packages "$solution"
  fi
fi

bash scripts/verify-skills.sh
bash scripts/verify-schemas.sh
bash scripts/code-quality.sh --no-restore --solution "$solution" verify
dotnet build "$solution" --configuration "$configuration" --no-restore
test_dotnet_args=(
  --no-restore
  --solution "$solution"
  --configuration "$configuration"
  --no-build
)
if [ "${#test_args[@]}" -gt 0 ]; then
  test_dotnet_args+=("${test_args[@]}")
fi
bash scripts/test-dotnet.sh "${test_dotnet_args[@]}"

if [ -n "$filesystem_package_source" ]; then
  prepublication_cli_package_dir="${prepublication_restore_root}/cli-package"
  mkdir -p "$prepublication_cli_package_dir"
  prepublication_cli_version="$(
    dotnet msbuild src/Ucli/Ucli.csproj -getProperty:PackageVersion -nologo |
      tail -n 1
  )"
  if [ -z "$prepublication_cli_version" ]; then
    echo "failed to resolve the CLI package version." >&2
    exit 1
  fi

  dotnet pack src/Ucli/Ucli.csproj \
    --configuration "$configuration" \
    --no-restore \
    --output "$prepublication_cli_package_dir" \
    -p:Version="$prepublication_cli_version" \
    -p:PackageVersion="$prepublication_cli_version"
  bash scripts/verify-cli-package.sh \
    "$prepublication_cli_package_dir" \
    "$prepublication_cli_version" \
    --filesystem-package-source "$filesystem_package_source"
fi

if [ "$include_unity" = true ]; then
  test_unity_args=(--configuration "$configuration")
  if [ -n "$filesystem_package_source" ]; then
    test_unity_args+=(--filesystem-package-source "$filesystem_package_source")
  fi
  if [ "${#unity_args[@]}" -gt 0 ]; then
    test_unity_args+=("${unity_args[@]}")
  fi
  bash scripts/test-unity.sh "${test_unity_args[@]}"
fi
