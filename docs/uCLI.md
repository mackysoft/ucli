## 名称規則
- 正式名称：uCLI
- 名前空間・アセンブリ等（.NET / C#）：`Ucli`
- コマンド：`ucli`

## 対象
- CLI：.NET 8 以上

## 文書マップ
- [uCLI.md](uCLI.md)：製品概要、実行契約、コマンド仕様、`ucli test` 統合仕様の入口
- [uCLI-design-principles.md](uCLI-design-principles.md)：設計原則の正本
- [uCLI-architecture.md](uCLI-architecture.md)：project 責務境界、依存方向、型の配置判断の正本
- [json-request-spec.md](json-request-spec.md)：JSON リクエスト入力契約の正本
- [uCLI-command-reference.md](uCLI-command-reference.md)：コマンド一覧、option table、終了コード、実行例
- [uCLI-property-reference.md](uCLI-property-reference.md)：JSON 契約と `payload` / catalog のプロパティ定義
- [daemon-startup-lifecycle.md](daemon-startup-lifecycle.md)：`daemon start` の起動 lifecycle、startup blocker、process policy、diagnosis / artifact の正本
- [ops-catalog.md](ops-catalog.md)：primitive operation の補助カタログ
- [package-operations.md](package-operations.md)：契約パッケージと Unity 側依存復元の運用手順
- [uCLI-skills.md](uCLI-skills.md)：agent 向け公式 SKILL の仕様、生成方針、責務境界、配布運用

## 契約正本と検証境界

| 対象 | 正本 | 説明・補助 | 検証 |
| --- | --- | --- | --- |
| CLI command set | `UcliCommandCatalog` と help output | [uCLI-command-reference.md](uCLI-command-reference.md) | CLI host の登録結果と help output の一致確認 |
| 公開 CLI JSON 出力 | 専用 JSON writer、command output DTO、Golden files | [uCLI-property-reference.md](uCLI-property-reference.md) | CLI output contract tests と Golden files |
| 公開 CLI payload schema | command output DTO、専用 JSON writer、Golden files から生成される schema artifact | [uCLI-property-reference.md](uCLI-property-reference.md) | generated schema tests、Golden validation、semantic invariant validator tests |
| daemon startup lifecycle | [daemon-startup-lifecycle.md](daemon-startup-lifecycle.md) | [uCLI-command-reference.md](uCLI-command-reference.md)、[uCLI-property-reference.md](uCLI-property-reference.md) | daemon start/status contract tests と startup diagnosis tests |
| JSON request 入力 | [json-request-spec.md](json-request-spec.md) | [uCLI-command-reference.md](uCLI-command-reference.md) の実行例 | request contract tests と static validation tests |
| operation args / result / metadata | `Ucli.Contracts` の Args / Result contract 型、contract 属性、`UcliOperationMetadata`、`ucli ops describe` | [ops-catalog.md](ops-catalog.md) | `ucli ops describe` contract tests、operation schema / validator tests、Unity operation authoring tests |
| package metadata | `Directory.Build.props`、package 固有 `.csproj`、Unity nuspec | [package-operations.md](package-operations.md) | package metadata の評価テストと package verify scripts |
| 公式 SKILL / README / help | 正本ではなく利用者向け導線 | [uCLI-skills.md](uCLI-skills.md) | SKILL tests、CLI package tests、help registration tests |

README、help、SKILL、補助 catalog には、公開 operation contract や command reference の詳細を複製しない。仕様変更時は正本を更新し、必要な Golden files、contract tests、補助文書を同じ変更単位で揃える。

## コンセプト
**安全にUnityを編集できるCLIツール。**
- Unityを **ヘッドレス（batchmode）** で起動して実行できる（oneshot）
- Unityを **常駐サーバ（デーモン）** として起動し、繰り返しリクエストを処理できる
- CLI起動、ユーザー起動のGUIインスタンス、両方でデーモンは起動する
- 変更は Unity Editor API（AssetDatabase / Scene / Prefab / SerializedObject 等）に限定する。YAML直編集を前提にしない
- すべての入出力はJSONを基本にする
- Unity Test Framework の実行と結果正規化を、`ucli test` として統合提供する

設計思想と境界原則の詳細は [uCLI-design-principles.md](uCLI-design-principles.md) を正本とする。

## アーキテクチャ
- コア：.NET製 CLI（`ucli`）
- サーバ：Unity Editor プラグイン（`Ucli.Unity`）

### 実行モード（`--mode`）
`daemon` と `logs` コマンドを除く各コマンドは `--mode` を受け取る。未指定時の既定値は `auto`。

IPCを利用するコマンドは `--timeout <int>`（ミリ秒）を受け取る。  
未指定時は `config.json` の `ipcTimeoutMillisecondsByCommand[command]` を優先し、未設定または `null` の場合は `ipcDefaultTimeoutMilliseconds` を使用する。  
`--timeout` は `1..2147483647` の整数のみ許可し、空文字・空白・非数値・0以下は `INVALID_ARGUMENT` とする。
timeout は mode decision、plugin verify、IPC dispatch、readiness wait をまたいで request の共有 budget として消費する。`oneshot` では、response 受信後または request timeout/error 後の Unity 終了待ちに内部 cleanup budget を使う。cleanup は stale と断定できる `Temp/UnityLockfile` 残存を抑えるための後処理であり、公開 CLI option では制御しない。

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

### デーモン Editor mode（`--editorMode`）
`ucli daemon start` は `--editorMode <batchmode|gui>` を受け取る。未指定時は既存 running session の editorMode を優先し、session が無くても対象 project を開いている GUI Editor process を検出できる場合は GUI Editor session として接続待ちし、どちらも無い場合だけ `batchmode` を起動する。

- `batchmode`
  - Unity を `-batchmode -nographics` で起動し、CLI 所有の daemon session を登録する
  - `daemon stop` は対象 process の終了まで管理する
- `gui`
  - 既に対象 project を開いている GUI Editor の uCLI endpoint を検出し、同一 session 管理へ参加させる
  - 起動済み GUI に uCLI endpoint がまだ登録されていない場合、同じ GUI process に対する endpoint と session 登録が完了するまで待機する
  - 起動済み GUI が存在しない場合、Unity GUI Editor を起動し、uCLI endpoint と session 登録が完了するまで待機する
  - ユーザー起動 GUI の `daemon stop` は endpoint / session 登録と session token を無効化し、Unity process は終了しない
  - CLI 起動 GUI の `daemon stop` は `canShutdownProcess = true` の session に限り Unity process の終了まで管理する

既存 GUI Editor に接続した session は `ownerKind = user`、`canShutdownProcess = false` とする。CLI が新規起動した GUI Editor は `ownerKind = cli` とし、process 終了まで管理できる場合だけ `canShutdownProcess = true` とする。明示した `--editorMode` と既存 running session または検出済み GUI Editor process の editorMode が一致しない場合は `DAEMON_EDITOR_MODE_MISMATCH` を返し、暗黙に別 Editor session を追加起動しない。

`daemon start` の成功は endpoint と session の登録完了を意味し、`lifecycleState = ready` を保証しない。endpoint 登録前の起動ブロックは startup lifecycle として扱い、分類できる場合は `DAEMON_STARTUP_BLOCKED`、分類不能 timeout の場合だけ `IPC_TIMEOUT` を返す。起動 lifecycle、既存 GUI Editor 検出、`--onStartupBlocked` の process policy、diagnosis / artifact の契約は [daemon-startup-lifecycle.md](daemon-startup-lifecycle.md) を正とする。

