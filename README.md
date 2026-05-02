# uCLI - CLI workflow for Unity automation

[![verify](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml/badge.svg)](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml) [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=MackySoft.Ucli)](https://www.nuget.org/packages/MackySoft.Ucli) [![NuGet Unity](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=MackySoft.Ucli.Unity)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://twitter.com/makihiro_dev))**

uCLI lets you inspect, plan, apply, and verify Unity Editor automation from a terminal, script, continuous integration job, or agent workflow. It is built for Unity project changes that should go through Unity Editor APIs instead of direct YAML editing.

## What You Can Do

- Query assets, scenes, GameObjects, components, schemas, and operation metadata.
- Send JSON requests that combine primitive Unity operations and higher-level edit steps.
- Validate and plan requests before applying them.
- Apply changes through Unity Editor APIs with `call`.
- Use `call --withPlan` for a compact plan-and-apply flow.
- Use a daemon for repeated Unity-backed commands, or one-shot batchmode for isolated jobs.
- Run Unity Test Framework tests and collect normalized artifacts.
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

Use a pinned `<version>` for both the CLI and Unity plugin in released automation.

## Compatibility and Stability

- Pin `MackySoft.Ucli` and `MackySoft.Ucli.Unity` to compatible released versions and update them together.
- `protocolVersion: 1` is the current request protocol for automation workflows.
- `MackySoft.Ucli.Contracts` is for direct IPC protocol and tooling consumers.
- `MackySoft.Ucli.Infrastructure` is an advanced integration package for runtime support code.
- Operations marked `dangerous` are outside the normal guarded edit path and require an explicit `ucli call --allowDangerous`.

## Typical Workflow

uCLI is normally driven by a runner: a local shell script, a continuous integration job, or an agent. The runner reads Unity state, builds one JSON request, sends that request to uCLI, and decides whether to accept the result.

Set up optional project-local configuration once per repository:

```bash
ucli init
```

Confirm that uCLI can resolve the target Unity project:

```bash
ucli status --projectPath ./UnityProject
```

Start a daemon when an interactive session or local automation will run several Unity-backed commands:

```bash
ucli daemon start --projectPath ./UnityProject
```

For one-off local commands and CI jobs, you can skip the daemon. The default `--mode auto` uses a running daemon when one is available and falls back to one-shot batchmode when it is not.

## Automation Output Contract

Except for `ucli logs`, the automation commands listed below write one JSON result envelope to standard output. Help and version output are human-readable command-line output. Progress messages and diagnostics that are not part of the JSON result contract are written to standard error.

`ucli logs unity` and `ucli logs daemon` write log entries to standard output. Use `--format json` when a runner needs newline-delimited JSON log events.

Automation should parse standard output and treat standard error as diagnostic text.

## Reading Project State

Read before you write. These commands emit machine-readable JSON.

```bash
ucli refresh --projectPath ./UnityProject

ucli query assets find \
  --projectPath ./UnityProject \
  --type "UnityEngine.Material, UnityEngine.CoreModule" \
  --limit 100

ucli query scene tree \
  --projectPath ./UnityProject \
  --path Assets/Scenes/Main.unity \
  --depth 1

ucli query comp schema \
  --projectPath ./UnityProject \
  --type "Game.EnemySpawner, Assembly-CSharp"

ucli resolve \
  --projectPath ./UnityProject \
  --scene Assets/Scenes/Main.unity \
  --hierarchyPath Root/Enemies/Spawner \
  --componentType "Game.EnemySpawner, Assembly-CSharp"
```

For read-heavy workflows, `--readIndexMode` controls whether query-like commands may use stored index data:

| Mode | Behavior |
| --- | --- |
| `disabled` | Skip stored index data and read from Unity when the command needs project state. |
| `allowStale` | Use stored index data even when it is stale, and fall back when it is unavailable. |
| `requireFresh` | Use stored index data only when it is fresh; otherwise refresh from Unity when the command supports it. |

The operation catalog is also available from the CLI:

```bash
ucli ops list --projectPath ./UnityProject
ucli ops describe ucli.scene.open --projectPath ./UnityProject
```

`ops describe` returns the generated `argsSchema` and `resultSchema` for one primitive operation. uCLI operation contracts are authored as typed Args/Result contract types in `MackySoft.Ucli.Contracts`; JSON Schema is generated from those types and their schema attributes. Agents should inspect `ops describe` instead of copying raw JSON examples into requests.

## Applying Changes

Request commands read JSON from standard input by default. Keep the request in your runner and pipe it to uCLI.

```bash
REQUEST_JSON='{
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
            "spawnInterval": 3.0,
            "maxCount": 10
          }
        }
      ],
      "commit": "context"
    }
  ]
}'

printf '%s' "$REQUEST_JSON" | ucli call --projectPath ./UnityProject --withPlan
```

This example opens `Assets/Scenes/Main.unity`, selects `Root/Enemies/Spawner`, edits the `Game.EnemySpawner` component, and saves the scene through `commit: "context"`.

When a human review step or quality gate must inspect the plan before applying changes, split execution into `validate`, `plan`, and `call`:

```bash
printf '%s' "$REQUEST_JSON" | ucli validate --projectPath ./UnityProject
PLAN_JSON="$(printf '%s' "$REQUEST_JSON" | ucli plan --projectPath ./UnityProject)"

PLAN_TOKEN="$(printf '%s' "$PLAN_JSON" | jq -r '.payload.planToken')"
printf '%s' "$REQUEST_JSON" | ucli call --projectPath ./UnityProject --planToken "$PLAN_TOKEN"
```

`validate` checks request shape and static constraints. `plan` checks the request against current Unity state and returns a `planToken` without applying persistent changes. `call` applies the same request when the token still matches the request and project state.

Use `--requestPath` only when a file path is the natural interface for your tool. Standard input is the primary request path for scripts and agents.

## Request DSL Core

This section covers the core request shape used by common automation. Operation-specific arguments and policies come from the operation catalog exposed by `ucli ops list` and `ucli ops describe`.

A request is one ordered unit of work:

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "steps": []
}
```

| Field | Meaning |
| --- | --- |
| `protocolVersion` | Request protocol version. Use `1`. |
| `requestId` | UUID generated by the runner for this request. |
| `steps` | Ordered steps. uCLI runs them in array order. |

Each step is either `kind: "op"` or `kind: "edit"`.

### Primitive Operation Step

Use `op` when the operation you need is already in the operation catalog.

```json
{
  "kind": "op",
  "id": "openMainScene",
  "op": "ucli.scene.open",
  "args": {
    "path": "Assets/Scenes/Main.unity"
  }
}
```

| Field | Meaning |
| --- | --- |
| `id` | Step identifier used in results and diagnostics. |
| `op` | Operation name, such as `ucli.scene.open`. |
| `args` | Operation-specific argument object. |

### Edit Step

Use `edit` for common Unity edits where you want to name a context, select targets, apply actions, and choose the save boundary.

```json
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
        "spawnInterval": 3.0,
        "weights.Array.data[0]": 0.25
      }
    }
  ],
  "commit": "context"
}
```

| Field | Meaning |
| --- | --- |
| `on` | The edit context and persistence boundary. |
| `select` | The object or objects to edit inside the context. |
| `actions` | One or more edits to apply to the selected target. |
| `commit` | Save behavior. Use `none`, `context`, or `project`. |

`scene` and `prefab` edits that mutate or use `commit: "context"` need that context open. Put `ucli.scene.open` or `ucli.prefab.open` before the edit step when the runner has not opened it already.

### Edit Contexts

| Context | JSON | Use it for |
| --- | --- | --- |
| Scene | `{ "scene": "Assets/Scenes/Main.unity" }` | GameObjects and components in a scene. |
| Prefab | `{ "prefab": "Assets/Prefabs/Enemy.prefab" }` | GameObjects and components in a prefab stage. |
| Asset | `{ "asset": "Assets/Data/GameBalance.asset" }` | A main asset such as a ScriptableObject. |
| Project | `{ "project": true }` | Project-scoped assets such as `ProjectSettings/TagManager.asset`. |

### Selectors

For scene and prefab contexts, select a GameObject by hierarchy path. Add `component` when the action should target a component on that GameObject.

```json
{
  "gameObject": "Root/Enemies/Spawner",
  "component": "Game.EnemySpawner, Assembly-CSharp",
  "cardinality": "one"
}
```

For an asset context, select the asset itself:

```json
{
  "self": true,
  "cardinality": "one"
}
```

For project-scoped settings, select the project asset path:

```json
{
  "projectAsset": {
    "path": "ProjectSettings/TagManager.asset"
  },
  "cardinality": "one"
}
```

For a scene context, select a set produced by `ucli.scene.query`:

```json
{
  "from": {
    "op": "ucli.scene.query",
    "args": {
      "pathPrefix": "Root/Enemies",
      "componentType": "Game.EnemySpawner, Assembly-CSharp"
    }
  },
  "cardinality": "all"
}
```

`cardinality` is required:

| Value | Meaning |
| --- | --- |
| `one` | Exactly one target must match. |
| `first` | Use the first target from the selector's deterministic match order. |
| `all` | Apply the same action to every matched target. |
| `atMostOne` | Allow zero or one target. |

`all` runs the same action list for every selected target in deterministic order. Actions that create a new global resource, such as `createAsset` and `createPrefab`, require the selection to resolve to at most one target.

### Actions

If `target` is omitted, the action uses the current selected target. `createPrefab` is the exception: it always requires an explicit `target` and `path`. An action that creates or ensures an object can expose it with `as`, and later actions in the same step can refer to it with `"$name"`.

```json
{
  "kind": "edit",
  "id": "ensureSpawner",
  "on": {
    "scene": "Assets/Scenes/Main.unity"
  },
  "select": {
    "gameObject": "Root/Enemies/Spawner",
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "ensureComponent",
      "type": "Game.EnemySpawner, Assembly-CSharp",
      "as": "spawner"
    },
    {
      "kind": "set",
      "target": "$spawner",
      "values": {
        "spawnInterval": 3.0,
        "maxCount": 10
      }
    }
  ],
  "commit": "context"
}
```

| Action | Required fields | Use it for |
| --- | --- | --- |
| `set` | `values` | Set serialized properties on the selected object, component, or asset. |
| `ensureComponent` | `type` | Add a component when it is missing and reuse it when it exists. |
| `createObject` | `name` | Create a GameObject in the selected context. |
| `createAsset` | `type`, `path` | Create a ScriptableObject main asset under `Assets/`. |
| `createPrefab` | `target`, `path` | Create a prefab asset from a GameObject. |
| `delete` | none | Delete the current target or the explicit `target`. |
| `reparent` | `parent` | Move a GameObject under another parent. |

`set.values` uses Unity serialized property paths:

```json
{
  "kind": "set",
  "values": {
    "spawnInterval": 3.0,
    "weights.Array.data[0]": 0.25
  }
}
```

### Commit

| Value | Behavior |
| --- | --- |
| `none` | Apply the edit in memory and do not save from this step. |
| `context` | Save the current scene, prefab, asset, or project context. |
| `project` | Save project-scoped changes and request-attributed open scene or prefab contexts. |

uCLI does not implicitly save an edit step. Choose `commit` intentionally.

### Primitive Target Selectors

Primitive operations that take a `target` use one of these selector shapes:

```json
{ "globalObjectId": "GlobalObjectId_V1-..." }
{ "assetGuid": "0123456789abcdef0123456789abcdef" }
{ "assetPath": "Assets/Data/GameBalance.asset" }
{ "projectAssetPath": "ProjectSettings/TagManager.asset" }
{ "scene": "Assets/Scenes/Main.unity", "hierarchyPath": "Root/Enemies/Spawner" }
{ "scene": "Assets/Scenes/Main.unity", "hierarchyPath": "Root/Enemies/Spawner", "componentType": "Game.EnemySpawner, Assembly-CSharp" }
{ "prefab": "Assets/Prefabs/Enemy.prefab", "hierarchyPath": "Root/Visual" }
```

Raw `set` operations use `sets`, while edit steps use the shorter `values` form:

```json
{
  "kind": "op",
  "id": "setSpawnerRaw",
  "op": "ucli.comp.set",
  "args": {
    "target": {
      "scene": "Assets/Scenes/Main.unity",
      "hierarchyPath": "Root/Enemies/Spawner",
      "componentType": "Game.EnemySpawner, Assembly-CSharp"
    },
    "sets": [
      {
        "path": "spawnInterval",
        "value": 3.0
      }
    ]
  }
}
```

## Operation Catalog Summary

Use `edit` for common edits. Use `op` when you need an explicit primitive operation from the catalog. The live catalog for the installed Unity plugin is available through `ucli ops list` and `ucli ops describe`.

### Read Operations

| Operation | Type | Args | Use it for |
| --- | --- | --- | --- |
| `ucli.assets.find` | query | `{ type?, pathPrefix?, nameContains? }` | Find main assets under `Assets/`. At least one filter is required. |
| `ucli.asset.schema` | query | `{ type }` or `{ target }` | Read writable serialized fields for an asset type or existing asset. |
| `ucli.comp.schema` | query | `{ type }` | Read writable serialized fields for a component type. |
| `ucli.go.describe` | query | `{ target, depth? }` | Inspect one GameObject and its components. |
| `ucli.resolve` | query | selector object | Resolve a selector to a Unity object identifier. |
| `ucli.scene.query` | query | `{ scene, pathPrefix?, componentType? }` | Find scene GameObjects or components for selection. |
| `ucli.scene.tree` | query | `{ path, depth? }` | Read a scene hierarchy. |

### Context And Save Operations

| Operation | Type | Args | Use it for |
| --- | --- | --- | --- |
| `ucli.scene.open` | query | `{ path }` | Ensure a scene is loaded. |
| `ucli.scene.save` | mutation | `{ path }` | Save a loaded scene. |
| `ucli.prefab.open` | query | `{ path }` | Open a prefab editing context. |
| `ucli.prefab.save` | mutation | `{ path }` | Save the opened prefab context. |
| `ucli.project.refresh` | mutation | `{}` | Refresh the Unity project and AssetDatabase. |
| `ucli.project.save` | mutation | `{}` | Save project assets, project settings, and tracked open contexts. |

### Mutation Operations

| Operation | Type | Args | Use it for |
| --- | --- | --- | --- |
| `ucli.asset.create` | mutation | `{ type, path }` | Create a ScriptableObject main asset under `Assets/`. |
| `ucli.asset.set` | mutation | `{ target, sets[] }` | Set serialized properties on an asset or project-scoped asset. |
| `ucli.comp.ensure` | mutation | `{ target, type }` | Ensure a component exists on a GameObject. |
| `ucli.comp.set` | mutation | `{ target, sets[] }` | Set serialized properties on a component. |
| `ucli.go.create` | mutation | `{ name, scene }` or `{ name, parent }` | Create a GameObject at a scene root or under a parent. |
| `ucli.go.delete` | mutation | `{ target }` | Delete a GameObject. |
| `ucli.go.reparent` | mutation | `{ target, parent }` | Move a GameObject under a new parent. |
| `ucli.prefab.create` | mutation | `{ target, path }` | Create a prefab asset from a GameObject. |

### Dangerous Operations

`ucli call` blocks operations marked `dangerous` unless the command includes `--allowDangerous`. Prefer the normal `edit` flow and non-dangerous primitive operations. Use dangerous operations only when the catalog marks the required operation that way and the request has been reviewed.

## Verifying Changes

Run Unity tests after applying edits:

```bash
ucli test run \
  --projectPath ./UnityProject \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

