# uCLI - Contract-first Unity automation for agents, CI, and tools

[![verify](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml/badge.svg)](https://github.com/mackysoft/ucli/actions/workflows/verify.yaml) [![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli?label=MackySoft.Ucli)](https://www.nuget.org/packages/MackySoft.Ucli) [![NuGet Unity](https://img.shields.io/nuget/v/MackySoft.Ucli.Unity?label=MackySoft.Ucli.Unity)](https://www.nuget.org/packages/MackySoft.Ucli.Unity) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://twitter.com/makihiro_dev))**

uCLI turns Unity Editor changes into reviewable, repeatable, machine-readable workflows for scripts, CI, and AI agents.

It reads Unity state, declares the intended change, applies it through Unity Editor APIs with internal validation and planning, chooses the save boundary, and returns structured evidence.

uCLI is not a remote-control wrapper around the Unity Editor. It is an execution protocol for workflows where automation must be inspected, replayed, gated, and trusted.

## ❓ Why uCLI?

Unity automation usually fails in the control plane, not only in the edit API. Scripts and agents need to know which Unity project they are talking to, whether the Editor is compiling or reloading, whether a request was applied before a timeout, what contexts were touched, and whether a reviewed plan is still valid when applied.

In short:

- Unity is callable.
- Unity changes should be reviewable.
- The normal workflow is `read -> call --withPlan -> verify`.

uCLI focuses on those guarantees:

- **Planned calls by default:** `ucli call --withPlan` validates, plans, and applies one request in one command; split `planToken` flows are available when a review gate must separate planning from mutation.
- **Live Unity source of truth:** mutations go through Unity Editor APIs and are re-resolved against live Unity state.
- **Context-bound edits:** every edit belongs to a scene, prefab, asset, or project boundary.
- **Explicit persistence:** changing an object and saving a context are separate decisions.
- **Machine-readable evidence:** automation receives structured JSON with `opResults`, `applied`, `changed`, `touched`, errors, logs, and artifacts.
- **Lifecycle-aware execution:** compile, domain reload, busy, play mode, shutdown, and blocked states are surfaced as protocol states or structured errors.
- **Worktree-safe sessions:** daemon state, indexes, artifacts, and writer exclusion are scoped by project identity.
- **Dangerous operations are opt-in:** unsafe paths are isolated behind operation policy and `--allowDangerous`.

## 🧭 What uCLI Makes Explicit

uCLI is built around the review boundary: an automated Unity change should be inspectable before mutation, attributable after execution, and diagnosable when the runtime state is uncertain.

| Concern | What must be knowable | uCLI contract |
| --- | --- | --- |
| Editor readiness | Whether Unity can accept the request now. | Lifecycle states are surfaced; execution waits or fails with structured errors. |
| Reviewed plan drift | Whether Unity state still matches the reviewed plan. | `planToken` validates request and state before `call`. |
| Project and worktree identity | Which project owns local state, indexes, artifacts, and writer exclusion. | Local state is scoped by `projectFingerprint`. |
| Timeout recovery | Whether a retry is safe after timeout or disconnect. | Timeout is not proof of no-op; inspect returned results when available and logs before retrying. |
| Persistence | Whether a mutation also saved project data. | `commit` makes save boundaries explicit. |
| Evidence | What changed, what was touched, and where diagnostics live. | JSON envelopes, logs, and test artifacts are first-class outputs. |
| Operation discovery | Which operation contract is installed for this project. | `ops describe` exposes the installed operation's kind, policy, argument schema, and static constraints. |
| Read freshness | Whether cached read data is fresh, stale, or advisory. | readIndex accelerates reads, while `call` re-resolves against live Unity state. |
| Guarded execution | Which requests cross the normal edit boundary. | Dangerous operations are isolated and require explicit opt-in. |

## 🧠 Design Contracts

uCLI is designed around assurance, not convenience-first automation.

| Contract | What uCLI does |
| --- | --- |
| Unity remains the source of truth | Mutations go through Unity Editor APIs. |
| Edits have context | Every edit declares a scene, prefab, asset, or project context. |
| Planned writes are explicit | `ucli call --withPlan` validates, plans, and applies in one command; `ucli plan` and `--planToken` support separated review gates. |
| Saves are explicit | `commit` controls persistence with `"none"`, `"context"`, or `"project"`. |
| Results are evidence | JSON exposes `opResults`, `applied`, `changed`, `touched`, errors, logs, and artifacts. |
| Runtime is operational | `daemon`, `auto`, and `oneshot` do not change request meaning. |
| Unsafe paths are isolated | Dangerous operations require catalog policy and `--allowDangerous`. |

## ✨ What You Can Do

Use uCLI when you need to automate Unity from scripts, CI, or agents without losing visibility into project state and saved changes.

### 🤖 For Agents

Agents should not hard-code operation arguments or guess Unity state from memory.

- Discover available operations with `ucli ops list` and `ucli ops describe`.
- Treat `ucli ops describe <operation>` as the runtime contract for that operation's kind, policy, argument schema, and static constraints.
- Inspect assets, scene trees, components, and serialized schemas before editing.
- Build JSON requests with primitive `op` steps and higher-level `edit` steps.
- Use `ucli call --withPlan` for normal writes; reserve `ucli validate` and `ucli plan` for diagnostics or separated review gates.

### 🧪 For CI

- Run Unity in one-shot headless batchmode for isolated jobs.
- Execute Unity Test Framework tests and collect normalized artifacts.
- Parse one JSON result envelope from standard output for automation decisions.
- Avoid log scraping by using structured result envelopes, exit codes, and test summaries.

### 🧰 For Local Tool Workflows

- Start a daemon for repeated Unity-backed commands.
- Reuse read indexes for operation metadata, schemas, asset search, and scene inspection.
- Read Unity and daemon logs from the same CLI.

### 🌿 For Multi-Worktree Development

- Scope sessions, indexes, artifacts, and writer exclusion by project identity.
- Keep daemon state separate across Git worktrees.

## 📦 Installation

### ✅ Requirements

- .NET 8 or later.
- A Unity project with NuGetForUnity when installing the Unity plugin.

### 💻 CLI

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

### 🎮 Unity Plugin

Install `MackySoft.Ucli.Unity` into the Unity project with NuGetForUnity. The Unity project must be able to restore packages from nuget.org.

If you manage `Assets/packages.config` directly, add:

```xml
<package id="MackySoft.Ucli.Unity" version="<version>" manuallyInstalled="true" targetFramework="netstandard2.1" />
```

> **IMPORTANT:** Use a pinned `<version>` for both the CLI and Unity plugin in released automation.

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

Inspect the installed operation catalog:

```bash
ucli ops list
ucli ops describe ucli.scene.open
```

Read project state:

```bash
ucli query assets find --pathPrefix Assets --limit 10
```

For repeated local automation, start a daemon:

```bash
ucli daemon start
```

Use `--projectPath <path>` when a single command needs to override the environment value.

## 🧭 Compatibility and Stability

- Pin `MackySoft.Ucli` and `MackySoft.Ucli.Unity` to compatible released versions and update them together.
- The CLI generates the internal request protocol metadata for automation workflows.
- `MackySoft.Ucli.Contracts` is for direct IPC protocol and tooling consumers.
- `MackySoft.Ucli.Infrastructure` is an advanced integration package for runtime support code.
- Operations marked `dangerous` are outside the normal guarded edit path and require an explicit `ucli call --allowDangerous`.

## 🔄 Typical Workflow

uCLI is normally driven by a runner: a local shell script, a continuous integration job, or an agent. The runner reads Unity state, builds one JSON request, sends that request to uCLI, and decides whether to accept the result.

Set up optional project-local configuration once per repository:

```bash
ucli init
```

Confirm that uCLI can resolve the target Unity project:

```bash
ucli status
```

Start a daemon when an interactive session or local automation will run several Unity-backed commands:

```bash
ucli daemon start
```

> **NOTE:** For one-off local commands and CI jobs, you can skip the daemon. The default `--mode auto` uses a running daemon when one is available and falls back to one-shot batchmode when it is not.

### 🧭 One Contract, Multiple Runtimes

uCLI can run Unity-backed commands through three execution modes:

| Mode | Use it for |
| --- | --- |
| `oneshot` | Start Unity in batchmode for isolated commands and CI jobs. |
| `daemon` | Require an existing Unity-backed daemon for repeated local or agent requests. |
| `auto` | Reuse a running daemon when available; otherwise fall back to one-shot batchmode. |

The request protocol does not change between these modes. Runtime choice is operational, not semantic.

## 📤 Automation Output Contract

> **IMPORTANT:** Except for `ucli logs`, the automation commands listed below write one JSON result envelope to standard output. Help and version output are human-readable command-line output. Progress messages and diagnostics that are not part of the JSON result contract are written to standard error.

`ucli logs unity` and `ucli logs daemon` write log entries to standard output. Use `--format json` when a runner needs newline-delimited JSON log events.

> **IMPORTANT:** Automation should parse standard output and treat standard error as diagnostic text.

The common JSON envelope contains `protocolVersion`, `command`, `status`, `exitCode`, `message`, `payload`, and `errors`.
Use `status` and `errors[]` for command-level success or failure.
For request commands, inspect `payload.opResults` to determine which steps applied, changed, or returned operation-specific result data.
Use other command-specific `payload` fields for results such as `planToken`, `readIndex`, and test artifact paths.

## 🔍 Reading Project State

> **TIP:** Read before you write. These commands emit machine-readable JSON.

Use `ucli refresh` when Unity project state may be stale. It may trigger refresh or import work; query commands remain the read-only inspection path.

```bash
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

### 🗃️ Read Index for Agent Loops

uCLI includes a read index for read-heavy automation. It lets agents and scripts inspect operation metadata, schemas, asset search data, GUID/path mappings, and lightweight scene structure without reconnecting to Unity for every reasoning step.

Mutations still re-resolve against live Unity state. The read index improves planning and validation, but it is never the source of truth for `call`.

For read-heavy workflows, `--readIndexMode` controls whether query-like commands may use stored index data:

| Mode | Behavior |
| --- | --- |
| `disabled` | Skip stored index data and read from Unity when the command needs project state. |
| `allowStale` | Use stored index data even when it is stale, and fall back when it is unavailable. |
| `requireFresh` | Use stored index data only when it is fresh; otherwise refresh from Unity when the command supports it. |

The operation catalog is also available from the CLI:

```bash
ucli ops list
ucli ops describe ucli.scene.open
```

`ops describe` returns the agent-facing operation contract for one primitive operation. Agents should use `description`, `inputs[].constraints`, `inputs[].variants[].fields[].constraints`, `resultContract`, and `assurance` to choose the operation, build `steps[].args`, and interpret results. Reusable operation values such as scene asset paths, prefab asset paths, hierarchy paths, GlobalObjectId strings, asset GUIDs, and Unity type identifiers are modeled as semantic Args/Result value types in C#, while the IPC JSON remains primitive strings. Input descriptions and semantic constraints are generated from Args property attributes and those semantic value-type attributes. The generated `argsSchema` and `resultSchema` validate only JSON structure; descriptions and semantic constraints live in the describe contract, not in JSON Schema constraint keywords.

## Authoring Custom Operations

Custom operations are Unity Editor code. Put the implementation in an Editor assembly that references `MackySoft.Ucli.Unity`, and put reusable Args/Result contracts in a shared assembly when another tool should compile against them. If only the Unity-side implementation needs the CLR types, the public wire contract is still available through `ucli ops describe`.

An operation has three parts:

1. Define a typed Args contract and, when needed, a typed Result contract.
2. Add descriptions and semantic constraints to Args properties or reusable semantic value types.
3. Implement `UcliOperation<TArgs,TResult>` and mark the class with `[UcliOperation]`.

Use `UcliNoResult` for operations that do not emit `opResults[].result`.

The Args and Result CLR types are the source of truth for the public operation contract. `UcliOperationMetadata.Create<TArgs,TResult>` derives `inputs`, `resultContract`, `argsSchema`, and `resultSchema` from those types, their attributes, and the metadata passed to `Create`. Do not hand-write JSON Schema for a normal operation.

Contract rules:

| Rule | Why it matters |
| --- | --- |
| Put `[UcliDescription]` on every Args/Result contract type and every public contract property. | `ops describe` uses these descriptions as the user-facing and agent-facing explanation. |
| Use `[UcliRequired]` for required properties. Do not use C# `required` for the uCLI contract. | Unity-compatible builds and schema generation read the uCLI attribute. |
| Leave optional properties nullable and omit `[UcliRequired]`. | Optional inputs are not listed in `argsSchema.required`. |
| Use `[JsonConstructor]` when the contract has a non-default constructor. | uCLI deserializes `steps[].args` with `System.Text.Json` before validation. |
| Use `[JsonPropertyName]` when the JSON member name must differ from the C# property name. | `argsSchema`, `resultSchema`, and `ops describe` use the JSON name. |
| Use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` for optional result or selector properties that should be omitted when absent. | Omitted and explicit `null` have different meanings in the public JSON contract. |
| Use `[UcliJsonAllowNull]` only when explicit JSON `null` is valid for a reference-type property. | Nullable value types such as `int?` already allow JSON `null`; nullable reference syntax alone does not change the runtime JSON contract. |
| Use `[UcliJsonAnyValue]` only for intentional arbitrary JSON value slots, such as a serialized property value. | It disables structural validation for that property. |

Use existing semantic value types before adding new primitive strings: `SceneAssetPath`, `PrefabAssetPath`, `UnityAssetPath`, `ProjectSettingsAssetPath`, `CreatableUnityAssetPath`, `CreatablePrefabAssetPath`, `ProjectRelativePathPrefix`, `UnityHierarchyPath`, `UnityHierarchyPathPrefix`, `UnityGlobalObjectId`, `UnityAssetGuid`, `UnityTypeId`, `UnityComponentTypeId`, and `SerializedPropertyPath`.
User-defined semantic value objects are supported for string-shaped values that remain JSON strings on the IPC boundary.
Create one only when the same meaning appears in multiple Args/Result contracts or when the meaning is important enough to name in the public contract.
For one-off meaning, keep a normal property and put `[UcliDescription]` and `[UcliInputConstraint]` on that property instead.
If you need a new string-shaped semantic value, derive from `UcliStringValue`, define a public `string` constructor, add `[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]`, add `[UcliDescription]`, and put `[UcliInputConstraint]` attributes on the value type.
Do not use arbitrary custom scalar wrappers unless uCLI has a contract base type and schema generator support for that wire shape.

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

`AddressableKey` remains a JSON string in `steps[].args`, so the generated `argsSchema` contains `"key": { "type": "string" }`.
Because `Key` has its own `[UcliDescription]`, `ops describe` uses the property description for that input.
If the property does not declare `[UcliDescription]`, uCLI falls back to the `UcliStringValue` type description.
The `NonEmpty` constraint comes from the value type and appears in `ops describe` as input metadata.

Input constraints describe the meaning of values in `ops describe`; they are not JSON Schema keywords. Put `[UcliInputConstraint]` on a semantic value type when every use of that type has the same meaning, or on a property when the meaning is specific to one operation.

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

For object references and selectors, prefer existing contract types such as `AssetReferenceArgs`, `GameObjectReferenceArgs`, `SceneGameObjectReferenceArgs`, `ComponentReferenceArgs`, and `ResolveSelectorArgs`. If an operation needs a new reference object, use `[UcliExclusiveRequiredPropertySet]` on the object type to define mutually exclusive selector shapes, and `[UcliPropertyRequires]` when one property requires other properties.

Choose operation metadata deliberately:

| Metadata | Values | Use it for |
| --- | --- | --- |
| `UcliOperationKind` | `Query`, `Command`, `Mutation` | `Query` observes only. `Command` changes Editor or AssetDatabase state without content mutation as the main purpose. `Mutation` can dirty or persist scene, prefab, asset, or project content. |
| `OperationPolicy` | `Safe`, `Advanced`, `Dangerous` | `Safe` is suitable for normal guarded automation. `Advanced` covers writes and broader project effects. `Dangerous` is for escape hatches and requires explicit `ucli call --allowDangerous`. |
| `UcliOperationAssuranceContract` | side effects, dirty/persist flags, touched kinds, plan mode | Machine-readable behavior that lets runners decide whether an operation is acceptable. |
| `UcliOperationPlanMode` | `ValidationOnly`, `ObservesLiveUnity`, `MayCreatePreviewState` | How much the `Plan` phase may do before `Call`. |

Keep phase behavior consistent:

- `Validate` checks typed args and cheap preconditions.
- `Plan` may inspect Unity state according to `planMode`, but must not persist content.
- `Call` performs the operation.
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
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            description: "Counts GameObjects in a Unity scene.",
            assurance: new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                new[] { IpcExecuteTouchedResourceKindNames.Scene },
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
        return Task.FromResult(SuccessWithResult(new CountSceneObjectsResult(0), applied: true, changed: false));
    }
}
```

The example leaves the Unity scene traversal out of the snippet so the contract shape is visible. In a real operation, keep Unity object resolution and mutation inside `Validate`, `Plan`, or `Call`, and keep `JsonElement` out of the operation body. Use existing semantic value types such as `SceneAssetPath`, `PrefabAssetPath`, `UnityHierarchyPath`, `UnityGlobalObjectId`, `UnityAssetGuid`, and `UnityTypeId` before introducing a new value type.

After Unity recompiles the Editor assembly, confirm that the operation is discoverable and that its contract is usable by agents:

```bash
ucli ops describe game.scene.countGameObjects --projectPath ./UnityProject
```

## 🛠️ Applying Changes

> **IMPORTANT:** Request commands read JSON only from redirected standard input. Keep the request in your runner and pipe it to uCLI.

Use `call --withPlan` for compact local automation where the same runner plans and applies immediately.

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

Use `ucli plan` and `ucli call --planToken` only when a human review step, CI gate, or agent supervisor must inspect and approve a plan before mutation.

> **IMPORTANT:** A timeout or disconnect does not prove that nothing was applied. Inspect the JSON result, `opResults`, touched units, Unity logs, and daemon logs before retrying.

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

### ⚙️ Primitive Operation Step

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

> **IMPORTANT:** `scene` and `prefab` edits that mutate or use `commit: "context"` need that context open. Put `ucli.scene.open` or `ucli.prefab.open` before the edit step when the runner has not opened it already.

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

### 🧷 Primitive Target Selectors

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

Do not put `{ "var": "..." }` or `"var": null` in public raw `op` args. To name a value produced by an edit action, use the edit action `as` field and refer to that name through the edit DSL form, such as `$createdObject`. `ops describe` omits the raw `var` selector branch, and raw `op` execution rejects it.

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

## 📚 Operation Catalog

The installed Unity plugin exposes its primitive operation catalog at runtime.

```bash
ucli ops list
ucli ops describe ucli.comp.set
```

Use `ops describe` as the source of truth for:

- operation kind and policy
- argument schema and static constraints
- `readIndex` source and freshness metadata

README examples show common operations only. The installed Unity plugin's operation catalog is the runtime contract.

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
- `ucli.assets.*` / `ucli.asset.*` - find assets, inspect schemas, and update asset values.
- `ucli.go.*` - create, describe, delete, and reparent GameObjects.
- `ucli.comp.*` - inspect, ensure, and set components.
- `ucli.project.*` - refresh and save project-scoped state.

## 🧱 Extensible by Contract

Extensions can expose operations under names such as `myorg.navmesh.bake`.

Custom operations are not hidden shortcuts. Once they are in the catalog, they participate in the same policy, schema, and JSON result envelope contracts as built-in operations, so agents and CI can discover them with `ucli ops list` and inspect them with `ucli ops describe`.

## ⚠️ Dangerous Operations

> **WARNING:** `ucli call` blocks operations marked `dangerous` unless every guard allows them: project policy, operation allowlist, and the explicit `--allowDangerous` flag. Prefer the normal `edit` flow and non-dangerous primitive operations.

uCLI keeps the normal edit path declarative, typed, planned, and reviewable.

## 🧪 Verifying Changes

Run Unity tests after applying edits:

```bash
ucli test run \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

