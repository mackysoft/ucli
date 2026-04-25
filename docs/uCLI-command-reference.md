> [!IMPORTANT]
> この文書は、uCLI のコマンド一覧、option table、サブコマンド規則、終了コード、実行例のリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、JSON プロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。
>
> 現在の公開 CLI host が登録している top-level command は `init`、`status`、`refresh`、`resolve`、`validate`、`plan`、`call`、`daemon`、`logs`、`ops`、`test` である。
> `query` は、この文書では内部 execute 契約の設計メモとしてのみ扱い、現行 CLI では利用できない。

## コマンド概要

| Command | 概要 | 備考 |
| --- | --- | --- |
| `ucli init` | `.ucli` の設定雛形を生成する | Git repository root を優先する |
| `ucli refresh` | プロジェクト更新を独立コマンドとして実行する | 固定の `ucli.project.refresh` を実行する |
| `ucli resolve` | selector 1 件を GlobalObjectId へ解決する | scene-tree-lite index を優先し、必要時だけ Unity IPC へ fallback する |
| `ucli validate` | JSON リクエストを静的に lint する | Unity へ接続せず readIndex snapshot を参照する |
| `ucli plan` | JSON リクエストの plan フェーズを実行する | static preflight 後に Unity IPC `plan` を実行する |
| `ucli call` | JSON リクエストの call フェーズを実行する | static preflight 後に Unity IPC `call` を実行する |
| `ucli ops` | primitive operation の一覧・詳細を返す | `list` / `describe` を持つ |
| `ucli status` | daemon と lifecycle の状態を返す | `ProjectVersion.txt` 由来の `unityVersion` を返す |
| `ucli logs` | Unity / daemon のログを取得する | 成功時はイベントストリームを返す |
| `ucli daemon` | daemon の起動・停止・掃除・状態取得を行う | `start` / `stop` / `cleanup` / `status` / `list` |
| `ucli test` | Unity Test Framework 実行と結果正規化を扱う | `run` / `profile init` |

## 公開コマンド
- `ucli ops`
  - `list` は利用可能なオペレーション一覧を返す。
  - `describe <opName>` は特定オペレーションの引数スキーマを返す。
  - `--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - `--failFast` は live source fallback に対してのみ適用し、readIndex hit では Unity 接続も readiness wait も行わない。
  - `mode` / `timeout` は readIndex hit 時も妥当性を検証し、不正値は `INVALID_ARGUMENT` を返す。
- `ucli status`
  - daemon と lifecycle の状態を JSON で返す。
  - `--timeout <int>` で daemon 状態確認タイムアウトを上書きする。
- `ucli validate`
  - `stdin` または `--requestPath` から JSON リクエストを読み、snapshot lint を返す。
  - `--projectPath <string?>` と `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--mode` / `--timeout` は受け付けず、Unity IPC に接続しない。
  - 成功時 payload は `readIndex` のみを返す。
  - `allowStale` では snapshot 欠落時に syntax-only へ縮退し、`requireFresh` では `READ_INDEX_BOOTSTRAP_FAILED` / `READ_INDEX_FORMAT_INVALID` / `READ_INDEX_FRESH_REQUIRED` を返す。
- `ucli resolve`
  - selector flags から 1 件だけ解決し、JSON request、`stdin`、`--requestPath` は受け付けない。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - selector は `--globalObjectId` / `--assetGuid` / `--assetPath` / `--projectAssetPath` / `--scene --hierarchyPath [--componentType]` / `--prefab --hierarchyPath` の exactly one とする。
  - 成功時 payload は `requestId`、`opResults`、`readIndex` を返す。
- `ucli plan`
  - `stdin` または `--requestPath` から JSON リクエストを読み、static preflight 後に Unity IPC `plan` を実行する。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - 成功時 payload は `requestId`、`opResults`、`readIndex`、`planToken` を返す。
  - `allowStale` では snapshot 欠落時に syntax-only へ縮退して継続し、`requireFresh` では snapshot 欠落・破損・非 fresh で失敗する。
- `ucli call`
  - `stdin` または `--requestPath` から JSON リクエストを読み、static preflight 後に Unity IPC `call` を実行する。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--planToken <string?>`、`--withPlan`、`--allowDangerous`、`--failFast` を受け付ける。
  - `--readIndexMode` は受け付けない。
  - 成功時 payload は `requestId`、`opResults`、必要時のみ `plan` を返す。

## 実行系コマンド共通規則

