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
timeout は mode decision、plugin verify、IPC dispatch、readiness wait をまたいで 1 回のコマンド全体で共有 budget として消費する。

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
| 起動中 | デーモン経由で実行 | デーモン経由で実行 | JSONエラー（`toolError`, `DAEMON_RUNNING_ONESHOT_FORBIDDEN`） |
| 未起動 | JSONエラー（`toolError`, `DAEMON_NOT_RUNNING`） | oneshotで実行 | oneshotで実行 |

## 公開 CLI 出力と JSON 入力契約
- 公開 CLI 出力は `request-response` 型と `stream` 型の2種類を持つ
- `request-response` 型では `stdout` に JSON を1件だけ出力する
- `stream` 型では `stdout` にイベントを逐次出力する
- 現在 `stream` 型を使うのは `ucli logs` の成功時だけであり、`--format json` は NDJSON（1行1JSONオブジェクト）を返す
- 進行ログと診断ログは `stderr` に出力する
- `request-response` 型の公開 CLI JSON 出力は、共通の CLI エンベロープを返す
- `protocolVersion` は `request-response` 型の公開 CLI JSON 出力、JSON リクエスト、内部 IPC 応答で必須とする
- 現在の公開 CLI host が登録している command は `init`、`status`、`refresh`、`resolve`、`query`、`validate`、`plan`、`call`、`daemon`、`logs`、`ops`、`test` である
- 内部 execute request では、リクエストにも `protocolVersion` を必須とする
- 互換性判定は `protocolVersion` で行う
- ただし、CLIフレームワーク（ConsoleAppFramework）の既定経路（`--help` / `help` / `--version` など）は、既定のテキスト出力を返す。これらは本JSON契約の適用対象外とする

### CLI出力契約
- `request-response` 型の `status` は `ok | error` を使用する
  - `ok`：コマンドが契約どおり完了した
  - `error`：入力不正、インフラ障害、外部ツール障害などで完了できなかった
- `request-response` 型の終了コードはコマンド別契約に従う
- request系 payload を返すコマンド（`validate` / `plan` / `call` / `resolve` / `query`）は `status=ok` のとき `exit code = 0`、`status=error` のとき `exit code != 0`
  - `ucli test run` は `status=ok` かつ `payload.result=fail` の場合に `exit code = 1` を返す
- `stream` 型の成功時は `CommandResult` を返さない
- `stream` 開始前に失敗した場合は、`request-response` 型のエラー JSON を返す

### 公開 CLI 共通エンベロープ
`request-response` 型の公開 CLI JSON 出力が返す共通エンベロープのフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### 内部 IPC 応答
CLI と Unity runtime の間では、公開 CLI エンベロープとは別に IPC 専用エンベロープを使う。`requestId`、IPC の `status`、IPC の `payload`、IPC の `errors` はこの内部契約に属する。公開 CLI が `requestId` や execute 結果を返す場合は、各コマンドの `payload` へ写像する。

### `protocolVersion` 規則
- 初版はメジャーバージョン整数のみを使用する（例：`1`）
- 受信したメジャーバージョンがサーバー対応値と一致しない場合は、処理を実行せずJSONエラーを返す
- 推奨エラーコード：`PROTOCOL_VERSION_MISMATCH`

### JSONリクエスト入力
内部 execute request が受け付ける JSON リクエストのトップレベル構造、step 種別、編集 DSL、参照表現、サンプルは [json-request-spec.md](json-request-spec.md) を正本とする。  
本書では、そのリクエストを CLI と Unity runtime がどのように検証・実行するかだけを定義する。