GUI Editor session も同じ物理 `UnityProjectRoot` の project lifecycle lock に参加するが、同じ GUI Editor 内でユーザーが行う Inspector / Scene / Prefab Stage 操作は排他できない。reviewed mutation workflow では `planToken` と state drift 検知を使い、`plan` 後の手動変更を `call` 前に検出する。GUI session の `query` / `resolve` / `plan` は、selection、active Scene、Prefab Stage、dirty state、Undo stack に観測由来の変更を残してはならない。観測由来の Editor state を復元できない場合、その `query` / `resolve` / `plan` は成功として扱わない。

### `projectPath` 解決順序
`--projectPath` を受け取るコマンドは、対象 Unity project path 候補を次の順で選択する。選択された候補はまだ未正規化の入力であり、その後に絶対 path 化、Unity project marker 判定、storage root / `projectFingerprint` 解決を行う。

1. `--projectPath`
2. 環境変数 `UCLI_PROJECT_PATH`
3. コマンド固有の fallback
4. カレントディレクトリ

現在のコマンド固有 fallback は `test run` の `profile.json` `projectPath` のみである。`test run` 以外のコマンドと、`profile.json` `projectPath` が未指定の `test run` は、最終 fallback としてカレントディレクトリを使用する。

Project context resolution は Git worktree を project path 入力候補として探索しない。`daemon list` は、解決済み Unity project から現在の Git worktree root と project-relative path を取得し、同じ relative path を sibling worktree に適用して inventory 候補を観測する。sibling worktree 側で Unity project marker 判定に失敗した候補は一覧対象外として扱う。

Project context resolution 由来の入力不正は、公開 CLI JSON の envelope 構造を変えずに `errors[].code` へ投影する。代表的な code は `PROJECT_PATH_INVALID_FORMAT`、`PROJECT_PATH_NOT_FOUND`、`UNITY_PROJECT_MARKER_MISSING` である。

### モード挙動マトリクス
| デーモン状態 | `--mode daemon` | `--mode auto`（既定） | `--mode oneshot` |
| --- | --- | --- | --- |
| 起動中 | デーモン経由で実行 | デーモン経由で実行 | JSONエラー（`toolError`, `DAEMON_RUNNING_ONESHOT_FORBIDDEN`） |
| 未起動 | JSONエラー（`toolError`, `DAEMON_NOT_RUNNING`） | oneshotで実行 | oneshotで実行 |

## 公開 CLI 出力と JSON 入力契約
- 公開 CLI 出力は `request-response` 型と `stream` 型の2種類を持つ
- `request-response` 型では `stdout` に JSON を1件だけ出力する
- `stream` 型では `stdout` にイベントを逐次出力する
- 現在 `stream` 型を使うのは `ucli logs unity read` と `ucli logs daemon read` の成功時だけであり、`--format json` は NDJSON（1行1JSONオブジェクト）を返す
- `ucli logs unity clear` は `request-response` 型であり、成功時は共通の CLI エンベロープを1件返す
- 進行ログと診断ログは `stderr` に出力する
- `request-response` 型の公開 CLI JSON 出力は、共通の CLI エンベロープを返す
- `protocolVersion` は `request-response` 型の公開 CLI JSON 出力、CLI が生成する内部 execute request、内部 IPC 応答で必須とする。ユーザー入力 JSON リクエストには含めない
- 現在の公開 CLI host が登録している command は `init`、`status`、`ready`、`refresh`、`compile`、`resolve`、`query`、`validate`、`plan`、`call`、`verify`、`daemon`、`logs`、`ops`、`codes`、`skills`、`test` である
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
- assurance command（`ready` / `compile` / `verify`）は `status=ok` でも verifier outcome によって `exitCode=1` を返し得る。`payload.verdict=pass` は `exitCode=0`、`payload.verdict=fail` または `incomplete` は `exitCode=1` とする
- `stream` 型の成功時は `CommandResult` を返さない
- `stream` 開始前に失敗した場合は、`request-response` 型のエラー JSON を返す
- `errors[].code` は open code set とする。利用側は未知コードを契約違反として拒否せず、既知コードに一致しない値は汎用失敗として扱う。
  - C# 契約では機械判定用エラーコードを `UcliErrorCode` で扱い、既知コードは責務別の typed code definition として定義する。JSON wire shape は文字列のままとする。

### Code catalog
`errors[].code`、diagnostic code、`reasonCode`、claim code、risk code は、agent や CI が失敗後の次行動と保証不足を判定するための制御トークンである。`message` は人間向け説明であり、agent や CI は message の文面で分岐しない。

`ucli codes` は、公開 JSON 契約に現れる open code set の静的な意味を機械可読に返す正本台帳である。code value は error、diagnostic、reasonCode、claim、risk の種別を問わず uCLI 全体で一意とする。`kind` は分類と一覧 filter のための metadata であり、code identity ではない。`lifecycleState`、`blockingReason`、`startupBlockingReason`、`retryDisposition`、`diagnosis.reason`、`fallbackReason` のような lowerCamel の分類値は code ではない。v1 では `reason` kind は予約済みであり、標準 payload は `reasonCode` field を emit しない。

- `ucli codes list` は現在の client に登録されている既知 code 一覧を返す
- `ucli codes describe IPC_TIMEOUT` は1つの code の意味、確認対象、既定の再試行分類を返す
- `claim`、`diagnostic`、`risk` の code は catalog の対象だが、各 code は descriptor が登録された client でだけ既知 code として解決される
- `ucli codes describe error:IPC_TIMEOUT` は期待 kind の検証付き alias として扱い、code が存在しても kind が一致しない場合は `INVALID_ARGUMENT` を返す

`ucli codes describe <CODE>` は、未知 code を既定では失敗にしない。未知 code は open code set の通常ケースとして `known=false` を返し、呼び出し側は汎用失敗または汎用保証不足として扱う。既知 code だけを許容したい検証用途では `--requireKnown` を指定し、その場合だけ未知 code を `INVALID_ARGUMENT` とする。

同じ code を複数 field に出してよいのは、どの field でも静的意味が完全に同じ場合だけである。意味が異なる場合は `kind` で分けず、code value 自体を分ける。code rename は breaking change として扱う。uCLI の JSON は bare code を返し、tool 横断の canonical identity は上位 supervisor が `ucli:<CODE>` のような `tool:code` として扱う。

uCLI 定義の code は uppercase snake case を基本形とする。claim の読みやすい主張は code 文字列ではなく `claims[].statement` に置く。

`errors[]` には長い説明や全候補原因を埋め込まない。実行時レスポンスは発生固有の `code`、`message`、`opId`、および可能な場合の `payload.opResults`、`payload.readPostcondition`、診断情報だけを返す。静的な意味は `ucli codes describe <CODE>` で取得する。

### 公開 CLI 共通エンベロープ
`request-response` 型の公開 CLI JSON 出力が返す共通エンベロープのフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### 公開 CLI payload schema
公開 CLI JSON は `Envelope<TSuccessPayload,TErrorPayload>` として扱う。top-level の `command` が `payload` の schema を決める discriminator であり、`payload` 自体へ payload kind field は追加しない。

- `status=ok` のとき、`errors=[]` とし、`payload` は command-specific success payload schema に一致しなければならない。
- `status=error` のとき、`errors.length >= 1` とし、`payload` は command-specific error payload schema に一致しなければならない。
- 多くの command は error payload を空 object としてよい。`daemon start` の startup diagnosis など、失敗時にも構造化情報を返す command だけが専用 error payload schema を持つ。

payload schema は手書き文書を正本にしない。正本は command output DTO、専用 JSON writer、Golden files であり、JSON Schema はそこから生成される package / release artifact とする。v1 では `ucli schemas` のような schema discovery command は command surface に追加しない。runtime discovery が必要になった場合も、Unity object schema を扱う `query comp schema` / `query asset schema` と混同しない別 surface として設計する。

schema artifact は repository root、`MackySoft.Ucli` package、GitHub Releases で同じ相対配置を使う。

