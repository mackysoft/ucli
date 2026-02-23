## 名称規則
- 正式名称：uCLI
- 名前空間・アセンブリ等（.NET / C#）：`Ucli`
- コマンド：`ucli`

## 対象
- CLI：.NET 8 以上

## コンセプト
**安全にUnityを編集できるCLIツール。**
- Unityを **ヘッドレス（batchmode）** で起動して実行できる（oneshot）
- Unityを **常駐サーバ（デーモン）** として起動し、繰り返しリクエストを処理できる
- CLI起動、ユーザー起動のGUIインスタンス、両方でデーモンは起動する
- 変更は Unity Editor API（AssetDatabase / Scene / Prefab / SerializedObject 等）に限定する。YAML直編集を前提にしない
- すべての入出力はJSONを基本にする

## アーキテクチャ
- コア：.NET製 CLI（`ucli`）
- サーバ：Unity Editor プラグイン（`Ucli.Unity`）

### 実行モード（`--mode`）
`daemon` コマンドを除く各コマンドは `--mode` を受け取る。未指定時の既定値は `auto`。

- `daemon`
  - 既存デーモンへの接続を必須とする
  - デーモン未起動時はエラーを返す
  - CLIはデーモンを自動起動しない
- `auto`（既定）
  - デーモン起動中ならデーモン経由で実行する
  - デーモン未起動ならoneshot（`-batchmode`）で単発実行する
  - CLIはデーモンを自動起動しない
- `oneshot`
  - デーモンが起動中ならエラーを返す
  - デーモン未起動時のみoneshot（`-batchmode`）で単発実行する

デーモン起動は `ucli daemon start` の明示操作でのみ行う。

### モード挙動マトリクス
| デーモン状態 | `--mode daemon` | `--mode auto`（既定） | `--mode oneshot` |
| --- | --- | --- | --- |
| 起動中 | デーモン経由で実行 | デーモン経由で実行 | JSONエラー（`DAEMON_RUNNING_ONESHOT_FORBIDDEN`） |
| 未起動 | JSONエラー（`DAEMON_NOT_RUNNING`） | oneshotで実行 | oneshotで実行 |

## 入出力のJSON契約
- すべてのコマンドの成功・失敗レスポンスはJSONで返す
- `protocolVersion` はリクエスト・レスポンス双方で必須
- 互換性判定は `protocolVersion` で行う

### `protocolVersion` 規則
- 初版はメジャーバージョン整数のみを使用する（例：`1`）
- 受信したメジャーバージョンがサーバー対応値と一致しない場合は、処理を実行せずJSONエラーを返す
- 推奨エラーコード：`PROTOCOL_VERSION_MISMATCH`

### リクエスト最小構造
- `protocolVersion`：プロトコルメジャーバージョン整数（必須）
- `requestId`：追跡用UUID（必須）
- `ops`：実行するオペレーション配列（必須、順次実行）

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "ops": []
}
```

### レスポンス最小構造
- `protocolVersion`：リクエストと同じ値（必須）
- `requestId`：リクエストと同じ値（必須）
- `status`：`ok | partial | error`（必須）
- `opResults`：各opの結果配列（必須）
- `errors`：エラー配列（必須、正常時は空配列）

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "status": "ok",
  "opResults": [],
  "errors": []
}
```