### execute 系応答の内部契約と公開/未公開写像
`plan` / `call` / `resolve` / `query` / `refresh` は、内部では `IpcResponse.payload = IpcExecuteResponse` を受け取る。公開 CLI がこの内部応答を返す場合は、その値を各コマンドの `payload` へ写像する。
- `requestId` は IPC 相関 ID であり、公開 CLI の共通 top-level property ではない
- `opResults` は execute 応答に属し、公開するコマンドでは `payload.opResults` に写像する
- `opResults` の単位は public `steps[]` であり、lower 後 primitive trace をそのまま公開しない
- `planToken` は execute 応答に属し、`ucli plan` では `payload.planToken` に写像する
- `ucli call --withPlan` は、事前 `plan` 実行の結果を `payload.plan` へ写像する
- `ucli call` は `payload.readIndex` を返さない
- `resolve` は `payload.requestId`、`payload.opResults`、`payload.readIndex` を返す
- `query` は `payload.requestId`、`payload.opResults`、`payload.readIndex` を返す
- `refresh` は `payload.requestId` と `payload.opResults` を返す
- request 系コマンドの機械判定用エラーは、公開 CLI では共通エンベロープの `errors[]` に載せる
- `status=error` であっても、先行する実行単位が適用済みである場合がある。`payload.opResults` を返すコマンドでは、その値で適用状況を機械判定する
- `IPC_TIMEOUT`、reload disconnect、runtime crash は、それだけで「未適用」を意味しない
- timeout / disconnect / crash の場合でも、呼び出し側は `status` だけで未適用と断定しない

## 内部 `plan` / `call` 実行の基本入力
内部 `plan` と `call` は JSONリクエストを受け取る。  
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

### `projectFingerprint` 単位の writer 排他
- 同一 `projectFingerprint` に対する writer は常に 1 本だけ許可する
- `daemon` / `oneshot` / `refresh` / `test.run` / mutation `call` は、同じ writer 排他モデルに参加する
- `status` / `daemon status` / `logs` / `ops` / `resolve` / `query` / `validate` / `plan` は reader 側の経路とし、writer lane を取らない
- queue と即時拒否のどちらを採るかは実装選択とするが、並列 writer を許可しないことは固定契約とする

### `planToken` とドリフト検知
- `plan` は内部 execute 応答で `planToken` を発行し、公開 `ucli plan` では `payload.planToken` として返す
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
- request digest は `protocolVersion` と canonicalized `steps[]` を入力に使う
- plan token 検証では request digest に加えて compiled execution digest も使い、compile / query semantics のドリフトを検出する
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
- `blockedByModal` / `safeMode` / `playmode` / `shuttingDown` は readiness gate の即時失敗状態として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで失敗させる
- `starting` / `busy` / `compiling` は既定で待機対象とし、`--failFast` 指定時だけライフサイクル専用エラーで失敗させる
- `domainReloading` は request を AppDomain reload 越しに再開しないため、既定でもライフサイクル専用エラーで失敗させる
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
  - `busy`
  - `compiling`
  - `domainReloading`
  - `playmode`
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
- Play Mode ではない
- `busy` 判定の対象となる Editor 内部処理が進行中ではない
- 終了処理中ではない

### 状態優先順位
同時に複数候補が成立しうる場合、`lifecycleState` は次の優先順位で1つに正規化する。
1. `daemonStatus != running` の場合は `lifecycleState = null`
2. `shuttingDown`
3. `blockedByModal`
4. `safeMode`
5. `playmode`
6. `domainReloading`
7. `compiling`
8. `busy`
9. `starting`
10. `ready`

`blockedByModal` は前面に出ているモーダルを最優先のブロック要因として表し、背後で発生している compile / domain reload より優先して返す。

- batchmode daemon の現実装で観測・返却する `lifecycleState` は `starting` / `ready` / `busy` / `compiling` / `domainReloading` / `playmode` / `shuttingDown` のみとする
- `blockedByModal` と `safeMode` は GUI / non-batchmode runtime 向けの reserved literal として保持するが、batchmode daemon ではまだ返さない

### 状態項目
`ucli status` / `ucli daemon status` が返す `runtime` / `compileState` / `compileGeneration` / `domainReloadGeneration` / `blockingReason` / `canAcceptExecutionRequests` のプロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `null` と `na`
- API payload では未観測・非適用値を `null` で返す
- `stateFingerprint` の内部入力では未取得値を `na` に正規化して算出する
- したがって、レスポンス契約の `null` と内部ハッシュ入力の `na` は同義ではない

