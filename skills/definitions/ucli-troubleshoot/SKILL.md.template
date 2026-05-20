# uCLI Troubleshoot

Use this skill to diagnose uCLI execution, daemon, lifecycle, readIndex, timeout, and log problems.

## Workflow
1. Identify the command, project path, mode, timeout, and exact code value.
2. Use `ucli status`, `ucli ready --for mutation`, daemon commands, readIndex output, and logs to locate the failing boundary.
3. Use `ucli codes describe <CODE>` to read the static meaning of machine-readable codes.
4. If an operation-specific result is involved, run `ucli ops describe <opName>` and interpret `description`, `inputs`, `resultContract`, `assurance`, and optional `codeContract`.
5. Use `argsSchema` and `resultSchema` only to validate JSON argument and result structure.
6. Preserve the safe request path when recovering from a failed mutation: `read -> ready -> describe -> build request -> validate -> plan -> call --withPlan -> verify`.
7. Verify the final state after any recovery step.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or long command reference text into diagnostic output.
- Do not use fixed sleep to wait out compile, reload, or daemon readiness. Use lifecycle-aware uCLI commands and bounded timeouts.
- Do not use log scraping as a pass/fail gate. Use claim packets and bounded log commands only when a code or claim requires evidence.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Check returned `payload.opResults[].applied`, `changed`, and `touched`, then inspect logs if needed.
- Use `ucli codes describe <CODE>` for machine-readable code meaning; do not branch on free-form messages.
- If a failed mutation returned `readPostcondition`, satisfy it before trusting affected reads.
- If `ready --mode auto` returns `probeOnly`, do not treat it as proof that a later mutation has a reusable session.
- Do not use arbitrary C# execution, arbitrary shell execution, or Unity YAML direct edits as normal shortcuts.
- Do not include `--allowDangerous` in normal troubleshooting workflows.
- Keep output bounded: likely boundary, evidence, next command, and whether retry is safe.

## References
- Read `references/troubleshooting-workflow.md` for boundary-specific diagnostic order and retry rules.
