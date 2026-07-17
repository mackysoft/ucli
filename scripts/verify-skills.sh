#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

cd "$DOTNET_REPO_ROOT"
dotnet tool restore >/dev/null
dotnet tool run agent-skills -- build --root skills --check

echo "Generated skills are up to date."
