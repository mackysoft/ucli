#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
Usage: scripts/create-version-sync-pr.sh --repository <owner/repo> --default-branch <branch> --sync-branch <branch> --commit-message <message> --pr-title <title> (--pr-body <body> | --pr-body-file <path>) --changed-path <path>... [--verify-workflow <workflow>]

Commits prepared version sync changes, pushes the sync branch, creates or reuses the pull request, and dispatches verification.
EOF
}

repository=""
default_branch=""
sync_branch=""
commit_message=""
pr_title=""
pr_body=""
pr_body_file=""
verify_workflow="verify.yaml"
changed_paths=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repository)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      repository="$2"
      shift 2
      ;;
    --default-branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      default_branch="$2"
      shift 2
      ;;
    --sync-branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      sync_branch="$2"
      shift 2
      ;;
    --commit-message)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      commit_message="$2"
      shift 2
      ;;
    --pr-title)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      pr_title="$2"
      shift 2
      ;;
    --pr-body)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      pr_body="$2"
      shift 2
      ;;
    --pr-body-file)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      pr_body_file="$2"
      shift 2
      ;;
    --verify-workflow)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      verify_workflow="$2"
      shift 2
      ;;
    --changed-path)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      changed_paths+=("$2")
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

if [[ -z "${repository}" || -z "${default_branch}" || -z "${sync_branch}" || -z "${commit_message}" || -z "${pr_title}" || ${#changed_paths[@]} -eq 0 ]]; then
  usage
  exit 2
fi

if [[ -n "${pr_body}" && -n "${pr_body_file}" ]]; then
  echo "--pr-body and --pr-body-file are mutually exclusive." >&2
  exit 2
fi

if [[ -n "${pr_body_file}" ]]; then
  if [[ ! -f "${pr_body_file}" ]]; then
    echo "Pull request body file does not exist: ${pr_body_file}" >&2
    exit 1
  fi

  pr_body="$(cat "${pr_body_file}")"
fi

if [[ -z "${pr_body}" ]]; then
  echo "Pull request body is required." >&2
  exit 2
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

if git diff --quiet -- "${changed_paths[@]}"; then
  echo "No repository version sync required."
  exit 0
fi

git add "${changed_paths[@]}"
git commit -m "${commit_message}"

if git ls-remote --exit-code --heads origin "${sync_branch}" >/dev/null; then
  git fetch origin "${sync_branch}:refs/remotes/origin/${sync_branch}"
  remote_sync_sha="$(git rev-parse "refs/remotes/origin/${sync_branch}")"
  git push --force-with-lease="refs/heads/${sync_branch}:${remote_sync_sha}" origin "HEAD:${sync_branch}"
else
  git push origin "HEAD:${sync_branch}"
fi

existing_pr_url="$(gh pr list \
  --repo "${repository}" \
  --state open \
  --head "${sync_branch}" \
  --json url \
  --jq '.[0].url // empty')"
if [[ -n "${existing_pr_url}" ]]; then
  echo "Version sync pull request already exists: ${existing_pr_url}"
else
  gh pr create \
    --repo "${repository}" \
    --base "${default_branch}" \
    --head "${sync_branch}" \
    --title "${pr_title}" \
    --body "${pr_body}"
fi

gh workflow run "${verify_workflow}" \
  --repo "${repository}" \
  --ref "${sync_branch}"