### エラーオブジェクト最小構造
- `code`：機械判定用エラーコード（必須）
- `message`：説明（必須）
- `opId`：該当opの `id`（該当なしの場合は `null`）

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "status": "error",
  "opResults": [],
  "errors": [
    {
      "code": "DAEMON_NOT_RUNNING",
      "message": "Daemon is not running for mode=daemon.",
      "opId": null
    }
  ]
}
```

## `plan` / `call` の基本入力
`plan` と `call` は JSONリクエストを受け取る。  
リクエスト実行は既定opを束ねて行うか、Unity側に新しいopを追加して行う。  
リクエストは原則として1本ずつ直列実行する。

- `plan`：リクエストを **実変更なし**（または最小の観測のみ）で検証・解決し、差分見積りを返す
- `call`：同じリクエストを **実際に適用して保存**する
  - `call` は実行前に `plan` 相当（validate/resolve/差分計算/実行可能性確認）の検証を挟む
  - uCLIでの操作による永続化は `call` でのみ可能

## 入力方法（CLI）
- 基本：**stdin のJSONを読む**
  - `ucli plan < request.json`
  - `ucli call < request.json`
- オプション：`--requestPath <jsonPath>` で JSONリクエストファイルを指定可能

## オペレーション
オペレーションは、JSONの `ops[]` に並ぶ **最小ステップ** の処理単位。  
`plan` と `call` は同じ `ops[]` を、実行フェーズ（plan/call）だけ変えて回す。

### 命名規約
- コア：`ucli.<domain>.<verb>`（例：`ucli.scene.open`, `ucli.comp.set`）
- 拡張：`<org>.<domain>.<verb>`（例：`myorg.navmesh.bake`）

### ガード（`operationPolicy + operationAllowlist`）
- 実行可否はプロジェクト設定ファイルの `operationPolicy` と `operationAllowlist` の両方で判定する
- `operationPolicy`：`safe | advanced | dangerous`
- `dangerous` は既定で無効
- `operationAllowlist` は使用可能opを定義し、正規表現を使用可能
- `ucli.*` は既定で許可対象

### 設定ファイル配置
- パス：`<projectRoot>/.ucli/config.json`
- `projectRoot` は `CWD` または `--projectPath` から解決する
- CLI と Unityデーモンは同じ設定ファイルを読み、同じガード判定を行う

```json
{
  "schemaVersion": 1,
  "operationPolicy": "safe",
  "operationAllowlist": [
    "^ucli\\."
  ]
}
```

### opのメタデータ
- `name`：op名（例：`ucli.comp.set`）
- `kind`：`query | mutation`
- `policy`：`safe | advanced | dangerous`
- `argsSchema`：引数 `args` のJSONスキーマ

### 実行フェーズ
各opは内部的に次の3フェーズを持つ（`call`は事前に `plan` 相当を必ず行う）。
- **Validate**：引数・型・存在・許可プロファイル等の検証（副作用なし）
- **Plan**：対象解決（GlobalObjectId化）、差分見積り、影響範囲（touch）算出（保存しない）
- **Call**：Unity Editor APIで変更し、必要に応じて保存

### 登録方式
Unity起動時に、Editorアセンブリ内のop実装を走査して **オペレーションレジストリ** に登録する。
- op実装クラスに `[UcliOperation]` 属性を付与
- `IUcliOperation` を実装
- 起動時に列挙し、`OperationRegistry` に追加
- これにより `ucli ops list/describe` が動的にopを列挙できる

### 暫定設計構想
```cs
public interface IUcliOperation
{
    OperationMeta Meta { get; }

    void Validate(JsonElement args, OpContext ctx);
    OpResult Plan(JsonElement args, OpContext ctx);
    OpResult Apply(JsonElement args, OpContext ctx);
}

public sealed class OpContext
{
    public string ProjectPath { get; }
    public UcliPhase Phase { get; }
    public OperationSafety Profile { get; }

    public ValueStore Values { get; }
    public IResolver Resolver { get; }

    public CancellationToken CancellationToken { get; }
}

public sealed class ValueStore
{
    private readonly Dictionary<string, UcliValue> values = new();
}

public enum UcliValueKind
{
    UnityObject,
    Type,
    Json,
    String,
    Number,
    Bool,
    Null
}

public sealed class UcliValue
{
    public UcliValueKind Kind { get; }
    public object? Value { get; }
}

public sealed record VarRef(string Name);

public enum SelectorKind
{
    GlobalObjectId,
    AssetGuid,
    AssetPath,
    HierarchyPath,
    Query
}

public abstract record UcliRef
{
    public sealed record Var(VarRef Ref) : UcliRef;
    public sealed record Selector(SelectorRef Ref) : UcliRef;
}

public interface IResolver
{
    UnityEngine.Object ResolveUnityObject(UcliRef reference, OpContext ctx);
    T ResolveUnityObject<T>(UcliRef reference, OpContext ctx) where T : UnityEngine.Object;
}

public sealed record TypeRef(string Name, string? Assembly = null, bool ExpectUnique = true);
```

## JSONリクエストのスキーマ概要
### op（1要素）の最小構造
- `id`：op識別子（ログ・結果参照用）
- `op`：op名（例：`ucli.scene.open`）
- `args`：op引数（`ops describe` が返すschemaで検証）
- `as`：後続参照用の変数名（任意）
- `expect`：件数・成功条件（特に `resolve/query` 系で誤爆防止）

### 参照表現
- 変数参照：`{ "var": "<name>" }`（例：`{ "var": "go" }`）
- セレクタは `ucli.resolve` の `args` として表現し、`plan` 時点で一意化する運用を基本とする

### 任意コード呼び出し（`dangerous`）
`ucli.cs.invoke` は `policy=dangerous` のopとして扱う。実行可否は `operationPolicy` と `operationAllowlist` の判定に従う。  
コンパイルと運用の観点から、`ucli.cs.invoke` は `--mode oneshot` を基本推奨とする。

```cs
using System.Text.Json;
using UnityEditor;

