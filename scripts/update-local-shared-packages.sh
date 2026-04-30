#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage:
  scripts/update-local-shared-packages.sh [--repo-root <path>] [--prune]

Description:
  1. Read MackySoft.Ucli.Contracts and MackySoft.Ucli.Infrastructure versions from src/Ucli.Unity/Assets/packages.config.
  2. Require both shared packages to use the same version.
  3. Restore and pack src/Ucli.Contracts/Ucli.Contracts.csproj.
  4. Restore and pack src/Ucli.Infrastructure/Ucli.Infrastructure.csproj.
  5. Restore src/Ucli.Unity/Assets/packages.config via src/Ucli.Unity/Assets/NuGet.config.
  6. Remove NuGet placeholder .meta files so Unity can regenerate valid importer settings.
  7. Optionally prune multi-target assets to avoid Unity duplicate-assembly issues.
EOF
}

repository_root=""
prune_assets="false"

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

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet command is required." >&2
  exit 1
fi

if ! command -v nuget >/dev/null 2>&1; then
  echo "ERROR: nuget command is required." >&2
  exit 1
fi

contracts_package_id="MackySoft.Ucli.Contracts"
infrastructure_package_id="MackySoft.Ucli.Infrastructure"
contracts_csproj="${repository_root}/src/Ucli.Contracts/Ucli.Contracts.csproj"
infrastructure_csproj="${repository_root}/src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"
unity_packages_config="${repository_root}/src/Ucli.Unity/Assets/packages.config"
unity_nuget_config="${repository_root}/src/Ucli.Unity/Assets/NuGet.config"
local_package_source="${repository_root}/src/Ucli.Unity/Packages/nuget-local-source"
unity_packages_dir="${repository_root}/src/Ucli.Unity/Assets/Packages"

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

contracts_package_version="$(read_package_version "${contracts_package_id}")"
infrastructure_package_version="$(read_package_version "${infrastructure_package_id}")"

if [[ -z "${contracts_package_version}" ]]; then
  echo "ERROR: Failed to resolve ${contracts_package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

if [[ -z "${infrastructure_package_version}" ]]; then
  echo "ERROR: Failed to resolve ${infrastructure_package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

if [[ "${contracts_package_version}" != "${infrastructure_package_version}" ]]; then
  echo "ERROR: Shared package versions must match. ${contracts_package_id}=${contracts_package_version}, ${infrastructure_package_id}=${infrastructure_package_version}" >&2
  exit 1
fi

shared_package_version="${contracts_package_version}"

echo "[1/8] Restore ${contracts_csproj}"
dotnet restore "${contracts_csproj}"

echo "[2/8] Restore ${infrastructure_csproj}"
dotnet restore "${infrastructure_csproj}"

echo "[3/8] Pack shared packages ${shared_package_version} to local source"
mkdir -p "${local_package_source}"
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

echo "[4/8] Reset Unity NuGet restore outputs"
# NOTE:
# `Assets/Packages` and NuGetForUnity caches are generated restore outputs. Recreating the directory
# avoids stale package versions lingering when `nuget restore` updates packages additively or when
# prior restores used a different casing for the extracted directory name.
rm -rf "${unity_packages_dir}"
mkdir -p "${unity_packages_dir}"
rm -rf "${repository_root}/src/Ucli.Unity/.nuget-cache"
rm -rf "${repository_root}/src/Ucli.Unity/.nuget-packages"

echo "[5/8] Restore Unity packages.config from local/source feeds"
nuget restore "${unity_packages_config}" \
  -PackagesDirectory "${unity_packages_dir}" \
  -ConfigFile "${unity_nuget_config}" \
  -NoCache \
  -NonInteractive

echo "[6/8] Remove NuGet placeholder .meta files from restored Unity packages"
# NOTE:
# `nuget restore` creates minimal `.meta` files for package assets. Fresh worktrees can then fail
# to resolve shared package DLLs until Unity regenerates proper PluginImporter metadata.
# Removing these generated `.meta` files here lets the next Unity launch recreate them officially.
find "${unity_packages_dir}" -type f -name '*.meta' -delete
find "${unity_packages_dir}" -depth -type d -empty -delete

if [[ "${prune_assets}" == "true" ]]; then
  echo "[7/8] Prune multi-target assets to prevent duplicate assembly imports"
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
  echo "[7/8] Skip prune step (use --prune to enable)"
fi

echo "[8/8] Completed local package refresh for shared packages ${shared_package_version}"