### 状態遷移契約
| `lifecycleState` | 意味 | 遷移起点 | 許可コマンド | 拒否時エラーコード | 離脱条件 |
| --- | --- | --- | --- | --- | --- |
| `starting` | runtime 起動直後で、まだ `ready` 条件を満たしていない | daemon 起動直後、GUI runtime の初期化直後、domain reload 後の再初期化 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_STARTING` | `ready` 条件を満たす、または他の優先状態へ移る |
| `ready` | 実行要求を安全に受け付けられる | startup 完了、compile 完了、domain reload 完了、modal 解消、Safe Mode 解消、Play Mode 終了、busy 解消 | 全コマンド | なし | compile 開始、domain reload 開始、modal 発生、Safe Mode 進入、Play Mode 進入、busy 開始、shutdown 開始 |
| `busy` | compile / reload 以外の Editor 内部処理により実行受付を止めている | refresh 直後の内部同期、保存前後の排他的処理、サーバーが busy と判定する Unity 側処理 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_BUSY` | busy 解消後に `ready` または他の優先状態へ移る |
| `compiling` | スクリプト compile が進行中 | スクリプト変更、refresh / import、package 変更等で compile が始まる | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_COMPILING` | compile 完了後、`ready` または `domainReloading` / 他の優先状態へ移る |
| `domainReloading` | domain reload 中で実行コンテキストが安定していない | compile 完了後の domain reload、手動再読み込み | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_DOMAIN_RELOADING` | domain reload 完了後、`starting` または `ready` / 他の優先状態へ移る |
| `playmode` | Play Mode 中で通常実行を許可しない | Editor の Play 実行開始 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_PLAYMODE` | Play Mode 終了後に `starting` または `ready` / 他の優先状態へ移る |
| `blockedByModal` | モーダルダイアログが前面にあり、進行不能 | reload prompt、保存確認、ライセンス・認証・確認ダイアログ等 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_MODAL_BLOCKED` | モーダルが解消され、次の優先状態を再評価する |
| `safeMode` | Safe Mode により通常実行を許可しない | compile error 起因の Safe Mode 進入 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_SAFE_MODE` | Safe Mode 解消後に `starting` または `ready` / 他の優先状態へ移る |
| `shuttingDown` | runtime が終了処理中 | `daemon stop`、親プロセス終了、Unity 側終了要求 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_SHUTTING_DOWN` | runtime 終了後に `daemonStatus = notRunning | stale` になる |

- `starting` は `ping` / `status` / fail-fast 実行要求で snapshot を読んだだけでは消費しない。runtime 側の startup 完了イベントが観測されるまで継続する。

### `failFast`
- `failFast` は CLI option のみであり、JSON request に追加しない
- `.ucli/config.json` に既定値は持たない
- readiness gate を持つ実行系コマンドは既定で待機し、`--failFast` を指定した場合だけ fail-fast に反転する
- 待機対象は `starting` / `busy` / `compiling` とする
- `blockedByModal` / `safeMode` / `playmode` / `shuttingDown` は待機中でも即時失敗する
- 待機は既存の `--timeout` budget を消費し、使い切った場合は `IPC_TIMEOUT` を返す
- `ops list` / `ops describe` では live source fallback に対してのみ意味を持つ。readIndex hit では Unity 接続も readiness wait も行わない
- `test.run` では daemon-backed execution に対してのみ意味を持つ。`oneshot` と `auto -> oneshot` は direct `-runTests` のままとし、readiness wait を行わない

