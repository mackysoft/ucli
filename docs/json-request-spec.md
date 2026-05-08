> [!IMPORTANT]
> この文書は、uCLI の JSON リクエスト入力契約の正本である。
> 実行時契約とコマンド仕様は [uCLI.md](uCLI.md)、設計原則は [uCLI-design-principles.md](uCLI-design-principles.md) を参照する。
> operation ごとの `args` / `result` / metadata は `Ucli.Contracts` と `ucli ops describe` を正本とし、この文書では request envelope と edit DSL を定義する。

## 目的

uCLI の JSON リクエストは、次の2要件を同時に満たす。

1. **primitive な Unity 操作を完全に表現できること**  
    例: Scene を開く、保存する、Project を保存する、対象を resolve する、情報を query する
2. **高頻度の編集を短く安全に表現できること**  
    例: 特定 Scene の特定 Component を編集して保存する

このため、uCLI の JSON リクエストは**単一構文**ではなく、  
**primitive operation 用の `op` と、高級編集用の `edit` を併存**させる。
## トップレベル構造

ユーザーが入力するトップレベル構造は次のとおり。

```json
{
  "steps": []
}
```

### 必須フィールド
- `steps`: step 配列。空配列は許可する
### 予約フィールド
- `protocolVersion`: CLI が内部リクエストへ付与する。ユーザー入力に含めてはならない
- `requestId`: CLI が UUID として生成する。ユーザー入力に含めてはならない
### 実行原則
- 実行順は `steps[]` の並び順
- 既定は fail-fast
- `plan` / `call` は request 全体に対して作用する
## step の識別

各 step は、**明示タグ `kind`** により識別する。  
内容推論による判定は採用しない。

許容値は次の2つ。
- `kind: "op"`
- `kind: "edit"`
## `kind: "op"` の仕様
`op` は、primitive な Unity 操作をそのまま表現する。
### 正規形

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

### 必須フィールド
- `kind`: `"op"`
- `id`: step 識別子
- `op`: 実行する op 名
- `args`: op 固有引数

JSON リクエスト入力の機械構造は `RequestEnvelopeSchema + operation ごとの argsSchema + edit DSL` で定義する。`RequestEnvelopeSchema` は `protocolVersion` / `requestId` / `steps[]` / step 共通 field を定義し、`kind:"op"` の `args` だけを operation ごとの `argsSchema` に委譲する。

現行 contract では `RequestEnvelopeSchema` を取得する個別コマンドまたは standalone schema file は定義しない。この文書の `protocolVersion` / `requestId` / `steps[]` / step 共通 field の定義を request envelope の正本とする。

