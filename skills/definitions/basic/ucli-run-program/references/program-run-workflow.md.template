# Program Run Workflow

Use this reference when a typed Program spans lifecycle boundaries or its Run must be observed after the original response is unavailable.

## Input And Preparation
1. Select one input source: preset, Program file, or standard input.
2. For a preset, discover it with `ucli program presets list` and read its resolved Program with `ucli program presets describe <presetId>`.
3. Run `ucli program validate` before planning. Static diagnostics stop the workflow; they do not create a Run.
4. Run `ucli program plan` against the same input. Inspect only the reported current planning frontier, required options, and diagnostics.

## Run Observation
1. Start the accepted definition with `ucli program run`.
2. Preserve the first `program.run.started` progress event. Its `runId` and `definitionDigest` identify the newly created Run.
3. The starting CLI remains the attached supervisor. Do not create another Run to attach, resume, or recover it.
4. If the starting response is unavailable and the progress event supplied a `runId`, use `ucli program status --runId <runId>` to observe the existing Run. Without that identifier, do not replay the definition.
5. Use `ucli program cancel --runId <runId>` only for a user-requested cancellation. Read its returned state rather than assuming that a cancellation request has already stopped every started action.

## Result Interpretation
- `program validate` and `program plan` report static validity and planning information; neither produces a Run verdict.
- Read `state` as execution progress and `verdict` as the aggregate judgement. `completed` with `fail` or `incomplete` is a terminal execution result, not a command transport error.
- A terminal result supplies terminal state, terminal `applicationState`, completed and unstarted step counts, and the terminal record reference. Step results and their artifact references provide typed evidence for individual actions.
- `applicationState=notApplied`, `applied`, `partiallyApplied`, `indeterminate`, and `unknown` describe different evidence states. Preserve that distinction in the result instead of inferring a stronger application claim.