### 出力契約の参照先
- 公開 CLI 出力の種別、`CommandResult`、内部 IPC との関係は [uCLI.md](uCLI.md) を正本とする。
- 共通フィールドの shape は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `--failFast`
- `--failFast` は CLI option のみであり、JSON request や `.ucli/config.json` には入れない。
- readiness gate を持つ実行系コマンドは、既定で `starting`, `busy`, `compiling` の解消を待ってから実行する。
- `domainReloading` は AppDomain reload を跨いで要求を再開しないため、既定でも待機せず `EDITOR_DOMAIN_RELOADING` を返す。
- `--failFast` 指定時だけ `lifecycleState != ready` を即時エラーとして返す。
- `blockedByModal`, `safeMode`, `playmode`, `shuttingDown` は待機中でも即時失敗する。
- current batchmode daemon が実際に観測・返却する非 ready 状態は `starting`, `busy`, `compiling`, `domainReloading`, `playmode`, `shuttingDown`。`blockedByModal` と `safeMode` は reserved literal だが batchmode ではまだ返さない。
- 待機は既存の `--timeout` budget を消費し、budget を使い切った場合は `IPC_TIMEOUT` を返す。
- `ucli ops list` / `ucli ops describe` では live source fallback に対してのみ意味を持つ。readIndex hit では readiness wait を行わない。
- `ucli test run` では daemon-backed execution に対してのみ意味を持つ。`oneshot` と `auto -> oneshot` は従来どおり direct `-runTests` を使い、readiness wait を行わない。

### 共通エラー契約
- lifecycle 専用エラー
  - `EDITOR_STARTING`
  - `EDITOR_BUSY`
  - `EDITOR_COMPILING`
  - `EDITOR_DOMAIN_RELOADING`
  - `EDITOR_PLAYMODE`
  - `EDITOR_MODAL_BLOCKED`
  - `EDITOR_SAFE_MODE`
  - `EDITOR_SHUTTING_DOWN`
- timeout
  - 既定待機の timeout も既存の `IPC_TIMEOUT` を使用する。

## `ucli daemon`
デーモンの起動、停止、安全な残骸掃除、状態確認、登録一覧取得を行う管理コマンド。

### `daemon` 共通 options（`start` / `stop` / `cleanup` / `status` / `list`）
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |

### `daemon` 出力契約（共通）
- `ucli daemon` は共通エンベロープを返す。
- `command` はサブコマンドごとに次を返す。
  - `ucli daemon start`：`daemon.start`
  - `ucli daemon stop`：`daemon.stop`
  - `ucli daemon cleanup`：`daemon.cleanup`
  - `ucli daemon status`：`daemon.status`
  - `ucli daemon list`：`daemon.list`
- 成功時 `payload` は `timeoutMilliseconds` を常に含む。
- `sessionToken` はレスポンスに含めない。
- `payload` のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `daemon` のエラー契約
- `INVALID_ARGUMENT`
  - `--timeout` が空文字、空白、非数値、`0` 以下
  - `--projectPath` が Unity プロジェクトとして解決不能
  - `ucli daemon list` の対象 project が Git worktree 配下にない
- `IPC_TIMEOUT`
  - `ucli daemon cleanup` が project lifecycle lock 待機中、または安全判定用 probe 開始前に timeout budget を使い切った
  - `daemon list` 全体の共有 timeout budget が Git worktree 列挙の完了前に尽きた
  - item 単位の probe が共有 budget 消費前に individual timeout として失敗した場合は、コマンド全体は成功のまま `items[*].reason = probeTimeout` を返す
  - Git worktree 列挙完了後に共有 timeout budget が尽きた場合は、コマンド全体は成功のまま `isComplete = false` と `completionReason = timeout` を返す
- `INTERNAL_ERROR`
  - Git worktree 列挙失敗、起動失敗、停止失敗、cleanup 対象 artifact の削除失敗などの内部エラー
  - `daemon list` の item 単位の invalid session、stale session、unexpected probe failure はコマンド全体の失敗にはせず、`items[*].state` / `items[*].reason` に反映する

### `daemon` 実行例
```bash
ucli daemon start --projectPath ./UnityProject
ucli daemon stop --projectPath ./UnityProject --timeout 5000
ucli daemon cleanup --projectPath ./UnityProject
ucli daemon status --projectPath ./UnityProject
ucli daemon list --projectPath ./UnityProject
```

## `ucli logs`
ログ取得コマンド。`ucli logs unity` と `ucli logs daemon` を提供する。

