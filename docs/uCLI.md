## 名称規則
- 正式名称：uCLI
- 名前空間・アセンブリ等（.NET / C#）：`Ucli`
- コマンド：`ucli`

## 対象
- CLI：.NET 8 以上

## 文書マップ
- [uCLI.md](uCLI.md)：製品概要、実行契約、コマンド仕様、`ucli test` 統合仕様の入口
- [uCLI-design-principles.md](uCLI-design-principles.md)：設計原則の正本
- [json-request-spec.md](json-request-spec.md)：JSON リクエスト入力契約の正本
- [uCLI-command-reference.md](uCLI-command-reference.md)：コマンド一覧、option table、終了コード、実行例
- [uCLI-property-reference.md](uCLI-property-reference.md)：JSON 契約と `payload` / catalog のプロパティ定義
- [ops-catalog.md](ops-catalog.md)：primitive operation の補助カタログ
- [package-operations.md](package-operations.md)：契約パッケージと Unity 側依存復元の運用手順

## コンセプト
**安全にUnityを編集できるCLIツール。**
- Unityを **ヘッドレス（batchmode）** で起動して実行できる（oneshot）
- Unityを **常駐サーバ（デーモン）** として起動し、繰り返しリクエストを処理できる
- CLI起動、ユーザー起動のGUIインスタンス、両方でデーモンは起動する
- 変更は Unity Editor API（AssetDatabase / Scene / Prefab / SerializedObject 等）に限定する。YAML直編集を前提にしない
- すべての入出力はJSONを基本にする
- Unity Test Framework の実行と結果正規化を、`ucli test` として統合提供する予定である

設計思想と境界原則の詳細は [uCLI-design-principles.md](uCLI-design-principles.md) を正本とする。

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

### 共通レスポンスオブジェクト
`init` / `ops` / `status` / `daemon` / `test` / `refresh` が返す共通エンベロープのフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `protocolVersion` 規則
- 初版はメジャーバージョン整数のみを使用する（例：`1`）
- 受信したメジャーバージョンがサーバー対応値と一致しない場合は、処理を実行せずJSONエラーを返す
- 推奨エラーコード：`PROTOCOL_VERSION_MISMATCH`

### JSONリクエスト入力
`validate` / `plan` / `call` / `resolve` / `query` が受け付ける JSON リクエストのトップレベル構造、step 種別、編集 DSL、参照表現、サンプルは [json-request-spec.md](json-request-spec.md) を正本とする。  
本書では、そのリクエストを CLI と Unity runtime がどのように検証・実行するかだけを定義する。

### request系レスポンス
`validate` / `plan` / `call` / `resolve` / `query` が返すレスポンス、`opResults`、エラーオブジェクトのフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。  
`status=error` であっても、先行する実行単位が適用済みである場合がある。適用状況は `opResults` で機械判定する。

## `plan` / `call` の基本入力
`plan` と `call` は JSONリクエストを受け取る。  
リクエストの入力形式は [json-request-spec.md](json-request-spec.md) に従う。  
リクエストは必要に応じて lower と正規化を経て内部実行単位へ展開して処理する。  
リクエストは原則として1本ずつ直列実行する。

- `plan`：リクエストを **実変更なし**（または最小の観測のみ）で検証・解決し、差分見積りを返す
- `call`：同じリクエストを **実際に適用して保存**する
  - `call` は実行前に `plan` 相当（validate/resolve/差分計算/実行可能性確認）の検証を挟む
  - uCLIでの操作による永続化は `call` でのみ可能

### 失敗時実行方針
- `fail-fast` 固定とする
- `call` で失敗した場合、失敗した実行単位以降は実行しない
- 未実行の実行単位は `phase=skipped`、`applied=false`、`changed=false` として返す

### `planToken` とドリフト検知
- `plan` はレスポンスに `planToken` を返す
- `call` は `planToken` がある場合に、署名・有効期限・リクエスト一致・状態一致を検証する
- CLI入力JSON（`protocolVersion` / `requestId` / `steps`）には `planToken` を含めない
- `planToken` は `ucli call --planToken <token>` で渡し、CLIがIPC `execute` リクエストの `planToken` フィールドへ転送する
- `call` の実行順序は次で固定する
  - Editor lifecycle の `ready` 判定を行う
  - `ready` でない場合はライフサイクル専用エラーで即時失敗し、`Validate` / `Plan` / `Call` は1件も実行しない
  - `ready` の場合に全stepに対して `Validate` / `Plan` を実行（fail-fast）
  - `planToken` を検証
  - 検証成功時のみ全stepの `Call` を実行（fail-fast）
