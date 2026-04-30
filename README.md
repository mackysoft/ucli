# uCLI - CLI workflow for Unity automation

[![verify](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml/badge.svg)](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml) [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=MackySoft.Ucli)](https://www.nuget.org/packages/MackySoft.Ucli) [![NuGet Unity](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=MackySoft.Ucli.Unity)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://github.com/mackysoft))**

uCLI is a command line workflow for automating Unity projects from local terminals, scripts, continuous integration jobs, and agent-driven tools. It drives Unity through Unity Editor APIs, keeps request and response data machine-readable, and provides both one-shot batchmode execution and long-running daemon execution.

## Key Features

- Unity edit requests expressed as JSON and executed through `validate`, `plan`, and `call`.
- Safe planning flow with `planToken`, request validation, and drift checks before applying changes.
- `auto`, `daemon`, and `oneshot` execution modes for interactive work, repeated automation, and isolated batchmode runs.
- Typed query commands for assets, scenes, GameObjects, components, schemas, and object resolution.
- Read index support for fast lookup paths that can avoid opening Unity when cached data is fresh enough.
- Daemon lifecycle commands, Unity and daemon log streaming, and status reporting.
- Unity Test Framework execution through `ucli test run`, with normalized result artifacts.
- NuGet distribution for the .NET global tool, Unity plugin, IPC contracts, and shared infrastructure packages.

## Installation

### Requirements

- .NET 8 or later for the `ucli` global tool.
- A Unity project with the `MackySoft.Ucli.Unity` plugin installed when you run Unity editor operations.
- NuGetForUnity in the Unity project when installing the Unity plugin package.

The verified Unity Editor versions are release-dependent. Check the release notes and project requirements for the version you install.

### Install the CLI

Install a pinned version from nuget.org:

```bash
dotnet tool install --global MackySoft.Ucli --version <version>
```

Update an existing global tool installation:

```bash
dotnet tool update --global MackySoft.Ucli --version <version>
```

Check the installed command:

```bash
ucli --version
```

### Install the Unity plugin

Install `MackySoft.Ucli.Unity` with NuGetForUnity. The package is published to nuget.org and restores its uCLI contract, infrastructure, and runtime dependencies through NuGet.

Make sure the Unity project has a nuget.org package source. A minimal `Assets/NuGet.config` source entry is:

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Then install the package from the NuGetForUnity UI, or add the package to `Assets/packages.config`:

```xml
<packages>
  <package id="MackySoft.Ucli.Unity" version="<version>" manuallyInstalled="true" targetFramework="netstandard2.1" />
</packages>
```

After restore, the plugin marker is placed under the restored package:

```text
Assets/Packages/MackySoft.Ucli.Unity.<version>/ucli-plugin.json
```

## Usage

Initialize optional project-local uCLI settings:

```bash
ucli init
```

Create a request file:

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "steps": [
    {
      "kind": "op",
      "id": "openMainScene",
      "op": "ucli.scene.open",
      "args": {
        "path": "Assets/Scenes/Main.unity"
      }
    },
    {
      "kind": "edit",
      "id": "editSpawner",
      "on": {
        "scene": "Assets/Scenes/Main.unity"
      },
      "select": {
        "gameObject": "Root/Enemies/Spawner",
        "component": "Game.EnemySpawner, Assembly-CSharp",
        "cardinality": "one"
      },
      "actions": [
        {
          "kind": "set",
          "values": {
            "spawnInterval": 3.0
          }
        }
      ],
      "commit": "context"
    }
  ]
}
```

Validate the request without connecting to Unity:

```bash
ucli validate --requestPath ./request.json --projectPath ./UnityProject
```

Plan the request before applying changes:

```bash
ucli plan --requestPath ./request.json --projectPath ./UnityProject
```

Apply the request with the `planToken` returned by `ucli plan`:

```bash
ucli call --requestPath ./request.json --projectPath ./UnityProject --planToken "<PLAN_TOKEN>"
```

Query project data:

```bash
ucli query assets find --projectPath ./UnityProject --type "UnityEngine.Material, UnityEngine.CoreModule" --limit 100
ucli query scene tree --projectPath ./UnityProject --path Assets/Scenes/Main.unity --depth 1
```

Run a Unity daemon for repeated requests:

```bash
ucli daemon start --projectPath ./UnityProject
ucli status --projectPath ./UnityProject
ucli daemon stop --projectPath ./UnityProject
```

Run Unity tests and collect normalized artifacts:

```bash
ucli test run \
  --projectPath ./UnityProject \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