```text
schemas/
  v1/
    schema-manifest.json
    cli-output/
      envelope.schema.json
      payload/
        status.schema.json
        validate.schema.json
        plan.schema.json
        call.schema.json
        resolve.schema.json
        query.assets.find.schema.json
        query.scene.tree.schema.json
        ready.schema.json
        compile.schema.json
        verify.schema.json
      defs/
        project.schema.json
        op-result.schema.json
        read-index.schema.json
        touched.schema.json
        diagnostic.schema.json
        window.schema.json
        verifier.schema.json
        assurance-claim.schema.json
        evidence.schema.json
        report-ref.schema.json
        residual-risk.schema.json
    request/
      request-envelope.schema.json
      edit-dsl.schema.json
```

schema set version は `schemas/v{major}/` directory で表す。file name は schema の意味名だけを表し、versioned schema directory 配下では file name に major version を含めない。schema major version は `protocolVersion` major と一致させる。`protocolVersion=1` の runtime JSON を検証する consumer は `schemas/v1/` を使う。closed schema へ field を追加する場合は `protocolVersion` を上げるか、明示的な extension field を使う。

各 schema file は canonical `$id` を持ち、`$id` は schema set major version と `schemas/v{major}/` 配下の path に対応する URL とする。例: `https://schemas.mackysoft.dev/ucli/v1/cli-output/envelope.schema.json`、`https://schemas.mackysoft.dev/ucli/v1/cli-output/payload/compile.schema.json`、`https://schemas.mackysoft.dev/ucli/v1/cli-output/defs/assurance-claim.schema.json`、`https://schemas.mackysoft.dev/ucli/v1/request/request-envelope.schema.json`。`$schema` は JSON Schema dialect を表し、schema identity や uCLI protocol version とは別である。

runtime CLI JSON envelope は `schemaVersion` を持たない。consumer は runtime の `protocolVersion` と `command` から envelope schema と command-specific payload schema を選ぶ。package version は schema path に入れず、`schema-manifest.json` にだけ記録する。

`schemas/v1/schema-manifest.json` は schema set の index であり、少なくとも `schemaSet`、`schemaSetVersion`、`protocolVersion`、`packageVersion`、`jsonSchemaDialect`、`schemas[]` を持つ。`schemas[]` は各 schema の `$id`、version directory からの relative `path`、`kind`、command-specific payload の場合は `command` を対応づける。

schema family は次の層に分ける。

1. common envelope schema
2. common component schema
3. command payload schema
4. request envelope / edit DSL schema
5. semantic invariant validator

request schema artifact は user-authored request JSON の枠だけを検証する。`schemas/v1/request/request-envelope.schema.json` は `steps[]`、step 共通 field、`kind:"op"` / `kind:"edit"` の識別を検証し、`kind:"op"` の `args` は operation ごとの `argsSchema` に委譲する。`schemas/v1/request/edit-dsl.schema.json` は edit DSL の構造を検証し、Unity object や operation 固有の意味制約は `ops describe` と runtime verifier に委譲する。

公開 payload schema は原則 closed にする。JSON Schema 2020-12 を使う場合は `unevaluatedProperties:false`、uCLI supported subset では object ごとに `additionalProperties:false` を使う。拡張が必要な場合は `protocolVersion` を上げるか、明示的な extension field を追加する。`errors[].code`、`claims[].id`、diagnostic code、risk code のような open code set は schema enum にしない。schema は string shape だけを検証し、静的意味は `ucli codes describe <CODE>` が返す。

request command payload schema は、`payload.project`、`payload.requestId`、`payload.opResults[]`、`payload.readIndex`、`payload.planToken`、`payload.readPostcondition` などの command result envelope を検証する。`opResults[].result` の中身は operation ごとの `resultSchema` へ委譲し、command payload schema に複製しない。

assurance command payload schema は `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` を共通必須 component として持つ。`ready`、`compile`、`verify` はこの共通 component に command-specific field を追加する。

JSON Schema は required field、type、enum、array / object shape、closed property、report reference entry の構造などを検証する。`payload.verdict` の再計算、`claims[].verifierRef` と `payload.verifiers[].id` の参照解決、required claim と required verifier の整合、`primaryClaims[]` の存在、`evidenceRef` / `reportRef` の解決、code の global uniqueness は JSON Schema ではなく semantic invariant validator と Golden tests で検証する。semantic invariant validator tests は `scripts/verify.sh` の .NET verification path に含め、schema 生成結果、Golden output、semantic invariants を同じ CI gate で検証する。

semantic invariant validator tests は少なくとも次を固定する。

- `claims[].verifierRef` は `payload.verifiers[].id` に存在する。
- `claims[].required=true` の claim は `required=true` の verifier を参照する。
- `payload.verifiers[].primaryClaims[]` の各 code は `claims[].id` に存在する。
- `payload.verifiers[].primaryClaims[]` の各 claim は同じ verifier を `verifierRef` で参照する。
- `payload.verifiers[].required=true` の verifier は `primaryClaims[]` を空にしない。
- `payload.verdict` は required claim の `status` / `coverage` と claim-local / payload-global `residualRisks[].blocking` から再計算できる。
- built-in verify profile は `built-in:default`、`built-in:mutation`、`built-in:project`、`built-in:script` を選択でき、`--profile` と `--profilePath` の同時指定は `INVALID_ARGUMENT` になる。
- `payload.profile` は `source`、`name`、`path`、`digest` を返し、digest 入力に profile identity と effective steps を含める。`built-in:default` と `built-in:project` が同じ verifier set でも profile 名が異なるため digest は異なる。
- `payload.verifiers[].effects[]` は verifier kind と args から計算した値であり、profile 入力に書かれた `effects[]` が計算値と異なる場合は `INVALID_ARGUMENT` になる。
- `ready --mode auto` が daemon に解決された場合は `validity.kind=sessionBound`、`validity.guaranteesReusableSession=true` を返し、oneshot probe に解決された場合は `validity.kind=probeOnly`、`validity.guaranteesReusableSession=false` を返す。
- ready claim は常に `validity` を持つ。
- `verify --from` が `coverageImpact=partial` または `indeterminate` の `opResults[].diagnostics[]` を消費する場合、`payload.claims[]` または `payload.residualRisks[]` に partial / indeterminate / blocking risk として反映される。
- `verify --from` の入力不整合は `VERIFY_INPUT_SCHEMA_UNSUPPORTED`、`VERIFY_INPUT_PROTOCOL_VERSION_MISMATCH`、`VERIFY_INPUT_COMMAND_UNSUPPORTED`、`VERIFY_INPUT_PAYLOAD_INVALID`、`VERIFY_INPUT_PROJECT_MISSING`、`PROJECT_FINGERPRINT_MISMATCH` のいずれかの code で command failure になる。
- `evidence[].evidenceRef` と `payload.verifiers[].reportRef` は `payload.reports` の key に解決できる。
- `payload.reports[ref]` は `kind` と locator（`path` または `uri`）を持ち、digest-only entry を拒否する。
- `ucli codes list` 内の code value は kind を問わず global unique である。
- `ucli codes` catalog は `errors[].code`、`diagnosis.primaryDiagnostic.code`、`opResults[].diagnostics[].code`、`claims[].id`、`residualRisks[].code`、明示的な `reasonCode` field の descriptor を登録できる。client が descriptor を持つ code だけが既知 code として解決される。
- `ucli codes describe <UNKNOWN_CODE>` は `known=false` として成功し、`--requireKnown` 指定時の unknown code だけを `INVALID_ARGUMENT` とする。

### 内部 IPC 応答
CLI と Unity runtime の間では、公開 CLI エンベロープとは別に IPC 専用エンベロープを使う。外側 IPC envelope の `requestId`、IPC の `status`、IPC の `payload`、IPC の `errors` はこの内部契約に属する。
execute 系コマンドの公開 `payload.requestId` は、CLI が内部 execute request に付与した `requestId` であり、外側 IPC envelope の `requestId` とは別の値として扱う。