`argsSchema` は `steps[].args` の JSON 構造検証だけを担う。利用者やエージェントは `ucli ops describe <opName>` の `operation.description` / `inputs[]` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` を先に読み、operation 選択と `args` の組み立てを行う。最後に `operation.argsSchema` で `args` の構造を検証する。

public raw `op` の `args` では request-local alias selector branch の `var` を使用しない。`var` は予約済み property であり、値が `null` でも raw `op` 実行時に拒否する。`ops describe` の `argsSchema` / `inputs[].variants[]` にも出さない。

`opResults[].result` は operation ごとの Result contract 型に対応する主データであり、実行状態や副作用情報は envelope の `phase` / `applied` / `changed` / `touched` / `errors` を参照する。結果の有無と読み方は `ops describe` の `operation.resultContract` と `operation.resultSchema` を参照する。

`steps` は空配列を許可する。`steps: []` は no-op request であり、Unity state、project file、readIndex を変更しない。

- `validate`：成功し、実行 step は生成しない。
- `plan`：成功し、`opResults: []` を返す。永続化、副作用、`touched` は発生しない。
- `call`：成功し、`opResults: []` を返す。永続化、副作用、`touched` は発生しない。

現行 contract では no-op 専用の warning field や message field は定義しない。

### 用途
`op` は少なくとも次を表現する。
- scene / prefab / project の open / save / refresh
- resolve
- describe
- tree
- schema
- find
- その他 primitive operation
### 原則
- `op` は自己完結 primitive とする
- 前段 step の結果や束縛に依存しない
- step 間データフローは持たない
- `ucli.prefab.applyOverrides` / `ucli.prefab.revertOverrides` は edit lowering 専用 primitive であり、ユーザー入力の raw `kind:"op"` から直接呼び出してはならない。全モードで `INVALID_ARGUMENT` とする

### 代表例: `ucli.assets.find`

```json
{
  "kind": "op",
  "id": "findBalanceAssets",
  "op": "ucli.assets.find",
  "args": {
    "type": "Game.GameBalanceAsset, Assembly-CSharp",
    "pathPrefix": "Assets/Data",
    "nameContains": "Balance"
  }
}
```

- `args` では `type` / `pathPrefix` / `nameContains` のうち少なくとも1つを指定する
- `type` は stable `typeId` を受け取り、runtime type が指定型へ assignable な main asset を一致とみなす
- `pathPrefix` は `Assets` またはその配下を受け取り、ordinal prefix で比較する
- `nameContains` は main asset 名に対する大小文字無視の部分一致で評価する
- primitive `ucli.assets.find` 自体は limit / cursor を持たず、deterministic order の全件結果を返す

### 代表例: `ucli.cs.eval`

`ucli.cs.eval` は `operationPolicy = dangerous`、operation allowlist 一致、`ucli call --allowDangerous` の全条件を満たす場合だけ `call` できる。利用者は `ucli ops list` で operation と policy を確認し、`ucli ops describe ucli.cs.eval` の `codeContract` で entry point と `UcliCsEvalContext` API を確認する。

```json
{
  "kind": "op",
  "id": "eval",
  "op": "ucli.cs.eval",
  "args": {
    "source": "using MackySoft.Ucli.Unity.Execution.CsEval; namespace Scratch { public static class Entry { public static object? Run(UcliCsEvalContext context) { context.Log(\"checked\"); context.DeclareNoTouchedResources(); return new { ok = true }; } } }",
    "entryPoint": "Scratch.Entry.Run"
  }
}
```

- `source` は `using`、`namespace`、`class`、entry point method を含む完全な C# コンパイル単位である
- `entryPoint` の method 名は `Run` に限り、entry point は `public static object? Run(UcliCsEvalContext context)` の同期メソッドだけを許可する
- `plan` は任意コードを実行せず、compile status と diagnostics を返す
- `call` の `opResults[].result` は `CsEvalResult` で、利用者コードの戻り値は `result.returnValue` に JSON 値として格納される
- `UcliCsEvalContext` の touched resource 宣言は project-relative path だけを受け付ける。Scene は `.unity`、Prefab は `.prefab`、ProjectSettings は `ProjectSettings/` 配下を宣言する。`.unity` と `.prefab` は `DeclareTouchedAsset` ではなく専用 API で宣言する
- touched resource を宣言しない `call` は `touchedResources.state = "unknown"` になり、project-wide asset search / guid path と全 Scene の scene tree read index を stale として扱う
- timeout / cancel は同期 entry point 実行中の使用者コードを強制停止しない

## `kind: "edit"` の仕様
`edit` は、高頻度の編集を短く表現するための上位構文である。  
`edit` は公開 DSL であり、内部では primitive `op` へ lower される。
### 正規形

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
        "maxCount": 10
      }
    }
  ],
  "commit": "context"
}
```
### 必須フィールド
- `kind`: `"edit"`
- `id`: step 識別子
- `on`: 編集コンテキスト
- `select`: 対象選択
- `actions`: 実行する編集操作列
- `commit`: 保存境界
### 実行前提
- `scene` / `prefab` context の `edit` は、`createAsset` だけを含む step を除き、対応する live context が既に使えることを前提にする
- `scene` context で mutation action または `commit: "context"` を使う場合、対象 Scene は loaded であるか、前段 `kind: "op"` で `ucli.scene.open` を実行しておく
- `prefab` context で mutation action または `commit: "context"` を使う場合、対象 Prefab は opened stage であるか、前段 `kind: "op"` で `ucli.prefab.open` を実行しておく
- Play Mode 変更は `--allowPlayMode` 指定時だけ有効で、`scene` context は `commit: "none"` のみ、`prefab` / `asset` / `project` context は通常の `edit` と同じ commit 契約で扱う
- Play Mode 変更の `prefab` context は opened stage 前提の例外であり、runtime は対象 Prefab asset を編集用 context として開き、`commit` に従って保存または破棄できる
- CLI の static validation / preflight は構造・登録 schema・primitive 参照・認可だけを検証し、Scene / Prefab の live open 状態までは保証しない
- Play Mode 変更の runtime 条件、対象 live object、Prefab instance lineage、request-attributed property path は static validation では保証せず、`plan --allowPlayMode` で検証する
- CLI の static validation / preflight は `select` の実ヒット件数を見ず、structural lowering で参照される primitive を認可対象に含める。`cardinality: "all"` や `"atMostOne"` が runtime で 0 件になって no-op になる step でも、その action primitive は事前認可が必要である
## `on` の仕様
`on` は編集コンテキストであり、**永続化境界**を表す。
### 許容形