### 実行系コマンドの状態別挙動
- `status`, `daemon status`, `logs`, `daemon stop` は常時利用可能とする
- 未公開の request static validation は Editor lifecycle gate の対象外とする
- 内部 `plan`, `call`, `resolve`, `query` と公開 `refresh`、daemon-backed `test.run` は `lifecycleState = ready` 必須とする
- 公開 `ops list` / `ops describe` は readIndex hit では lifecycle gate の対象外とし、live source fallback 時だけ `lifecycleState = ready` を要求する
- 上記コマンドは既定で wait 対象状態を待機し、`--failFast` 指定時のみ `lifecycleState != ready` で即時失敗する
- `test.run` の `oneshot` target は readiness wait を行わず、`--failFast` を指定しても挙動を変えない
- `test.run` の timeout は発生源で分類する
  - daemon IPC timeout と readiness wait の timeout は `IPC_TIMEOUT`
  - oneshot Unity process timeout は `UNITY_TEST_EXECUTION_TIMEOUT`

### `planToken` / ドリフト検知との関係
- `compileState` と `domainReloadGeneration` の変化は `stateFingerprint` に反映し、`STATE_CHANGED_SINCE_PLAN` の候補に含める
- `blockedByModal`, `safeMode`, `playmode`, `shuttingDown` は readiness gate として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで拒否する
- `starting`, `busy`, `compiling` は既定で待機対象とし、`--failFast` 指定時だけライフサイクル専用エラーで拒否する
- `domainReloading` は AppDomain reload を跨いで要求を再開しないため、既定でも即時拒否する
- `call` は `ready` 判定に失敗した場合、その時点で終了する
  - `Validate` / `Plan` / `planToken` 検証 / `Call` は1件も実行しない
- `call` は `ready` のときのみ `Validate -> Plan -> planToken 検証 -> Call` に進む
- `compileGeneration` は公開 telemetry として返すが、本版では `stateFingerprint` の入力には含めない

### mutation 後の read 安全性
- mutation 後に正本とみなせるのは live Unity で再観測した状態のみとする
- `call` / `refresh` が read surface を無効化した場合、payload に `readPostcondition.requirements[]` を返す
- 対象 surface は `assetSearch`、`guidPath`、`sceneTreeLite(scenePath)` のみとする
- `query` / `resolve` は matching requirement があるとき、`payload.readIndex.generatedAtUtc >= requirement.minSafeGeneratedAtUtc` を満たす readIndex だけを safe とみなす
- `generatedAtUtc` 欠落または requirement より古い readIndex は、`freshness` や `allowStale` にかかわらず unsafe とし、live Unity へフォールバックする
- matching requirement が無い surface にだけ既存の `freshness` 契約を適用する
- この postcondition 契約は `assetSearch.lookup`、`guid-path.lookup`、`scene-tree-lite` に限定し、`ops` / `types` / `schemas` catalog や static preflight の `readIndex` には適用しない

