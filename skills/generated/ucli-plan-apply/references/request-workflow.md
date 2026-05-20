# Request Workflow

Use this reference when constructing or applying a uCLI JSON request.

## Request Construction
1. Start from the user's intended outcome, not from a guessed operation shape.
2. Read enough project state to identify context, target selector, and save boundary.
3. Run `ucli ops describe <opName>` for each primitive operation being used.
4. Prefer one focused request over a broad batch unless the edits share one review boundary.
5. Keep request metadata stable and let the CLI supply internal protocol fields.

## Execution
1. Run `ucli ready --for mutation` before the mutation workflow.
2. Run `ucli validate` and fix static contract errors first.
3. Run `ucli plan` to preview targets, changed state, touched contexts, and plan token behavior.
4. Run `ucli call --withPlan` only after reviewing the plan.
5. Preserve returned evidence, especially `payload.opResults[].applied`, `changed`, `touched`, and any error payload.
6. Run `ucli verify --profile built-in:mutation --from <result.json>` when post-mutation evidence is needed.
7. For C# script changes, run `ucli compile` or `ucli verify --profile built-in:script --from <result.json>`.
8. When `readPostcondition` is present, perform the requested read before reporting final state.

## Failure Handling
- Parse and validation failures mean the request has not reached Unity execution.
- Lifecycle, disconnect, crash, and `IPC_TIMEOUT` failures can still include partial or applied results.
- Use `ucli codes describe <CODE>` before deciding recovery from machine-readable codes.
- Retry only after checking returned evidence and bounded logs. Avoid blind replay of mutating requests.