```json
{ "scene": "Assets/Scenes/Main.unity" }
```

```json
{ "prefab": "Assets/Prefabs/Enemy.prefab" }
```

```json
{ "asset": "Assets/Data/GameBalance.asset" }
```

```json
{ "project": true }
```

### 原則
- `on` は context のみを表す
- target や filter を混ぜない
- 1つの `edit` step は 1つの `on` だけを持つ
## `select` の仕様

`select` は、context の内側で**何を対象にするか**を表す。  
`where` ではなく、正式名称として **`select`** を採用する。

### 直接 selector 形

```json
{
  "gameObject": "Root/Enemies/Spawner",
  "component": "Game.EnemySpawner, Assembly-CSharp",
  "cardinality": "one"
}
```

### query 起点形

```json
{
  "from": {
    "op": "ucli.scene.query",
    "args": {
      "componentType": "Game.EnemySpawner, Assembly-CSharp",
      "pathPrefix": "Root/Enemies"
    }
  },
  "cardinality": "all"
}
```
### `cardinality`
選択件数制約は `select` の責務とする。  
`cardinality` は必須フィールドとする。  
許容値は次の4つ。
- `one`
- `first`
- `all`
- `atMostOne`
### 原則
- 直接 selector 形は target をその場で指す
- `from` は candidate set を作り、selection set に正規化する
- query 起点でも selection set に正規化する
- raw JSON を直接 mutation target にしない
- 件数制約は `select.cardinality` に集約する
- `select.from` の公開対応は #141 では scene context のみ
- `gameObject` と `hierarchyPath` は `/` 区切りの hierarchy path として解釈するため、各 segment の GameObject 名に `/` は含めない
- `/` を含む GameObject 名は hierarchyPath で表現できないため、`scene.query` と `select.from` の candidate にも含めない
## `actions` の仕様

`actions` は編集内容の配列であり、`do` ではなく **`actions`** を正式採用する。  
理由は、配列であることが自然で、各要素を discriminated union として扱いやすいためである。
`actions` は 1 件以上を含む非空配列とする。

### 基本形

```json
"actions": [
  {
    "kind": "set",
    "values": {
      "spawnInterval": 3.0,
      "maxCount": 10
    }
  }
]
```
### action の正式種別
現時点の中核 action は次とする。
- `set`
- `ensureComponent`
- `createObject`
- `createAsset`
- `createPrefab`
- `delete`
- `reparent`
- `applyPrefabOverrides`
- `revertPrefabOverrides`
### `set`

```json
{
  "kind": "set",
  "values": {
    "spawnInterval": 3.0,
    "maxCount": 10
  }
}
```

#### 原則
- property path は Unity の serialized path に寄せる
- `values` は path → value の辞書
- 1つの `set` は atomic に評価・適用する
### `ensureComponent`

```json
{
  "kind": "ensureComponent",
  "type": "Game.EnemySpawner, Assembly-CSharp",
  "as": "spawner"
}
```
#### 意味
- current target に指定型 component が存在する状態を保証する
- 結果を `as` で後続 action に束縛できる
### `applyPrefabOverrides`

```json
{
  "kind": "applyPrefabOverrides",
  "targetAssetPath": "Assets/Prefabs/Enemy.prefab",
  "propertyPaths": [
    "baseSpeed"
  ]
}
```

