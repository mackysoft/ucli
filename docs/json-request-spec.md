> [!IMPORTANT]
> この文書は、uCLI の JSON リクエスト入力契約の正本である。
> 実行時契約とコマンド仕様は [uCLI.md](uCLI.md)、設計原則は [uCLI-design-principles.md](uCLI-design-principles.md) を参照する。

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
- CLI の static validation / preflight は構造・登録 schema・primitive 参照・認可だけを検証し、Scene / Prefab の live open 状態までは保証しない
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
  `scene` / `prefab` context ではその context を保存し、`asset` / `project` context では `ucli.project.save` を実行する
- `project`: project 全体を保存する
### 原則
- `commit` は `edit` では必須
- 暗黙保存は禁止
- `modify != persist` を徹底する
- `ucli.project.save` は request 中に追跡した open Scene / opened Prefab の変更もあわせて保存し得る
## 複数 Scene / 複数 context の扱い

複数 context を扱う場合は、**step を並べて表現する**。  
1 step で複数 Scene をまたぐ編集対象を明示しない。  
ただし `asset` / `project` context の `commit: "context"` は `ucli.project.save` を使うため、request 中に追跡した open Scene / opened Prefab の保存まで含み得る。

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
  "commit": "project"
}
```

### 意図
- `on` は責任境界
- `select` はその内部 target
- project を file context に縮退させない
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
