#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage:
  scripts/update-local-shared-packages.sh [--repo-root <path>] [--filesystem-package-source <dir>] [--shared-package-output <dir>] [--prune]

Description:
  1. Read MackySoft.Ucli.Contracts and MackySoft.Ucli.Infrastructure versions from src/Ucli.Unity/Assets/packages.config.
  2. Require the two uCLI-owned shared packages to use the same version.
  3. When an external MackySoft.FileSystem.0.1.0.nupkg is supplied, isolate it in a temporary source and empty global package cache.
  4. Restore and pack the two uCLI-owned shared packages, optionally copying them to a verification output.
  5. Restore src/Ucli.Unity/Assets/packages.config from separate provider, uCLI-owned, and nuget.org sources.
  6. Remove NuGet placeholder .meta files so Unity can regenerate valid importer settings.
  7. Optionally prune multi-target assets to avoid Unity duplicate-assembly issues.
EOF
}

repository_root=""
filesystem_package_source="${FILESYSTEM_PACKAGE_SOURCE:-}"
shared_package_output=""
prune_assets="false"
prepublication_restore_root=""

cleanup() {
  if [[ -n "${prepublication_restore_root}" && -d "${prepublication_restore_root}" ]]; then
    rm -rf "${prepublication_restore_root}"
  fi

  if [[ -n "${prepublication_restore_root}" && -n "${repository_root}" ]]; then
    rm -rf \
      "${repository_root}/src/Ucli.Contracts/bin" \
      "${repository_root}/src/Ucli.Contracts/obj" \
      "${repository_root}/src/Ucli.Infrastructure/bin" \
      "${repository_root}/src/Ucli.Infrastructure/obj"
  fi
}

trap cleanup EXIT

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo-root)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: --repo-root requires a path." >&2
        exit 1
      fi
      repository_root="$2"
      shift 2
      ;;
    --filesystem-package-source)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: --filesystem-package-source requires a directory." >&2
        exit 1
      fi
      filesystem_package_source="$2"
      shift 2
      ;;
    --filesystem-package-source=*)
      filesystem_package_source="${1#--filesystem-package-source=}"
      shift
      ;;
    --shared-package-output)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: --shared-package-output requires a directory." >&2
        exit 1
      fi
      shared_package_output="$2"
      shift 2
      ;;
    --shared-package-output=*)
      shared_package_output="${1#--shared-package-output=}"
      shift
      ;;
    --prune)
      prune_assets="true"
      shift
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      echo "ERROR: Unknown option '$1'." >&2
      print_usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "${repository_root}" ]]; then
  repository_root="$(git rev-parse --show-toplevel)"
fi

if [[ ! -d "${repository_root}" ]]; then
  echo "ERROR: Repository root not found: ${repository_root}" >&2
  exit 1
fi