Use `--unityEditorPath <path>` when the runner must use a specific Unity executable or `.app` directory, or when Unity is not installed in a standard searchable location.
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
ucli logs unity --tail 200 --level error
ucli logs daemon --tail 200
```

Stop the daemon at the end of an interactive automation session:

```bash
ucli daemon stop
```

## 🧰 Command Guide

| Command | Use it when you need to |
| --- | --- |
| `ucli init` | Create optional project-local uCLI configuration. |
| `ucli status` | Check Unity project resolution and daemon lifecycle state. |
| `ucli refresh` | Refresh Unity project state. |
| `ucli query` | Read project data without writing changes. |
| `ucli resolve` | Resolve a selector to a Unity object identifier. |
| `ucli ops` | List and inspect available primitive operations. |
| `ucli call` | Apply a request; use `--withPlan` for the normal planned write path. |
| `ucli plan` | Prepare a separated review gate and receive a `planToken`. |
| `ucli validate` | Diagnose static request validation without running `plan` or `call`. |
| `ucli logs` | Read Unity or daemon logs. |
| `ucli daemon` | Manage daemon sessions. |
| `ucli test` | Run Unity Test Framework tests. |

Common options:

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--projectPath <path>` | Unity-backed commands | Target Unity project path. Overrides `UCLI_PROJECT_PATH` and current-directory resolution. |
| `--mode auto\|daemon\|oneshot` | Unity-backed commands | Choose daemon reuse or one-shot batchmode. |
| `--timeout <milliseconds>` | Unity-backed commands | Override the command timeout. |
| `--readIndexMode disabled\|allowStale\|requireFresh` | Query-like commands | Control read-index use. |
| `--failFast` | Unity-backed commands | Fail when the Unity editor lifecycle is not ready instead of waiting. |
| `--withPlan` | `ucli call` | Run a plan pass inside `call` and include it in the result. |
| `--planToken <token>` | `ucli call` | Apply a request using a token returned by `ucli plan`. |
| `--allowDangerous` | `ucli call` | Allow operations marked dangerous by the operation catalog. |

