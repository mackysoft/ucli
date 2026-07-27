#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/.." && pwd)"
project_path="$repository_root/tools/Ucli.SchemaGenerator/Ucli.SchemaGenerator.csproj"
expected_reference="../../src/Ucli/Ucli.csproj"
references=()

while IFS= read -r reference; do
  references+=("$reference")
done < <(
  sed -nE \
    's/^[[:space:]]*<ProjectReference[[:space:]]+Include="([^"]+)"[^>]*\/?>[[:space:]]*$/\1/p' \
    "$project_path"
)

if [ "${#references[@]}" -ne 1 ] || [ "${references[0]}" != "$expected_reference" ]; then
  echo "Ucli.SchemaGenerator must directly reference only $expected_reference." >&2
  printf 'Actual direct ProjectReference: %s\n' "${references[@]:-<none>}" >&2
  exit 1
fi

echo "Ucli.SchemaGenerator direct ProjectReference verified: $expected_reference"