- 検証エラー例
  - `PLAN_TOKEN_REQUIRED`
  - `PLAN_TOKEN_INVALID`
  - `PLAN_TOKEN_EXPIRED`
  - `PLAN_TOKEN_REQUEST_MISMATCH`
  - `STATE_CHANGED_SINCE_PLAN`

#### `requestDigest` の生成
- Unityサーバー側で生成する
- 対象は `protocolVersion` と `steps`（`requestId` は対象外）
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
- `compileState` と `domainReloadGeneration` は state drift 判定用の入力として保持する
- `lifecycleState = starting | blockedByModal | safeMode | shuttingDown` は readiness gate として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで失敗させる
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
- `ready` 判定は `planToken` 検証より前に行う
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

## Editor Lifecycle
Editor lifecycle は、Unity runtime の要求受付可否を外部から機械判定するための公開契約である。  
`runtime` が `batchmode` / `gui` のどちらであっても同じ状態語彙を使う。  
ただし、`ucli daemon start` / `ucli daemon stop` の管理対象は batchmode daemon を正本とする。

### 状態軸
- `daemonStatus`
  - `running`：daemon endpoint に到達可能で、現在の session が有効
  - `notRunning`：対象プロジェクトに daemon session が存在しない、または endpoint に到達できない
  - `stale`：session は残っているが、現在の daemon は到達不能または外部終了が疑われる
- `lifecycleState`
  - `starting`
  - `ready`
  - `compiling`
  - `domainReloading`
  - `blockedByModal`
  - `safeMode`
  - `shuttingDown`
- `daemonStatus != running` の場合、`lifecycleState` は `null` とする

### `ready` の定義
`ready` は次をすべて満たす状態とする。
- IPC 到達可能
- コンパイル中ではない
- ドメインリロード中ではない
- モーダルダイアログによって停止していない
- Safe Mode ではない
- 終了処理中ではない

### 状態優先順位
同時に複数候補が成立しうる場合、`lifecycleState` は次の優先順位で1つに正規化する。
1. `daemonStatus != running` の場合は `lifecycleState = null`
2. `shuttingDown`
3. `blockedByModal`
4. `safeMode`
5. `domainReloading`
6. `compiling`
7. `starting`
8. `ready`

`blockedByModal` は前面に出ているモーダルを最優先のブロック要因として表し、背後で発生している compile / domain reload より優先して返す。

### 状態項目
`ucli status` / `ucli daemon status` が返す `runtime` / `compileState` / `domainReloadGeneration` / `blockingReason` / `canAcceptExecutionRequests` のプロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `null` と `na`
- API payload では未観測・非適用値を `null` で返す
- `stateFingerprint` の内部入力では未取得値を `na` に正規化して算出する
- したがって、レスポンス契約の `null` と内部ハッシュ入力の `na` は同義ではない

### 状態遷移契約
| `lifecycleState` | 意味 | 遷移起点 | 許可コマンド | 拒否時エラーコード | 離脱条件 |
| --- | --- | --- | --- | --- | --- |
| `starting` | runtime 起動直後で、まだ `ready` 条件を満たしていない | daemon 起動直後、GUI runtime の初期化直後、domain reload 後の再初期化 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_STARTING` | `ready` 条件を満たす、または他の優先状態へ移る |
| `ready` | 実行要求を安全に受け付けられる | startup 完了、compile 完了、domain reload 完了、modal 解消、Safe Mode 解消 | 全コマンド | なし | compile 開始、domain reload 開始、modal 発生、Safe Mode 進入、shutdown 開始 |
| `compiling` | スクリプト compile が進行中 | スクリプト変更、refresh / import、package 変更等で compile が始まる | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_COMPILING` | compile 完了後、`ready` または `domainReloading` / 他の優先状態へ移る |
| `domainReloading` | domain reload 中で実行コンテキストが安定していない | compile 完了後の domain reload、手動再読み込み | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_DOMAIN_RELOADING` | domain reload 完了後、`starting` または `ready` / 他の優先状態へ移る |
| `blockedByModal` | モーダルダイアログが前面にあり、進行不能 | reload prompt、保存確認、ライセンス・認証・確認ダイアログ等 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_MODAL_BLOCKED` | モーダルが解消され、次の優先状態を再評価する |
| `safeMode` | Safe Mode により通常実行を許可しない | compile error 起因の Safe Mode 進入 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_SAFE_MODE` | Safe Mode 解消後に `starting` または `ready` / 他の優先状態へ移る |
| `shuttingDown` | runtime が終了処理中 | `daemon stop`、親プロセス終了、Unity 側終了要求 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_SHUTTING_DOWN` | runtime 終了後に `daemonStatus = notRunning | stale` になる |

