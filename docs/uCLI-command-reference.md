> [!IMPORTANT]
> この文書は、uCLI のコマンド一覧、option table、サブコマンド規則、終了コード、実行例のリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、JSON プロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。

## コマンド概要

| Command | 概要 | 備考 |
| --- | --- | --- |
| `ucli init` | `.ucli` の設定雛形を生成する | Git repository root を優先する |
| `ucli validate` | JSON リクエストの静的検証を行う | ローカル静的検証のみ |
| `ucli plan` | 対象解決と差分見積りを返す | 公開 payload では `planToken` を返す |
| `ucli call` | Unity へリクエストを送って適用・保存する | 実行前に `plan` 相当の検証を挟む |
| `ucli resolve` | セレクタを durable identity へ解決する | `readIndex` を利用可能 |
| `ucli query` | 検索・構造取得・スキーマ取得を行う | `readIndex` を利用可能 |
| `ucli refresh` | プロジェクト更新を独立コマンドとして実行する | 固定の `ucli.project.refresh` を実行する |
| `ucli ops` | primitive operation の一覧・詳細を返す | `list` / `describe` を持つ |
| `ucli status` | daemon と lifecycle の状態を返す | `ProjectVersion.txt` 由来の `unityVersion` を返す |
| `ucli logs` | Unity / daemon のログを取得する | 成功時はイベントストリームを返す |
| `ucli daemon` | daemon の起動・停止・掃除・状態取得を行う | `start` / `stop` / `cleanup` / `status` / `list` |
| `ucli test` | Unity Test Framework 実行と結果正規化を扱う | `run` / `profile init` |

## 基本コマンド
- `ucli validate`
  - JSON リクエストの静的検証を行う。
  - 保証範囲は形式、スキーマ、許可判定までで、実在確認や差分見積りは含まない。
  - Unity 実体への接続や解決は行わない。
  - `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
- `ucli plan`
  - 対象解決と差分見積りを返す。
  - 公開 payload では `planToken` を返す。
  - `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--waitUntilReady` を受け付ける。
- `ucli call`
  - Unity へリクエストを送って実行し、保存する。
  - `--planToken <token>` を受け付ける。
  - `dangerous` op を含む場合は `--allowDangerous` を必須とする。
  - `--withPlan` は call レスポンスに plan 相当を同梱する任意オプションとする。
  - `--waitUntilReady` を受け付ける。
- `ucli resolve`
  - セレクタを GlobalObjectId などへ解決する。
  - `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--waitUntilReady` を受け付ける。
- `ucli query`
  - 検索、構造取得、スキーマ取得を行う。
  - `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--waitUntilReady` を受け付ける。
- `ucli ops`
  - `list` は利用可能なオペレーション一覧を返す。
  - `describe <opName>` は特定オペレーションの引数スキーマを返す。
  - `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--waitUntilReady` を受け付けるが、適用されるのは live source fallback が必要な場合だけとする。
- `ucli status`
  - daemon と lifecycle の状態を JSON で返す。
  - `--timeout <int>` で daemon 状態確認タイムアウトを上書きする。

## 実行系コマンド共通規則

### 出力契約の参照先
- 公開 CLI 出力の種別、`CommandResult`、内部 IPC との関係は [uCLI.md](uCLI.md) を正本とする。
- 共通フィールドの shape は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `--waitUntilReady`
- `--waitUntilReady` は CLI option のみであり、JSON request や `.ucli/config.json` には入れない。
- 既定動作は fail-fast とし、未指定時は `lifecycleState != ready` を即時エラーとして返す。
- 指定時だけ `starting`, `busy`, `compiling`, `domainReloading` を待機対象とする。
- `blockedByModal`, `safeMode`, `playmode`, `shuttingDown` は待機中でも即時失敗する。
- 待機は既存の `--timeout` budget を消費し、budget を使い切った場合は `IPC_TIMEOUT` を返す。
- `ops list` / `ops describe` は readIndex hit の場合は待機を行わず、live source fallback 時だけこの契約を適用する。

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
  - `--waitUntilReady` による待機 timeout も既存の `IPC_TIMEOUT` を使用する。

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

## `ucli refresh`
`ucli refresh` は独立コマンドであり、`ucli call` の別名ではない。  
CLI は内部で固定の標準 `call` `execute` リクエストを組み立て、Unity 側の既存 `ucli.project.refresh` 実装へ流す。

### `refresh` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--waitUntilReady` | - | `ready` になるまで待機してから実行する |

### `refresh` 実行契約
- `stdin` は読まない。
- `--requestPath` / `--planToken` / `--withPlan` / `--readIndexMode` は指定できない。
- `--waitUntilReady` 未指定時は fail-fast、指定時は wait 対象状態だけ待機する。
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
| `--testPlatform <string?>` | - | `editmode` or `playmode` |
| `--buildTarget <string?>` | `-t` | Build target used when `testPlatform=playmode` |
| `--testFilter <string?>` | `-f` | Test name filter pattern |
| `--testCategory <string?>` | - | Comma-separated test categories |
| `--assemblyName <string?>` | `-a` | Comma-separated assembly names |
| `--testSettingsPath <string?>` | `-s` | Path to `TestSettings.json` |
| `--timeout <int?>` | - | タイムアウト（ミリ秒）。`1..2147483647` |
| `--waitUntilReady` | - | `ready` になるまで待機してから実行する |

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
  "buildTarget": null,
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