### ライフサイクル専用エラーコード
- `EDITOR_STARTING`
- `EDITOR_BUSY`
- `EDITOR_COMPILING`
- `EDITOR_DOMAIN_RELOADING`
- `EDITOR_PLAYMODE`
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
    "lifecycleState": "busy",
    "blockingReason": "busy",
    "compileState": "ready",
    "compileGeneration": "173",
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
  "command": "call",
  "status": "error",
  "exitCode": 4,
  "message": "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.",
  "payload": {
    "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
    "opResults": []
  },
  "errors": [
    {
      "code": "EDITOR_COMPILING",
      "message": "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.",
      "opId": null
    }
  ]
}
```

既定の readiness wait で timeout budget を使い切ったレスポンス例:

```json
{
  "protocolVersion": 1,
  "command": "call",
  "status": "error",
  "exitCode": 4,
  "message": "Timed out while waiting for Unity editor to become ready.",
  "payload": {
    "requestId": "4b977408-e66e-48eb-bcc5-24ea5bce9b62",
    "opResults": []
  },
  "errors": [
    {
      "code": "IPC_TIMEOUT",
      "message": "Timed out while waiting for Unity editor to become ready.",
      "opId": null
    }
  ]
}
```

## 入力方法（request CLI）
- 基本：**stdin のJSONを読む**
  - `ucli plan < request.json`
  - `ucli call < request.json`
- オプション：`--requestPath <jsonPath>` で JSONリクエストファイルを指定可能
- `call` の `planToken` は `--planToken <token>` で指定する

## オペレーション
JSON リクエストの step のうち `kind: "op"` は primitive operation を表す。  
入力 DSL の正式形、`kind: "edit"`、`on` / `select` / `actions` / `as` / `commit` の仕様は [json-request-spec.md](json-request-spec.md) を正本とする。  
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
- `kind`：`query | command | mutation`
  - `query`：観測のみ
  - `command`：Editor 状態や AssetDatabase 状態を変えるが、永続化対象の内容変更を主目的にしない
  - `mutation`：Scene / Prefab / Asset / Project の永続化対象を dirty または保存し得る
- `policy`：`safe | advanced | dangerous`
- `description`：operation の目的、使いどころ、注意点。agent が operation を選ぶための必須説明
- `inputs`：ユーザー入力から `steps[].args` を組み立てるための主契約。入力値の意味制約は `inputs[].constraints` に置く
- `resultContract`：`opResults[].result` の有無、Result contract 型名、主データの意味を表す。step 間データフローや durable output は表さない
- `assurance`：`sideEffects`、`mayDirty`、`mayPersist`、`touchedKinds`、`planMode` による機械判定可能な保証情報
- `argsSchema`：引数 `args` のJSONスキーマ。`steps[].args` の JSON 構造検証だけを担う
- `resultSchema`：`opResults[].result` のJSONスキーマ。結果を返さない operation は `null` とし、実行結果でも `result` field を省略する

Args/Result contract 型は `MackySoft.Ucli.Contracts` に置き、公開 JSON 構造の正本にする。Scene asset path、Prefab asset path、Hierarchy path、GlobalObjectId、asset GUID、request-local alias、Unity type identifier など、複数 operation で同じ意味を持つ string 入力/出力は C# contract 上では semantic value type として表し、IPC JSON では primitive string のまま扱う。入力ごとの説明と意味制約は Args 型の property 属性、または semantic value type の属性（`UcliDescriptionAttribute` / `UcliInputConstraintAttribute`）に置き、`ops describe` の `inputs[]` はその属性から生成する。operation 全体の説明、副作用保証、result の読み方は operation metadata に置く。operation 実装や CLI query builder は JSON 境界に入る前に typed contract を扱う。`JsonElement` は IPC codec、schema validation、低レベル deserialize 境界に限定する。

`argsSchema` と `resultSchema` は検証用の派生成果物であり、agent 向けの主契約ではない。agent は `description` / `inputs[].constraints` / `resultContract` / `assurance` を使って operation を選び、`args` を組み立てる。`argsSchema` は組み立て後の JSON 構造検証に使う。schema には説明文や意味制約を置かない。

`result` は operation 固有の主データだけを表す。`phase`、`applied`、`changed`、`touched`、`errors` は execute envelope 側の契約であり、operation result 型へ含めない。

1つの operation は1つのユーザー意図だけを表す。`description` が「A または B」を説明する形になる operation は分割対象とする。`globalObjectId` / `sceneHierarchy` / `prefabHierarchy` のような差は operation variant ではなく、`inputs[].variants[]` に置く reference / input の表現方法として扱う。

### 実行フェーズ
各opは内部的に次の3フェーズを持つ。
- **Validate**：引数・型・存在・許可プロファイル等の検証（副作用なし）
- **Plan**：対象解決（GlobalObjectId化）、差分見積り、影響範囲（touch）算出（保存しない）
- **Call**：Unity Editor APIで変更し、必要に応じて保存

`call` はリクエスト単位で `Validate/Plan` 完了後に `planToken` を検証し、検証成功時のみ `Call` を実行する2パス方式とする。

### 登録方式
Unity起動時に、Editorアセンブリ内のop実装を走査して **オペレーションレジストリ** に登録する。
- op実装クラスに `[UcliOperation]` 属性を付与
- `IUcliOperation` を実装する。新規 operation は `UcliOperation<TArgs,TResult>` を優先し、phase 本体が typed Args を受け取る形にする
- Args property または semantic value type には `UcliDescriptionAttribute` と必要な `UcliInputConstraintAttribute` を付与し、`inputs[]` の説明と意味制約を Args contract から生成する
- 起動時に列挙し、`OperationRegistry` に追加
- これにより `ucli ops list/describe` が動的にopを列挙できる

### JSON リクエスト入力 DSL
`kind: "op"` / `kind: "edit"` の使い分け、`on` / `select` / `actions` / `as` / `commit` の仕様、入力サンプルは [json-request-spec.md](json-request-spec.md) を参照する。  
利用可能な primitive operation の一覧と概況は [ops-catalog.md](ops-catalog.md) を参照する。

## コマンド
Unityプロジェクトを対象に実行するコマンドは、CWDがUnityプロジェクトと判定可能な場合はそれを使う。そうでない場合は `--projectPath` を指定する。

- `ucli init`：設定雛形を作成する
  - 生成先：Git repository root 直下（`<repoRoot>/.ucli`）
  - 生成対象：`.ucli/config.json`, `.ucli/.gitignore`
  - `--force`：既存設定を上書き
- `ucli validate`：JSONリクエストの静的検証（snapshot lint）
  - 保証範囲：JSON/DSL の構文、必須項目、`edit` の structural lowering、readIndex snapshot に基づく許可 op・args schema 検証
  - 対象実在確認、selector 解決、差分見積り、実行可否判定は行わない
  - Unity実体への接続やフォールバックは行わず、ローカル readIndex のみを参照する
  - `--requestPath <path>`：JSONリクエストファイルを指定する。未指定時は `stdin` を読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
    - `disabled`：構文・DSL・lowering のみ検証する
    - `allowStale`：snapshot があれば `fresh/probable/stale` を使い、無ければ syntax-only に縮退する
    - `requireFresh`：fresh な snapshot を必須とし、欠落・破損・非 fresh で失敗する
- `ucli plan`：対象解決・差分見積り（実変更なし、または最小）を返す
  - `stdin` または `--requestPath <path>` から JSON リクエストを読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：static preflight で使う readIndex 利用モードを上書きする
    - `disabled`：syntax-only preflight へ縮退し、`payload.readIndex.used=false` と `fallbackReason="readIndex disabled by mode."` を返す
    - `allowStale`：snapshot 欠落時は syntax-only preflight へ縮退して Unity IPC `plan` を継続する
    - `requireFresh`：snapshot 欠落・破損・非 fresh では Unity IPC 前に失敗する
  - `--failFast`：Unity readiness wait を行わず即時失敗する
  - 成功時 payload は `requestId`、`opResults`、`readIndex`、`planToken` を返す
  - parse / project 解決より前で失敗した場合は空 payload、preflight 以降の失敗では `requestId` と `readIndex` を残し、`planToken` は省略する
- `ucli call`：Unityへリクエストを送って実行し、保存する
  - `stdin` または `--requestPath <path>` から JSON リクエストを読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `--planToken <token>`：`plan` が返したトークンを指定する
  - `--withPlan`：CLI が事前に `plan` を 1 回実行し、その結果を `payload.plan` に同梱する
  - `dangerous` opを含む場合は `--allowDangerous` 必須
  - `--failFast`：Unity readiness wait を行わず即時失敗する
  - `call` は readIndex に依存せず、Unity実体で再解決・再検証して実行する
  - `--readIndexMode` は受け付けない
  - 成功時 payload は `requestId`、`opResults`、必要時のみ `readPostcondition`、必要時のみ `plan` を返す
  - `payload.readPostcondition` は mutation により stale 化した read surface の safe 条件を返す
  - `payload.plan` は `requestId`、`opResults`、必要時のみ `planToken` を返し、`readPostcondition` は含めない
  - parse / project 解決より前で失敗した場合は空 payload、preflight 以降の失敗では `requestId` を残し、`opResults` は `[]` を返す
- `ucli resolve`：selector 1 件を GlobalObjectId へ解決する
  - JSON request、`stdin`、`--requestPath` は受け付けない
  - selector は `--globalObjectId` / `--assetGuid` / `--assetPath` / `--projectAssetPath` / `--scene --hierarchyPath [--componentType]` / `--prefab --hierarchyPath` の exactly one とする
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity fallback 時の実行モード指定（既定 `auto`）
  - `--timeout <int>`：Unity fallback 時の IPC 待機タイムアウト（ミリ秒）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
  - `--failFast`：Unity fallback 時の readiness wait を行わず即時失敗する
  - `--scene --hierarchyPath` かつ `--componentType` なしの場合、scene-tree-lite readIndex で解決できれば Unity IPC へ接続しない
  - readIndex で解決できない selector は Unity IPC `execute(command=resolve)` へ fallback する
  - 成功時 payload は `requestId`、`opResults`、`readIndex` を返し、解決結果は `opResults[0].result.globalObjectId` に置く
- `ucli query`：検索・構造取得・スキーマ取得（型付きサブコマンド）
  - `assets find`：`ucli.assets.find` を実行する
    - `--type <string?>` / `--pathPrefix <string?>` / `--nameContains <string?>` の 1 つ以上を指定する
  - `scene tree`：`ucli.scene.tree` を実行する
    - `--path <string>` を必須とし、既定 depth は `1`
  - `go describe`：`ucli.go.describe` を実行する
    - `--globalObjectId <id>` または `--scene <path> --hierarchyPath <path>` または `--prefab <path> --hierarchyPath <path>` を exactly one とし、既定 depth は `0`
  - `comp schema`：`ucli.comp.schema` を実行する
    - `--type <string>` を必須とする
  - `asset schema`：`ucli.asset.schema` を実行する
    - `--type <string>` / `--globalObjectId <id>` / `--assetGuid <guid>` / `--assetPath <path>` / `--projectAssetPath <path>` の exactly one とする
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity fallback または Unity 専用 query の実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
  - `--failFast`：Unity fallback または Unity 専用 query の readiness wait を行わず即時失敗する
  - 一覧系の `assets find` と `scene tree` は既定で shallow summary と deterministic order を返す
  - 一覧系は `--limit <int>`（既定 `100`、最大 `10000`）、`--after <cursor>`、`--all` を受け付ける
  - `ucli query` が一覧結果を束ねる場合の bounded window は command/query layer 側で適用し、primitive op 自体は limit / cursor を持たない
  - `--all` は `--limit` / `--after` と同時指定できない
  - `--fullDepth` と `--depth` は同時指定できず、全件列挙や深い展開は opt-in とする
  - `assets find` と `scene tree` は readIndex lookup を優先し、必要時だけ live Unity source へ fallback する
  - `go describe` / `comp schema` / `asset schema` は Unity IPC `execute(command=query)` へ委譲し、`Validate -> Plan` を実行する
  - 成功時 payload は `requestId`、`opResults`、`readIndex` を返す
  - `assets.find` の live query は `Assets/` 配下の persistent main asset を正本として検索し、readIndex 版は同じ契約へ追従する
- `ucli refresh`：AssetDatabase更新、インポート、コンパイル等でプロジェクトを最新化する
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `stdin` / `--requestPath` / `--planToken` / `--withPlan` / `--readIndexMode` は受け取らない
  - CLI内部で `ucli.project.refresh` 1件のみを含む固定 `execute` リクエストを生成する
  - 出力は共通の `CommandResult` エンベロープを返す
  - 成功時 payload は `requestId`、`opResults`、必要時のみ `readPostcondition` を返す
- `ucli ops`：利用可能なオペレーション一覧・詳細
  - `list`：利用可能なオペレーション一覧
- `describe <opName>`：特定オペレーションの agent 向け contract と検証用 schema
  - `--mode <auto|daemon|oneshot>`：live source fallback が必要な場合の実行モード指定
  - `--timeout <int>`：live source fallback が必要な場合の IPC 待機タイムアウト
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
  - `--failFast`：live source fallback 時の Unity readiness wait を行わず即時失敗する
  - `mode` / `timeout` は readIndex hit 時も妥当性を検証し、不正値は `INVALID_ARGUMENT` とする
  - readIndex hit では `--failFast` の有無にかかわらず Unity 接続しない
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
  - 既定で bounded 取得とし、cursor / 時刻 / tail による段階取得を前提とする
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
  - `scene.tree` 軽量版索引
- 非対象：`call`
  - `call` は readIndex を参照せず、Unity実体で再解決・再検証して実行する
  - mutation の正本は常に live Unity であり、readIndex は観測補助に限定する

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
- `query` / `resolve` の一覧系は readIndex 利用時も bounded-by-default とし、deterministic order を崩さない
- mutation 後に matching `readPostcondition` がある surface は、`generatedAtUtc` が requirement を満たす readIndex または live Unity 再観測のみを safe とみなす

### ディレクトリ構造
```text
<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/index/
  catalogs/
    ops.catalog.json
    types.catalog.json
    schemas.catalog.json
  lookups/
    asset-search.lookup.json
    guid-path.lookup.json
    scene-tree-lite/
      <sceneKey>.lookup.json
  inputs/
    manifest.json