### 実行系コマンドの状態別挙動
- `status`, `daemon status`, `logs`, `daemon stop` は常時利用可能とする
- `ops`, `resolve`, `query`, `plan`, `call`, `refresh`, `test.run` は `lifecycleState = ready` 必須とする
- `ucli validate` はローカル静的検証コマンドであり、Editor lifecycle gate の対象外とする
- `lifecycleState != ready` のとき、実行系コマンドはサーバー側で即時拒否し、固定 `sleep` を前提にしない
- `oneshot` でも、起動後に `ready` 判定が成立するまでは実行要求を受け付けない

### `planToken` / ドリフト検知との関係
- `compileState` と `domainReloadGeneration` の変化は `stateFingerprint` に反映し、`STATE_CHANGED_SINCE_PLAN` の候補に含める
- `starting`, `blockedByModal`, `safeMode`, `shuttingDown` は readiness gate として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで拒否する
- `call` は `ready` 判定に失敗した場合、その時点で終了する
  - `Validate` / `Plan` / `planToken` 検証 / `Call` は1件も実行しない
- `call` は `ready` のときのみ `Validate -> Plan -> planToken 検証 -> Call` に進む

### ライフサイクル専用エラーコード
- `EDITOR_STARTING`
- `EDITOR_COMPILING`
- `EDITOR_DOMAIN_RELOADING`
- `EDITOR_MODAL_BLOCKED`
- `EDITOR_SAFE_MODE`
- `EDITOR_SHUTTING_DOWN`

### レスポンス例
非 `ready` の `ucli status` 成功レスポンス例:

```json
{
  "protocolVersion": 1,
  "command": "status",
  "status": "ok",
  "exitCode": 0,
  "message": "Daemon status observed successfully.",
  "payload": {
    "daemonStatus": "running",
    "unityVersion": "6000.0.43f1",
    "serverVersion": "0.13.1",
    "runtime": "batchmode",
    "lifecycleState": "compiling",
    "blockingReason": "compile",
    "compileState": "compiling",
    "domainReloadGeneration": "42",
    "canAcceptExecutionRequests": false,
    "timeoutMilliseconds": 3000
  },
  "errors": []
}
```

`call` がライフサイクル専用エラーで即時拒否されるレスポンス例:

```json
{
  "protocolVersion": 1,
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "status": "error",
  "opResults": [],
  "errors": [
    {
      "code": "EDITOR_COMPILING",
      "message": "Unity editor is compiling scripts. Wait until lifecycleState=ready before executing request.",
      "opId": null
    }
  ]
}
```

## 入力方法（CLI）
- 基本：**stdin のJSONを読む**
  - `ucli plan < request.json`
  - `ucli call < request.json`
- オプション：`--requestPath <jsonPath>` で JSONリクエストファイルを指定可能
- `call` の `planToken` は `--planToken <token>` で指定する

## オペレーション
JSON リクエストの step のうち `kind: "op"` は primitive operation を表す。  
入力 DSL の正式形、`kind: "edit"`、`on` / `select` / `actions` / `export` / `commit` の仕様は [json-request-spec.md](json-request-spec.md) を正本とする。  
本節では、オペレーションの命名、ガード、登録、実行フェーズなど、実行基盤側の契約だけを扱う。

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
- `config.json` が存在しない場合は既定値を適用し、ファイル生成を必須前提にしない

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
    "daemon.cleanup": 3000,
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

