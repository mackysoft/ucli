# uCLI Verify Changes

Use this skill to verify whether a uCLI-backed Unity change actually reached the intended state.

## Workflow
1. Start from the command result, not from the exit code alone.
2. Inspect `payload.opResults[].applied`, `changed`, and `touched` for each public step.
3. If the task involves primitive operations, use `ucli ops describe <opName>` to interpret the operation's assurance and result contract.
4. If mutation returned `readPostcondition`, perform the required follow-up read before trusting affected surfaces.
5. Use targeted `ucli query`, `ucli resolve`, `ucli test run`, or `ucli logs` evidence based on the change.
6. Preserve the overall safe path: `read -> describe -> build request -> validate -> plan -> call -> verify`.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or long command reference text into verification output.
- Do not use fixed sleep before verification. Re-read state or inspect lifecycle/log evidence instead.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Partial payload evidence can be authoritative.
- Do not include `--allowDangerous` in normal verification workflows.
- Keep output bounded: verification command, observed evidence, pass/fail conclusion, and any residual uncertainty.

## References
- Read `references/verification-workflow.md` when deciding what evidence is sufficient for a completed change.
