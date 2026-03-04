#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage:
  scripts/update-local-contracts-package.sh [--repo-root <path>] [--prune]

Description:
  1. Read MackySoft.Ucli.Contracts version from src/Ucli.Unity/Assets/packages.config.
  2. Restore src/Ucli.Contracts/Ucli.Contracts.csproj.
  3. Pack src/Ucli.Contracts/Ucli.Contracts.csproj into src/Ucli.Unity/Packages/nuget-local-source.
  4. Remove extracted contracts package under src/Ucli.Unity/Assets/Packages.
  5. Restore src/Ucli.Unity/Assets/packages.config via src/Ucli.Unity/Assets/NuGet.config.
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

if ! command -v nuget >/dev/null 2>&1; then
  echo "ERROR: nuget command is required." >&2
  exit 1
fi

package_id="MackySoft.Ucli.Contracts"
contracts_csproj="${repository_root}/src/Ucli.Contracts/Ucli.Contracts.csproj"
unity_packages_config="${repository_root}/src/Ucli.Unity/Assets/packages.config"
unity_nuget_config="${repository_root}/src/Ucli.Unity/Assets/NuGet.config"
local_package_source="${repository_root}/src/Ucli.Unity/Packages/nuget-local-source"
unity_packages_dir="${repository_root}/src/Ucli.Unity/Assets/Packages"

for required_path in \
  "${contracts_csproj}" \
  "${unity_packages_config}" \
  "${unity_nuget_config}"; do
  if [[ ! -f "${required_path}" ]]; then
    echo "ERROR: Required file not found: ${required_path}" >&2
    exit 1
  fi
done

package_version="$(
  sed -nE "s#.*<package id=\"${package_id}\" version=\"([^\"]+)\".*#\\1#p" "${unity_packages_config}" | head -n 1
)"

if [[ -z "${package_version}" ]]; then
  echo "ERROR: Failed to resolve ${package_id} version from ${unity_packages_config}" >&2
  exit 1
fi

echo "[1/6] Restore ${contracts_csproj}"
dotnet restore "${contracts_csproj}"

echo "[2/6] Pack ${package_id} ${package_version} to local source"
mkdir -p "${local_package_source}"
dotnet pack "${contracts_csproj}" \
  --configuration Release \
  --output "${local_package_source}" \
  --no-restore \
  -p:PackageVersion="${package_version}"

echo "[3/6] Remove extracted ${package_id} package from Unity Assets/Packages"
rm -rf "${unity_packages_dir}/${package_id}."*

echo "[4/6] Restore Unity packages.config from local/source feeds"
nuget restore "${unity_packages_config}" \
  -PackagesDirectory "${unity_packages_dir}" \
  -ConfigFile "${unity_nuget_config}" \
  -NonInteractive

if [[ "${prune_assets}" == "true" ]]; then
  echo "[5/6] Prune multi-target assets to prevent duplicate assembly imports"
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
  echo "[5/6] Skip prune step (use --prune to enable)"
fi

echo "[6/6] Completed local package refresh for ${package_id} ${package_version}"