The command result includes `payload.artifactsDir` and `payload.summaryJsonPath`.
Test artifacts are written under `.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/`.

| Artifact | Use it for |
| --- | --- |
| `summary.json` | Fast pass/fail checks, result counts, and top failures. |
| `results.json` | Normalized per-test results for automation. |
| `results.xml` | Raw Unity Test Framework output from `-testResults`. |
| `editor.log` | Unity Editor diagnostics for setup failures, compiler errors, and runtime exceptions. |
| `meta.json` | The resolved test run configuration and timestamps. |

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
| `ucli init` | Create optional project-local uCLI configuration. |
| `ucli status` | Check Unity project resolution and daemon lifecycle state. |
| `ucli refresh` | Refresh Unity project state. |
| `ucli query` | Read project data without writing changes. |
| `ucli resolve` | Resolve a selector to a Unity object identifier. |
| `ucli ops` | List and inspect available primitive operations. |
| `ucli validate` | Check a request before Unity execution. |
| `ucli plan` | Preview a request and receive a `planToken`. |
| `ucli call` | Apply a request. |
| `ucli logs` | Read Unity or daemon logs. |
| `ucli daemon` | Manage daemon sessions. |
| `ucli test` | Run Unity Test Framework tests. |

Common options:

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--projectPath <path>` | Unity-backed commands | Target Unity project path. |
| `--mode auto\|daemon\|oneshot` | Unity-backed commands | Choose daemon reuse or one-shot batchmode. |
| `--timeout <milliseconds>` | Unity-backed commands | Override the command timeout. |
| `--readIndexMode disabled\|allowStale\|requireFresh` | Query-like commands | Control read-index use. |
| `--failFast` | Unity-backed commands | Fail when the Unity editor lifecycle is not ready instead of waiting. |
| `--withPlan` | `ucli call` | Run a plan pass inside `call` and include it in the result. |
| `--planToken <token>` | `ucli call` | Apply a request using a token returned by `ucli plan`. |
| `--allowDangerous` | `ucli call` | Allow operations marked dangerous by the operation catalog. |

## Packages

| Package | Install when |
| --- | --- |
| `MackySoft.Ucli` | You need the `ucli` command. |
| `MackySoft.Ucli.Unity` | You need Unity Editor operations in a Unity project. |
| `MackySoft.Ucli.Contracts` | You build advanced tooling that exchanges uCLI IPC contracts directly. |
| `MackySoft.Ucli.Infrastructure` | You build advanced uCLI runtime integrations that need shared infrastructure helpers. |

## Support

Use [GitHub Issues](https://github.com/mackysoft/ucli/issues) for bugs, feature requests, usage questions, and README problems.

For bug reports, include:

- `ucli --version`
- Unity version
- Operating system
- The command you ran
- `--mode` and `--readIndexMode` values, when relevant
- Error output or logs from `ucli logs unity` / `ucli logs daemon`

Use [Pull Requests](https://github.com/mackysoft/ucli/pulls) for focused fixes and README improvements.

## Sponsor

If uCLI helps your Unity automation workflow, please support MackySoft through GitHub Sponsors:

<https://github.com/sponsors/mackysoft>

## Author

Hiroya Aramaki is an indie game developer in Japan.

- Website: <https://mackysoft.net/>
- GitHub: <https://github.com/mackysoft>
- Sponsors: <https://github.com/sponsors/mackysoft>

## License

uCLI is under the [MIT License](LICENSE).
