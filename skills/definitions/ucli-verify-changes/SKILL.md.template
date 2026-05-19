# uCLI Verify Changes

Use this skill to verify whether a uCLI-backed Unity change actually reached the intended state.

## Workflow
1. Start from the command result, not from the exit code alone.
2. Inspect `payload.opResults[].applied`, `changed`, and `touched` for each public step.
3. If the task involves primitive operations, use `ucli ops describe <opName>` to interpret the operation's assurance and result contract.
4. If the command result includes `readPostcondition`, satisfy those requirements before trusting affected read surfaces.
5. Run `ucli verify --profile built-in:mutation --from <result.json>` when only post-mutation Unity-local evidence is needed, and read `payload.verdict`, `claims[]`, `reports`, and `residualRisks[]`.
6. Use `ucli verify --profile built-in:script --from <result.json>` for C# script changes, or omit `--profile` only when the default project-level verification and its compile side effects are intended.
7. Use targeted `ucli query`, `ucli resolve`, `ucli test run`, or `ucli logs` evidence only when the claim packet or task scope requires it.
8. Preserve the overall safe path: `read -> describe -> build request -> validate -> plan -> call -> verify`.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or long command reference text into verification output.
- Do not use fixed sleep before verification. Re-read state or inspect lifecycle/log evidence instead.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Partial payload evidence can be authoritative.
- Do not treat `payload.verdict=pass` as reviewless green by itself; it is Unity-local assurance only.
- Do not use probe-only readiness as proof that a later mutation reused the same Unity process.
- Use `ucli codes describe <CODE>` for machine-readable code meaning; do not branch on free-form messages.
- Do not include `--allowDangerous` in normal verification workflows.
- Keep output bounded: verification command, observed evidence, pass/fail conclusion, and any residual uncertainty.

## References
- Read `references/verification-workflow.md` when deciding what evidence is sufficient for a completed change.