### `logs` 出力契約
- `ucli logs` の成功時は共通エンベロープを返さず、`stdout` にログイベントを逐次出力する。
- `--format json` は NDJSON とし、1イベントを1行のJSONオブジェクトで出力する。
- `--format text` は1イベントを1行のテキストで出力する。
- `--stream` 未指定時は取得条件に一致する範囲を出力して終了する。
- `--stream` 指定時は終了条件（`Ctrl+C`、`--idleTimeoutMilliseconds` 到達、`--until` 到達）まで継続出力する。
- 入力検証エラーなど、ストリーム開始前に失敗した場合は共通エンベロープの `status=error` を1件返して終了する。

### `logs` 共通 options（`unity` / `daemon`）
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

### `logs unity` options
| Option | Short | Description |
| --- | --- | --- |
| `--source <string?>` | - | `compile` / `runtime` / `all` |
| `--stackTrace <string?>` | - | `none` / `error` / `all` |
| `--stackTraceMaxFrames <int?>` | - | スタックトレースの最大フレーム数（`1..512`） |
| `--stackTraceMaxChars <int?>` | - | スタックトレースの最大文字数（`256..131072`） |

### `logs daemon` options
| Option | Short | Description |
| --- | --- | --- |
| `--category <string?>` | - | `lifecycle` / `ipc` / `auth` / `transport` / `health` / `all` |

### `logs` オプション規則
- `--after` と `--since` を同時指定した場合は `--after` を優先する。
- `--since` は `2026-03-05T10:30:00+09:00` や `2026-03-05T01:30:00Z` の形式を受け付ける。
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

### `logs` 実行例
```bash
ucli logs unity --projectPath ./UnityProject --tail 200 --level error --source runtime
ucli logs unity --projectPath ./UnityProject --after "<cursor>" --stream --format text
ucli logs daemon --projectPath ./UnityProject --since "2026-03-05T00:00:00+09:00" --format json
ucli logs daemon --projectPath ./UnityProject --stream --pollIntervalMilliseconds 500 --idleTimeoutMilliseconds 60000 --category ipc
ucli logs unity --projectPath ./UnityProject --since "2026-03-05T09:00:00+09:00" --until "2026-03-05T10:00:00+09:00"
```

## `ucli init`
`ucli init` は Git repository root を対象として設定雛形を生成する。  
Git root が判定できない環境では実行時 CWD を対象にする。

`ucli init` は必須初期化ではない。`config.json` が無い場合も通常コマンドは既定値で動作する。  
生成対象は `.ucli/config.json` と `.ucli/.gitignore`。

### `init` options
| Option | Short | Description |
| --- | --- | --- |
| `--force` | - | 既存の `.ucli/config.json` と `.ucli/.gitignore` を上書きする |

### `init` のエラー契約
- `INVALID_ARGUMENT`
  - 既存テンプレートファイルがあり `--force` 未指定
- `INTERNAL_ERROR`

### `init` の出力
- `payload` のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

## `ucli resolve`
`ucli resolve` は selector 1 件を解決する読取コマンドである。
CLI は JSON request を読まず、selector flags から固定の `ucli.resolve` 1 step を組み立てる。

### `resolve` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--readIndexMode <string?>` | - | `disabled`, `allowStale`, or `requireFresh` |
| `--failFast` | - | Unity fallback 時に `ready` になる前なら待機せず即失敗する |
| `--globalObjectId <string?>` | - | GlobalObjectId selector |
| `--assetGuid <string?>` | - | asset GUID selector |
| `--assetPath <string?>` | - | Unity asset path selector |
| `--projectAssetPath <string?>` | - | project-relative asset path selector |
| `--scene <string?>` | - | scene hierarchy selector の scene path |
| `--hierarchyPath <string?>` | - | scene / prefab 内の GameObject hierarchy path |
| `--componentType <string?>` | - | scene hierarchy selector の component type |
| `--prefab <string?>` | - | prefab hierarchy selector の prefab path |

### `resolve` 実行契約
- selector は exactly one とし、`stdin` と `--requestPath` は受け付けない。
- `--scene --hierarchyPath` かつ `--componentType` なしの場合、scene-tree-lite readIndex で解決できれば Unity IPC へ接続しない。
- `--globalObjectId`、asset 系 selector、`--componentType` 付き scene selector、prefab selector、readIndex miss は Unity IPC `execute(command=resolve)` へ fallback する。
- Unity fallback は `Validate -> Plan` を実行し、`planToken` は発行しない。
- `--failFast` は Unity fallback 時だけ適用する。readIndex hit では Unity readiness wait を行わない。