### `protocolVersion` 規則
- 初版はメジャーバージョン整数のみを使用する（例：`1`）
- 受信したメジャーバージョンがサーバー対応値と一致しない場合は、処理を実行せずJSONエラーを返す
- 推奨エラーコード：`PROTOCOL_VERSION_MISMATCH`

### JSONリクエスト入力
ユーザーが CLI に入力する JSON リクエストのトップレベル構造、step 種別、編集 DSL、参照表現、サンプルは [json-request-spec.md](json-request-spec.md) を正本とする。CLI はユーザー入力 JSON を正規化し、`protocolVersion` と `requestId` を付与した内部 execute request として Unity runtime へ送る。
本書では、そのリクエストを CLI と Unity runtime がどのように検証・実行するかだけを定義する。

### execute 系応答の内部契約と公開/未公開写像
`plan` / `call` / `resolve` / `query` / `refresh` は、内部では `IpcResponse.payload = IpcExecuteResponse` を受け取る。公開 CLI がこの内部応答を返す場合は、その値を各コマンドの `payload` へ写像する。
- execute payload 内の `requestId` は公開 CLI の共通 top-level property ではなく、必要な場合だけ各コマンドの `payload.requestId` に写像する
- execute payload 内の `project` は公開 CLI の共通 top-level property ではなく、project 同一性を後続検証へ渡す各コマンドの `payload.project` に写像する
- `opResults` は execute 応答に属し、公開するコマンドでは `payload.opResults` に写像する
- `opResults` の単位は public `steps[]` であり、lower 後 primitive trace をそのまま公開しない
- `planToken` は execute 応答に属し、`ucli plan` では `payload.planToken` に写像する
- `ucli call --withPlan` は、事前 `plan` 実行の結果を `payload.plan` へ写像する
- `ucli call` は `payload.readIndex` を返さない
- `resolve` は `payload.project`、`payload.requestId`、`payload.opResults`、`payload.readIndex` を返す
- `query` は `payload.project`、`payload.requestId`、`payload.opResults`、`payload.readIndex` を返す
- `refresh` は `payload.project`、`payload.requestId`、`payload.opResults`、必要時のみ `payload.readPostcondition` を返す
- request 系コマンドの機械判定用エラーは、公開 CLI では共通エンベロープの `errors[]` に載せる
- `status=error` であっても、先行する実行単位が適用済みである場合がある。`payload.opResults` を返すコマンドでは、その値で適用状況を機械判定する
- `IPC_TIMEOUT`、reload disconnect、runtime crash は、それだけで「未適用」を意味しない
- timeout / disconnect / crash の場合でも、呼び出し側は `status` だけで未適用と断定しない

## 内部 `plan` / `call` 実行の基本入力
CLI の `plan` と `call` はユーザー入力 JSON リクエストを受け取り、内部 execute request へ正規化して Unity runtime へ送る。
ユーザー入力の形式は [json-request-spec.md](json-request-spec.md) に従う。
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
- `plan` は内部 execute 応答で `planToken` を発行し、公開 `ucli plan` では `payload.planToken` として返す
- `call` は `planToken` がある場合に、署名・有効期限・リクエスト一致・状態一致を検証する
- ユーザー入力 JSON には `planToken` を含めない。`protocolVersion` と `requestId` は CLI が内部 execute request へ付与する
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
- `blockedByModal` / `safeMode` / `playmode` / `shuttingDown` は readiness gate の即時失敗状態として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで失敗させる。ただし `--allowPlayMode` 付きの Play Mode 変更では `playmode` を許可状態として扱う
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

### `requestId` の冪等性（デーモン内部）
- デーモンモードでは、Unity IPC の外側 request envelope にある `requestId` を冪等キーとして扱う
- これはユーザー入力 JSON のフィールドではなく、execute payload 内の `requestId` とも別の内部 IPC 相関 ID である
- 同一 IPC `requestId` かつ同一 execute request 内容は再実行せず、前回レスポンスを返す
- 同一 IPC `requestId` かつ異なる execute request 内容は `REQUEST_ID_CONFLICT` で拒否する
- 保持先はデーモン単位のメモリ内キャッシュ（ディスク永続化しない）
- キャッシュ保持項目は IPC `requestId`、`requestDigest`、`response`、`createdAt`、`expiresAt`
- 既定値は TTL 24時間、最大 10,000 件（超過時は古い順に破棄）

## Editor Lifecycle
Editor lifecycle は、Unity runtime の要求受付可否を外部から機械判定するための公開契約である。  
`editorMode` が `batchmode` / `gui` のどちらであっても同じ状態語彙を使う。
GUI Editor session も batchmode Editor session と同じ `projectFingerprint`、session token、同一ユーザー IPC 認可、物理 `UnityProjectRoot` 単位の project lifecycle lock に参加する。

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

- batchmode Editor session が観測・返却する `lifecycleState` は `starting` / `ready` / `busy` / `compiling` / `domainReloading` / `playmode` / `shuttingDown` の subset とする
- GUI Editor session は `blockedByModal` と `safeMode` も実状態として返す

### 状態項目
`ucli status` / `ucli daemon status` が返す `editorMode` / `compileState` / `compileGeneration` / `domainReloadGeneration` / `blockingReason` / `canAcceptExecutionRequests` のプロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `null` と `na`
- API payload では未観測・非適用値を `null` で返す
- `stateFingerprint` の内部入力では未取得値を `na` に正規化して算出する
- したがって、レスポンス契約の `null` と内部ハッシュ入力の `na` は同義ではない

