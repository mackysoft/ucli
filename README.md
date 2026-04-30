# uCLI - CLI workflow for Unity automation

[![verify](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml/badge.svg)](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml) [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=MackySoft.Ucli)](https://www.nuget.org/packages/MackySoft.Ucli) [![NuGet Unity](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=MackySoft.Ucli.Unity)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

uCLI lets you run Unity Editor operations and Unity Test Framework tests from a terminal, script, continuous integration job, or agent workflow. It is designed for Unity project automation where changes should be inspected, planned, applied, and verified without editing Unity YAML by hand.

Created by Hiroya Aramaki ([Makihiro](https://github.com/mackysoft)).

## What You Can Do

- Inspect Unity project state from the command line.
- Query assets, scenes, GameObjects, components, and schemas.
- Validate and plan JSON edit requests before applying them.
- Apply Unity edits through Unity Editor APIs.
- Reuse a daemon for repeated Unity-backed commands, or run one-shot batchmode commands.
- Run Unity tests and collect normalized artifacts.
- Read Unity and daemon logs when automation fails.

## Installation

### Requirements

- .NET 8 or later.
- A Unity project with NuGetForUnity when installing the Unity plugin.

### CLI

```bash
dotnet tool install --global MackySoft.Ucli --version <version>
```

Update an existing installation:

```bash
dotnet tool update --global MackySoft.Ucli --version <version>
```

Confirm the command is available:

```bash
ucli --version
```

### Unity Plugin

Install `MackySoft.Ucli.Unity` into the Unity project with NuGetForUnity. The Unity project must be able to restore packages from nuget.org.

If you manage `Assets/packages.config` directly, add:

```xml
<package id="MackySoft.Ucli.Unity" version="<version>" manuallyInstalled="true" targetFramework="netstandard2.1" />
```

## How uCLI Is Used

uCLI is usually driven by an automation runner: a local script, a continuous integration job, or an agent that needs to inspect and modify a Unity project. The runner sends structured requests to uCLI, reads structured JSON results, and lets a developer or quality gate decide whether the change is acceptable.

Set up optional project-local configuration once per repository:

```bash
ucli init
```

Before automation starts, confirm that uCLI can resolve the target Unity project:

```bash
ucli status --projectPath ./UnityProject
```

For an interactive agent session or a local script that will run several Unity-backed commands, start a daemon:

```bash
ucli daemon start --projectPath ./UnityProject
```

For one-off local commands or CI jobs, you can skip the daemon. The default `--mode auto` uses a running daemon when available and falls back to one-shot batchmode when it is not.

Read project state before deciding what to change:

```bash
ucli query assets find --projectPath ./UnityProject --type "UnityEngine.Material, UnityEngine.CoreModule" --limit 100
ucli query scene tree --projectPath ./UnityProject --path Assets/Scenes/Main.unity --depth 1
```

Generate a JSON edit request in your runner, then pass the same request body through `validate`, `plan`, and `call`. `plan` returns JSON; read `payload.planToken` from that result and pass it to `call`.

This shell example uses `jq`; use your runner's JSON parser if you do not use `jq`.

```bash
# REQUEST_JSON is produced by your script, CI job, or agent.
printf '%s' "$REQUEST_JSON" | ucli validate --projectPath ./UnityProject
PLAN_JSON="$(printf '%s' "$REQUEST_JSON" | ucli plan --projectPath ./UnityProject)"

PLAN_TOKEN="$(printf '%s' "$PLAN_JSON" | jq -r '.payload.planToken')"
printf '%s' "$REQUEST_JSON" | ucli call --projectPath ./UnityProject --planToken "$PLAN_TOKEN"
```

Use `--requestPath` only when a file path is the natural interface for your tool.

After edits, run Unity tests from the same automation flow:

```bash
ucli test run \
  --projectPath ./UnityProject \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

Test artifacts are written under `.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/`.

When a command or test fails, read Unity and daemon logs before retrying:

```bash
ucli logs unity --projectPath ./UnityProject --tail 200 --level error
ucli logs daemon --projectPath ./UnityProject --tail 200
```

Stop the daemon at the end of an interactive automation session:

```bash
ucli daemon stop --projectPath ./UnityProject
```

## Command Guide

| Command | Use it when you need to |
| --- | --- |
| `ucli status` | Check daemon and Unity lifecycle state. |
| `ucli query` | Read project data without writing changes. |
| `ucli resolve` | Resolve a selector to a Unity object identifier. |
| `ucli validate` | Check a request before Unity execution. |
| `ucli plan` | Preview an edit request and receive a `planToken`. |
| `ucli call` | Apply an edit request. |
| `ucli refresh` | Run Unity project refresh. |
| `ucli logs` | Read Unity or daemon logs. |
| `ucli daemon` | Manage daemon sessions. |
| `ucli test` | Run Unity Test Framework tests. |
| `ucli ops` | List and inspect available primitive operations. |

## Packages

| Package | Install when |
| --- | --- |
| `MackySoft.Ucli` | You need the `ucli` command. |
| `MackySoft.Ucli.Unity` | You need Unity Editor operations in a Unity project. |
| `MackySoft.Ucli.Contracts` | You build tooling that exchanges uCLI IPC contracts directly. |
| `MackySoft.Ucli.Infrastructure` | You build uCLI runtime integrations that need shared infrastructure helpers. |

## Reference

- [Command reference](https://github.com/mackysoft/ucli/blob/master/docs/uCLI-command-reference.md)
- [JSON request specification](https://github.com/mackysoft/ucli/blob/master/docs/json-request-spec.md)
- [JSON property reference](https://github.com/mackysoft/ucli/blob/master/docs/uCLI-property-reference.md)
- [Operation catalog](https://github.com/mackysoft/ucli/blob/master/docs/ops-catalog.md)
- [Package operations](https://github.com/mackysoft/ucli/blob/master/docs/package-operations.md)

## Support

Use [GitHub Issues](https://github.com/mackysoft/ucli/issues) for bugs, feature requests, and documentation problems.

For bug reports, include:

- `ucli --version`
- Unity version
- Operating system
- The command you ran
- `--mode` value, when relevant
- Error output or logs from `ucli logs unity` / `ucli logs daemon`

Use [Pull Requests](https://github.com/mackysoft/ucli/pulls) for focused fixes and documentation improvements.

## Sponsor

If uCLI helps your Unity automation workflow, please consider sponsoring MackySoft. Sponsorship supports maintenance and continued development.

GitHub Sponsors: <https://github.com/sponsors/mackysoft>

## Author

Hiroya Aramaki is an indie game developer in Japan.

- GitHub: <https://github.com/mackysoft>
- Blog: <https://mackysoft.net/blog>

## License

uCLI is under the [MIT License](LICENSE).
