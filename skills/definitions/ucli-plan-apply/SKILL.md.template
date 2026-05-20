# uCLI Plan Apply

Use this skill to turn an intended Unity change into a validated and reviewable uCLI request.

## Workflow
1. Read the target project state before writing the request.
2. Run `ucli ready --for mutation` before the mutation workflow.
3. For every primitive operation in the request, run `ucli ops describe <opName>` and use that runtime contract.
4. Build the smallest JSON request that expresses the intended context, selection, action, and commit boundary.
5. Run `ucli validate` before any Unity-backed execution.
6. Run `ucli plan` and inspect the plan evidence before mutating.
7. Run `ucli call --withPlan` only after the plan is acceptable, using a plan token when available or required.
8. Run `ucli verify --profile built-in:mutation --from <result.json>` after mutation, then add targeted reads, tests, or logs only when the claim packet requires them.
9. For C# script changes, run `ucli compile` or `ucli verify --profile built-in:script --from <result.json>`. Omit `--profile` only when `built-in:default` project-level verification is intended, because it can trigger compile / domain reload.

The required sequence is `read -> ready -> describe -> build request -> validate -> plan -> call --withPlan -> verify`.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or command reference text into the skill output.
- Do not use fixed sleep while waiting for readiness. Let uCLI lifecycle and timeout results drive the next step.
- Do not use log scraping as a pass/fail gate. Use claim packets and bounded log commands only when a code or claim requires evidence.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Inspect any returned `payload.opResults[].applied`, `changed`, and `touched` evidence.
- Use `ucli codes describe <CODE>` for machine-readable code meaning; do not branch on free-form messages.
- If mutation returns `readPostcondition`, perform the required follow-up read before using affected read surfaces.
- If `ready --mode auto` returns `probeOnly`, do not treat it as proof that a later mutation has a reusable session.
- Do not use arbitrary C# execution, arbitrary shell execution, or Unity YAML direct edits as normal shortcuts.
- Do not include `--allowDangerous` in normal workflows. Dangerous operation opt-in requires explicit user intent.
- Keep output bounded: request path or snippet, validation result, plan result, call result, and verification evidence.

## References
- Read `references/request-workflow.md` before constructing or applying a non-trivial request.