### `resolve` のレスポンス契約
- 成功時 payload は `requestId`、`opResults`、`readIndex` を返す。
- `opResults[0].opId` は `resolve`、`op` は `ucli.resolve`、`phase` は `plan`、`applied=false`、`changed=false` とする。
- 解決結果は `opResults[0].result.globalObjectId` に置く。
- readIndex 完結時は `payload.readIndex.source=index`、`used=true` を返す。
- Unity fallback 時は `payload.readIndex.source=unity`、`used=false`、`fallbackReason` に fallback 理由を返す。

### `resolve` 実行例
```bash
ucli resolve --projectPath ./UnityProject --scene Assets/Scenes/Main.unity --hierarchyPath Root/Child
ucli resolve --projectPath ./UnityProject --assetGuid 11111111111111111111111111111111 --mode daemon --failFast
ucli resolve --projectPath ./UnityProject --prefab Assets/Prefabs/Card.prefab --hierarchyPath Root/Label
```

## `ucli plan`
`ucli plan` は最初の公開 request-driven execute コマンドである。  
CLI は JSON リクエストを `stdin` または `--requestPath` から読み、static preflight を行ったうえで Unity IPC `execute(command=plan)` を 1 回だけ送る。

### `plan` options
| Option | Short | Description |
| --- | --- | --- |
| `--requestPath <string?>` | - | JSON リクエストファイルの path。未指定時は `stdin` を読む |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--readIndexMode <string?>` | - | `disabled`, `allowStale`, or `requireFresh` |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `plan` 実行契約
- `stdin` と `--requestPath` は同時指定できない。
- `payload.readIndex` は Unity 実行経路ではなく、Unity IPC `plan` 実行前の static preflight で readIndex をどう再利用したかを表す。
- `--readIndexMode=disabled` は validate と同じ syntax-only preflight に縮退し、`payload.readIndex` は `used=false`、`hit=false`、`source=index`、`freshness=probable`、`fallbackReason="readIndex disabled by mode."` を返す。
- `--readIndexMode=allowStale` は snapshot 欠落時に syntax-only preflight へ縮退し、Unity IPC `plan` を継続する。
- `--readIndexMode=requireFresh` は snapshot 欠落・破損・非 fresh なら Unity IPC 前に失敗する。
- Unity へ送る execute request は `command=plan` とし、`IpcExecuteRequest.FailFast` に `--failFast` をそのまま写像する。request 側の `planToken` は常に送らない。
- `--mode` / `--timeout` が不正な場合でも、request parse と static preflight が完了していれば失敗 payload に `requestId` と `readIndex` を残す。

### `plan` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- 成功時 payload は `requestId`、`opResults`、`readIndex`、`planToken` を返す。
- request parse / project 解決より前で失敗した場合は空 payload を返す。
- preflight 以降で失敗した場合は `payload.requestId` と `payload.readIndex` を返し、`payload.opResults` は `[]` または Unity 応答の部分結果を返す。
- 失敗時は `planToken` field 自体を省略する。

### `plan` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | 成功 |
| `3` | 入力不正、static validation failure |
| `4` | IPC timeout、lifecycle failure、daemon/tool/internal failure |

### `plan` 実行例
```bash
ucli plan --projectPath ./UnityProject --readIndexMode allowStale < request.json
ucli plan --requestPath ./request.json --projectPath ./UnityProject --mode daemon --failFast
```

## `ucli call`
`ucli call` は request-driven execute コマンドであり、CLI は JSON リクエストを `stdin` または `--requestPath` から読み、static preflight と dangerous operation 判定を行ったうえで Unity IPC `execute(command=call)` を送る。

### `call` options
| Option | Short | Description |
| --- | --- | --- |
| `--requestPath <string?>` | - | JSON リクエストファイルの path。未指定時は `stdin` を読む |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--planToken <string?>` | - | 既存 `plan` 実行で取得した plan token |
| `--withPlan` | - | `call` 前に CLI が `plan` を実行し、その結果を `payload.plan` に同梱する |
| `--allowDangerous` | - | `dangerous` operation の実行を明示許可する |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `call` 実行契約
- `stdin` と `--requestPath` は同時指定できない。
- `--readIndexMode` は受け付けない。
- `dangerous` operation は、設定上許可されていても `--allowDangerous` が無ければ `OPERATION_NOT_ALLOWED` で失敗する。
- `kind:"edit"` step の dangerous 判定は、public DSL を lower した primitive operation 群に対して行う。
- `--withPlan` 指定時、CLI は Unity IPC `execute(command=plan)` を先に 1 回送り、その結果を `payload.plan` に保持する。
- `--planToken` 未指定で `--withPlan` が plan token を発行した場合、CLI はその token を後続の `call` request に転送する。
- `--planToken` 指定時はユーザー指定値を優先し、`payload.plan` は表示用としてのみ保持する。
- `call` 全体の timeout budget は 1 本であり、`--withPlan` 時は pre-plan と call で残り時間を順に消費する。

