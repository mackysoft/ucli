# uCLI Read Project

Use this skill to inspect a Unity project with uCLI before deciding what to edit or verify.

## Workflow
1. Resolve the target project with `ucli status` or an explicit `--projectPath`.
2. Read the current state with the narrowest useful `ucli query` or `ucli resolve` command.
3. Before depending on any primitive operation, run `ucli ops describe <opName>` and treat that output as the current contract.
4. Keep the safe mutation path intact: `read -> ready -> describe -> build request -> validate -> plan -> call --withPlan -> verify`.
5. If a later step may mutate state, hand off to the request workflow only after the selector, context, and save boundary are known.

## Guardrails
- Do not copy operation catalogs, argument schemas, result schemas, or command reference text into the answer.
- Do not use fixed sleep as a readiness strategy. Use uCLI lifecycle, status, logs, and command results.
- Do not use log scraping as a pass/fail gate. Use claim packets and bounded log commands only when a code or claim requires evidence.
- Do not treat `IPC_TIMEOUT` as proof that no operation ran. Inspect any returned `payload.opResults[].applied`, `changed`, and `touched` evidence before retrying.
- Use `ucli codes describe <CODE>` for machine-readable code meaning; do not branch on free-form messages.
- If a response includes `readPostcondition`, perform the required follow-up read before trusting stale read surfaces.
- If `ready --mode auto` returns `probeOnly`, do not treat it as proof that a later mutation has a reusable session.
- Do not use `--all` in normal reasoning loops. Page with `--limit` and `--after`.
- Do not use arbitrary C# execution, arbitrary shell execution, or Unity YAML direct edits as normal shortcuts.
- Do not include `--allowDangerous` in normal workflows. Only discuss dangerous opt-in when the user explicitly asks for that path.
- Keep output bounded: summarize the relevant state, selectors, operation names to describe next, and unresolved risks.

## References
- Read `references/project-reading-workflow.md` when choosing query, resolve, status, readIndex, or operation metadata reads.