```

### 無効化（stale）ルール
- `ops/types/schema`
  - `Library/ScriptAssemblies`
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`
  - `.asmdef/.asmref`
- `asset-search.lookup`
  - 上記 `ops/types/schema` の全入力
  - `Assets/` 配下の asset 本体と `.meta` の内容ハッシュ
- `guid-path.lookup`
  - `Assets/` 配下の asset 本体と `.meta` の内容ハッシュ
- `scene-tree-lite`
  - 対象 Scene (`Assets/**/*.unity`) 本体と同名 `.meta` の内容ハッシュ
  - Scene ごとに `<sceneKey>.lookup.json` を個別再生成する
  - source fallback は persisted scene asset を preview scene として読み直し、loaded scene や request-local temporary scene は観測しない
  - `--mode auto` は daemon 到達可能時に daemon を優先するが、daemon / oneshot のどちらでも persisted preview を読むため scene 意味論は同一
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
- `session.json`、daemon log、diagnosis、artifact は reload-safe なファイルとして扱う
  - 長寿命 file handle を前提にせず、reopen-safe または temp-then-rename 系の書き込みを採る

### 識別
- `repoRoot = realpath(Git repository root from CWD or --projectPath)`
- `projectFingerprint = SHA256(normalize(repoRoot) + "\n" + normalize(relativeUnityProjectPathFromRepoRoot))`
  - 同一リポジトリ配下に複数Unityプロジェクトが存在しても衝突しない識別子とする

### projectFingerprint 単位の排他
- 同一 `projectFingerprint` を対象にする writer は、runtime や transport にかかわらず同じ排他モデルに参加する
- `--mode auto` であっても、別 writer が存在する場合に暗黙に別 writer を増やして回避しない

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
- `starting` / `busy` / `compiling` の間、readiness gate を持つ実行系コマンドは既定で待機する
- `domainReloading` / `blockedByModal` / `safeMode` / `shuttingDown` の間、実行系コマンドはライフサイクル専用エラーで即時拒否する
- `status` / `daemon status` / `logs` / `daemon stop` はライフサイクル状態に関わらず利用可能とする
- ドメインリロード後は再初期化を経て `starting` から再評価し、最終的に `ready` または他のブロック状態へ遷移する
- idle 時の runtime は inert を原則とし、background work は明示コマンドまたは厳密な TTL に限定する
