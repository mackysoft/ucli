## 目的

uCLI の JSON リクエストは、次の2要件を同時に満たす。

1. **primitive な Unity 操作を完全に表現できること**  
    例: Scene を開く、保存する、Project を保存する、対象を resolve する、情報を query する
2. **高頻度の編集を短く安全に表現できること**  
    例: 特定 Scene の特定 Component を編集して保存する

このため、uCLI の JSON リクエストは**単一構文**ではなく、  
**primitive operation 用の `op` と、高級編集用の `edit` を併存**させる。
## トップレベル構造

トップレベルの正規形は次のとおり。

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "steps": []
}
```

### 必須フィールド
- `protocolVersion`: 整数。現行は `1`
- `requestId`: UUID 文字列
- `steps`: step 配列。1件以上必須
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
### 任意フィールド
- `export`: step 結果から durable value を抽出する
- `meta`: 将来拡張用
### 用途
`op` は少なくとも次を表現する。
- scene / prefab / project の open / save / refresh
- resolve
- describe
- tree
- schema
- find
- その他 primitive operation
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
### 任意フィールド
- `export`: durable value の抽出
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
許容値は次の4つ。
- `one`
- `first`
- `all`
- `atMostOne`
### 原則
- query 起点でも selection set に正規化する
- raw JSON を直接 mutation target にしない
- 件数制約は `select.cardinality` に集約する
## `actions` の仕様

`actions` は編集内容の配列であり、`do` ではなく **`actions`** を正式採用する。  
理由は、配列であることが自然で、各要素を discriminated union として扱いやすいためである。

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
複数 context 一括保存は中核仕様に入れない。
### 許容値
- `none`
- `context`
- `project`
### 意味
- `none`: 保存しない
- `context`: 現在の context を保存する
- `project`: project 全体を保存する
### 原則
- `commit` は `edit` では必須
- 暗黙保存は禁止
- `modify != persist` を徹底する
## 複数 Scene / 複数 context の扱い

複数 context を扱う場合は、**step を並べて表現する**。  
1 step で複数 Scene をまたぐことはしない。

### 例

```json
{
  "protocolVersion": 1,
  "requestId": "uuid",
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
      "mode": "samePatchEach",
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
## `expect` / `assert` の扱い

### 結論
- **中核仕様には採用しない**
- 必要なら将来の**補助検証機能**として `assert` を追加可能
- `expect` は採用しない
### 理由
- `expect` は件数制約、null 制約、結果検証、状態検証が混ざりやすい
- 件数制約は `select.cardinality` に入れる方が強い
- 中核構文は `on / select / actions / commit` で十分
## step 間受け渡し

step 間受け渡しには、**`export`** を使う。  
受け渡すのは durable value のみ。
### 許容値
- `assetPath`
- `guid`
- `scenePath`
- `hierarchyPath`
- `componentType`
- scalar 値
### 禁止値
- UnityObject の生参照
- step 内局所束縛の再利用
- raw query JSON
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
