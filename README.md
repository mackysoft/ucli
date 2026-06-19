# uCLI - Reviewable Unity automation for agents, CI, and tools

[![verify](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml/badge.svg)](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml) [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=MackySoft.Ucli)](https://www.nuget.org/packages/MackySoft.Ucli) [![NuGet Unity](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=MackySoft.Ucli.Unity)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://twitter.com/makihiro_dev))**

uCLI turns Unity Editor changes into reviewable, repeatable, machine-readable workflows for scripts, CI, and AI agents.

It reads Unity state, declares the intended change, applies it through Unity Editor APIs with validation and planning, chooses the save boundary, and returns structured evidence.

uCLI is not a remote-control wrapper around the Unity Editor. Use it when automated Unity changes need to be inspected, replayed, gated, and trusted.

## ❓ Why uCLI?

uCLI starts from a simple premise:

- Unity is callable.
- Unity changes should be reviewable.
- The normal workflow is `status -> ready -> read -> call --withPlan -> verify`.

uCLI keeps automated Unity changes reviewable: inspect the plan before mutation, see what happened after execution, and diagnose uncertain Editor state.

| Concern | What you need to know | How uCLI handles it |
| --- | --- | --- |
| Editor readiness | Whether Unity can accept the request now. | Compile, domain reload, busy, play mode, shutdown, and blocked states are surfaced as readiness states or structured errors. |
| Live Unity state | Whether the mutation is applied through Unity itself. | Mutations go through Unity Editor APIs and re-resolve against live Unity state. |
| Edit context | Which scene, prefab, asset, or project context owns the edit. | Every edit declares a scene, prefab, asset, or project context. |
| Planned writes and drift | Whether planning is explicit and Unity state still matches the reviewed plan. | `ucli call --withPlan` validates, plans, and applies in one command; `ucli plan` and `--planToken` support separated review gates and validate request/state before mutation. |
| Project and worktree identity | Which project owns local state, indexes, artifacts, and launch coordination. | Daemon state, indexes, and artifacts are scoped by project identity; Unity process launches are serialized by physical project root. |
| Persistence | Whether a mutation also saved project data. | `commit` controls persistence with `"none"`, `"context"`, or `"project"`. |
| Evidence | What changed, what was touched, and where diagnostics live. | JSON exposes `opResults`, `applied`, `changed`, `touched`, errors, logs, and artifacts. |

## ✨ What You Can Do

Use uCLI for automated Unity work that needs structured state, planned writes, and verifiable results:

- Agents can inspect Unity state, apply reviewed edits, and base follow-up decisions on structured evidence.
- CI jobs can run Unity-backed checks, tests, and builds without scraping editor logs as the primary result.
- Local tools can reuse daemon sessions, read indexes, and project-scoped artifacts across repeated commands.

## 📦 Installation

### ✅ Requirements

- .NET 8 or later.
- A Unity project with NuGetForUnity when installing the Unity plugin.

### 💻 CLI

Install the CLI and confirm the command is available:

```bash
dotnet tool install --global MackySoft.Ucli --version <version>
ucli --version
```

Update an existing installation:

```bash
dotnet tool update --global MackySoft.Ucli --version <version>
```

### 🎮 Unity Plugin

Install `MackySoft.Ucli.Unity` into the Unity project with NuGetForUnity. The Unity project must be able to restore packages from nuget.org.

If you manage `Assets/packages.config` directly, add:

```xml
<package id="MackySoft.Ucli.Unity" version="<version>" manuallyInstalled="true" targetFramework="netstandard2.1" />
```

> **IMPORTANT:** Use the same pinned `<version>` for both the CLI and Unity plugin in released automation, and update them together.

### 🤖 Agent Skills

uCLI ships official agent skills with the CLI package. Install them when an agent host should know the uCLI read, plan, apply, verify, and troubleshooting workflows for a Unity repository.

Install the skills into a repository for the agent host you use:

```bash
ucli skills install --host openai --scope project
```

Supported host keys are `openai`, `claude`, and `copilot`. Project scope installs host-native skill files under the repository root:

| Host | Project target |
| --- | --- |
| `openai` | `.agents/skills` |
| `claude` | `.claude/skills` |
| `copilot` | `.github/skills` |

Run the command from the target repository. Use `--repoRoot <path>` only when the current working directory is outside the repository or when automation needs to select a repository explicitly.

Use `skills list` when you want to inspect the bundled skills, supported hosts, target directories, and reload guidance:

```bash
ucli skills list
```

Use user scope only for local, non-repository defaults:

```bash
ucli skills install --host openai --scope user
```

Preview file changes before writing:

```bash
ucli skills install --host openai --scope project --dryRun --printDiff
```

Keep installed skills aligned with the current CLI version and diagnose drift:

```bash
ucli skills update --host openai --scope project
ucli skills doctor --host openai --scope project
```

After installing or updating, reload the agent host. The command result includes `payload.reloadGuidance`; for Codex, start a new session or restart the app so newly installed skills are loaded.

## 🚀 Quick Start

Set the target Unity project once for your shell session:

```bash
export UCLI_PROJECT_PATH=./UnityProject
```

If your shell is already in the Unity project root, you can omit both `UCLI_PROJECT_PATH` and `--projectPath` for most commands.

Create repository defaults when you want project-local configuration:

```bash
ucli init
```

This step is optional; the commands below can run with `UCLI_PROJECT_PATH` or current-directory resolution.

Then confirm that uCLI can resolve the project:

```bash
ucli status
```

Wait until Unity can answer read-only work:

```bash
ucli ready --for execution
```

Inspect the installed operation catalog:

```bash
ucli ops list
ucli ops describe ucli.assets.find
```

Read project state:

```bash
ucli query assets find --pathPrefix Assets --limit 10
```

Run one planned Unity-backed request and capture the JSON result:

```bash
ucli call --withPlan > result.json <<'JSON'
{
  "steps": [
    {
      "kind": "op",
      "id": "findAssets",
      "op": "ucli.assets.find",
      "args": {
        "pathPrefix": "Assets",
        "limit": 10
      }
    }
  ]
}
JSON
```

Inspect `result.json` to confirm the request status, plan, and asset-search `opResults`.

For repeated local automation, start a daemon:

```bash
ucli daemon start
```

Use `--projectPath <path>` when a single command needs to override the environment value.

## 🧭 Runtime Modes

uCLI can run Unity-backed commands through three execution modes:
Use `--mode auto|daemon|oneshot` on Unity-backed commands to choose the mode for one command.

| Mode | Use it for |
| --- | --- |
| `oneshot` | Start Unity in batchmode for isolated commands and CI jobs. |
| `daemon` | Require an existing Unity-backed daemon for repeated local automation. |
| `auto` | Reuse a running daemon when available; otherwise fall back to one-shot batchmode, so one-off local commands and CI jobs do not need a daemon. |

Requests mean the same thing in every mode. The mode only controls process reuse and startup behavior.

## 📤 Machine-Readable Output

> **IMPORTANT:** Automation commands listed below write one final JSON result envelope to standard output. Other human-readable command-line output, progress messages, diagnostics, and entry streams are written to standard error.

Automation should parse standard output as the final JSON result. Commands with entry streams, such as `ucli logs unity read`, `ucli logs daemon read`, and `ucli test run`, write entries to standard error before the final result.

Use `--format json` when automation needs newline-delimited JSON entries from standard error. Without `--format json`, standard error is diagnostic or human-readable output and is not a stable parse target.
When a command can write entries, drain standard error concurrently while waiting for the final standard output result.

The common JSON envelope contains `protocolVersion`, `command`, `status`, `exitCode`, `message`, `payload`, and `errors`.
Use `status` and `errors[]` for command-level success or failure.
For request commands, inspect `payload.opResults` to determine which steps applied, changed, or returned operation-specific result data.
For assurance commands, inspect `payload.verdict`, `payload.verifiers[]`, `payload.claims[]`, `payload.reports`, and `payload.residualRisks[]`.
Use `ucli codes describe IPC_TIMEOUT` or another code value to read the static meaning of machine-readable codes.

Published JSON schemas are available for tools that validate uCLI output. They cover the common envelope and each command payload; operation-specific `opResults[].result` follows the `resultSchema` shown by `ucli ops describe`. Treat verifier verdicts and evidence references as returned result data, not as something JSON Schema alone can decide.

## 🔍 Reading Project State

> **TIP:** Read before you write. These commands emit machine-readable JSON.

Use `ucli refresh` when Unity project state may be stale. It may trigger refresh or import work; query commands remain the read-only inspection path.

```bash
ucli status
ucli ready --for execution
ucli refresh

ucli query assets find \
  --type "UnityEngine.Material, UnityEngine.CoreModule" \
  --limit 100

ucli query scene tree \
  --path Assets/Scenes/Main.unity \
  --depth 1

ucli query comp schema \
  --type "Game.EnemySpawner, Assembly-CSharp"

ucli resolve \
  --scene Assets/Scenes/Main.unity \
  --hierarchyPath Root/Enemies/Spawner \
  --componentType "Game.EnemySpawner, Assembly-CSharp"
```

### 🗃️ Read Index for Repeated Reads

uCLI includes a read index for read-heavy automation. It lets scripts and agents inspect operation details, asset search data, GUID/path mappings, and lightweight scene structure without reconnecting to Unity for every read.

Write commands still resolve targets against live Unity state. The read index speeds up planning, but `call` does not treat stored index data as final state.

For read-heavy workflows, `--readIndexMode` controls whether query-like commands may use stored index data. The `--read-index-mode` spelling is accepted as an alias.

| Mode | Behavior |
| --- | --- |
| `disabled` | Skip stored index data and read from Unity when the command needs project state. |
| `allowStale` | Use stored index data even when it is stale, and fall back when it is unavailable. |
| `requireFresh` | Use stored index data only when it is fresh; otherwise refresh from Unity when the command supports it. |

`ucli ready --for readIndex --readIndexMode requireFresh` checks the stored operation catalog, asset search, and GUID/path lookup data used by public read commands.
Validate a scene snapshot with `ucli query scene tree --path <scene> --readIndexMode requireFresh`.

## ✅ Assurance Workflow for Writes

Use the assurance path for normal automated writes:

```bash
ucli status
ucli ready --for mutation
ucli ops describe ucli.scene.open
ucli query scene tree --path Assets/Scenes/Main.unity --depth 2
ucli call --withPlan < request.json > result.json
ucli verify --profile built-in:mutation --from result.json
```

After C# script changes, `compile` is also available as a standalone gate before a broader verification profile:

```bash
ucli compile
ucli verify --profile built-in:script --from result.json
```

The `built-in:default` verify profile includes `compile`, so `ucli verify --from result.json` may trigger AssetDatabase refresh, script compilation, and domain reload. Use `--profile built-in:mutation` when you only need evidence from Unity after the mutation, and use `--profile built-in:script` for C# script changes. Inspect `payload.verifiers[].effects[]` when automation needs to know which verification steps may refresh, compile, or reload Unity.
`payload.profile` records the selected profile source, name, path, and digest. The digest includes profile identity as well as effective steps.

If `ucli ready --mode auto` resolves to a transient oneshot probe, the ready claim is diagnostic evidence only for that probe session. It does not guarantee that a later mutation command will reuse the same Unity process.

When a command fails, read code meanings and bounded logs instead of scraping free-form messages:

```bash
ucli codes describe IPC_TIMEOUT
ucli logs daemon read --tail 200 --level error
ucli logs unity read --tail 200 --level error
```

## 🛠️ Request Input and Planned Writes

> **IMPORTANT:** Request commands read JSON only from redirected standard input. Keep the request in your script or job and pipe it to uCLI.

Use `call --withPlan` for compact local automation where the same script or job plans and applies immediately.

```bash
ucli call --withPlan <<'JSON'
{
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
}
JSON
```

This example opens `Assets/Scenes/Main.unity`, selects `Root/Enemies/Spawner`, edits the `Game.EnemySpawner` component, and saves the scene through `commit: "context"`.

Use `ucli plan` and `ucli call --planToken` only when a review step or CI gate must inspect and approve a plan before mutation.

> **IMPORTANT:** A timeout or disconnect does not prove that nothing was applied. Inspect the JSON result, `opResults`, touched units, Unity logs, and daemon logs before retrying.

Use `ucli eval` when a local operator intentionally needs to run ad hoc C# inside the Unity Editor process without hand-writing a JSON request. It wraps `ucli.cs.eval`, returns the standard JSON envelope with `payload.plan` and `payload.opResults`, and still requires the dangerous-operation guards.

```bash
ucli eval --allowDangerous \
  --source 'return UnityEngine.Application.unityVersion;'

ucli eval --mode daemon --allowDangerous \
  --file ./eval.cs
```

## ⚠️ Dangerous Operations

> **WARNING:** `ucli call` and `ucli eval` block operations whose policy is `dangerous` unless every guard allows them: project policy, operation allowlist, and the explicit `--allowDangerous` flag. Prefer the normal `edit` flow and non-dangerous operations.

## 🏗️ Building Player Artifacts

Use `ucli build run` to run Unity BuildPipeline from a build profile and collect machine-readable build results:

```bash
ucli build run \
  --profilePath .ucli/build/player.json \
  --buildTarget standaloneLinux64
```

The build profile defines the default `buildTarget`, scenes, options, and output policy. Pass `--buildTarget` to override the profile `buildTarget` for one run. `ucli build run` writes the final JSON result to standard output, and may write progress entries to standard error before that final result.

Build artifacts are written under `.ucli/local/fingerprints/<projectFingerprint>/artifacts/build/<runId>/`.

| Artifact | Use it for |
| --- | --- |
| `build.json` | uCLI build run metadata, resolved inputs, generation validity, summary, and artifact references. |
| `build-report.json` | Normalized Unity BuildReport data. |
| `build.log` | Unity log entries for the build execution window. |
| `output-manifest.json` | File sizes and digests for generated player output files. |
| `output/` | Generated player output files. |

## 🧪 Unity Test Runs

Run Unity tests after applying edits:

```bash
ucli test run \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

Use `--unityEditorPath <path>` when the job must use a specific Unity executable or `.app` directory, or when Unity is not installed in a standard searchable location.
For repeated test settings, generate a profile with `ucli test profile init --outputPath test.profile.json` and pass it to `ucli test run` with `--profilePath test.profile.json`.

The command result includes `payload.artifactsDir` and `payload.summaryJsonPath`.
Test artifacts are written under `.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/`.

| Artifact | Use it for |
| --- | --- |
| `summary.json` | Fast pass/fail checks, result counts, and top failures. |
| `results.json` | Normalized per-test results for automation. |
| `results.xml` | Raw Unity Test Framework output from `-testResults`. |
| `editor.log` | Unity Editor diagnostics for setup failures, compiler errors, and runtime exceptions. |
| `meta.json` | The resolved test run configuration and timestamps. |

> **TIP:** When a command or test fails, read Unity and daemon logs before retrying:

```bash
ucli logs unity read --tail 200 --level error
ucli logs daemon read --tail 200
```

Stop the daemon at the end of an interactive automation session:

```bash
ucli daemon stop
```

## 🧩 Request DSL Core

This section covers the core request shape used by common automation. Operation-specific arguments and policies come from the operation catalog exposed by `ucli ops list` and `ucli ops describe`.

A request is one ordered unit of work:

```json
{
  "steps": []
}
```

| Field | Meaning |
| --- | --- |
| `steps` | Ordered steps. uCLI runs them in array order. |

`protocolVersion` and `requestId` are generated by the CLI and must not be included in user-authored request JSON.

Each step is either `kind: "op"` or `kind: "edit"`.

### ⚙️ Direct Operation Step

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

### ✏️ Edit Step

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

> **IMPORTANT:** `scene` and `prefab` edits that mutate or use `commit: "context"` need that context open. Put `ucli.scene.open` or `ucli.prefab.open` before the edit step when the request has not opened it already.

### 📍 Edit Contexts

| Context | JSON | Use it for |
| --- | --- | --- |
| Scene | `{ "scene": "Assets/Scenes/Main.unity" }` | GameObjects and components in a scene. |
| Prefab | `{ "prefab": "Assets/Prefabs/Enemy.prefab" }` | GameObjects and components in a prefab stage. |
| Asset | `{ "asset": "Assets/Data/GameBalance.asset" }` | A main asset such as a ScriptableObject. |
| Project | `{ "project": true }` | Project-scoped assets such as `ProjectSettings/TagManager.asset`. |

### 🎯 Selectors

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

> **IMPORTANT:** `all` runs the same action list for every selected target in deterministic order. Actions that create a new global resource, such as `createAsset` and `createPrefab`, require the selection to resolve to at most one target.

### ▶️ Actions

> **NOTE:** If `target` is omitted, the action uses the current selected target. `createPrefab` is the exception: it always requires an explicit `target` and `path`. An action that creates or ensures an object can expose it with `as`, and later actions in the same step can refer to it with `"$name"`.

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

### 💾 Commit

| Value | Behavior |
| --- | --- |
| `none` | Apply the edit in memory and do not save from this step. |
| `context` | Save the current scene, prefab, asset, or project context. |
| `project` | Save project-scoped changes and request-attributed open scene or prefab contexts. |

> **IMPORTANT:** uCLI does not implicitly save an edit step. Choose `commit` intentionally.

### 🧷 Direct Operation Target Selectors

Direct operation steps that take a `target` use one of these selector shapes:

```json
{ "globalObjectId": "GlobalObjectId_V1-..." }
{ "assetGuid": "0123456789abcdef0123456789abcdef" }
{ "assetPath": "Assets/Data/GameBalance.asset" }
{ "projectAssetPath": "ProjectSettings/TagManager.asset" }
{ "scene": "Assets/Scenes/Main.unity", "hierarchyPath": "Root/Enemies/Spawner" }
{ "scene": "Assets/Scenes/Main.unity", "hierarchyPath": "Root/Enemies/Spawner", "componentType": "Game.EnemySpawner, Assembly-CSharp" }
{ "prefab": "Assets/Prefabs/Enemy.prefab", "hierarchyPath": "Root/Visual" }
```

Do not put `{ "var": "..." }` or `"var": null` in direct `op` args.
To name a value produced by an edit action, use the edit action `as` field and refer to that name through the edit DSL form, such as `$createdObject`.
`ops describe` omits the `var` selector branch, and direct `op` execution rejects it.

Direct `set` operations use `sets`, while edit steps use the shorter `values` form:

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

## 📚 Operation Catalog

The installed Unity plugin exposes the operations available to requests.

```bash
ucli ops list
ucli ops describe ucli.comp.set
```

`ops list` returns operations that can be used directly in `kind:"op"` request steps. Operations used only by higher-level edit flows are not listed for direct selection.

Use `ops describe` to check:

- operation kind and policy
- inputs, constraints, result data, and JSON input/result shape
- `readIndex` source and freshness metadata

README examples show common operations only. The installed Unity plugin's operation catalog is the authoritative list for that project.

| Operation | Type | Args | Use it for |
| --- | --- | --- | --- |
| `ucli.scene.open` | command | `{ path }` | Ensure a scene is loaded. |
| `ucli.scene.save` | mutation | `{ path }` | Save a loaded scene. |
| `ucli.prefab.open` | command | `{ path }` | Open a prefab editing context. |
| `ucli.prefab.save` | mutation | `{ path }` | Save the opened prefab context. |
| `ucli.project.refresh` | command | `{}` | Refresh the Unity project and AssetDatabase. |
| `ucli.project.save` | mutation | `{}` | Save project assets, project settings, and tracked open contexts. |

Common operation groups include:

- `ucli.scene.*` - open, inspect, and save scenes.
- `ucli.prefab.*` - open, edit, save, and create prefabs.
- `ucli.assets.*` / `ucli.asset.*` - find assets, inspect asset data, and update asset values.
- `ucli.go.*` - create, describe, delete, and reparent GameObjects.
- `ucli.comp.*` - inspect, ensure, and set components.
- `ucli.project.*` - refresh and save project-scoped state.

## 🧰 Command Guide

| Command | Use it when you need to |
| --- | --- |
| `ucli init` | Create optional project-local uCLI configuration. |
| `ucli status` | Check Unity project resolution and daemon lifecycle state. |
| `ucli ready` | Wait until Unity is ready for reading or mutation. |
| `ucli refresh` | Refresh Unity project state. |
| `ucli compile` | Verify Unity script compilation and domain reload readiness. |
| `ucli query` | Read project data without writing changes. |
| `ucli resolve` | Resolve a selector to a Unity object identifier. |
| `ucli ops` | List and inspect available operations. |
| `ucli codes` | List and describe machine-readable code values used in JSON output. |
| `ucli call` | Apply a request; use `--withPlan` for the normal planned write path. |
| `ucli eval` | Run ad hoc C# through `ucli.cs.eval` without authoring a JSON request. |
| `ucli plan` | Prepare a separated review gate and receive a `planToken`. |
| `ucli validate` | Diagnose static request validation without running `plan` or `call`. |
| `ucli verify` | Return a JSON verification result for Unity-side checks. |
| `ucli build run` | Run Unity BuildPipeline from a build profile and return build evidence. |
| `ucli logs` | Read Unity or daemon logs. |
| `ucli daemon` | Manage daemon sessions. |
| `ucli test` | Run Unity Test Framework tests. |

Common options:

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--projectPath <path>` | Unity-backed commands | Target Unity project path. Overrides `UCLI_PROJECT_PATH` and current-directory resolution. |
| `--mode auto\|daemon\|oneshot` | Unity-backed commands | Choose daemon reuse or one-shot batchmode. |
| `--timeout <milliseconds>` | Unity-backed commands | Override the command timeout. |
| `--readIndexMode disabled\|allowStale\|requireFresh`, `--read-index-mode disabled\|allowStale\|requireFresh` | Query-like commands | Control read-index use. |
| `--failFast` | Unity-backed commands | Fail when the Unity editor lifecycle is not ready instead of waiting. |
| `--withPlan` | `ucli call` | Run a plan pass inside `call` and include it in the result. |
| `--planToken <token>` | `ucli call` | Apply a request using a token returned by `ucli plan`. |
| `--allowDangerous` | `ucli call`, `ucli eval` | Allow operations whose catalog policy is `dangerous`. |
| `--allowPlayMode` | `ucli plan`, `ucli call`, `ucli eval` | Allow guarded Play Mode mutation in a GUI Editor session. |

Use `--mode daemon` when CI must fail specifically because no daemon is running. With `--mode auto`, a missing daemon may start a one-shot Unity process; if startup fails, inspect `payload.startup`, `payload.diagnosis`, and `retryDisposition`.

> **NOTE:** Project path resolution uses `--projectPath`, then `UCLI_PROJECT_PATH`, then the command default. The default is usually the current working directory.

### Lifecycle Lock Location

Lifecycle lock files are stored in the current user's OS local application data directory, not under repo-local `.ucli` state:

| OS | Local application data root | Lifecycle lock path |
| --- | --- | --- |
| Windows | `%LOCALAPPDATA%` | `%LOCALAPPDATA%\MackySoft\ucli\lifecycle-locks\unity-projects\<sha256>\lifecycle.lock` |
| macOS | `$HOME/Library/Application Support` | `$HOME/Library/Application Support/MackySoft/ucli/lifecycle-locks/unity-projects/<sha256>/lifecycle.lock` |
| Linux | `$XDG_DATA_HOME` when absolute, otherwise `$HOME/.local/share` | `${XDG_DATA_HOME:-$HOME/.local/share}/MackySoft/ucli/lifecycle-locks/unity-projects/<sha256>/lifecycle.lock` |

`<sha256>` is derived from the normalized physical `UnityProjectRoot`, so paths that resolve to the same physical Unity project share one launch lock.

Unity's project-local `Temp/UnityLockfile` is treated as a Unity-owned marker for editors opened outside uCLI. uCLI does not clean it on a timer or in the background; it attempts cleanup only before starting a new Unity process and after a uCLI-launched Unity process exits. uCLI deletes the marker only when those checks can prove it is stale. Active ownership returns `UNITY_PROJECT_ALREADY_OPEN`, unsafe ownership checks return `UNITY_PROJECT_LOCK_AMBIGUOUS`, and stale-lock deletion failures return `UNITY_PROJECT_LOCK_CLEANUP_FAILED`.

## 🧱 Authoring Project-Specific Operations

Extensions can expose operations under names such as `myorg.navmesh.bake`.

Custom operations are not hidden shortcuts. Once they are in the catalog, they follow the same policy, JSON output, and discovery rules as built-in operations, so scripts, agents, and CI can find them with `ucli ops list` and inspect them with `ucli ops describe`.

Skip this section if you only run the built-in Unity operations. Use it when your Unity project needs to expose project-specific operations through uCLI.

Custom operations are Unity Editor code. Put the implementation in an Editor assembly that references `MackySoft.Ucli.Unity`. If another tool needs to compile against the same Args/Result types, put those types in a shared assembly. The published operation details are available through `ucli ops describe`.

An operation has three parts:

1. Define a typed Args type and, when needed, a typed Result type.
2. Add descriptions and constraints to Args properties or reusable semantic value types.
3. Implement `UcliOperation<TArgs,TResult>` and mark the class with `[UcliOperation]`.

Use `UcliNoResult` for operations that do not emit `opResults[].result`.

The Args and Result types define what callers send and receive. `UcliOperationMetadata.Create<TArgs,TResult>` publishes the operation inputs, result data, JSON shapes, public kind, and policy from those types and their attributes. Do not hand-write JSON Schema for a normal operation.

Request/result rules:

| Rule | Why it matters |
| --- | --- |
| Put `[UcliDescription]` on every Args/Result type and every public property. | `ops describe` shows these descriptions to callers. |
| Use `[UcliRequired]` for required properties. Do not use C# `required` for the uCLI JSON shape. | uCLI uses the attribute when publishing and validating request JSON. |
| Leave optional properties nullable and omit `[UcliRequired]`. | Optional inputs stay optional in the published JSON shape. |
| Use `[JsonConstructor]` when the type has a non-default constructor. | uCLI deserializes `steps[].args` with `System.Text.Json` before validation. |
| Use `[JsonPropertyName]` when the JSON member name must differ from the C# property name. | `ops describe` and JSON validation use the JSON name. |
| Use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` for optional result or selector properties that should be omitted when absent. | Omitted and explicit `null` have different meanings in the published JSON shape. |
| Use `[UcliJsonAllowNull]` only when explicit JSON `null` is valid for a reference-type property. | Nullable value types such as `int?` already allow JSON `null`; nullable reference syntax alone does not change runtime validation. |
| Use `[UcliJsonAnyValue]` only for intentional arbitrary JSON value slots, such as a serialized property value. | It disables structural validation for that property. |

Use existing semantic value types before adding new plain strings:

- `SceneAssetPath`
- `PrefabAssetPath`
- `UnityAssetPath`
- `ProjectSettingsAssetPath`
- `CreatableUnityAssetPath`
- `CreatablePrefabAssetPath`
- `ProjectRelativePathPrefix`
- `UnityHierarchyPath`
- `UnityHierarchyPathPrefix`
- `UnityGlobalObjectId`
- `UnityAssetGuid`
- `UnityTypeId`
- `UnityComponentTypeId`
- `SerializedPropertyPath`

User-defined semantic value objects are supported for string-shaped values that remain JSON strings in requests and results.
Create one only when the same meaning appears in multiple Args/Result types or when the meaning is important enough to name for callers.
For one-off meaning, keep a normal property and put `[UcliDescription]` and `[UcliInputConstraint]` on that property instead.
If you need a new string-shaped semantic value:

- Derive from `UcliStringValue`.
- Define a public `string` constructor.
- Add `[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]`.
- Add `[UcliDescription]`.
- Put `[UcliInputConstraint]` attributes on the value type.

Do not use arbitrary custom scalar wrappers unless uCLI has a supported base type for that JSON shape.

```csharp
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Addressable asset key used by this project.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
public sealed record AddressableKey : UcliStringValue
{
    [JsonConstructor]
    public AddressableKey (string value)
        : base(value)
    {
    }
}

[UcliDescription("Arguments for setting an Addressables label.")]
public sealed record SetAddressableLabelArgs
{
    [JsonConstructor]
    public SetAddressableLabelArgs (
        AddressableKey key,
        string label)
    {
        Key = key;
        Label = label;
    }

    [UcliRequired]
    [UcliDescription("Addressable key to update.")]
    public AddressableKey Key { get; init; }

    [UcliRequired]
    [UcliDescription("Label to assign.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Label { get; init; }
}
```

`AddressableKey` remains a JSON string in `steps[].args`.
Because `Key` has its own `[UcliDescription]`, `ops describe` uses the property description for that input.
If the property does not declare `[UcliDescription]`, uCLI falls back to the `UcliStringValue` type description.
The `NonEmpty` constraint comes from the value type and appears in `ops describe` as input metadata.

Input constraints describe the meaning of values in `ops describe`.
Put `[UcliInputConstraint]` on a semantic value type when every use of that type has the same meaning, or on a property when the meaning is specific to one operation.

| Constraint kind | Required parameter | Use it for |
| --- | --- | --- |
| `NonEmpty` | none | Non-empty strings, arrays, or objects. |
| `Range` | `Min`, `Max`, or both | Inclusive numeric bounds. |
| `ProjectRelativePath` | none | Paths relative to the Unity project. |
| `AssetExists` | `AssetKind` | Existing asset, scene, prefab, or project settings paths. |
| `AssetCreatable` | `AssetKind` | Asset or prefab paths that an operation may create. |
| `GlobalObjectId` | none | Unity GlobalObjectId strings. |
| `HierarchyPath` | none | Unity scene or prefab hierarchy paths. |
| `ReferenceResolvable` | `TargetKind` | Object references that must resolve to an asset, GameObject, or component. |
| `TypeExists` | none | Unity type identifiers that must resolve in the project. |
| `TypeAssignableTo` | `TypeKind` | Unity type identifiers assignable to a specific Unity kind, such as component. |
| `SerializedProperty` | `Access` | SerializedProperty paths that must be writable for the operation. |
| `AssetGuid` | none | Unity asset GUID strings. |
| `Cursor` | none | Opaque bounded-window cursors returned by read operations. |

For object references and selectors, prefer existing reference types such as `AssetReferenceArgs`, `GameObjectReferenceArgs`, `SceneGameObjectReferenceArgs`, `ComponentReferenceArgs`, and `ResolveSelectorArgs`.
If an operation needs a new reference object, use `[UcliExclusiveRequiredPropertySet]` on the object type to define mutually exclusive selector shapes.
Use `[UcliPropertyRequires]` when one property requires other properties.

Declare operation behavior deliberately:

| Behavior field | Values | Use it for |
| --- | --- | --- |
| `declaredKind` | `Query`, `Command`, `Mutation` | The operation's public intent. |
| `UcliOperationAssuranceContract` | side effects, dirty/persist flags, touched kinds, plan mode | Behavior facts that help callers decide whether an operation is acceptable. |
| `UcliOperationPlanMode` | `ValidationOnly`, `ObservesLiveUnity` | How much the `Plan` phase may do before `Call`. |
| `UcliOperationCodeContract` | source forms, entry point, source-visible API, return constraints | Required for operations that accept source code. Arbitrary source execution is dangerous. |
| `UcliOperationExposure` | `Public`, `EditLoweringOnly`, `Internal` | Whether callers can select the operation directly or only through higher-level edit flows. |

Do not choose `policy` manually.
uCLI publishes it from the operation's declared intent, side effects, persistence behavior, touched targets, source-code execution, exposure, destructive scope, and external process or filesystem access.

Safe operations are bounded observations that cannot dirty, persist, or change Editor state, and do not execute arbitrary code or external processes.
Advanced operations include deterministic Unity Editor API writes, Editor state changes, dirty or persisted Unity content, AssetDatabase refresh/import/compile effects, and broader project effects.
Dangerous operations are escape hatches.
Examples include arbitrary C# execution, arbitrary shell/process/filesystem writes, unbounded destructive operations, or operations whose touched/save boundary cannot be sufficiently guaranteed.

Keep phase behavior consistent:

- `Validate` checks typed args and cheap preconditions.
- `Plan` may inspect Unity state according to `planMode`, but must not persist content.
- `Call` performs the operation.
- Query operations must report `applied:false`, `changed:false`, and `touched:[]`.
- `applied`, `changed`, and `touched` belong to the operation result envelope. Do not put those fields in `TResult`.
- `TResult` should contain only the operation-specific main data. Use `UcliNoResult` when there is no main data.

```csharp
using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

[UcliDescription("Arguments for counting GameObjects in a scene.")]
public sealed record CountSceneObjectsArgs
{
    [JsonConstructor]
    public CountSceneObjectsArgs (SceneAssetPath path)
    {
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
    public SceneAssetPath Path { get; init; }
}

[UcliDescription("GameObject count result.")]
public sealed record CountSceneObjectsResult
{
    [JsonConstructor]
    public CountSceneObjectsResult (int count)
    {
        Count = count;
    }

    [UcliRequired]
    [UcliDescription("Number of GameObjects found in the scene.")]
    public int Count { get; init; }
}

[UcliOperation]
internal sealed class CountSceneObjectsOperation : UcliOperation<CountSceneObjectsArgs, CountSceneObjectsResult>
{
    public override UcliOperationMetadata Metadata { get; } =
        UcliOperationMetadata.Create<CountSceneObjectsArgs, CountSceneObjectsResult>(
            operationName: "game.scene.countGameObjects",
            declaredKind: UcliOperationKind.Query,
            description: "Counts GameObjects in a Unity scene.",
            assurance: new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));

    protected override Task<OperationPhaseStepResult> Validate (
        NormalizedOperation operation,
        CountSceneObjectsArgs args,
        OperationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationPhaseStepResult.Success());
    }

    protected override Task<OperationPhaseStepResult> Plan (
        NormalizedOperation operation,
        CountSceneObjectsArgs args,
        OperationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SuccessWithResult(new CountSceneObjectsResult(0), applied: false, changed: false));
    }

    protected override Task<OperationPhaseStepResult> Call (
        NormalizedOperation operation,
        CountSceneObjectsArgs args,
        OperationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SuccessWithResult(new CountSceneObjectsResult(0), applied: false, changed: false));
    }
}
```

The example leaves the Unity scene traversal out of the snippet so the request/result shape is visible.
In a real operation, keep Unity object resolution and mutation inside `Validate`, `Plan`, or `Call`, and keep `JsonElement` out of the operation body.
Use existing semantic value types such as `SceneAssetPath`, `PrefabAssetPath`, `UnityHierarchyPath`, `UnityGlobalObjectId`, `UnityAssetGuid`, and `UnityTypeId` before introducing a new value type.

After Unity recompiles the Editor assembly, confirm that the operation is discoverable from the CLI:

```bash
ucli ops describe game.scene.countGameObjects --projectPath ./UnityProject
```

## 📦 Packages

| Package | NuGet | Role |
| --- | --- | --- |
| `MackySoft.Ucli` | [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=)](https://www.nuget.org/packages/MackySoft.Ucli) | .NET global tool that provides the `ucli` command. |
| `MackySoft.Ucli.Unity` | [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) | Unity Editor plugin for uCLI IPC and automation. |
| `MackySoft.Ucli.Contracts` | [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli.Contracts?label=)](https://www.nuget.org/packages/MackySoft.Ucli.Contracts) | Shared IPC protocol and data contract types. |
| `MackySoft.Ucli.Infrastructure` | [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli.Infrastructure?label=)](https://www.nuget.org/packages/MackySoft.Ucli.Infrastructure) | Shared infrastructure services used by uCLI runtime components. |

## 💬 Support

Use [GitHub Issues](https://github.com/mackysoft/ucli/issues) for bugs, feature requests, usage questions, and README problems.

For bug reports, include:

- `ucli --version`
- Unity version
- Operating system
- The command you ran

## 💖 Sponsor

If uCLI helps your Unity automation workflow, please support MackySoft through GitHub Sponsors:

<https://github.com/sponsors/mackysoft>

## 👤 Author

Hiroya Aramaki is an indie game developer in Japan.

- Website: <https://mackysoft.net/>
- GitHub: <https://github.com/mackysoft>

## 📄 License

uCLI is under the [MIT License](LICENSE).