### 状態遷移契約
| `lifecycleState` | 意味 | 遷移起点 | 許可コマンド | 拒否時エラーコード | 離脱条件 |
| --- | --- | --- | --- | --- | --- |
| `starting` | runtime 起動直後で、まだ `ready` 条件を満たしていない | daemon 起動直後、GUI Editor session の初期化直後、domain reload 後の再初期化 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_STARTING` | `ready` 条件を満たす、または他の優先状態へ移る |
| `ready` | 実行要求を安全に受け付けられる | startup 完了、compile 完了、domain reload 完了、modal 解消、Safe Mode 解消、Play Mode 終了、busy 解消 | 全コマンド | なし | compile 開始、domain reload 開始、modal 発生、Safe Mode 進入、Play Mode 進入、busy 開始、shutdown 開始 |
| `busy` | compile / reload 以外の Editor 内部処理により実行受付を止めている | refresh 直後の内部同期、保存前後の排他的処理、サーバーが busy と判定する Unity 側処理 | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_BUSY` | busy 解消後に `ready` または他の優先状態へ移る |
| `compiling` | スクリプト compile が進行中 | スクリプト変更、refresh / import、package 変更等で compile が始まる | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_COMPILING` | compile 完了後、`ready` または `domainReloading` / 他の優先状態へ移る |
| `domainReloading` | domain reload 中で実行コンテキストが安定していない | compile 完了後の domain reload、手動再読み込み | `validate`, `status`, `daemon status`, `logs`, `daemon stop` | `EDITOR_DOMAIN_RELOADING` | domain reload 完了後、`starting` または `ready` / 他の優先状態へ移る |
| `playmode` | Play Mode 中で通常実行を許可しない | Editor の Play 実行開始 | `validate`, `status`, `daemon status`, `logs`, `daemon stop`, `--allowPlayMode` 付きの Play Mode 変更 `plan` / `call` | `EDITOR_PLAYMODE` | Play Mode 終了後に `starting` または `ready` / 他の優先状態へ移る |
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
- `--allowPlayMode` が指定され、Play Mode 変更契約を満たす `plan` / `call` だけは `playmode` を即時失敗状態として扱わない
- 待機は既存の `--timeout` budget を消費し、使い切った場合は `IPC_TIMEOUT` を返す
- `ops list` / `ops describe` では live source fallback に対してのみ意味を持つ。readIndex hit では Unity 接続も readiness wait も行わない
- `test.run` では daemon-backed execution に対してのみ意味を持つ。`oneshot` と `auto -> oneshot` は direct `-runTests` のままとし、readiness wait を行わない

### 実行系コマンドの状態別挙動
- `status`, `daemon status`, `logs`, `daemon stop` は常時利用可能とする
- 未公開の request static validation は Editor lifecycle gate の対象外とする
- 内部 `plan`, `call`, `resolve`, `query` と公開 `refresh`、daemon-backed `test.run` は `lifecycleState = ready` 必須とする
- `--allowPlayMode` 付き `plan` / `call` は例外として `editorMode = gui` かつ `lifecycleState = playmode` の Play Mode 変更だけを許可する
- 公開 `ops list` / `ops describe` は readIndex hit では lifecycle gate の対象外とし、live source fallback 時だけ `lifecycleState = ready` を要求する
- 上記コマンドは既定で wait 対象状態を待機し、`--failFast` 指定時のみ `lifecycleState != ready` で即時失敗する
- `test.run` の `oneshot` target は readiness wait を行わず、`--failFast` を指定しても挙動を変えない
- `test.run` の timeout は発生源で分類する
  - daemon IPC timeout と readiness wait の timeout は `IPC_TIMEOUT`
  - oneshot Unity process timeout は `UNITY_TEST_EXECUTION_TIMEOUT`

### `planToken` / ドリフト検知との関係
- `compileState` と `domainReloadGeneration` の変化は `stateFingerprint` に反映し、`STATE_CHANGED_SINCE_PLAN` の候補に含める
- `blockedByModal`, `safeMode`, `playmode`, `shuttingDown` は readiness gate として扱い、`STATE_CHANGED_SINCE_PLAN` ではなくライフサイクル専用エラーで拒否する。ただし `--allowPlayMode` 付きの Play Mode 変更では `playmode` を許可状態として扱う
- `starting`, `busy`, `compiling` は既定で待機対象とし、`--failFast` 指定時だけライフサイクル専用エラーで拒否する
- `domainReloading` は AppDomain reload を跨いで要求を再開しないため、既定でも即時拒否する
- `call` は `ready` 判定に失敗した場合、その時点で終了する
  - `Validate` / `Plan` / `planToken` 検証 / `Call` は1件も実行しない
- `call` は `ready` のときのみ `Validate -> Plan -> planToken 検証 -> Call` に進む
- `compileGeneration` は公開 telemetry として返すが、`stateFingerprint` の入力には含めない

### Play Mode 変更
Play Mode 中の変更 request は通常の `plan` / `call` では拒否し、`--allowPlayMode` を指定した場合だけ Play Mode 変更として扱う。`--allowPlayMode` は mutation request 用の明示ガードであり、`query` / `resolve` / `validate` / `ops` には適用しない。

- 実行条件
  - `editorMode = gui`
  - `lifecycleState = playmode`
  - command は `plan` または `call`
  - request は `kind: "edit"` step のみで構成する
  - 各 step の `on` は `scene` / `prefab` / `asset` / `project` context のいずれか
- 許可対象
  - Play Mode 中の実行中 Scene に存在する GameObject / Component の一時的な変更
  - Prefab / asset / project context の通常編集契約に従う変更
  - Scene context では `set` / `ensureComponent` / `createObject` / `delete` / `reparent` のうち永続化を伴わない action
  - Scene context の Prefab instance に対して `applyPrefabOverrides(targetAssetPath: "...")` を明示した Prefab asset 反映
  - Scene context の Prefab instance に対して `revertPrefabOverrides(targetAssetPath: "...")` を明示した live object revert
- 拒否対象
  - Scene context の `commit: "context"` / `commit: "project"`
  - Play Mode 変更での `commit: "project"`
  - raw `kind: "op"` として送られた Prefab apply / revert primitive
  - `ucli.scene.save`

Scene 上の Prefab instance は Scene context として扱い、`commit: "none"` のみ許可する。Scene context の `commit: "none"` は Scene 保存を行わない指定であり、`applyPrefabOverrides` による対象 Prefab asset 保存とは矛盾しない。Prefab instance の override を Prefab asset へ反映する場合は、`commit` ではなく `applyPrefabOverrides` action を明示する。`applyPrefabOverrides` は Scene context から明示 Prefab asset へ保存する secondary persistence action であり、Scene asset は保存しない。Prefab instance の override を Prefab asset の値へ戻す場合は、`revertPrefabOverrides` action を明示する。

`applyPrefabOverrides` / `revertPrefabOverrides` の `targetAssetPath` は既存の `Assets/.../*.prefab` で、current target の Prefab instance lineage / valid target chain に含まれる必要がある。Nested Prefab / Variant の apply / revert 先は暗黙推論しない。対象 property は、同一 edit step / 同一 current target の先行 `set` が effective changed にした exact `SerializedProperty.propertyPath` だけとする。`propertyPaths` を省略した場合は対象 path 全部、指定した場合はその subset だけを対象にする。`propertyPaths: []`、重複 path、先行 `set` に由来しない path、effective changed でない path、parent path 指定による child property 対象化は拒否する。同一 property を複数回 `set` した場合は最終 effective value だけを対象にし、最終値が pre-request 値と同じなら対象外とする。child GameObject / child Component へは潜らず、`ensureComponent` / `createObject` / `delete` / `reparent` 由来の構造変更 override は対象外とする。

`revertPrefabOverrides` は同一 step の先行 `set` に由来する request-attributed override だけを Prefab asset 値へ戻し、pre-request 時点ですでに存在した override は拒否する。Unity Editor の一般的な Revert Overrides のように、既存 override 全体を戻す操作として扱わない。`applyPrefabOverrides` は pre-request override であっても、同一 step の先行 `set` が exact path を effective changed にした場合だけ許可する。apply / revert は全対象 property を preflight 検証してから実行し、検証エラーでは action 全体を適用しない。Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない。

Prefab asset 自体は Prefab context として扱い、runtime は必要に応じて対象 Prefab asset を編集用 context として開き、通常の `edit` と同じ action / commit / dangerous guard を適用する。Asset / project context も同じく通常の `edit` と同じ契約で保存できる。Prefab / asset / project の `commit: "context"` は対象永続化単位だけを保存し、Scene 保存や open Scene を巻き込む一括 project save を伴ってはならない。`commit: "project"` は project-wide save であり、Play Mode 変更では許可しない。

Play Mode 変更の `plan` / `call` は Play Mode の live object を正本とし、readIndex を対象解決や scene / prefab / asset / project 観測に使わない。`--allowPlayMode` と明示 `--readIndexMode` の併用は `INVALID_ARGUMENT` とする。`--readIndexMode` 未指定時は実効 readIndex mode を `disabled` とし、`plan --allowPlayMode` の `payload.readIndex` は `used=false`、`source=unity`、`fallbackReason="Play Mode mutation uses live Unity state."` を返す。

Scene context の Play Mode 変更は Play Mode の live object にだけ適用し、Prefab apply / revert を含まない場合の `opResults[].touched` は空配列、`readPostcondition` は省略する。`applyPrefabOverrides` で Prefab asset へ反映した場合は、その Prefab asset を `touched` に返し、必要な `readPostcondition` を返す。`revertPrefabOverrides` は Scene live object だけを戻すため、`touched` は空配列、`readPostcondition` は返さない。Prefab / asset / project context の保存を伴う Play Mode 変更は、通常の永続化変更と同じく保存した永続化単位を `touched` に返し、必要な `readPostcondition` を返す。`canAcceptExecutionRequests` は通常実行要求の可否を表すため、`lifecycleState = playmode` では `--allowPlayMode` の可否に関わらず `false` とする。

Play Mode 変更専用エラーは次とする。
- `PLAYMODE_NOT_ACTIVE`
- `PLAYMODE_REQUIRES_GUI_EDITOR`
- `PLAYMODE_PERSISTENCE_FORBIDDEN`

### mutation 後の read 安全性
- mutation 後に正本とみなせるのは live Unity で再観測した状態のみとする
- `call` / `refresh` が read surface を無効化した場合、payload に `readPostcondition.requirements[]` を返す
- 対象 surface は `assetSearch`、`guidPath`、`sceneTreeLite` のみとする。`sceneTreeLite` は `scenePath` 付きなら単一 Scene、`scenePath` なしなら全 Scene を対象にする
- `query` / `resolve` は matching requirement があるとき、`payload.readIndex.generatedAtUtc >= requirement.minSafeGeneratedAtUtc` を満たす readIndex だけを safe とみなす
- `generatedAtUtc` 欠落または requirement より古い readIndex は、`freshness` や `allowStale` にかかわらず unsafe とし、live Unity へフォールバックする
- matching requirement が無い surface にだけ既存の `freshness` 契約を適用する
- この postcondition 契約は `assetSearch.lookup`、`guid-path.lookup`、`scene-tree-lite` に限定し、`ops` / `types` / `schemas` catalog や static preflight の `readIndex` には適用しない

### ライフサイクル専用エラーコード
次の値は代表的な既知コードであり、`errors[].code` / `IpcError.code` の値集合を閉じない。

- `EDITOR_STARTING`
- `EDITOR_BUSY`
- `EDITOR_COMPILING`
- `EDITOR_DOMAIN_RELOADING`
- `EDITOR_PLAYMODE`
- `EDITOR_MODAL_BLOCKED`
- `EDITOR_SAFE_MODE`
- `EDITOR_SHUTTING_DOWN`

### Unity project lock エラーコード
`Temp/UnityLockfile` による Unity process 新規起動の preflight では、次のコードを返す。

- `UNITY_PROJECT_ALREADY_OPEN`: 対象 project を開いている live Unity process が確認できる
- `UNITY_PROJECT_LOCK_AMBIGUOUS`: `Temp/UnityLockfile` はあるが、所有 process の有無を安全に判定できない
- `UNITY_PROJECT_LOCK_CLEANUP_FAILED`: stale lock と判定できたが、`Temp/UnityLockfile` の削除に失敗した

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
    "editorMode": "batchmode",
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
- JSON リクエストは **redirected stdin のみ** から読む
- `call` の `planToken` は `--planToken <token>` で指定する

```bash
ucli plan <<'JSON'
{"steps":[]}
JSON

ucli call <<'JSON'
{"steps":[]}
JSON
```

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
    "ops": 120000,
    "daemon.start": 60000,
    "daemon.stop": 10000,
    "daemon.cleanup": 3000,
    "daemon.status": 3000,
    "daemon.list": 3000,
    "logs.daemon.read": 3000,
    "logs.unity.read": 3000,
    "logs.unity.clear": 3000
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
  - `query`：観測のみ。完了後に Editor 状態や永続化単位への変更を残さない
  - `command`：Scene のロード、Prefab 編集コンテキスト、AssetDatabase refresh / import のような一時的な Editor / runtime 状態を動かすが、ユーザーデータの永続化単位を直接編集・保存する操作ではない
  - `mutation`：Unity object、asset、project-scoped asset、または永続化単位を変更する
- `policy`：`safe | advanced | dangerous`
- `description`：operation の目的、使いどころ、注意点。agent が operation を選ぶための必須説明
- `inputs`：ユーザー入力から `steps[].args` を組み立てるための主契約。input 全体の意味制約は `inputs[].constraints`、variant field の意味制約は `inputs[].variants[].fields[].constraints` に置く
- `resultContract`：`opResults[].result` の有無、Result contract 型名、主データの意味を表す。step 間データフローや durable output は表さない
- `codeContract`：source code を受け取る operation だけが持つ任意 contract。source forms、entry point 署名、uCLI が提供する source-visible API、戻り値制約を表す。Unity API や project assembly の参照可否を列挙する allowlist ではない
- `assurance`：`sideEffects`、`mayDirty`、`mayPersist`、`touchedKinds`、`planMode` と、`planSemantics` / `callSemantics` / `touchedContract` / `readPostconditionContract` / `failureSemantics` / `dangerousNotes` による実行保証情報
- `argsSchema`：引数 `args` のJSONスキーマ。`steps[].args` の JSON 構造検証だけを担う
- `resultSchema`：`opResults[].result` のJSONスキーマ。結果を返さない operation は `null` とし、実行結果でも `result` field を省略する

Args/Result contract 型は `MackySoft.Ucli.Contracts` に置き、公開 JSON 構造の正本にする。Scene asset path、Prefab asset path、Hierarchy path、GlobalObjectId、asset GUID、Unity type identifier など、複数 operation で同じ意味を持つ string 入力/出力は C# contract 上では semantic value type として表し、IPC JSON では primitive string のまま扱う。入力ごとの説明と意味制約は Args 型の property 属性、または semantic value type の属性（`UcliDescriptionAttribute` / `UcliInputConstraintAttribute`）に置き、`ops describe` の `inputs[]` はその属性から生成する。operation 全体の説明、副作用保証、result の読み方は operation metadata に置く。operation 実装や CLI query builder は JSON 境界に入る前に typed contract を扱う。`JsonElement` は IPC codec、schema validation、低レベル deserialize 境界に限定する。

request-local alias の `var` selector branch は予約済み property である。public raw `op` の `steps[].args` では値が `null` でも使用できず、`ops describe` の `argsSchema` / `inputs[].variants[]` からも除外する。

`argsSchema` と `resultSchema` は検証用の派生成果物であり、agent 向けの主契約ではない。agent は `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` を使って operation を選び、`args` を組み立てる。source code を受け取る operation では `codeContract` を source form と C# API 契約として読む。`argsSchema` は組み立て後の JSON 構造検証に使う。schema には説明文や意味制約を置かない。

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
- `ucli ready`：次の操作へ進める Unity readiness gate を claim として返す
  - `--for <execution|mutation|test|readIndex>`：readiness target
  - `payload.verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`target`、`resolvedMode`、`sessionKind`、`lifecycle` を返す
  - `--mode auto` は daemon があれば daemon readiness、なければ oneshot readiness probe とし、payload に `resolvedMode` と `sessionKind` を返す
  - `sessionKind=transientProbe` の ready claim は probe session の readiness だけを保証し、後続 command が同じ Unity process を使うことは保証しない
  - `--for readIndex` でのみ `--readIndexMode allowStale|requireFresh` を受け付ける
  - 暗黙の `refresh`、`compile`、`test`、保存、修復は行わない
- `ucli compile`：AssetDatabase refresh、script compilation、domain reload state、最終 lifecycle を compile claim へ変換する
  - `payload.verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`compile.refresh`、`compile.scriptCompilation`、`compile.domainReload` を返す
  - `UNITY_DOMAIN_RELOAD_SETTLED` は reload 発生ではなく、compile 後の domain reload state が settled であることを表す
- `ucli validate`：JSONリクエストの静的検証（snapshot lint）
  - 保証範囲：JSON/DSL の構文、必須項目、`edit` の structural lowering、readIndex snapshot に基づく許可 op・args schema 検証
  - 対象実在確認、selector 解決、差分見積り、実行可否判定は行わない
  - Play Mode 変更の runtime 条件、対象 live object、Prefab instance lineage、request-attributed property path は保証しない。これらは `plan --allowPlayMode` で検証する
  - Unity実体への接続やフォールバックは行わず、ローカル readIndex のみを参照する
  - redirected `stdin` から JSON リクエストを読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - 成功時 payload は `project` と `readIndex` を返す
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
    - `disabled`：構文・DSL・lowering のみ検証する
    - `allowStale`：snapshot があれば `fresh/probable/stale` を使い、無ければ syntax-only に縮退する
    - `requireFresh`：fresh な snapshot を必須とし、欠落・破損・非 fresh で失敗する
- `ucli plan`：対象解決・差分見積り（実変更なし、または最小）を返す
  - redirected `stdin` から JSON リクエストを読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：static preflight で使う readIndex 利用モードを上書きする
    - `disabled`：syntax-only preflight へ縮退し、`payload.readIndex.used=false` と `fallbackReason="readIndex disabled by mode."` を返す
    - `allowStale`：snapshot 欠落時は syntax-only preflight へ縮退して Unity IPC `plan` を継続する
    - `requireFresh`：snapshot 欠落・破損・非 fresh では Unity IPC 前に失敗する
    - `--allowPlayMode` と明示 `--readIndexMode` の併用は `INVALID_ARGUMENT` とする
  - `--allowPlayMode`：GUI Editor session の Play Mode 中に変更 plan を許可する
  - `--failFast`：Unity readiness wait を行わず即時失敗する
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex`、`planToken` を返す
  - parse / project 解決より前で失敗した場合は空 payload、preflight 以降の失敗では `requestId` と `readIndex` を残し、`planToken` は省略する
- `ucli call`：Unityへリクエストを送って実行し、保存する
  - redirected `stdin` から JSON リクエストを読む
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `--planToken <token>`：`plan` が返したトークンを指定する
  - `--withPlan`：CLI が事前に `plan` を 1 回実行し、その結果を `payload.plan` に同梱する
  - `dangerous` opを含む場合は `--allowDangerous` 必須
  - `--allowPlayMode`：GUI Editor session の Play Mode 中に変更 call を許可する
  - `--failFast`：Unity readiness wait を行わず即時失敗する
  - `call` は readIndex に依存せず、Unity実体で再解決・再検証して実行する
  - `--readIndexMode` は受け付けない
  - 成功時 payload は `project`、`requestId`、`opResults`、必要時のみ `readPostcondition`、必要時のみ `plan` を返す
  - `payload.readPostcondition` は mutation により stale 化した read surface の safe 条件を返す
  - `payload.plan` は `project`、`requestId`、`opResults`、必要時のみ `planToken` を返し、`readPostcondition` は含めない
  - parse / project 解決より前で失敗した場合は空 payload、preflight 以降の失敗では `requestId` を残し、`opResults` は `[]` を返す
- `ucli resolve`：selector 1 件を GlobalObjectId へ解決する
  - JSON request と `stdin` は受け付けない
  - selector は `--globalObjectId` / `--assetGuid` / `--assetPath` / `--projectAssetPath` / `--scene --hierarchyPath [--componentType]` / `--prefab --hierarchyPath` の exactly one とする
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：Unity fallback 時の実行モード指定（既定 `auto`）
  - `--timeout <int>`：Unity fallback 時の IPC 待機タイムアウト（ミリ秒）
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
  - `--failFast`：Unity fallback 時の readiness wait を行わず即時失敗する
  - `--scene --hierarchyPath` かつ `--componentType` なしの場合、scene-tree-lite readIndex で解決できれば Unity IPC へ接続しない
  - readIndex で解決できない selector は Unity IPC `execute(command=resolve)` へ fallback する
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返し、解決結果は `opResults[0].result.globalObjectId` に置く
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
  - `ucli query` が一覧結果を束ねる場合の bounded window は command/query layer と primitive operation の両方で適用する。raw `ucli.assets.find` と `ucli.scene.tree` も `limit` / `cursor` を持ち、既定 `limit=100`、最大 `10000` とする
  - `--all` は `--limit` / `--after` と同時指定できない
  - `--fullDepth` と `--depth` は同時指定できず、全件列挙や深い展開は opt-in とする
  - `assets find` と `scene tree` は readIndex lookup を優先し、必要時だけ live Unity source へ fallback する
  - `go describe` / `comp schema` / `asset schema` は Unity IPC `execute(command=query)` へ委譲し、`Validate -> Plan` を実行する
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返す
  - `assets.find` の live query は `Assets/` 配下の persistent main asset を正本として検索し、readIndex 版は同じ契約へ追従する
- `ucli refresh`：AssetDatabase更新、インポート、コンパイル等でプロジェクトを最新化する
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - `--mode <auto|daemon|oneshot>`：実行モード指定（既定 `auto`）
  - `--timeout <int>`：IPC待機タイムアウト（ミリ秒）
  - `stdin` / `--planToken` / `--withPlan` / `--readIndexMode` は受け取らない
  - CLI内部で `ucli.project.refresh` 1件のみを含む固定 `execute` リクエストを生成する
  - 出力は共通の `CommandResult` エンベロープを返す
  - 成功時 payload は `project`、`requestId`、`opResults`、必要時のみ `readPostcondition` を返す
- `ucli verify`：Unity 側 verifier の結果を claim packet に束ねる
  - v1 は `ready`、`compile`、`postRead`、`test`、`logs` だけを扱い、外部 tool の実行、外部 report ingest、外部 finding 再解釈を行わない
  - uCLI の `verify` は Unity-local assurance を返す。静的解析 report などとの統合は外部 supervisor の責務であり、uCLI は他ツールの finding、verdict、coverage を再解釈しない
  - `--profile <built-in:default|built-in:mutation|built-in:project|built-in:script>` は built-in profile を選び、`--profilePath <path>` は user-authored profile file を選ぶ。同時指定は `INVALID_ARGUMENT` とする
  - `payload.verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`profile` と `profileDigest` を返す
  - profile 未指定時は `built-in:default` を使う。v1 の built-in profile 名は `built-in:default`、`built-in:mutation`、`built-in:project`、`built-in:script` とする
  - `built-in:default` と `built-in:project` は `compile` を含むため、AssetDatabase refresh、script compilation、domain reload を起こし得る
  - `verify --from` は `payload.project.projectFingerprint` を現在の project fingerprint と照合し、不一致なら `PROJECT_FINGERPRINT_MISMATCH` で失敗する
  - `postRead` は `readPostcondition`、`opResults[].applied`、`changed`、`touched`、Play Mode、commit 種別から claim を生成する
- `ucli ops`：利用可能なオペレーション一覧・詳細
  - `list`：利用可能なオペレーション一覧
  - `list` は `--nameRegex <regex>`、`--kind <query|mutation|command>`、`--maxPolicy <safe|advanced|dangerous>` で絞り込める
  - `--nameRegex` は operation name だけに適用し、glob 構文は受け付けない
  - `--kind` は構造化 exact match とし、複数フィルタは AND 条件で評価する
  - `--maxPolicy` は policy 上限であり、`safe` は safe のみ、`advanced` は safe / advanced、`dangerous` は safe / advanced / dangerous を返す
  - `list` の結果は operation name の ordinal 昇順で、該当 operation がない場合も成功として `operations: []` を返す
  - `describe <opName>`：特定オペレーションの agent 向け contract と検証用 schema
  - `--mode <auto|daemon|oneshot>`：live source fallback が必要な場合の実行モード指定
  - `--timeout <int>`：live source fallback が必要な場合の IPC 待機タイムアウト
  - `--readIndexMode <disabled|allowStale|requireFresh>`：readIndex利用モードを上書きする
  - `--failFast`：live source fallback 時の Unity readiness wait を行わず即時失敗する
  - `mode` / `timeout` は readIndex hit 時も妥当性を検証し、不正値は `INVALID_ARGUMENT` とする
  - readIndex hit では `--failFast` の有無にかかわらず Unity 接続しない
- `ucli codes`：公開 JSON 契約に現れる code value の静的意味を返す
  - code value は error、diagnostic、reasonCode、claim、risk の種別を問わず uCLI 全体で一意とする
  - `kind` は分類と list filter の metadata であり、code identity ではない
  - `describe <CODE>` は run 固有の結論ではなく、code の静的意味、出現 field、verdict / coverage / retry / review 判断への影響を返す
- `ucli status`：
  - CWDか `--projectPath` でプロジェクト指定
  - `--timeout <int>`（ミリ秒）で daemon 状態確認タイムアウトを上書きする（未指定時は config を使用）
  - `unityVersion` は常に `ProjectSettings/ProjectVersion.txt` 由来とする
  - `compileState = ready` であっても `lifecycleState = ready` を意味しない。意味は `Editor Lifecycle` 節を正とする
- `ucli test`：Unity Test Framework 実行と結果正規化
  - `run`：Unityテストを実行し、正規化結果をJSONで返す
  - `profile init`：`test` 実行用のプロファイルJSON雛形を生成する
- `ucli logs`：ログ取得と GUI Editor の Unity Console 表示クリア
  - `unity read`：Unityログ（コンパイルログ、実行ログ）を取得
  - `unity clear`：GUI Editor の Unity Console 表示をクリア
  - `daemon read`：デーモンログ（起動・停止・IPCなどの制御ログ）を取得
  - `--projectPath <path>`：対象Unityプロジェクト指定
  - 既定で bounded 取得とし、cursor / 時刻 / tail による段階取得を前提とする
  - `unity clear` は GUI Editor の Unity Console 表示だけを対象とし、daemon log、Unity log stream、`.ucli` 配下の物理ログファイルは削除しない
- `ucli daemon`：常駐サーバ管理
  - `start`：対象 `projectFingerprint` のデーモンを起動
    - `--editorMode <batchmode|gui>`：起動または接続する Editor mode。未指定時は既存 session、対象 project の既存 GUI Editor、batchmode 起動の順に選ぶ
    - `--onStartupBlocked <auto|keep|terminate>`：endpoint 登録前の起動ブロック検出時の process 扱いを指定する
    - 新規 Unity process 起動時の Editor executable は、`ProjectSettings/ProjectVersion.txt` の `m_EditorVersion` と既定の Unity install search roots から自動解決する
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
  - `ops` カタログ（list descriptor と describe detail）
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
次の値は代表的な既知コードであり、readIndex 関連の新しい失敗コードは同じ open code set に追加できる。

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
- `--allowPlayMode` と明示 `--readIndexMode` の併用は `INVALID_ARGUMENT` とし、未指定時だけ実効 readIndex mode を `disabled` とする
- `query` / `resolve` の一覧系は readIndex 利用時も bounded-by-default とし、deterministic order を崩さない
- mutation 後に matching `readPostcondition` がある surface は、`generatedAtUtc` が requirement を満たす readIndex または live Unity 再観測のみを safe とみなす

### ディレクトリ構造
```text
<repoRoot>/.ucli/local/fingerprints/<projectFingerprint>/index/
  catalogs/
    ops.catalog.json
    ops.describe/
      <opKey>.json
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

`ops.catalog.json` は `ops list` と `ops describe` の detail 参照に使う軽量目録であり、operation ごとの `name` / `kind` / `policy` / `description` と describe detail の参照情報を持つ。`ops.describe/<opKey>.json` は `ops describe` 用の単一 operation detail artifact であり、`description`、`inputs`、`resultContract`、`assurance`、`codeContract`、`argsSchema`、`resultSchema` を保持する。`opKey` は operation name から決定論的に作られる不透明な safe key であり、利用者は path を直接組み立てず `ucli ops describe <opName>` を読む。

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
本節は、Unity Test Framework の実行と結果正規化を `ucli test` として提供するための統合仕様を示す。
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

### 物理 UnityProjectRoot 単位の project lifecycle lock
- 同一物理 `UnityProjectRoot` を対象にする Unity process 起動は、worktree / storage root / `projectFingerprint` が異なっていても同じ lifecycle lock に参加する
- `daemon stop` / `daemon cleanup` は Unity process を起動しないが、session 操作を直列化するため同じ lifecycle lock に参加する
- project lifecycle lock は repo-local `.ucli` ではなく、ユーザー単位の OS local application data 配下に保存する
- `projectFingerprint` は session / IPC / artifact / readIndex / planToken の識別に使い、project lifecycle lock の identity と混同しない
- `status` / `daemon status` / `logs` / `ops` / `resolve` / `query` / `validate` / `plan` は reader 側の経路とし、project lifecycle lock を取らない
- 別物理 path の Unity project は、従来どおり並列起動できる
- `--mode auto` であっても、同じ物理 Unity project に暗黙に別 Unity process を増やして回避しない
- queue と即時拒否のどちらを採るかは実装選択とするが、同じ物理 Unity project への並列 Unity 起動を許可しないことは固定契約とする
- Unity の `Temp/UnityLockfile` は Unity 所有の二重起動防止 marker として扱う。uCLI は定期的または background では `Temp/UnityLockfile` を掃除しない
- uCLI が `Temp/UnityLockfile` の削除を試みるタイミングは、Unity process を新規起動する直前の preflight と、uCLI が起動した Unity process の終了後 cleanup の2つに限定する
- 起動直前の preflight では、stale と断定できる場合だけ `Temp/UnityLockfile` を自動削除して起動を続行する
- active 判定は、valid な uCLI session の live process、対象 project の `Library/EditorInstance.json` に記録された live process、OS process scan で `-projectPath` が対象物理 path と一致する Unity process の順に行う
- 対象 project を開いている live Unity process が確認できる場合は `UNITY_PROJECT_ALREADY_OPEN` を返す。lockfile はあるが process scan 失敗、権限不足、path 正規化失敗などで安全に所有者を判定できない場合は `UNITY_PROJECT_LOCK_AMBIGUOUS` を返し、lockfile を削除しない。stale と判定できたが削除に失敗した場合は `UNITY_PROJECT_LOCK_CLEANUP_FAILED` を返す
- uCLI が起動した Unity process の終了後に `Temp/UnityLockfile` が残った場合も同じ stale 判定を行い、stale と断定できれば削除する。この cleanup の成否だけで timeout / cancel / abnormal exit / artifact missing などの主エラー分類を `UNITY_PROJECT_ALREADY_OPEN` に変換しない
- `logs unity read`、`logs daemon read`、`daemon status`、既存 daemon への IPC は Unity process を新規起動しない reader 側の経路であり、`Temp/UnityLockfile` を作成・削除しない

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
- `ucli daemon start --editorMode batchmode` は batchmode Editor session を起動する
- `ucli daemon start --editorMode gui` は既存 GUI Editor session へ接続し、存在しない場合だけ GUI Editor session を起動する
- endpoint 登録前の待機、startup blocker 分類、process policy、diagnosis artifact は [daemon-startup-lifecycle.md](daemon-startup-lifecycle.md) を正とする
- `ucli daemon stop` は `canShutdownProcess` に従い、ユーザー起動 GUI Editor session では endpoint 登録解除に留める

#### Unity runtime
- Unity runtime は `editorMode = batchmode | gui` のいずれかで動作し、`Editor Lifecycle` 節の同一状態語彙を使う
- 起動直後は `starting` に入り、`ready` 条件を満たした時点で要求受付可能になる
- `starting` / `busy` / `compiling` の間、readiness gate を持つ実行系コマンドは既定で待機する
- `domainReloading` / `blockedByModal` / `safeMode` / `shuttingDown` の間、実行系コマンドはライフサイクル専用エラーで即時拒否する
- `status` / `daemon status` / `logs` / `daemon stop` はライフサイクル状態に関わらず利用可能とする
- ドメインリロード後は再初期化を経て `starting` から再評価し、最終的に `ready` または他のブロック状態へ遷移する
- idle 時の runtime は inert を原則とし、background work は明示コマンドまたは厳密な TTL に限定する
