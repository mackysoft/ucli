# Verification Workflow

Use this reference when deciding how to prove a Unity change after uCLI execution.

## Evidence Sources
- `payload.opResults[]`: first source for applied, changed, touched, phase, and result evidence.
- `readPostcondition`: requirements that must be satisfied before using affected read surfaces.
- `ucli query` and `ucli resolve`: targeted follow-up observation.
- `ucli test run`: Unity Test Framework proof when behavior depends on tests.
- `ucli logs unity` and `ucli logs daemon`: lifecycle, compile, reload, IPC, and runtime diagnostics.

## Verification Shape
1. Identify which contexts or assets should have changed.
2. Check `payload.opResults[].applied`, `changed`, and `touched`.
3. Perform the narrowest follow-up read that proves the expected state.
4. Run tests only when the requested acceptance criteria or change risk calls for them.
5. Report uncertainty explicitly when evidence is partial.

## Timeout And Disconnects
- `IPC_TIMEOUT`, disconnect, reload, or crash can leave a request partially applied.
- Prefer evidence-bearing responses and logs over assumptions.
- Do not replay a mutating request until the applied state has been checked.
