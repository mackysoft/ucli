# Troubleshooting Workflow

Use this reference when a uCLI command fails or returns uncertain state.

## Boundary Order
1. Parse or argument failure: fix command options or JSON shape before involving Unity.
2. Project resolution failure: confirm `--projectPath`, environment, or current directory.
3. Daemon or IPC failure: inspect daemon status and daemon logs.
4. Lifecycle failure: inspect Unity readiness, compile state, reload state, play mode, and shutdown state.
5. readIndex failure: determine whether stale, missing, or disabled index data affects only reads.
6. Operation failure: run `ucli ops describe <opName>` and compare the request with the runtime contract.

## Retry Rules
- Do not retry mutating calls blindly after `IPC_TIMEOUT`, disconnect, reload, or crash.
- First check any returned `payload.opResults[]`.
- Then inspect Unity or daemon logs when the returned payload is incomplete.
- Do not use log scraping as proof of success or failure; prefer claim packets and machine-readable command output.
- Re-read the touched context before replaying a request.

## Output
- Name the failing boundary and the evidence.
- Provide one bounded next diagnostic command.
- State whether the current evidence proves applied, not applied, partially applied, or unknown.