#### 意味
- Scene context の Prefab instance に対する request-attributed override を、明示した Prefab asset へ反映する
- `targetAssetPath` は必須とし、既存の `Assets/.../*.prefab` でなければならない
- `targetAssetPath` は current target の Prefab instance lineage / valid target chain に含まれていなければならない
- Nested Prefab / Variant の apply 先は暗黙推論しない
- `propertyPaths` は任意で、指定時は exact `SerializedProperty.propertyPath` の配列とする
- `propertyPaths` 省略時は、同一 edit step / 同一 current target の先行 `set` が effective changed にした property path 全部を対象にする
- `propertyPaths` 指定時は、同一 edit step / 同一 current target の先行 `set` が effective changed にした property path の subset だけを許可する
- `propertyPaths: []`、重複 path、先行 `set` に由来しない path、effective changed でない path は拒否する
- 同一 property を複数回 `set` した場合は最終 effective value だけを対象にし、最終値が pre-request 値と同じなら対象外とする
- parent path 指定で child property を再帰対象にしない。child GameObject / child Component へも潜らない
- pre-request override であっても、同一 step の先行 `set` が exact path を effective changed にした場合だけ許可する
- Scene context から明示 Prefab asset へ保存する secondary persistence action であり、Scene 保存ではなく、`commit` の代替として扱わない
- `ensureComponent` / `createObject` / `delete` / `reparent` 由来の構造変更 override は対象外とする
- 全対象 property を preflight 検証してから実行する。検証エラーでは action 全体を適用しない

### `revertPrefabOverrides`

```json
{
  "kind": "revertPrefabOverrides",
  "targetAssetPath": "Assets/Prefabs/Enemy.prefab",
  "propertyPaths": [
    "baseSpeed"
  ]
}
```

#### 意味
- Scene context の Prefab instance に対する request-attributed property override を、明示した Prefab asset の値へ戻す
- `targetAssetPath` と `propertyPaths` の契約は `applyPrefabOverrides` と同じとする
- `propertyPaths` 省略時は、同一 edit step / 同一 current target の先行 `set` が effective changed にした property path 全部を対象にする
- pre-request 時点ですでに override だった property は拒否する
- 同一 step の先行 `set` に由来する request-attributed override だけを Prefab asset 値へ戻し、Unity Editor の一般的な Revert Overrides のように既存 override 全体を戻す操作として扱わない
- 全対象 property を preflight 検証してから実行する。検証エラーでは action 全体を適用しない
- Scene 保存、Prefab asset 保存、Unity Undo stack の代替として扱わない

### `target` の原則
- 明示 `target` がなければ、初期 current target は `select` の結果
- `as` により束縛した対象は、後続 action で `target: "$name"` として参照できる
- current target の暗黙切替は限定的にのみ許可する
- `createAsset` と `createPrefab` は fixed-path action として扱い、複数 target に解決した edit step では使わない
## `as` の仕様
`as` は**採用する**。  
ただし、**局所束縛に限定**する。
### 許される用途
- `ensureComponent` の結果を後続 `set` に渡す
- `createObject` の結果を後続 `reparent` や `set` に渡す
- 同一 step 内でのみ参照する
### 禁止する用途
- 汎用 value store 化
- step をまたぐ長寿命参照
- raw JSON 結果の保持
- UnityObject 生参照の持ち回り
- `op` step への導入
### 例

