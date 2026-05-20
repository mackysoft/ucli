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
    dependency_versions="$(
      perl -ne '
        while (/<dependency\b([^>]*)>/g) {
          my $attributes = $1;
          next unless $attributes =~ /\bid="MackySoft\.Ucli\.Contracts"/;
          if ($attributes =~ /\bversion="([^"]+)"/) {
            print "$1\n";
          }
        }
      ' "${nuspec_path}"
    )"

    if [[ -z "${dependency_versions}" ]]; then
      echo "MackySoft.Ucli.Infrastructure is missing dependency: MackySoft.Ucli.Contracts." >&2
      exit 1
    fi

    unexpected_dependency_versions="$(
      grep -Fvx "${expected_version}" <<< "${dependency_versions}" || true
    )"
    if [[ -n "${unexpected_dependency_versions}" ]]; then
      echo "MackySoft.Ucli.Infrastructure dependency version does not match ${expected_version}." >&2
      printf '%s\n' "${unexpected_dependency_versions}" >&2
      exit 1
    fi
  fi
done

echo "Shared package verification passed: ${package_dir}"