### JSON リクエスト入力 DSL
`kind: "op"` / `kind: "edit"` の使い分け、`on` / `select` / `actions` / `export` / `commit` の仕様、入力サンプルは [json-request-spec.md](json-request-spec.md) を参照する。  
利用可能な primitive operation の一覧と概況は [ops-catalog.md](ops-catalog.md) を参照する。

## コマンド
Unityプロジェクトを対象に実行するコマンドは、CWDがUnityプロジェクトと判定可能な場合はそれを使う。そうでない場合は `--projectPath` を指定する。

- `ucli init`：設定雛形を作成する
  - 生成先：Git repository root 直下（`<repoRoot>/.ucli`）
  - 生成対象：`.ucli/config.json`, `.ucli/.gitignore`
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
  - 出力は共通の `CommandResult` エンベロープを返す
- `ucli ops`：利用可能なオペレーション一覧・詳細
  - `list`：利用可能なオペレーション一覧
  - `describe <opName>`：特定オペレーションの引数スキーマ
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
- `ucli status`：
  - CWDか `--projectPath` でプロジェクト指定
  - `--timeout <int>`（ミリ秒）で daemon 状態確認タイムアウトを上書きする（未指定時は config を使用）
  - `unityVersion` は常に `ProjectSettings/ProjectVersion.txt` 由来とする
  - `compileState = ready` であっても `lifecycleState = ready` を意味しない。意味は `Editor Lifecycle` 節を正とする
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
  - `cleanup`：安全に確定できる daemon 残骸を掃除
  - `status`：デーモン状態を取得
  - `list`：同一Git repository の worktree 群にある daemon 登録を一覧
  - `--projectPath <path>`：対象Unityプロジェクト指定

### コマンド詳細リファレンス
各コマンドの option table、サブコマンド規則、エラー契約、終了コード、実行例は [uCLI-command-reference.md](uCLI-command-reference.md) を参照する。  
各コマンドの `payload` フィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

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

### readIndex プロパティ
`payload.readIndex` と各 catalog / manifest のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

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

### 無効化（stale）ルール
- `types/schema`
  - `Library/ScriptAssemblies`
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`
  - `.asmdef/.asmref`
- 判定不能（manifest欠落・読込不能・入力不足）は `probable`

## test コマンド（統合仕様）
本節は、Unity Test Framework の実行と結果正規化を `ucli test` として提供するための統合仕様案を示す。
`ucli test run` / `ucli test profile init` の option table、設定解決順序、生成テンプレート、Artifacts layout、終了コード、実行例は [uCLI-command-reference.md](uCLI-command-reference.md) を参照する。  
`ucli test run` の `payload` とテストプロファイルJSONのプロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

## サーバー
別ディレクトリのworktreeは別fingerprintの別デーモンでなければならない。  
同一ディレクトリであれば同一デーモンとする。
- デーモンの同一性は `projectFingerprint` で判定する。

### local保存
- `.ucli/local/` はGit管理対象外とする（`.ucli/.gitignore` で除外）
- `.ucli/local/**` は通常コマンドが local 保存を初めて行う時点で遅延生成する
- `.ucli/.gitignore` は通常コマンドの初回 local 保存時に、不足している場合のみ自動生成する
- 既存の `.ucli/.gitignore` は通常コマンドで書き換えない
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
- `ucli daemon start` / `ucli daemon stop` の管理対象は batchmode daemon を正本とする

#### Unity runtime
- runtime は `batchmode | gui` のいずれかで動作し、`Editor Lifecycle` 節の同一状態語彙を使う
- 起動直後は `starting` に入り、`ready` 条件を満たした時点で要求受付可能になる
- `compiling` / `domainReloading` / `blockedByModal` / `safeMode` / `shuttingDown` の間、実行系コマンドはライフサイクル専用エラーで即時拒否する
- `status` / `daemon status` / `logs` / `daemon stop` はライフサイクル状態に関わらず利用可能とする
- ドメインリロード後は再初期化を経て `starting` から再評価し、最終的に `ready` または他のブロック状態へ遷移する
