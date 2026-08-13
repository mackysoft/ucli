# ucli-run-program

Use this skill to select, validate, plan, and supervise a closed typed uCLI Program.

## Workflow
1. Resolve exactly one Program input: a preset, a Program file, or standard input. Keep the Program limited to the typed steps accepted by the installed uCLI distribution.
2. When selecting a preset, run `ucli program presets list`, then `ucli program presets describe <presetId>` for the chosen ID. Use the returned Program, source, description, and definition digest as the current preset contract.
3. Run `ucli program validate` with the selected input. Read `valid`, `definitionDigest`, `sourceManifest`, and `diagnostics`. Do not continue to planning when static validation is unsuccessful.
4. Run `ucli program plan` with the same input. Read the current planning frontier from `steps[]`, required run options, and diagnostics. A Program plan does not authorize the whole Program or decide an execution verdict.
5. Run `ucli program run` only after the plan is acceptable. Use `--format json` when progress must be retained. Record the `runId` and `definitionDigest` from the first `program.run.started` progress event; another CLI uses that `runId` only to observe or cancel this existing Run.
6. Let the attached `program run` invocation supervise the Run to its final command result. Do not start a second Program Run to recover a missing response.
7. If the Run start response is lost after the progress event was received, run `ucli program status --runId <runId>` and continue from the returned persistent Run state. If no `runId` was observed, report that the started Run cannot be identified instead of replaying it.
8. Read `state` and `verdict` independently. A terminal `state=completed` can carry `verdict=fail` or `verdict=incomplete`; that is a completed execution with an unsuccessful or incomplete judgement, not a transport failure. A nonterminal status response is not completion.
9. For a terminal Run, read `terminal`, its `applicationState`, the Run and Step `applicationState` values, and the public artifact references such as `terminal.recordRef`, `steps[].resultRef`, and terminal-record artifact references. Use these typed records as evidence for what ran, what was applied, and what remains uncertain.
10. Cancel only when the user asks to stop the Run. Run `ucli program cancel --runId <runId>`, then read the returned state and terminal evidence. Cancellation never grants ownership to start later steps.

## Guardrails
- Use only `program presets list|describe`, `program validate|plan|run|status|cancel` and their documented JSON payloads for this workflow.
- Do not use shell strings, arbitrary commands, implementation state files, or non-public APIs as Program steps or recovery paths.
- Do not make a Program step invoke another Program. Do not infer a Run identity from matching definitions or replay a Run after an uncertain start.
- Keep `state`, `verdict`, `applicationState`, terminal records, and artifact references distinct. Do not convert a judgement result into an execution failure, or an uncertain application state into an applied result.
- Keep output bounded: selected input, validation and plan results, `runId`, current or terminal state, verdict, application state, and relevant artifact references.

## References
- Read `references/program-run-workflow.md` before supervising a non-trivial Program Run or recovering a lost Run response.
