# uCLI Plan Apply

Use this skill to turn an intended Unity change into a validated and reviewable uCLI request.

## Workflow
1. Read the target project state before writing the request.
2. For every primitive operation in the request, run `ucli ops describe <opName>` and use that runtime contract.
3. Build the smallest JSON request that expresses the intended context, selection, action, and commit boundary.
4. Run `ucli validate` before any Unity-backed execution.
5. Run `ucli plan` and inspect the plan evidence before mutating.
6. Run `ucli call` only after the plan is acceptable, using a plan token when available or required.
7. Verify with reads, tests, or logs appropriate to the change.

The required sequence is `read -> describe -> build request -> validate -> plan -> call -> verify`.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or command reference text into the skill output.
- Do not use fixed sleep while waiting for readiness. Let uCLI lifecycle and timeout results drive the next step.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Inspect any returned `payload.opResults[].applied`, `changed`, and `touched` evidence.
- If mutation returns `readPostcondition`, perform the required follow-up read before using affected read surfaces.
- Do not include `--allowDangerous` in normal workflows. Dangerous operation opt-in requires explicit user intent.
- Keep output bounded: request path or snippet, validation result, plan result, call result, and verification evidence.

## References
- Read `references/request-workflow.md` before constructing or applying a non-trivial request.
