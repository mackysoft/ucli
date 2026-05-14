# uCLI Troubleshoot

Use this skill to diagnose uCLI execution, daemon, lifecycle, readIndex, timeout, and log problems.

## Workflow
1. Identify the command, project path, mode, timeout, and exact code value.
2. Use `ucli status`, daemon commands, readIndex output, and logs to locate the failing boundary.
3. Use `ucli codes describe <CODE>` to read the static meaning of machine-readable codes.
4. If an operation-specific result is involved, run `ucli ops describe <opName>` before interpreting its contract.
5. Preserve the safe request path when recovering from a failed mutation: `read -> describe -> build request -> validate -> plan -> call -> verify`.
6. Verify the final state after any recovery step.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or long command reference text into diagnostic output.
- Do not use fixed sleep to wait out compile, reload, or daemon readiness. Use lifecycle-aware uCLI commands and bounded timeouts.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Check returned `payload.opResults[].applied`, `changed`, and `touched`, then inspect logs if needed.
- Use `ucli codes describe <CODE>` for machine-readable code meaning; do not branch on free-form messages.
- If a failed mutation returned `readPostcondition`, satisfy it before trusting affected reads.
- Do not include `--allowDangerous` in normal troubleshooting workflows.
- Keep output bounded: likely boundary, evidence, next command, and whether retry is safe.

## References
- Read `references/troubleshooting-workflow.md` for boundary-specific diagnostic order and retry rules.
