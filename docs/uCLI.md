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
- Unity Test Framework の実行と結果正規化を、`ucli test` として統合提供する予定である

## アーキテクチャ
- コア：.NET製 CLI（`ucli`）
- サーバ：Unity Editor プラグイン（`Ucli.Unity`）

### 実行モード（`--mode`）
`daemon` と `logs` コマンドを除く各コマンドは `--mode` を受け取る。未指定時の既定値は `auto`。

IPCを利用するコマンドは `--timeout <int>`（ミリ秒）を受け取る。  
未指定時は `config.json` の `ipcTimeoutMillisecondsByCommand[command]` を優先し、未設定または `null` の場合は `ipcDefaultTimeoutMilliseconds` を使用する。  
`--timeout` は `1..2147483647` の整数のみ許可し、空文字・空白・非数値・0以下は `INVALID_ARGUMENT` とする。

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

### `projectPath` 解決順序
`--projectPath` を受け取るコマンドは、対象 Unity project を次の順で解決する。

1. `--projectPath`
2. 環境変数 `UCLI_PROJECT_PATH`
3. コマンド固有の既定値

コマンド固有の既定値は次のとおり。
- `test run` を除くコマンドは `CWD`
- `test run` は `profile.json` `projectPath`、未指定時は `.`

### モード挙動マトリクス
| デーモン状態 | `--mode daemon` | `--mode auto`（既定） | `--mode oneshot` |
| --- | --- | --- | --- |
| 起動中 | デーモン経由で実行 | デーモン経由で実行 | JSONエラー（`DAEMON_RUNNING_ONESHOT_FORBIDDEN`） |
| 未起動 | JSONエラー（`DAEMON_NOT_RUNNING`） | oneshotで実行 | oneshotで実行 |

## 入出力のJSON契約
- `ucli logs` を除くすべてのコマンドの成功・失敗レスポンスはJSONで返す
- 進行ログと診断ログは `stderr` に出力する
- `ucli logs` を除くコマンドの `stdout` は常にJSONレスポンス1件のみを出力する
- `ucli logs` の成功時は `stdout` にイベントストリームを出力し、`--format json` は NDJSON（1行1JSONオブジェクト）を返す
- `protocolVersion` はすべてのレスポンスで必須
- JSONリクエストを受け取るコマンド（`validate` / `plan` / `call` / `resolve` / `query`）では、リクエストにも `protocolVersion` を必須とする
- 互換性判定は `protocolVersion` で行う
- ただし、CLIフレームワーク（ConsoleAppFramework）の既定経路（`--help` / `help` / `--version` など）は、既定のテキスト出力を返す。これらは本JSON契約の適用対象外とする

### CLI出力契約
- `status` は `ok | error` を使用する
  - `ok`：コマンドが契約どおり完了した
  - `error`：入力不正、インフラ障害、外部ツール障害などで完了できなかった
- 終了コードはコマンド別契約に従う
- request系レスポンスを返すコマンド（`validate` / `plan` / `call` / `resolve` / `query`）は `status=ok` のとき `exit code = 0`、`status=error` のとき `exit code != 0`
  - `ucli test run` は `status=ok` かつ `payload.result=fail` の場合に `exit code = 1` を返す

### コマンドレスポンス共通エンベロープ
`init` / `ops` / `status` / `daemon` / `test` / `refresh` は、次の共通エンベロープでレスポンスを返す。

- `protocolVersion`：プロトコルメジャーバージョン整数（必須）
- `command`：コマンド識別子（必須、例：`test.run`）
- `status`：`ok | error`（必須）
- `exitCode`：プロセス終了コード（必須）
- `message`：説明（必須）
- `payload`：コマンド固有結果（必須）
- `errors`：エラー配列（必須、正常時は空配列）

```json
{
  "protocolVersion": 1,
  "command": "test.run",
  "status": "ok",
  "exitCode": 1,
  "message": "Unity test execution completed with failed tests.",
  "payload": {
    "result": "fail"
  },
  "errors": []
}
```

### `protocolVersion` 規則
- 初版はメジャーバージョン整数のみを使用する（例：`1`）
- 受信したメジャーバージョンがサーバー対応値と一致しない場合は、処理を実行せずJSONエラーを返す
- 推奨エラーコード：`PROTOCOL_VERSION_MISMATCH`

### JSONリクエスト入力コマンドのリクエスト最小構造
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

### request系レスポンスの最小構造
- `protocolVersion`：リクエストと同じ値（必須）
- `requestId`：リクエストと同じ値（必須）
- `status`：`ok | error`（必須）
- `opResults`：各opの結果配列（必須）
- `errors`：エラー配列（必須、正常時は空配列）

`validate` / `plan` / `call` / `resolve` / `query` は、このレスポンス構造を返す。

