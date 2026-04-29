#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/setup-nuget-cli.sh [--version <nuget-version>]

Installs a NuGet CLI wrapper for Linux and macOS GitHub Actions runners.
EOF
}

nuget_version="${NUGET_CLI_VERSION:-7.3.0}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      nuget_version="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

runner_os="${RUNNER_OS:-$(uname -s)}"
case "${runner_os}" in
  Linux)
    if ! command -v mono >/dev/null 2>&1; then
      export DEBIAN_FRONTEND=noninteractive
      sudo apt-get update
      sudo apt-get install --yes mono-complete
    fi
    ;;
  macOS|Darwin)
    if ! command -v mono >/dev/null 2>&1; then
      if ! command -v brew >/dev/null 2>&1; then
        echo "Homebrew is required to install Mono on macOS runners." >&2
        exit 1
      fi

      brew install mono
    fi
    ;;
  *)
    echo "setup-nuget-cli.sh supports Linux and macOS runners only. Actual: ${runner_os}" >&2
    exit 2
    ;;
esac

mono --version | head -n 1

runner_temp="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
nuget_dir="${runner_temp}/nuget"
mkdir -p "${nuget_dir}"

curl --fail --location --silent --show-error \
  "https://dist.nuget.org/win-x86-commandline/v${nuget_version}/nuget.exe" \
  --output "${nuget_dir}/nuget.exe"

cat > "${nuget_dir}/nuget" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
mono "$(dirname "$0")/nuget.exe" "$@"
EOF
chmod +x "${nuget_dir}/nuget"

if [[ -n "${GITHUB_PATH:-}" ]]; then
  echo "${nuget_dir}" >> "${GITHUB_PATH}"
else
  echo "NuGet CLI wrapper created at ${nuget_dir}/nuget"
fi
