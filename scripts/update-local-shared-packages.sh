#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage:
  scripts/update-local-shared-packages.sh [--repo-root <path>] [--prune]

Description:
  1. Read all shared package versions from src/Ucli.Unity/Assets/packages.config.
  2. Require uCLI-owned packages to use the same version and external vocabularies to use 0.1.0.
  3. Restore external dependencies, then pack only the uCLI-owned shared package projects.
  4. Restore src/Ucli.Unity/Assets/packages.config via src/Ucli.Unity/Assets/NuGet.config.
  5. Remove NuGet placeholder .meta files so Unity can regenerate valid importer settings.
  6. Optionally prune multi-target assets to avoid Unity duplicate-assembly issues.
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

nuget_command=()
if command -v nuget >/dev/null 2>&1; then
  nuget_command=(nuget)
elif command -v nuget.exe >/dev/null 2>&1; then
  nuget_command=(nuget.exe)
else
  echo "ERROR: nuget or nuget.exe command is required." >&2
  exit 1
fi

external_package_ids=(
  "MackySoft.Text.Vocabularies"
  "MackySoft.Text.Vocabularies.Json"
)
package_ids=(
  "MackySoft.Ucli.Contracts"
  "MackySoft.Ucli.Infrastructure"
)
package_projects=(
  "${repository_root}/src/Ucli.Contracts/Ucli.Contracts.csproj"
  "${repository_root}/src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"
)
unity_packages_config="${repository_root}/src/Ucli.Unity/Assets/packages.config"
unity_nuget_config="${repository_root}/src/Ucli.Unity/Assets/NuGet.config"
local_package_source="${repository_root}/src/Ucli.Unity/Packages/nuget-local-source"
unity_packages_dir="${repository_root}/src/Ucli.Unity/Assets/Packages"

for required_path in "${package_projects[@]}" "${unity_packages_config}" "${unity_nuget_config}"; do
  if [[ ! -f "${required_path}" ]]; then
    echo "ERROR: Required file not found: ${required_path}" >&2
    exit 1
  fi
done

read_package_version() {
  local package_id="$1"

  sed -nE "s#.*<package id=\"${package_id}\" version=\"([^\"]+)\".*#\\1#p" "${unity_packages_config}" | head -n 1
}

external_package_version="0.1.0"
for package_id in "${external_package_ids[@]}"; do
  package_version="$(read_package_version "${package_id}")"
  if [[ "${package_version}" != "${external_package_version}" ]]; then
    echo "ERROR: ${package_id} must use fixed external version ${external_package_version}. Actual: ${package_version}" >&2
    exit 1
  fi
done

shared_package_version=""
for package_id in "${package_ids[@]}"; do
  package_version="$(read_package_version "${package_id}")"
  if [[ -z "${package_version}" ]]; then
    echo "ERROR: Failed to resolve ${package_id} version from ${unity_packages_config}" >&2
    exit 1
  fi

  if [[ -z "${shared_package_version}" ]]; then
    shared_package_version="${package_version}"
  elif [[ "${package_version}" != "${shared_package_version}" ]]; then
    echo "ERROR: Shared package versions must match. Expected ${shared_package_version}, ${package_id}=${package_version}" >&2
    exit 1
  fi
done

echo "[1/6] Restore uCLI shared package projects"
mkdir -p "${local_package_source}"
for package_project in "${package_projects[@]}"; do
  dotnet restore "${package_project}" \
    --source "${local_package_source}" \
    --source https://api.nuget.org/v3/index.json
done

echo "[2/6] Pack uCLI shared packages ${shared_package_version} to local source"
for package_project in "${package_projects[@]}"; do
  dotnet pack "${package_project}" \
    --configuration Release \
    --output "${local_package_source}" \
    --no-restore \
    -p:Version="${shared_package_version}" \
    -p:PackageVersion="${shared_package_version}"
done

echo "[3/6] Reset Unity NuGet restore outputs"
# NOTE:
# `Assets/Packages` and NuGetForUnity caches are generated restore outputs. Recreating the directory
# avoids stale package versions lingering when `nuget restore` updates packages additively or when
# prior restores used a different casing for the extracted directory name.
rm -rf "${unity_packages_dir}"
mkdir -p "${unity_packages_dir}"
rm -rf "${repository_root}/src/Ucli.Unity/.nuget-cache"
rm -rf "${repository_root}/src/Ucli.Unity/.nuget-packages"

echo "[4/6] Restore Unity packages.config from local/source feeds"
"${nuget_command[@]}" restore "${unity_packages_config}" \
  -PackagesDirectory "${unity_packages_dir}" \
  -ConfigFile "${unity_nuget_config}" \
  -NoCache \
  -NonInteractive

echo "[5/6] Remove NuGet placeholder .meta files from restored Unity packages"
# NOTE:
# `nuget restore` creates minimal `.meta` files for package assets. Fresh worktrees can then fail
# to resolve shared package DLLs until Unity regenerates proper PluginImporter metadata.
# Removing these generated `.meta` files here lets the next Unity launch recreate them officially.
find "${unity_packages_dir}" -type f -name '*.meta' -delete
find "${unity_packages_dir}" -depth -type d -empty -delete

if [[ "${prune_assets}" == "true" ]]; then
  echo "[6/6] Prune multi-target assets to prevent duplicate assembly imports"
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
  echo "[6/6] Skip prune step (use --prune to enable)"
fi

echo "Completed local package refresh for uCLI shared packages ${shared_package_version}; vocabularies ${external_package_version}"