```json
{
  "kind": "edit",
  "id": "editSpawner",
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
## `commit` の仕様
`commit` は保存境界を表す。  
1 step で複数編集 context を一括指定する保存モデルは中核仕様に入れない。
### 許容値
- `none`
- `context`
- `project`
### 意味
- `none`: 保存しない
- `context`: 現在の context に対応する保存境界を実行する  
  `scene` context ではその Scene、`prefab` context ではその Prefab asset、`asset` context では選択した asset、`project` context では選択した project-scoped asset を保存する
- `project`: project 全体を保存する。対象限定保存として扱わない
### 原則
- `commit` は `edit` では必須
- 暗黙保存は禁止
- `modify != persist` を徹底する
- `commit: "context"` は対象永続化単位の保存に限定し、`ucli.project.save` を使う広域保存に lower しない
- `ucli.project.save` は project-wide save であり、request 中に追跡した open Scene / opened Prefab の変更もあわせて保存し得る

## Play Mode 変更
Play Mode 中の変更は、通常の Play Mode 拒否契約に対する明示例外である。`--allowPlayMode` を指定しない `plan` / `call` は、Play Mode 中の request を lifecycle error として拒否する。`--allowPlayMode` は変更 request 用であり、query-only の `kind:"op"` step を許可するためには使わない。

### 許容形
Play Mode 変更で許可される step は `kind: "edit"` のみである。

```json
{
  "kind": "edit",
  "id": "tuneLiveSpawner",
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
        "spawnInterval": 1.5
      }
    }
  ],
  "commit": "none"
}
```

```json
{
  "kind": "edit",
  "id": "tuneRuntimeBalance",
  "on": {
    "prefab": "Assets/Prefabs/Enemy.prefab"
  },
  "select": {
    "gameObject": "Root",
    "component": "Game.EnemyDefaults, Assembly-CSharp",
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "baseSpeed": 4.0
      }
    }
  ],
  "commit": "context"
}
```

```json
{
  "kind": "edit",
  "id": "tuneRuntimeBalanceAsset",
  "on": {
    "asset": "Assets/Data/GameBalance.asset"
  },
  "select": {
    "self": true,
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "defaultSpawnInterval": 1.5
      }
    }
  ],
  "commit": "context"
}
```

```json
{
  "kind": "edit",
  "id": "tuneProjectRuntimeSetting",
  "on": {
    "project": true
  },
  "select": {
    "projectAsset": {
      "path": "ProjectSettings/TagManager.asset"
    },
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "layers.Array.data[8].name": "EnemyRuntime"
      }
    }
  ],
  "commit": "context"
}
```

```json
{
  "kind": "edit",
  "id": "tuneAndApplyPrefabInstance",
  "on": {
    "scene": "Assets/Scenes/Main.unity"
  },
  "select": {
    "gameObject": "Root/Enemies/EnemyA",
    "component": "Game.EnemyDefaults, Assembly-CSharp",
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "baseSpeed": 4.0
      }
    },
    {
      "kind": "applyPrefabOverrides",
      "targetAssetPath": "Assets/Prefabs/Enemy.prefab",
      "propertyPaths": [
        "baseSpeed"
      ]
    }
  ],
  "commit": "none"
}
```

```json
{
  "kind": "edit",
  "id": "tuneAndRevertPrefabInstance",
  "on": {
    "scene": "Assets/Scenes/Main.unity"
  },
  "select": {
    "gameObject": "Root/Enemies/EnemyA",
    "component": "Game.EnemyDefaults, Assembly-CSharp",
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "baseSpeed": 4.0
      }
    },
    {
      "kind": "revertPrefabOverrides",
      "targetAssetPath": "Assets/Prefabs/Enemy.prefab",
      "propertyPaths": [
        "baseSpeed"
      ]
    }
  ],
  "commit": "none"
}
```

### 制約
- `on` は `scene` / `prefab` / `asset` / `project` を許可する
- Play Mode 変更で許可される public step は `kind: "edit"` のみとし、raw `kind: "op"` は `--allowPlayMode` 指定時も許可しない
- `scene` context は `commit: "none"` のみ許可し、Scene 保存へ lower される形は許可しない
- `commit: "project"` は project-wide save であるため Play Mode 変更では許可しない
- Scene 上の Prefab instance は `scene` context として扱い、Prefab asset 自体は `prefab` context として扱う
- Scene 上の Prefab instance 変更を Prefab asset へ反映する場合は `applyPrefabOverrides` action を明示し、`targetAssetPath` で apply 先を指定する
- Scene 上の Prefab instance 変更を Prefab asset 値へ戻す場合は `revertPrefabOverrides` action を明示する。対象は同一 step の先行 `set` に由来する request-attributed override だけであり、pre-request 時点ですでに override だった property は拒否する
- Scene context の `commit:"none"` は Scene 保存を行わない指定であり、`applyPrefabOverrides` による対象 Prefab asset 保存とは矛盾しない
- `applyPrefabOverrides` は Scene context から明示 Prefab asset へ保存する secondary persistence action であり、Scene asset は保存しない
- `applyPrefabOverrides` / `revertPrefabOverrides` は同一 edit step / 同一 current target の先行 `set` が effective changed にした exact property path だけを対象にし、child object へ再帰しない
- `targetAssetPath` は current target の Prefab instance lineage / valid target chain に含まれる既存の `Assets/.../*.prefab` に限定する
- `propertyPaths: []`、重複 path、先行 `set` に由来しない path、effective changed でない path、parent path 指定による child property 対象化は拒否する
- 同一 property を複数回 `set` した場合は最終 effective value だけを対象にし、最終値が pre-request 値と同じなら対象外にする
- `applyPrefabOverrides` は pre-request override であっても、同一 step の先行 `set` が exact path を effective changed にした場合だけ許可する
- apply / revert は全対象 property を preflight 検証してから実行する。検証エラーでは action 全体を適用しない
- `prefab` / `asset` / `project` context は通常の `edit` と同じ action / commit / dangerous guard を適用し、明示 `commit` に従って保存できる
- Play Mode 変更で Prefab / asset / project を保存する場合は対象永続化単位に限定し、open Scene を巻き込む一括 project save は使わない
- Play Mode の live object を正本とし、readIndex を対象解決や scene / prefab / asset / project 観測に使わない
- Scene context の実行結果は Play Mode の live object にだけ作用し、Scene asset へ保存しない
- Scene context の `applyPrefabOverrides` は Scene asset を保存しないが、明示した Prefab asset を保存対象として `touched` に返す
- Scene context の `revertPrefabOverrides` は Scene live object だけを戻し、`touched` は空配列、`readPostcondition` は返さない
- apply / revert の Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない
- Prefab / asset / project context の保存を伴う実行結果は、通常の永続化変更と同じく保存した永続化単位を `touched` に返す

## 複数 Scene / 複数 context の扱い

複数 context を扱う場合は、**step を並べて表現する**。  
1 step で複数 Scene をまたぐ編集対象を明示しない。  
`asset` / `project` context の `commit: "context"` は対象 asset / project-scoped asset だけを保存し、open Scene / opened Prefab を巻き込まない。

### 例

```json
{
  "steps": [
    {
      "kind": "edit",
      "id": "editMainSpawner",
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
    },
    {
      "kind": "edit",
      "id": "editBossSpawner",
      "on": {
        "scene": "Assets/Scenes/Boss.unity"
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

## 同一型複数ターゲット編集

**採用する。**  
ただし、強く制限する。
### 許容条件
- 単一 context 内
- 同一 concrete type
- 全 target に同一 patch
- selection set を plan 時に凍結できる
### 例

```json
{
  "kind": "edit",
  "id": "normalizeSpawners",
  "on": {
    "scene": "Assets/Scenes/Main.unity"
  },
  "select": {
    "from": {
      "op": "ucli.scene.query",
      "args": {
        "componentType": "Game.EnemySpawner, Assembly-CSharp",
        "pathPrefix": "Root/Enemies"
      }
    },
    "cardinality": "all"
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
```

### 禁止事項
- target ごとに異なる値を与える
- 異種型を一括で扱う
- 複数 context を一括編集する
## 中核に含めないもの

- `expect`
- `export`
- `meta`
- step 間データフロー
- public `op.as`
## `asset` / `project` の扱い

### 通常 asset

通常の ScriptableObject や main asset は `on.asset` で扱う。

```json
{
  "kind": "edit",
  "id": "editGameBalance",
  "on": {
    "asset": "Assets/Data/GameBalance.asset"
  },
  "select": {
    "self": true,
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "defaultSpawnInterval": 2.5,
        "maxEnemyCount": 50
      }
    }
  ],
  "commit": "context"
}
```

### asset 内 object / sub-asset

asset は file context として扱い、内部 object は `select` で選ぶ。

### ProjectAsset / project-scoped settings

ProjectSettings のような project-scoped settings は `on.project` で扱う。  
`projectAsset` の path は `on` ではなく `select` に置く。
この target は通常の `assetPath` selector には縮退させず、project-scoped selector として解決する。

```json
{
  "kind": "edit",
  "id": "editTagManager",
  "on": {
    "project": true
  },
  "select": {
    "projectAsset": {
      "path": "ProjectSettings/TagManager.asset"
    },
    "cardinality": "one"
  },
  "actions": [
    {
      "kind": "set",
      "values": {
        "layers.Array.data[8].name": "Enemy"
      }
    }
  ],
  "commit": "context"
}
```

### 意図
- `on` は責任境界
- `select` はその内部 target
- project を file context に縮退させない
- `commit: "context"` は選択した project-scoped asset だけを保存する
## 最終サンプル

```json
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
            "maxCount": 10,
            "weights.Array.data[0]": 0.25
          }
        }
      ],
      "commit": "context"
    },
    {
      "kind": "op",
      "id": "saveProject",
      "op": "ucli.project.save",
      "args": {}
    }
  ]
}
```