if [[ -n "${shared_package_output}" ]]; then
  case "${shared_package_output}" in
    /*)
      ;;
    *)
      shared_package_output="${repository_root}/${shared_package_output}"
      ;;
  esac
  mkdir -p "${shared_package_output}"
  shared_package_output="$(cd "${shared_package_output}" && pwd)"
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet command is required." >&2
  exit 1
fi

nuget_command=()
if command -v nuget >/dev/null 2>&1; then
  nuget_command=(nuget)
elif command -v nuget.exe >/dev/null 2>&1; then
  nuget_command=(nuget.exe)
else
  echo "ERROR: nuget or nuget.exe command is required." >&2
  exit 1
fi

filesystem_package_id="MackySoft.FileSystem"
filesystem_package_version="0.1.0"
contracts_package_id="MackySoft.Ucli.Contracts"
infrastructure_package_id="MackySoft.Ucli.Infrastructure"
contracts_csproj="${repository_root}/src/Ucli.Contracts/Ucli.Contracts.csproj"
infrastructure_csproj="${repository_root}/src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"
unity_packages_config="${repository_root}/src/Ucli.Unity/Assets/packages.config"
unity_nuget_config="${repository_root}/src/Ucli.Unity/Assets/NuGet.config"
repository_local_package_source="${repository_root}/src/Ucli.Unity/Packages/nuget-local-source"
unity_packages_dir="${repository_root}/src/Ucli.Unity/Assets/Packages"
filesystem_package_file_name="${filesystem_package_id}.${filesystem_package_version}.nupkg"

for required_path in \
  "${contracts_csproj}" \
  "${infrastructure_csproj}" \
  "${unity_packages_config}" \
  "${unity_nuget_config}"; do
  if [[ ! -f "${required_path}" ]]; then
    echo "ERROR: Required file not found: ${required_path}" >&2
    exit 1
  fi
done

read_package_version() {
  local package_id="$1"

  sed -nE "s#.*<package id=\"${package_id}\" version=\"([^\"]+)\".*#\\1#p" "${unity_packages_config}" | head -n 1
}

configured_filesystem_package_version="$(read_package_version "${filesystem_package_id}")"
contracts_package_version="$(read_package_version "${contracts_package_id}")"
infrastructure_package_version="$(read_package_version "${infrastructure_package_id}")"

if [[ -z "${configured_filesystem_package_version}" ]]; then
  echo "ERROR: Failed to resolve ${filesystem_package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

if [[ -z "${contracts_package_version}" ]]; then
  echo "ERROR: Failed to resolve ${contracts_package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

if [[ -z "${infrastructure_package_version}" ]]; then
  echo "ERROR: Failed to resolve ${infrastructure_package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

if [[ "${configured_filesystem_package_version}" != "${filesystem_package_version}" ]]; then
  echo "ERROR: ${filesystem_package_id} must use fixed version ${filesystem_package_version}. Actual: ${configured_filesystem_package_version}" >&2
  exit 1
fi

if [[ "${contracts_package_version}" != "${infrastructure_package_version}" ]]; then
  echo "ERROR: uCLI-owned shared package versions must match. ${contracts_package_id}=${contracts_package_version}, ${infrastructure_package_id}=${infrastructure_package_version}" >&2
  exit 1
fi

shared_package_version="${contracts_package_version}"

echo "[1/9] Resolve external ${filesystem_package_id} ${filesystem_package_version}"
mkdir -p "${repository_local_package_source}"
# A candidate provider package must never become an implicit input to later restores.
find "${repository_local_package_source}" \
  -maxdepth 1 \
  -type f \
  -name "${filesystem_package_id}.*.nupkg" \
  -delete

local_package_source="${repository_local_package_source}"
active_unity_nuget_config="${unity_nuget_config}"
repository_nuget_packages="${repository_root}/src/Ucli.Unity/.nuget-packages"
dotnet_restore_args=()

if [[ -n "${filesystem_package_source}" ]]; then
  if [[ ! -d "${filesystem_package_source}" ]]; then
    echo "ERROR: Filesystem package source not found: ${filesystem_package_source}" >&2
    exit 1
  fi

  filesystem_package_source="$(cd "${filesystem_package_source}" && pwd)"
  external_filesystem_package="${filesystem_package_source}/${filesystem_package_file_name}"
  if [[ ! -f "${external_filesystem_package}" ]]; then
    echo "ERROR: External filesystem package not found: ${external_filesystem_package}" >&2
    exit 1
  fi

  prepublication_restore_root="$(mktemp -d "${TMPDIR:-/tmp}/ucli-filesystem-restore.XXXXXX")"
  isolated_filesystem_package_source="${prepublication_restore_root}/filesystem-source"
  isolated_ucli_package_source="${prepublication_restore_root}/ucli-source"
  isolated_nuget_packages="${prepublication_restore_root}/global-packages"
  active_unity_nuget_config="${prepublication_restore_root}/NuGet.config"
  mkdir -p \
    "${isolated_filesystem_package_source}" \
    "${isolated_ucli_package_source}" \
    "${isolated_nuget_packages}"
  cp "${external_filesystem_package}" \
    "${isolated_filesystem_package_source}/${filesystem_package_file_name}"

  cat > "${active_unity_nuget_config}" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="FileSystemCandidate" value="./filesystem-source" />
    <add key="UcliLocal" value="./ucli-source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="FileSystemCandidate">
      <package pattern="${filesystem_package_id}" />
    </packageSource>
    <packageSource key="UcliLocal">
      <package pattern="${contracts_package_id}" />
      <package pattern="${infrastructure_package_id}" />
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
  <config>
    <add key="packageInstallLocation" value="CustomWithinAssets" />
    <add key="repositoryPath" value="./Packages" />
    <add key="PackagesConfigDirectoryPath" value="." />
    <add key="slimRestore" value="true" />
    <add key="PreferNetStandardOverNetFramework" value="true" />
  </config>
</configuration>
EOF

  local_package_source="${isolated_ucli_package_source}"
  repository_nuget_packages="${isolated_nuget_packages}"
  dotnet_restore_args=(
    --configfile "${active_unity_nuget_config}"
    --no-cache
    --force-evaluate
  )

  for project_directory in \
    "$(dirname "${contracts_csproj}")" \
    "$(dirname "${infrastructure_csproj}")"; do
    rm -rf "${project_directory}/bin" "${project_directory}/obj"
  done

  echo "Using isolated ${filesystem_package_file_name} from ${filesystem_package_source}"
else
  dotnet_local_package_source="${repository_local_package_source}"
  if command -v cygpath >/dev/null 2>&1; then
    dotnet_local_package_source="$(cygpath -m "${repository_local_package_source}")"
  fi
  echo "Using ${filesystem_package_id} ${filesystem_package_version} from the configured local or public source."
fi

dotnet_nuget_packages="${repository_nuget_packages}"
if command -v cygpath >/dev/null 2>&1; then
  dotnet_nuget_packages="$(cygpath -m "${repository_nuget_packages}")"
fi
export NUGET_PACKAGES="${dotnet_nuget_packages}"

echo "[2/9] Restore ${contracts_csproj}"
if [[ -n "${filesystem_package_source}" ]]; then
  dotnet restore "${contracts_csproj}" "${dotnet_restore_args[@]}"
else
  dotnet restore "${contracts_csproj}"
fi

echo "[3/9] Restore ${infrastructure_csproj} from the local/source feeds"
if [[ -n "${filesystem_package_source}" ]]; then
  dotnet restore "${infrastructure_csproj}" \
    "${dotnet_restore_args[@]}" \
    -p:Version="${shared_package_version}" \
    -p:PackageVersion="${shared_package_version}"

  restored_filesystem_package="${isolated_nuget_packages}/mackysoft.filesystem/${filesystem_package_version}/mackysoft.filesystem.${filesystem_package_version}.nupkg"
  if [[ ! -f "${restored_filesystem_package}" ]] \
    || ! cmp -s "${external_filesystem_package}" "${restored_filesystem_package}"; then
    echo "ERROR: .NET restore did not resolve the supplied ${filesystem_package_file_name}." >&2
    exit 1
  fi
else
  dotnet restore "${infrastructure_csproj}" \
    "-p:RestoreAdditionalProjectSources=${dotnet_local_package_source}" \
    -p:Version="${shared_package_version}" \
    -p:PackageVersion="${shared_package_version}"
fi

echo "[4/9] Pack uCLI-owned shared packages ${shared_package_version} to local source"
dotnet pack "${contracts_csproj}" \
  --configuration Release \
  --output "${local_package_source}" \
  --no-restore \
  -p:Version="${shared_package_version}" \
  -p:PackageVersion="${shared_package_version}"
dotnet pack "${infrastructure_csproj}" \
  --configuration Release \
  --output "${local_package_source}" \
  --no-restore \
  -p:Version="${shared_package_version}" \
  -p:PackageVersion="${shared_package_version}"

if [[ -n "${shared_package_output}" ]]; then
  cp \
    "${local_package_source}/${contracts_package_id}.${contracts_package_version}.nupkg" \
    "${local_package_source}/${infrastructure_package_id}.${infrastructure_package_version}.nupkg" \
    "${shared_package_output}/"
fi

echo "[5/9] Reset Unity NuGet restore outputs"
# NOTE:
# `Assets/Packages` and NuGetForUnity caches are generated restore outputs. Recreating the directory
# avoids stale package versions lingering when `nuget restore` updates packages additively or when
# prior restores used a different casing for the extracted directory name.
rm -rf "${unity_packages_dir}"
mkdir -p "${unity_packages_dir}"
rm -rf "${repository_root}/src/Ucli.Unity/.nuget-cache"

echo "[6/9] Restore Unity packages.config from local/source feeds"
"${nuget_command[@]}" restore "${unity_packages_config}" \
  -PackagesDirectory "${unity_packages_dir}" \
  -ConfigFile "${active_unity_nuget_config}" \
  -NoCache \
  -NonInteractive

if [[ -n "${filesystem_package_source}" ]]; then
  restored_unity_filesystem_package="${unity_packages_dir}/${filesystem_package_id}.${filesystem_package_version}/${filesystem_package_file_name}"
  if [[ ! -f "${restored_unity_filesystem_package}" ]] \
    || ! cmp -s "${external_filesystem_package}" "${restored_unity_filesystem_package}"; then
    echo "ERROR: Unity restore did not resolve the supplied ${filesystem_package_file_name}." >&2
    exit 1
  fi
fi

echo "[7/9] Remove NuGet placeholder .meta files from restored Unity packages"
# NOTE:
# `nuget restore` creates minimal `.meta` files for package assets. Fresh worktrees can then fail
# to resolve shared package DLLs until Unity regenerates proper PluginImporter metadata.
# Removing these generated `.meta` files here lets the next Unity launch recreate them officially.
find "${unity_packages_dir}" -type f -name '*.meta' -delete
find "${unity_packages_dir}" -depth -type d -empty -delete

if [[ "${prune_assets}" == "true" ]]; then
  echo "[8/9] Prune multi-target assets to prevent duplicate assembly imports"
  find "${unity_packages_dir}" -type d -name analyzers -prune -exec rm -rf {} +
  find "${unity_packages_dir}" -type d -name runtimes -prune -exec rm -rf {} +
  find "${unity_packages_dir}" -type d \( -name build -o -name buildMultiTargeting -o -name buildTransitive \) -prune -exec rm -rf {} +

  while IFS= read -r -d '' package_dir; do
    lib_dir="${package_dir}/lib"
    if [[ ! -d "${lib_dir}" ]]; then
      continue
    fi

    keep_tfm=""
    for tfm in netstandard2.1 netstandard2.0 net462 net461; do
      if [[ -d "${lib_dir}/${tfm}" ]]; then
        keep_tfm="${tfm}"
        break
      fi
    done

    if [[ -n "${keep_tfm}" ]]; then
      find "${lib_dir}" -mindepth 1 -maxdepth 1 -type d ! -name "${keep_tfm}" -exec rm -rf {} +
    fi
  done < <(find "${unity_packages_dir}" -mindepth 1 -maxdepth 1 -type d -print0)
else
  echo "[8/9] Skip prune step (use --prune to enable)"
fi

echo "[9/9] Completed local package refresh for shared packages ${shared_package_version}"