[UcliEntryPoint("MyOrg.Tools.RebuildNavmesh.Run")]
public static class RebuildNavmesh
{
    public static UcliExecResult Run(UcliExecContext ctx, JsonElement args)
    {
        return UcliExecResult.Ok(new { changed = true });
    }
}
```

### リクエストJSON例
```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "ops": [
    {
      "id": "open",
      "op": "ucli.scene.open",
      "args": {
        "path": "Assets/Scenes/Main.unity"
      }
    },
    {
      "id": "resolveGO",
      "op": "ucli.resolve",
      "as": "go",
      "args": {
        "scene": "Assets/Scenes/Main.unity",
        "hierarchyPath": "Root/Enemies/Spawner"
      },
      "expect": {
        "count": 1
      }
    },
    {
      "id": "ensureComp",
      "op": "ucli.comp.ensure",
      "as": "spawner",
      "args": {
        "target": {
          "var": "go"
        },
        "type": "Game.EnemySpawner, Assembly-CSharp"
      }
    },
    {
      "id": "setSpawner",
      "op": "ucli.comp.set",
      "args": {
        "target": {
          "var": "spawner"
        },
        "mode": "atomic",
        "sets": [
          {
            "path": "spawnInterval",
            "value": 3.0
          },
          {
            "path": "maxCount",
            "value": 10
          },
          {
            "path": "weights.Array.data[0]",
            "value": 0.25
          }
        ]
      }
    },
    {
      "id": "save",
      "op": "ucli.scene.save",
      "args": {
        "path": "Assets/Scenes/Main.unity"
      }
    }
  ]
}
```

## コマンド
CWDがUnityプロジェクトと判定可能な場合はそれを使う。そうでない場合は `--projectPath` 指定が必要。

- `ucli validate`：JSONリクエストの静的検証（スキーマ/必須項目/許可op等）
- `ucli plan`：対象解決・差分見積り（実変更なし、または最小）を返す
- `ucli call`：Unityへリクエストを送って実行し、保存する（実行前にplan相当の検証を挟む）
  - `--withPlan`：callレスポンスにplan相当（resolved/diff等）を同梱する（任意）
- `ucli resolve`：セレクタ（例：scene+hierarchyPath 等）を GlobalObjectId 等へ解決する
- `ucli query`：検索・構造取得・スキーマ取得（規定操作）
  - 例：`scene.tree` / `go.describe` / `comp.schema` / `asset.schema` / `assets.find` / `scenes.findComponents`
- `ucli refresh`：AssetDatabase更新、インポート、コンパイル等でプロジェクトを最新化する
- `ucli ops`：利用可能なオペレーション一覧・詳細
  - `list`：利用可能なオペレーション一覧
  - `describe <opName>`：特定オペレーションの引数スキーマ
- `ucli status`：
  - CWDか `--projectPath` でプロジェクト指定
  - `--mode` の実行可能性を判定してJSONで返す（`mode`, `unityVersion`, `ucliUnityVersion`, `compileState` 等）
- `ucli daemon`：常駐サーバ管理
  - `start`：デーモンを起動（ヘッドレス/GUIインスタンスへのattach方針は実装で規定）
  - `stop`：デーモンを停止
  - `status`：デーモン状態を取得
  - `logs`：デーモンログを取得
  - `--projectPath <path>`：対象Unityプロジェクト指定

## サーバー
別ディレクトリのworktreeは別fingerprintの別デーモンでなければならない。  
同一ディレクトリであれば同一デーモンとする。

### 識別
- `projectRoot = realpath(CWD or --projectPath)`
- `projectFingerprint = SHA256(normalize(projectRoot))`（OS差分を吸収した正規化を必須）

### エンドポイント
OSごとに最適なローカルIPCを選ぶ。
- Windows：NamedPipe
- Mac / Linux：Unix domain socket
- フォールバック：TCP

### デーモン起動
#### CLI
- プロジェクト解決
- エンドポイント接続試行
- `--mode` 契約に従って実行形態を決定（デーモン自動起動はしない）

#### Unity
- サーバー起動（GUI・ヘッドレス両方）
- リクエスト受信 -> メインスレッド実行キュー -> レスポンス
- ドメインリロード後の復帰
