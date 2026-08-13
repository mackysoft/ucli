# Program Lifecycle System E2E

`run-macos.sh` verifies one public `ucli program run` against a disposable, GUI-hosted Unity project. The Program is fixed to the four Lifecycle boundaries `refresh`, `compile`, `play.enter`, and `play.exit`.

The runner waits for the Unity GUI registration, starts the daemon, runs the Program with one finite timeout, and always attempts Play Mode exit, daemon stop, and Unity termination. It retains the public CLI stream, final `CommandResult`, `program status` result, Unity log, daemon results, and copied Program Run terminal record under the result directory.

`program run --program-path` is executed through macOS `script` so its standard input remains a terminal rather than the runner's redirected stream. The unmodified pseudo-terminal transcript is retained as `program-run.stream.jsonl.raw`; the normalized public CLI stream is `program-run.stream.jsonl`.

The assertions use only public JSON and the artifacts referenced by that JSON. They require the Run to be completed with four ordered completed Steps, no child execution, non-null before/after generations, one terminal Lifecycle Execution reference per Step, and matching Program Step and Run terminal records. The four Lifecycle terminal artifacts must retain the same Unity process identity and Editor instance identifier; endpoint registration generations are intentionally allowed to change across lifecycle boundaries.

## Prerequisites

- macOS with a GUI login session.
- Unity Editor `2023.2.22f1`, matching `src/Ucli.Unity/ProjectSettings/ProjectVersion.txt`, installed and licensed.
- `dotnet`, `jq`, Git, and dependencies needed by `scripts/update-local-shared-packages.sh`.

Run from the repository root:

```bash
bash tests/System/ProgramExecution/run-macos.sh \
  --unity-editor "/Applications/Unity/Hub/Editor/2023.2.22f1/Unity.app" \
  --results-dir "$PWD/TestResults/ProgramExecution/manual"
```

`--results-dir` must not already exist. Omit it to create a timestamped directory. Add `--keep-work-directory` to retain the disposable Unity project Library, Logs, and Temp folders for diagnosis.