### `call` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- 成功時 payload は `requestId`、`opResults`、必要時のみ `plan` を返す。
- `payload.plan` は `requestId`、`opResults`、必要時のみ `planToken` を返す。
- `payload.readIndex` は返さない。
- request parse / project 解決より前で失敗した場合は空 payload を返す。
- preflight 以降で失敗した場合は `payload.requestId` を返し、`payload.opResults` は `[]` または Unity 応答の部分結果を返す。
- `--withPlan` の pre-plan が成功済みで、その後の `call` が失敗した場合でも `payload.plan` は保持する。

### `call` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | 成功 |
| `3` | 入力不正、static validation failure、dangerous operation guard failure |
| `4` | IPC timeout、lifecycle failure、daemon/tool/internal failure |

### `call` 実行例
```bash
ucli call --projectPath ./UnityProject --planToken "<token>" < request.json
ucli call --requestPath ./request.json --projectPath ./UnityProject --withPlan --allowDangerous --mode daemon --failFast
```

## `ucli refresh`
`ucli refresh` は独立コマンドであり、未公開の request 系 CLI surface の別名ではない。  
CLI は内部で固定の標準 `execute` リクエストを組み立て、Unity 側の既存 `ucli.project.refresh` 実装へ流す。

### `refresh` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `refresh` 実行契約
- `stdin` は読まない。
- `--requestPath` / `--planToken` / `--withPlan` / `--readIndexMode` は指定できない。
- 既定では `starting`, `busy`, `compiling` のみ待機し、`domainReloading` は即時失敗する。
- `--failFast` 指定時は wait 対象状態も fail-fast で返す。
- `requestId` は CLI が実行ごとに UUID を生成する。
- 実行対象は次の1件で固定する。

```json
{
  "protocolVersion": 1,
  "requestId": "<generated-uuid>",
  "steps": [
    {
      "kind": "op",
      "id": "refresh",
      "op": "ucli.project.refresh",
      "args": {}
    }
  ]
}
```

### `refresh` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- `planToken` は返さない。
- `payload` のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `refresh` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | 成功 |
| `3` | 入力不正、静的検証失敗 |
| `4` | IPC timeout、daemon未起動、内部障害、Unity側ツール失敗 |

### `refresh` 実行例
```bash
ucli refresh --projectPath ./UnityProject
ucli refresh --projectPath ./UnityProject --mode daemon --timeout 120000
```

## `ucli test`

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
| `--testPlatform <string?>` | - | `editmode`, `playmode`, or Unity `BuildTarget` literal |
| `--testFilter <string?>` | `-f` | Test name filter pattern |
| `--testCategory <string?>` | - | Comma-separated test categories |
| `--assemblyName <string?>` | `-a` | Comma-separated assembly names |
| `--testSettingsPath <string?>` | `-s` | Path to `TestSettings.json` |
| `--timeout <int?>` | - | タイムアウト（ミリ秒）。`1..2147483647` |
| `--failFast` | - | daemon-backed execution で `ready` 待機を行わず即失敗する |

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

#### `run` の出力
- `payload` のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。
- `--mode daemon` の `DAEMON_NOT_RUNNING` と `--mode oneshot` の `DAEMON_RUNNING_ONESHOT_FORBIDDEN` は `toolError` を返す。
- daemon IPC timeout と既定の readiness 待機 timeout は `IPC_TIMEOUT` を返す。
- `oneshot` 実行中の Unity process timeout は `UNITY_TEST_EXECUTION_TIMEOUT` を返す。
- `--failFast` は daemon-backed execution の opt-out であり、`oneshot` と `auto -> oneshot` では挙動を変えない。

### `ucli test profile init`
`test` 実行用のプロファイル JSON 雛形を作成する。

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
  "testFilter": null,
  "testCategories": [],
  "assemblyNames": [],
  "testSettingsPath": null,
  "timeout": 1800000
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
3. 既定の検索ルートで、解決済み `unityVersion` に一致する Editor を探索

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