> **NOTE:** Project path resolution uses `--projectPath`, then `UCLI_PROJECT_PATH`, then the command default. The default is usually the current working directory.

## 📦 Packages

| Package | Install when |
| --- | --- |
| `MackySoft.Ucli` | You need the `ucli` command. |
| `MackySoft.Ucli.Unity` | You need Unity Editor operations in a Unity project. |
| `MackySoft.Ucli.Contracts` | You build advanced tooling that exchanges uCLI IPC contracts directly. |
| `MackySoft.Ucli.Infrastructure` | You build advanced uCLI runtime integrations that need shared infrastructure helpers. |

## 💬 Support

Use [GitHub Issues](https://github.com/mackysoft/ucli/issues) for bugs, feature requests, usage questions, and README problems.

For bug reports, include:

- `ucli --version`
- Unity version
- Operating system
- The command you ran
- `--mode` and `--readIndexMode` values, when relevant
- For `ucli test run` failures, `payload.artifactsDir` or `payload.summaryJsonPath` when available
- Error output or logs from `ucli logs unity` / `ucli logs daemon`

Use [Pull Requests](https://github.com/mackysoft/ucli/pulls) for focused fixes and README improvements.

## 💖 Sponsor

If uCLI helps your Unity automation workflow, please support MackySoft through GitHub Sponsors:

<https://github.com/sponsors/mackysoft>

## 👤 Author

Hiroya Aramaki is an indie game developer in Japan.

- Website: <https://mackysoft.net/>
- GitHub: <https://github.com/mackysoft>
- Sponsors: <https://github.com/sponsors/mackysoft>

## 📄 License

uCLI is under the [MIT License](LICENSE).