`status=error` であっても、先行opが適用済みである場合がある。適用状況は `opResults` で機械判定する。

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "status": "ok",
  "opResults": [],
  "errors": []
}
```

### `opResults`（1要素）の最小構造
- `opId`：対応するリクエスト `ops[].id`（必須）
- `op`：オペレーション名（必須）
- `phase`：`validate | plan | call | skipped`（必須）
- `applied`：適用されたかどうか（必須）
- `changed`：変更が発生したかどうか（必須）
- `touched`：影響した永続化単位オブジェクトの配列（必須）
  - `kind`：`scene | prefab | asset | projectSettings`（必須）
  - `path`：プロジェクトルート相対パス（必須）
  - `guid`：取得可能な場合に付与（任意）
  - オブジェクト単位（GlobalObjectId）や値パス（SerializedProperty path）は含めない
- `result`：query系 op が返す結果本体（任意）

```json
{
  "opId": "setSpawner",
  "op": "ucli.comp.set",
  "phase": "call",
  "applied": true,
  "changed": true,
  "touched": [
    {
      "kind": "scene",
      "path": "Assets/Scenes/Main.unity",
      "guid": "11111111111111111111111111111111"
    }
  ]
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

### 失敗時実行方針
- `fail-fast` 固定とする
- `call` で失敗した場合、失敗したop以降は実行しない
- 未実行opは `phase=skipped`、`applied=false`、`changed=false` として返す

### `planToken` とドリフト検知
- `plan` はレスポンスに `planToken` を返す
- `call` は `planToken` がある場合に、署名・有効期限・リクエスト一致・状態一致を検証する
- CLI入力JSON（`protocolVersion` / `requestId` / `ops`）には `planToken` を含めない
- `planToken` は `ucli call --planToken <token>` で渡し、CLIがIPC `execute` リクエストの `planToken` フィールドへ転送する
- `call` の実行順序は次で固定する
  - 全opに対して `Validate` / `Plan` を実行（fail-fast）
  - `planToken` を検証
  - 検証成功時のみ全opの `Call` を実行（fail-fast）
- 検証エラー例
  - `PLAN_TOKEN_REQUIRED`
  - `PLAN_TOKEN_INVALID`
  - `PLAN_TOKEN_EXPIRED`
  - `PLAN_TOKEN_REQUEST_MISMATCH`
  - `STATE_CHANGED_SINCE_PLAN`

#### `requestDigest` の生成
- Unityサーバー側で生成する
- 対象は `protocolVersion` と `ops`（`requestId` は対象外）
- 同一内容で同一値になるように正規化してハッシュ化する（実装詳細はサーバー実装で統一）

#### `stateFingerprint` の生成
- Unityサーバー側で生成する
  - `plan` 実行時に作成し、`planToken` に埋め込む
  - `call` 実行時に再計算し、`planToken` の値と比較する
- 安定動作の範囲で次を含める
  - `projectFingerprint`
  - `unityVersion`
  - `compileState`
  - `domainReloadGeneration`
  - `configDigest`（`operationPolicy` / `operationAllowlist` / `planTokenMode`）
  - `touchedDigest`（永続化単位のみ）
- `touchedDigest` は `touched` の各要素を正規化して算出する
  - 対象項目：`kind`, `path`, `guid(任意)` と、サーバーが計測した `exists`, `size`, `lastWriteUtcTicks`
- `projectFingerprint` / `unityVersion` / `compileState` / `domainReloadGeneration` / `configDigest` / `touchedDigest` の各入力値は、未取得時に `na` を埋めて算出する（除外しない）

- `plan` は `Assets/` と `ProjectSettings/` への永続化を書き込まない
- 観測に伴う副作用（`didCompile` / `didReimport` / `domainReloadOccurred`）は `planObservations` に記録する

#### `planToken` に含める値と用途
- `v`：トークン形式バージョン
- `kid`：署名鍵識別子
- `projectFingerprint`：プロジェクト取り違え防止
- `requestDigest`：リクエスト一致判定
- `stateFingerprint`：状態ドリフト判定
- `issuedAtUtc`：発行時刻
- `expiresAtUtc`：有効期限
- `nonce`：トークン一意化

#### `planToken` の有効期限
- 既定TTLは 15 分
- 許容時計ずれは ±30 秒
- 有効期限を超過した `planToken` は `PLAN_TOKEN_EXPIRED` で失敗する

#### `call` での `planToken` 検証順序
- `required` 判定
- 構文/署名/期限
- `requestDigest` 一致
- `stateFingerprint` 一致
- いずれかで失敗した場合、`Call` フェーズは1件も実行しない

### `planTokenMode`（設定）
- `optional`（既定）
  - `planToken` がある場合は検証して実行する
  - `planToken` がない場合は `call` 内部で `plan` 相当検証を実行して適用する
- `required`
  - `call` で `planToken` を必須にする
  - `planToken` がない場合は `PLAN_TOKEN_REQUIRED` で失敗する

### `requestId` の冪等性（デーモン）
- デーモンモードでは `requestId` を冪等キーとして扱う
- 同一 `requestId` かつ同一内容は再実行せず、前回レスポンスを返す
- 同一 `requestId` かつ異なる内容は `REQUEST_ID_CONFLICT` で拒否する
- 保持先はデーモン単位のメモリ内キャッシュ（ディスク永続化しない）
- キャッシュ保持項目は `requestId`、`requestDigest`、`response`、`createdAt`、`expiresAt`
- 既定値は TTL 24時間、最大 10,000 件（超過時は古い順に破棄）

## 入力方法（CLI）
- 基本：**stdin のJSONを読む**
  - `ucli plan < request.json`
  - `ucli call < request.json`
- オプション：`--requestPath <jsonPath>` で JSONリクエストファイルを指定可能
- `call` の `planToken` は `--planToken <token>` で指定する

## オペレーション
オペレーションは、JSONの `ops[]` に並ぶ **最小ステップ** の処理単位。  
`plan` と `call` は同じ `ops[]` を、実行フェーズ（plan/call）だけ変えて回す。
`ops-catalog.md` は開発中のTODO参照であり、公開契約の正本は本ドキュメントを優先する。

### 命名規約
- コア：`ucli.<domain>.<verb>`（例：`ucli.scene.open`, `ucli.comp.set`）
- 拡張：`<org>.<domain>.<verb>`（例：`myorg.navmesh.bake`）

### ガード（`operationPolicy + operationAllowlist`）
- 実行可否はプロジェクト設定ファイルの `operationPolicy` と `operationAllowlist` の両方で判定する
- `operationPolicy`：`safe | advanced | dangerous`
- `dangerous` は既定で無効
- `operationAllowlist` は使用可能opを定義し、正規表現を使用可能
- `ucli.*` は既定で許可対象
- `call` で `dangerous` opを実行する場合は、`operationPolicy` が `dangerous` を許可し、`operationAllowlist` に一致し、かつ `--allowDangerous` が明示指定されている場合にのみ許可する（AND条件）

### 設定ファイル配置
- パス：`<repoRoot>/.ucli/config.json`
- `repoRoot` は `CWD` または `--projectPath` 起点で親方向に `.git` を探索して解決する（見つからない場合は Unity `projectRoot` を使用）
- CLI と Unityデーモンは同じ設定ファイルを読み、同じガード判定を行う

```json
{
  "schemaVersion": 1,
  "operationPolicy": "safe",
  "planTokenMode": "optional",
  "readIndexDefaultMode": "requireFresh",
  "ipcDefaultTimeoutMilliseconds": 3000,
  "ipcTimeoutMillisecondsByCommand": {
    "test": 300000,
    "status": 5000,
    "validate": 10000,
    "plan": 20000,
    "call": 60000,
    "resolve": 10000,
    "query": 10000,
    "refresh": 120000,
    "ops": 5000,
    "daemon.start": 60000,
    "daemon.stop": 10000,
    "daemon.status": 3000,
    "daemon.list": 3000
  },
  "operationAllowlist": [
    "^ucli\\."
  ]
}
```

- `ipcTimeoutMillisecondsByCommand` は空オブジェクト `{}` でも有効
- 各値は `null` または `1..2147483647` の整数

### opのメタデータ
- `name`：op名（例：`ucli.comp.set`）
- `kind`：`query | mutation`
- `policy`：`safe | advanced | dangerous`
- `argsSchema`：引数 `args` のJSONスキーマ

### 実行フェーズ
各opは内部的に次の3フェーズを持つ。
- **Validate**：引数・型・存在・許可プロファイル等の検証（副作用なし）
- **Plan**：対象解決（GlobalObjectId化）、差分見積り、影響範囲（touch）算出（保存しない）
- **Call**：Unity Editor APIで変更し、必要に応じて保存

`call` はリクエスト単位で `Validate/Plan` 完了後に `planToken` を検証し、検証成功時のみ `Call` を実行する2パス方式とする。

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
- `expect`：opの主要アウトプットに対する共通制約（任意）

### `expect`（共通制約）
- `expect` は各opが返す主要アウトプットに対して評価する
- 形式（任意項目）:
  - `nonNull`：`true` の場合、主要アウトプットが `null` でないことを要求する
  - `count`：件数の完全一致（`>= 0` の整数）
  - `min`：件数下限（`>= 0` の整数）
  - `max`：件数上限（`>= 0` の整数）
- ルール:
  - `count` と `min`/`max` の同時指定は禁止
  - `min` と `max` を同時指定する場合は `min <= max` を必須とする
  - `expect` は空オブジェクトを許可しない（少なくとも1項目を指定する）
  - 非コレクション出力に `count`/`min`/`max` を指定した場合は入力不正とする

### 参照表現
- 変数参照：`{ "var": "<name>" }`（例：`{ "var": "go" }`）
- セレクタは `ucli.resolve` の `args` として表現し、`plan` 時点で一意化する運用を基本とする

### 任意コード呼び出し（`dangerous`）
`ucli.cs.invoke` は `policy=dangerous` のopとして扱う。実行可否はガード節の判定条件に従う。  
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

`ucli.comp.set` は常に atomic に評価・適用し、`mode` 引数は受け付けない。

### Prefab op 契約
- `ucli.prefab.create`
  - `args = { "target": <GameObjectRef>, "path": "<Assets/...>.prefab" }`
  - `target` は必須。Loaded Scene 上の既存 GameObject だけを受け付ける
  - 空の Prefab を新規作成する用途は扱わない
- `ucli.prefab.open`
  - `args = { "path": "<Assets/...>.prefab" }`
  - `as` を指定した場合は、開いた Prefab の root GameObject を alias 保存する
- `ucli.prefab.save`
  - `args = { "path": "<Assets/...>.prefab" }`
  - `path` は、現在開いている Prefab と一致しなければならない
- `prefab.open` 後の編集は、`as` で受けた root alias を起点に `ucli.go.create` / `ucli.comp.ensure` / `ucli.comp.set` を連鎖させる

### Asset操作例
```json
{
  "protocolVersion": 1,
  "requestId": "ab2f4a7a-9b5d-4e0d-bc71-11b1d4f1ab73",
  "ops": [
    {
      "id": "createConfig",
      "op": "ucli.asset.create",
      "as": "config",
      "args": {
        "type": "Game.ConfigAsset, Assembly-CSharp",
        "path": "Assets/Data/GameConfig.asset"
      }
    },
    {
      "id": "setConfig",
      "op": "ucli.asset.set",
      "args": {
        "target": {
          "var": "config"
        },
        "sets": [
          {
            "path": "spawnInterval",
            "value": 3.0
          },
          {
            "path": "displayName",
            "value": "Main Config"
          }
        ]
      }
    },
    {
      "id": "saveProject",
      "op": "ucli.project.save",
      "args": {}
    }
  ]
}
```

- `ucli.asset.create`
  - `type` は concrete な `ScriptableObject` の `typeId` を受け付ける
  - `path` は `Assets/` 配下の project-relative path かつ `.asset` 拡張子必須
  - 親フォルダは既存必須、既存 path の上書きは行わない
- `ucli.asset.set`
  - `target` は `var` / `globalObjectId` / `assetGuid` / `assetPath` を受け付ける
  - 対象は `Assets/` 配下の existing main asset に限る
  - `ScriptableObject` だけでなく `Material` などの既存 main asset も対象に含む
  - sub-asset、scene asset、prefab asset は対象外
  - `call` は dirty 化までを行い、永続化は `ucli.project.save` に委ねる
- `ucli.asset.schema`
  - `{ "type": "<typeId>" }` は concrete な `ScriptableObject` 型のみ受け付ける
  - `{ "target": ... }` は `Assets/` 配下の existing main asset を受け付け、実ランタイム型の schema を返す

`ucli.asset.set` も `ucli.comp.set` と同様に atomic に評価・適用し、`mode` 引数は受け付けない。

## コマンド
Unityプロジェクトを対象に実行するコマンドは、CWDがUnityプロジェクトと判定可能な場合はそれを使う。そうでない場合は `--projectPath` を指定する。

- `ucli init`：`.ucli` 雛形を作成する
  - 生成先：Git repository root 直下（`<repoRoot>/.ucli`）
  - 生成対象：`.ucli/local/`, `.ucli/config.json`, `.ucli/.gitignore`
  - `--force`：既存設定を上書き
- `ucli validate`：JSONリクエストの静的検証（スキーマ/必須項目/許可op等）
  - 保証範囲：形式・スキーマ・許可判定まで（実在確認や差分見積りは含まない）
  - Unity実体への接続や解決は行わない（ローカル静的検証のみ）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli plan`：対象解決・差分見積り（実変更なし、または最小）を返す
  - `planToken` を返す
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli call`：Unityへリクエストを送って実行し、保存する（実行前にplan相当の検証を挟む）
  - `--planToken <token>`：`plan` が返したトークンを指定する
  - `dangerous` opを含む場合は `--allowDangerous` 必須
  - `--withPlan`：callレスポンスにplan相当（resolved/diff等）を同梱する（任意）
  - `call` は readIndex に依存せず、Unity実体で再解決・再検証して実行する
- `ucli resolve`：セレクタ（例：scene+hierarchyPath 等）を GlobalObjectId 等へ解決する
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli query`：検索・構造取得・スキーマ取得（規定操作）
  - 例：`scene.tree` / `go.describe` / `comp.schema` / `asset.schema` / `assets.find` / `scenes.findComponents`
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli refresh`：AssetDatabase更新、インポート、コンパイル等でプロジェクトを最新化する
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `stdin` / `--requestPath` / `--planToken` / `--withPlan` / `--readIndexMode` は受け取らない
  - CLI内部で `ucli.project.refresh` 1件のみを含む固定 `execute` リクエストを生成する
  - 出力は共通の `CommandResult` エンベロープとし、`payload.requestId` と `payload.opResults` を返す
- `ucli ops`：利用可能なオペレーション一覧・詳細
  - `list`：利用可能なオペレーション一覧
  - `describe <opName>`：特定オペレーションの引数スキーマ
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli status`：
  - CWDか `--projectPath` でプロジェクト指定
  - `--timeout <int>`（ミリ秒）で daemon 状態確認タイムアウトを上書きする（未指定時は config を使用）
  - JSON payload には `daemonStatus`, `unityVersion`, `serverVersion`, `compileState`, `runtime`, `timeoutMilliseconds` を返す
  - `unityVersion` は常に `ProjectSettings/ProjectVersion.txt` 由来とする
  - `serverVersion` / `compileState` / `runtime` は daemon 到達時のみ値が入り、未到達時は `null`
- `ucli test`：Unity Test Framework 実行と結果正規化
  - `run`：Unityテストを実行し、正規化結果をJSONで返す
  - `profile init`：`test` 実行用のプロファイルJSON雛形を生成する
- `ucli logs`：ログを取得する
  - `unity`：Unityログ（コンパイルログ、実行ログ）を取得
  - `daemon`：デーモンログ（起動・停止・IPCなどの制御ログ）を取得
  - `--projectPath <path>`：対象Unityプロジェクト指定
- `ucli daemon`：常駐サーバ管理
  - `start`：対象 `projectFingerprint` のデーモンを起動
  - `stop`：デーモンを停止
  - `status`：デーモン状態を取得
  - `list`：同一Git repository の worktree 群にある daemon 登録を一覧
  - `--projectPath <path>`：対象Unityプロジェクト指定

### `ucli daemon`
デーモンの起動・停止・状態確認・登録一覧取得を行う管理コマンド。

#### `daemon` 共通 options（`start` / `stop` / `status` / `list`）
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |

#### `daemon` 出力契約（共通）
- `ucli daemon` は共通エンベロープを返す。
- `command` はサブコマンドごとに次を返す。
  - `ucli daemon start`：`daemon.start`
  - `ucli daemon stop`：`daemon.stop`
  - `ucli daemon status`：`daemon.status`
  - `ucli daemon list`：`daemon.list`
- 成功時 `payload` は `timeoutMilliseconds` を常に含む。
- `sessionToken` はレスポンスに含めない。

#### `daemon start` の `payload`
- `startStatus`：`started | alreadyRunning`
- `daemonStatus`：`running`
- `timeoutMilliseconds`：実効タイムアウト（ミリ秒）
- `session`：セッション情報（`projectFingerprint`, `issuedAtUtc`, `runtimeKind`, `ownerKind`, `canShutdownProcess`, `endpointTransportKind`, `endpointAddress`, `processId`）

#### `daemon stop` の `payload`
- `stopStatus`：`stopped | notRunning`
- `daemonStatus`：`notRunning`
- `timeoutMilliseconds`：実効タイムアウト（ミリ秒）
- `session`：`null`

#### `daemon status` の `payload`
- `daemonStatus`：`running | notRunning | stale`
- `timeoutMilliseconds`：実効タイムアウト（ミリ秒）
- `session`：
  - `running` / `stale` の場合はセッション情報を返す
  - `notRunning` の場合は `null`
- `diagnosis`：
  - 保存済み daemon diagnosis がある場合に返す
  - `stale` では current session に一致する diagnosis を優先し、一致しない場合は `externalTerminationSuspected` を合成することがある
  - `running` の場合は `null`
  - `reportedBy` は `unity | cli` を返す
  - `isInferred` は CLI が後から推定して合成した diagnosis のとき `true` を返す

#### `daemon list` の `payload`
- `timeoutMilliseconds`：`daemon list` 全体に適用する実効タイムアウト（ミリ秒）
- `projectRelativePath`：現在の Git worktree root から見た対象 Unity project の相対パス
- `isComplete`：対象 worktree 群を最後まで観測できたかどうか
- `completionReason`：`null | timeout`
- `remainingWorktreeCount`：`isComplete = false` のとき、未観測の worktree 件数
- `items`：永続化済み daemon 登録の一覧。`session.json` が存在しない worktree は含めない
- `timeoutMilliseconds` は Git worktree 列挙、session 読み出し、daemon probe のすべてで共有消費する
- Git worktree 列挙完了後に共有 timeout budget が尽きた場合は、コマンド全体は成功のまま `isComplete = false` とし、観測済み item のみを返す
- `items[*]`：
  - `worktreePath`：Git worktree root path
  - `branchRef`：attached 時の branch ref。detached は `null`
  - `head`：worktree の HEAD commit hash
  - `projectPath`：worktree 内の Unity project root path
  - `projectFingerprint`：worktree ローカル storage root で解決した fingerprint
  - `state`：`running | stale | error`
  - `reason`：`null | staleSession | invalidSession | probeTimeout | probeFailed`
  - `issuedAtUtc` / `processId` / `endpointTransportKind` / `endpointAddress`：valid session を読めた場合のみ値が入る
  - `diagnosis`：`DaemonDiagnosisOutput | null`
    - `reason`, `message`, `reportedBy`, `isInferred`, `updatedAtUtc`, `processId` を持つ
    - `running` は `null`
    - `stale` は current session に一致する diagnosis を返す。無い場合は `externalTerminationSuspected` を合成することがある
    - `error` は `null`

#### `daemon` のエラー契約
- `INVALID_ARGUMENT`
  - `--timeout` が空文字・空白・非数値・`0` 以下
  - `--projectPath` がUnityプロジェクトとして解決不能
  - `ucli daemon list` の対象 project が Git worktree 配下にない
- `IPC_TIMEOUT`
  - `daemon list` 全体の共有 timeout budget が Git worktree 列挙の完了前に尽きた
  - item 単位の probe が共有 budget 消費前に individual timeout として失敗した場合は、コマンド全体は成功のまま `items[*].reason = probeTimeout` を返す
  - Git worktree 列挙完了後に共有 timeout budget が尽きた場合は、コマンド全体は成功のまま `isComplete = false` と `completionReason = timeout` を返す
- `INTERNAL_ERROR`
  - Git worktree 列挙失敗、起動失敗、停止失敗などの内部エラー
  - `daemon list` の item 単位の invalid session / stale session / unexpected probe failure はコマンド全体の失敗にはせず、`items[*].state` / `items[*].reason` に反映する

#### `daemon` 実行例
```bash
ucli daemon start --projectPath ./UnityProject
ucli daemon stop --projectPath ./UnityProject --timeout 5000
ucli daemon status --projectPath ./UnityProject
ucli daemon list --projectPath ./UnityProject
```

### `ucli logs`
ログ取得コマンド。`ucli logs unity` と `ucli logs daemon` を提供する。

#### `logs` 出力契約
- `ucli logs` の成功時は共通エンベロープを返さず、`stdout` にログイベントを逐次出力する。
- `--format json` は NDJSON とし、1イベントを1行のJSONオブジェクトで出力する（配列ラップしない）。
- `--format text` は1イベントを1行のテキストで出力する。
- `--stream` 未指定時は取得条件に一致する範囲を出力して終了する。
- `--stream` 指定時は終了条件（`Ctrl+C` / `--idleTimeoutMilliseconds` 到達 / `--until` 到達）まで継続出力する。
- 入力検証エラーなど、ストリーム開始前に失敗した場合は共通エンベロープの `status=error` を1件返して終了する。

#### `logs` 共通 options（`unity` / `daemon`）
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--tail <int?>` | - | 取得件数上限（新しい順）。`1..10000` |
| `--after <string?>` | - | 前回レスポンスの `nextCursor` 以降を取得する増分カーソル |
| `--since <string?>` | - | 取得開始時刻（ISO 8601、タイムゾーン必須） |
| `--until <string?>` | - | 取得終了時刻（ISO 8601、タイムゾーン必須） |
| `--level <string?>` | - | `error` / `warning` / `info` / `all` |
| `--query <string?>` | - | ログ検索クエリ |
| `--queryTarget <string?>` | - | `message` / `stack` / `both` |
| `--stream` | - | ストリーム取得。新規ログを継続表示する |
| `--pollIntervalMilliseconds <int?>` | - | `--stream` 時のポーリング間隔（ミリ秒）。`50..60000`、既定 `300` |
| `--idleTimeoutMilliseconds <int?>` | - | `--stream` 時に新規ログが無い状態で自動終了するまでの時間（ミリ秒）。`1..2147483647` |
| `--format <string?>` | - | `json` / `text` |

#### `logs unity` options
| Option | Short | Description |
| --- | --- | --- |
| `--source <string?>` | - | `compile` / `runtime` / `all` |
| `--stackTrace <string?>` | - | `none` / `error` / `all` |
| `--stackTraceMaxFrames <int?>` | - | スタックトレースの最大フレーム数（`1..512`） |
| `--stackTraceMaxChars <int?>` | - | スタックトレースの最大文字数（`256..131072`） |

#### `logs daemon` options
| Option | Short | Description |
| --- | --- | --- |
| `--category <string?>` | - | `lifecycle` / `ipc` / `auth` / `transport` / `health` / `all` |

#### `logs` オプション規則
- `--after` と `--since` を同時指定した場合は `--after` を優先する。
- `--since` は次の形式を受け付ける。`2026-03-05T10:30:00+09:00` / `2026-03-05T01:30:00Z`
- `--since` と `--until` を同時指定する場合は `since <= until` を必須とする。
- `--queryTarget=stack` と `--stackTrace=none` を同時指定した場合、stack 検索にはヒットしない。
- `ucli logs daemon` で `--queryTarget=stack` を指定した場合は `INVALID_ARGUMENT` とする。
- `--stackTrace=none` の場合、`--stackTraceMaxFrames` と `--stackTraceMaxChars` は無効化される。
- `--category` は `ucli logs daemon` でのみ指定可能とする。
- `--stream` はサーバープッシュではなく、`nextCursor` を使った増分ポーリングで実装する。
- `--pollIntervalMilliseconds` は `--stream` 指定時のみ有効とする。
- `--idleTimeoutMilliseconds` は `--stream` 指定時のみ有効とし、無通信時間が閾値を超えた時点で正常終了する。
- `--stream` と `--until` を同時指定した場合、`until` 到達時に正常終了する。
- `--format=json` は `--stream` 有無にかかわらず NDJSON を出力する。

#### `logs` 実行例
```bash
ucli logs unity --projectPath ./UnityProject --tail 200 --level error --source runtime
ucli logs unity --projectPath ./UnityProject --after "<cursor>" --stream --format text
ucli logs daemon --projectPath ./UnityProject --since "2026-03-05T00:00:00+09:00" --format json
ucli logs daemon --projectPath ./UnityProject --stream --pollIntervalMilliseconds 500 --idleTimeoutMilliseconds 60000 --category ipc
ucli logs unity --projectPath ./UnityProject --since "2026-03-05T09:00:00+09:00" --until "2026-03-05T10:00:00+09:00"
```

### `ucli init`
`ucli init` は Git repository root を対象として `.ucli` 雛形を生成する。  
Git root が判定できない環境では実行時CWDを対象にする。

生成対象は `.ucli/local/`, `.ucli/config.json`, `.ucli/.gitignore`。

#### `init` options
| Option | Short | Description |
| --- | --- | --- |
| `--force` | - | 既存の `.ucli/config.json` と `.ucli/.gitignore` を上書きする |

#### `init` の `payload` 契約
- `configPath`：生成した `.ucli/config.json` の絶対パス
- `gitignorePath`：生成した `.ucli/.gitignore` の絶対パス

```json
{
  "configPath": "/path/to/repository-root/.ucli/config.json",
  "gitignorePath": "/path/to/repository-root/.ucli/.gitignore"
}
```

#### `init` のエラー契約
- `INVALID_ARGUMENT`
  - 既存テンプレートファイルがあり `--force` 未指定
- `INTERNAL_ERROR`

### `ucli refresh`
`ucli refresh` は独立コマンドであり、`ucli call` の別名ではない。  
CLIは内部で固定の標準 `call` `execute` リクエストを組み立て、Unity側の既存 `ucli.project.refresh` 実装へ流す。

#### `refresh` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |

#### `refresh` 実行契約
- `stdin` は読まない
- `--requestPath` / `--planToken` / `--withPlan` / `--readIndexMode` は指定できない
- `requestId` はCLIが実行ごとにUUIDを生成する
- 実行対象は次の1件で固定する

```json
{
  "protocolVersion": 1,
  "requestId": "<generated-uuid>",
  "ops": [
    {
      "id": "refresh",
      "op": "ucli.project.refresh",
      "args": {}
    }
  ]
}
```

#### `refresh` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す
- `payload.requestId` に内部 `execute` リクエストの `requestId` を返す
- `payload.opResults` に `ucli.project.refresh` の `call` 結果を返す
- 失敗時は `errors` に machine-readable error を返す
- `planToken` は返さない

#### `refresh` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | 成功 |
| `3` | 入力不正、静的検証失敗 |
| `4` | IPC timeout、daemon未起動、内部障害、Unity側ツール失敗 |

#### `refresh` 実行例
```bash
ucli refresh --projectPath ./UnityProject
ucli refresh --projectPath ./UnityProject --mode daemon --timeout 120000
```

## readIndex（読取索引基盤）
readIndex は、Unity未接続時でも観測系情報をローカル参照できるようにするための読取索引基盤である。  
主目的は「観測→生成→検証ループをオフラインでも決定論的に回すこと」であり、単なる高速化ではない。

### 役割
- 接続コスト分離：`ops` / `type` / `schema` / `scene` / `asset` の観測情報をローカルで参照できるようにする
- 生成品質向上：引数スキーマや型候補を事前参照し、無駄な試行錯誤を減らす
- 決定論強化：`freshness` を機械判定可能にし、実行可否判断を自動化する
- 静的検証強化：`validate` / `plan` が同じ索引を再利用し、事前検証精度を高める

### 適用対象
- 対象コマンド：`ops` / `resolve` / `query` / `validate` / `plan`
- 対象データ：
  - `ops` カタログ
  - 型カタログ（`types.find` 相当、`SerializeReference` 候補）
  - スキーマ地図（`comp.schema` / `asset.schema`）
  - `assets.find` 索引
  - `GUID <-> Path` 変換索引
  - `scenes.findComponents` 索引
  - `scene.tree` 軽量版索引
- 非対象：`call`
  - `call` は readIndex を参照せず、Unity実体で再解決・再検証して実行する

### freshness
`payload.readIndex.freshness` は次のいずれかを返す。

- `fresh`：現在入力と索引入力ハッシュが一致している
- `probable`：接続不可や入力不足により推定最新扱いである
- `stale`：入力差分が検出され、古い可能性が高い

### payload 契約
`ops` / `resolve` / `query` / `validate` / `plan` は、`payload.readIndex` を常時含める。

```json
{
  "readIndex": {
    "used": true,
    "hit": true,
    "source": "index",
    "freshness": "fresh",
    "generatedAtUtc": "2026-02-26T00:00:00Z",
    "fallbackReason": null
  }
}
```

### エラーコード
- `READ_INDEX_BOOTSTRAP_FAILED`
- `READ_INDEX_FORMAT_INVALID`
- `READ_INDEX_FRESH_REQUIRED`

### 実行ポリシー
- 既定は `readIndexDefaultMode=requireFresh`
- 未生成または失効時は遅延生成し、必要な索引のみ更新する
- 索引で解決できない要求はUnityへフォールバックする
- 既存索引があり再生成不能な場合は `stale` で継続する
- 既定挙動は `requireFresh`（`fresh` 必須）
- `--readIndexMode=disabled` は readIndex を使用しない
- `--readIndexMode=allowStale` は `fresh|probable|stale` を許容する
- `--readIndexMode=requireFresh` は `fresh` でないと失敗する

### ディレクトリ構造
```text
<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/index/
  catalogs/
    types.catalog.json
    schemas.catalog.json
  inputs/
    manifest.json
```

### catalog 契約
- `types.catalog.json`
  - `schemaVersion`
  - `generatedAtUtc`
  - `sourceInputsHash`
  - `entries[]`
    - `typeId`
    - `displayName`
    - `namespace`
    - `assemblyName`
    - `baseTypeId`
    - `flags.isAbstract`
    - `flags.isGenericDefinition`
    - `flags.isUnityObject`
    - `flags.isComponent`
    - `flags.isScriptableObject`
    - `flags.isSerializeReferenceCandidate`
- `schemas.catalog.json`
  - `schemaVersion`
  - `generatedAtUtc`
  - `sourceInputsHash`
  - `entries[]`
    - `schemaKey`（`comp:<typeId>` or `asset:<typeId>`）
    - `kind`（`comp` or `asset`）
    - `typeId`
    - `displayName`
    - `properties[]`
      - `path`
      - `propertyType`（`SerializedPropertyType` と意味対応する単一 enum literal）
      - `declaredTypeId`
      - `isArray`
      - `elementTypeId`（`isArray=true` のときのみ非null）
      - `isReadOnly`
- `inputs/manifest.json`
  - `schemaVersion`
  - `generatedAtUtc`
  - `scriptAssembliesHash`
  - `packagesManifestHash`
  - `packagesLockHash`
  - `assemblyDefinitionHash`
  - `combinedHash`

### 無効化（stale）ルール
- `types/schema`
  - `Library/ScriptAssemblies`
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`
  - `.asmdef/.asmref`
- 判定不能（manifest欠落・読込不能・入力不足）は `probable`

## test コマンド（統合仕様）
本節は、旧 `uni-test-hub` の機能を uCLI へ統合するための仕様を示す。
旧仕様と背景情報は `docs/uni-test-hub.md` にアーカイブしている。

### `ucli test run`
Unity を `-batchmode -nographics -runTests` で起動し、実行結果を正規化して返す。

#### `run` options

| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | Unity project root path |
| `--profilePath <string?>` | `-c` | Profile configuration path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--unityVersion <string?>` | `-u` | Unity editor version |
| `--unityEditorPath <string?>` | - | Unity editor executable path or editor directory path |
| `--testPlatform <string?>` | - | `editmode` or `playmode` |
| `--buildTarget <string?>` | `-t` | Build target used when `testPlatform=playmode` |
| `--testFilter <string?>` | `-f` | Test name filter pattern |
| `--testCategory <string[]?>` | - | Test categories (repeat or comma-separated) |
| `--assemblyName <string[]?>` | `-a` | Assembly names (repeat or comma-separated) |
| `--testSettingsPath <string?>` | `-s` | Path to `TestSettings.json` |
| `--timeoutSeconds <int?>` | - | Timeout in seconds (`1..86400`) |

#### `run` の `payload` 契約
- `result`：`pass | fail`（`status=error` の場合は `null`）
- `errorKind`：`invalidInput | infraError | toolError | null`
- `runId`：実行ID
- `artifactsDir`：成果物ディレクトリ
- `summaryJsonPath`：サマリーJSONのパス

#### `run` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | pass |
| `1` | fail |
| `2` | infraError |
| `3` | invalidInput |
| `4` | toolError |

#### `run` 実行例
```bash
ucli test run \
  --projectPath ./UnityProject \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

### `ucli test profile init`
`test` 実行用のプロファイルJSON雛形を作成する。

#### `profile init` options

| Option | Short | Description |
| --- | --- | --- |
| `--outputPath <string?>` | `-o` | Output path for profile JSON (default: `test.profile.json`) |
| `--force` | `-f` | Overwrite existing file |

`--outputPath` が `.json` で終わらない場合は、末尾に `.json` を補完して保存する。  
`--outputPath` が `/` または `\` で終わるディレクトリ形式の場合は `invalidInput` として失敗する。
出力先ディレクトリが存在しない場合は、親ディレクトリを自動作成する。  
`--outputPath` 省略時は `<cwd>/test.profile.json` を使用する。

#### 生成テンプレート
```json
{
  "schemaVersion": 1,
  "projectPath": ".",
  "unityVersion": null,
  "unityEditorPath": null,
  "testPlatform": "editmode",
  "buildTarget": null,
  "testFilter": null,
  "testCategories": [],
  "assemblyNames": [],
  "testSettingsPath": null,
  "timeoutSeconds": 1800
}
```

### `test` 設定解決順序
- `projectPath`: `--projectPath > UCLI_PROJECT_PATH > profile.json > defaults`
- `projectPath` 以外: `CLI options > profile.json > defaults`

### UnityバージョンとEditor解決順序
`unityVersion` は次の順で解決する。

1. `--unityVersion`
2. `profile.json` `unityVersion`
3. `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion`)

`unityEditorPath` は次の順で解決する。

1. `--unityEditorPath`
2. `profile.json` `unityEditorPath`
3. 既定の検索ルートで、解決済み `unityVersion` に一致するEditorを探索

### Artifacts layout
各実行の成果物は次の構造で保存する。

`<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/`

```text
<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/artifacts/test/<runId>/
  meta.json
  results.xml
  editor.log
  results.json
  summary.json
```

## サーバー
別ディレクトリのworktreeは別fingerprintの別デーモンでなければならない。  
同一ディレクトリであれば同一デーモンとする。
- デーモンの同一性は `projectFingerprint` で判定する。

### local保存
- `.ucli/local/` はGit管理対象外とする（`.ucli/.gitignore` で除外）
- テスト成果物の出力先は `<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/artifacts/` とする
- readIndex は `<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/index/` に保存する
- `refresh` は readIndex 更新トリガーに使わない（読取コマンド実行時の遅延更新を採用する）
- `planToken` 本体は通常非永続化（呼び出し側のメモリ受け渡し）
- 永続化するのは署名鍵のみ
  - パス：`<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/plan-token.key`
  - 鍵は遅延生成（`ucli init` では生成しない）

### 識別
- `repoRoot = realpath(Git repository root from CWD or --projectPath)`
- `projectFingerprint = SHA256(normalize(repoRoot) + "\n" + normalize(relativeUnityProjectPathFromRepoRoot))`
  - 同一リポジトリ配下に複数Unityプロジェクトが存在しても衝突しない識別子とする

### エンドポイント
OSごとに最適なローカルIPCを選ぶ。
- Windows：NamedPipe
- Mac / Linux：Unix domain socket

### IPC認可境界
- 接続は同一ユーザーに限定する
  - Windows：NamedPipe ACL
  - Mac / Linux：UDSのディレクトリ/ソケット権限
- 接続時に `sessionToken` を必須照合する
  - 保管先：`<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/session.json`
  - 生成：`ucli daemon start` 時
  - 破棄：`ucli daemon stop` 時
  - 異常終了時：次回 `ucli daemon start` で上書き再生成し、旧トークンを無効化する

### デーモン起動
#### CLI
- プロジェクト解決
- エンドポイント接続試行
- `--mode` 契約に従って実行形態を決定（デーモン自動起動はしない）

#### Unity
- サーバー起動（GUI・ヘッドレス両方）
- リクエスト受信 -> メインスレッド実行キュー -> レスポンス
- ドメインリロード後の復帰