Test artifacts are written under `.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/`.

## Details

### Architecture

uCLI is split into four public packages:

| Package | Role |
| --- | --- |
| `MackySoft.Ucli` | .NET global tool that provides the `ucli` command. |
| `MackySoft.Ucli.Unity` | Unity Editor plugin for IPC, execution, indexing, logs, and Unity Test Framework integration. |
| `MackySoft.Ucli.Contracts` | Shared IPC protocol and data contract types. |
| `MackySoft.Ucli.Infrastructure` | Shared infrastructure services used by uCLI runtime components. |

The CLI and Unity plugin communicate through IPC and exchange JSON request and response contracts. Editing is performed through Unity Editor APIs such as Scene, Prefab, AssetDatabase, and SerializedObject APIs.

### Execution Modes

Most Unity-backed commands accept `--mode`:

| Mode | Behavior |
| --- | --- |
| `auto` | Uses an existing daemon when one is running. Falls back to one-shot batchmode when no daemon is running. |
| `daemon` | Requires an existing daemon and fails if it is not running. |
| `oneshot` | Runs a single Unity batchmode process and fails if a daemon is already running for the project. |

Daemon startup is explicit:

```bash
ucli daemon start --projectPath ./UnityProject
```

### Command Overview

| Command | Purpose |
| --- | --- |
| `ucli init` | Create optional `.ucli` configuration files. |
| `ucli validate` | Validate JSON requests without Unity IPC. |
| `ucli plan` | Resolve and plan JSON requests before applying changes. |
| `ucli call` | Apply JSON requests to Unity. |
| `ucli resolve` | Resolve selectors to Unity object identifiers. |
| `ucli query` | Run typed read commands for assets, scenes, GameObjects, components, and schemas. |
| `ucli refresh` | Run the standard project refresh operation. |
| `ucli ops` | List and describe primitive operations. |
| `ucli status` | Report daemon and lifecycle status. |
| `ucli logs` | Read Unity and daemon logs. |
| `ucli daemon` | Start, stop, clean up, inspect, and list daemon sessions. |
| `ucli test` | Run Unity Test Framework tests and initialize test profiles. |

### Documentation

- [Product and execution contract](https://github.com/mackysoft/ucli/blob/master/docs/uCLI.md)
- [Command reference](https://github.com/mackysoft/ucli/blob/master/docs/uCLI-command-reference.md)
- [JSON request specification](https://github.com/mackysoft/ucli/blob/master/docs/json-request-spec.md)
- [JSON property reference](https://github.com/mackysoft/ucli/blob/master/docs/uCLI-property-reference.md)
- [Operation catalog](https://github.com/mackysoft/ucli/blob/master/docs/ops-catalog.md)
- [Design principles](https://github.com/mackysoft/ucli/blob/master/docs/uCLI-design-principles.md)
- [Package operations](https://github.com/mackysoft/ucli/blob/master/docs/package-operations.md)

## Help and Contribute

Feature requests, bug reports, and pull requests are welcome in the GitHub repository:

- Issues: <https://github.com/mackysoft/ucli/issues>
- Pull requests: <https://github.com/mackysoft/ucli/pulls>

If uCLI or other MackySoft projects are useful to you, sponsorship is appreciated.

GitHub Sponsors: <https://github.com/sponsors/mackysoft>

## Author Info

Hiroya Aramaki is an indie game developer in Japan.

- GitHub: <https://github.com/mackysoft>
- Blog: <https://mackysoft.net/blog>

## License

uCLI is under the [MIT License](LICENSE).
