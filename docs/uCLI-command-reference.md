> [!IMPORTANT]
> この文書は、uCLI のコマンド一覧、option table、サブコマンド規則、終了コード、実行例のリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、JSON プロパティ定義は [uCLI-property-reference.md](uCLI-property-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。
> この文書は公開 CLI surface の仕様を定義する。

## コマンド概要

| Command | 概要 | 備考 |
| --- | --- | --- |
| `ucli init` | `.ucli` の設定雛形を生成する | Git repository root を優先する |
| `ucli status` | daemon と lifecycle の状態を返す | `ProjectVersion.txt` 由来の `unityVersion` を返す |
| `ucli ready` | 次の操作へ進める readiness claim を返す | 状態観測と bounded wait だけを行い、暗黙修復は行わない |
| `ucli refresh` | プロジェクト更新を独立コマンドとして実行する | 固定の `ucli.project.refresh` を実行する |
| `ucli compile` | script compilation と domain reload の保証 claim を返す | 専用 top-level command とし、`refresh` の拡張や primitive operation wrapper にはしない |
| `ucli play` | Unity Editor Play Mode を明示制御する | `status` / `enter` / `exit` を持つ lifecycle command |
| `ucli resolve` | selector 1 件を GlobalObjectId へ解決する | scene-tree-lite index を優先し、必要時だけ Unity IPC へ fallback する |
| `ucli query` | 型付きサブコマンドで検索・構造取得・スキーマ取得を行う | `assets find` / `scene tree` / `go describe` / `comp schema` / `asset schema` を持つ |
| `ucli validate` | JSON リクエストを静的に lint する | Unity へ接続せず readIndex snapshot を参照する |
| `ucli plan` | JSON リクエストの plan フェーズを実行する | static preflight 後に Unity IPC `plan` を実行する |
| `ucli call` | JSON リクエストの call フェーズを実行する | static preflight 後に Unity IPC `call` を実行する |
| `ucli eval` | C# source を `ucli.cs.eval` として実行する | JSON request を組み立てずに eval を実行する convenience command |
| `ucli verify` | 明示 profile に従い Unity 側 verifier の claim packet を返す | v1 は外部 tool を含めない |
| `ucli ops` | primitive operation の一覧・詳細を返す | `list` / `describe` を持つ |
| `ucli codes` | 公開 JSON 契約に現れる code value の台帳を返す | `list` / `describe` を持つ |
| `ucli skills` | 公式 SKILL の一覧、配布、導入、更新、削除、診断を行う | `list` / `export` / `install` / `update` / `uninstall` / `doctor` |
| `ucli logs` | Unity / daemon のログ取得、GUI Editor の Unity Console 表示クリアを行う | 取得系の成功時はイベントストリームを返す |
| `ucli daemon` | daemon の起動・停止・掃除・状態取得を行う | `start` / `stop` / `cleanup` / `status` / `list` |
| `ucli test` | Unity Test Framework 実行と結果正規化を扱う | `run` / `profile init` |

## 公開コマンド

### 実行可能 command paths

| Command |
| --- |
| `ucli init` |
| `ucli status` |
| `ucli ready` |
| `ucli compile` |
| `ucli verify` |
| `ucli refresh` |
| `ucli resolve` |
| `ucli validate` |
| `ucli plan` |
| `ucli call` |
| `ucli eval` |
| `ucli daemon start` |
| `ucli daemon stop` |
| `ucli daemon cleanup` |
| `ucli daemon status` |
| `ucli daemon list` |
| `ucli logs daemon read` |
| `ucli logs unity read` |
| `ucli logs unity clear` |
| `ucli ops list` |
| `ucli ops describe` |
| `ucli codes list` |
| `ucli codes describe` |
| `ucli play status` |
| `ucli play enter` |
| `ucli play exit` |
| `ucli skills list` |
| `ucli skills export` |
| `ucli skills install` |
| `ucli skills update` |
| `ucli skills uninstall` |
| `ucli skills doctor` |
| `ucli query assets find` |
| `ucli query scene tree` |
| `ucli query go describe` |
| `ucli query comp schema` |
| `ucli query asset schema` |
| `ucli test run` |
| `ucli test profile init` |

- `ucli ops`
  - `list` は利用可能なオペレーション一覧を返す。
  - `list` は `--nameRegex <regex>`、`--kind <query|mutation|command>`、`--maxPolicy <safe|advanced|dangerous>` による絞り込みを受け付ける。
  - `--nameRegex` は operation name だけに適用する。glob 構文は受け付けない。
  - `--kind` は構造化 exact match とし、許可値へ正規化して評価する。
  - `--maxPolicy` は policy 上限であり、`safe` は safe のみ、`advanced` は safe / advanced、`dangerous` は safe / advanced / dangerous を返す。
  - `list` の複数フィルタは AND 条件で評価する。該当 operation がない場合も成功とし、`payload.operations: []` を返す。
  - `list` の出力順は operation name の ordinal 昇順とする。
  - `list` の各 operation は `name` / `kind` / `policy` / `description` を返す。
  - `list` は public raw `kind:"op"` として呼べる operation だけを返し、`editLoweringOnly` operation は返さない。
  - 無効な regex / kind / maxPolicy は `INVALID_ARGUMENT` を返す。
  - `describe <opName>` は特定の public raw operation の agent 向け contract と検証用 schema を返す。
  - `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` は operation 選択、入力構築、結果解釈の主契約である。
  - `policy` は operation contract facts から導出された admission policy であり、author の任意ラベルではない。
  - source code を受け取る operation では `codeContract` が source forms、entry point 署名、source-visible API、戻り値制約を表す。
  - `argsSchema` / `resultSchema` は Args/Result contract 型から生成された JSON Schema であり、`steps[].args` と `opResults[].result` の JSON 構造検証だけに使う。
  - `--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - `--failFast` は live source fallback に対してのみ適用し、readIndex hit では Unity 接続も readiness wait も行わない。
  - `mode` / `timeout` は readIndex hit 時も妥当性を検証し、不正値は `INVALID_ARGUMENT` を返す。
- `ucli skills`
  - `list` は bundled official SKILL と supported host を返す。
  - `list` の `supportedHosts[]` は `host`、`projectTargetDirectory`、`userTargetDirectory`、`reloadGuidance` を返す。
  - `export --host <host> --format directory --output <dir>` は指定 host 向けに公式 SKILL を一括 materialize する。`--format` 未指定時は `directory` とする。
  - `export --host <host> --format zip --output <file>` は release 用 zip を deterministic に生成する。
  - `install --host <host> --scope project --repoRoot <path>` は未導入の公式 SKILL を一括導入し、既存 target は暗黙上書きしない。
  - `update --host <host> --scope project --repoRoot <path>` は未導入の公式 SKILL を作成し、clean な旧版だけを更新し、最新なら no-op とする。
  - `uninstall --host <host> --scope project --repoRoot <path>` は clean な uCLI 管理済み公式 SKILL だけを削除し、`agent-skill.json` が無い directory は unmanaged として残す。
  - `doctor --host <host> --scope project --repoRoot <path>` は指定 host の SKILL 配布物だけを診断する。
  - `install` / `update` / `uninstall` / `doctor` は `--scope user` も受け付ける。user scope では `--repoRoot` を受け付けず、`--targetDir` 未指定時は host 既定 user target を使う。
  - `--targetDir <path>` は任意で受け付ける。project scope では repository root 配下に限定し、user scope では absolute path だけ許可する。
  - 成功時 payload は `host`、`scope`、`repositoryRoot`、`targetRoot`、`reloadGuidance` を返す。user scope では `repositoryRoot` は `null` になる。`update` は `createdCount` / `updatedCount` / `noOpCount`、`uninstall` は `deletedCount` / `noOpCount` / `skippedUnmanagedCount` を返す。
- `ucli status`
  - daemon と lifecycle の状態を JSON で返す。
  - `--timeout <int>` で daemon 状態確認タイムアウトを上書きする。
- `ucli ready`
  - Unity Editor が指定用途に対して次の要求を受け付けられるかを bounded wait 付きで判定し、readiness claim を返す。
  - `--for <execution|mutation|test|readIndex>`、`--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - 状態観測、wait、blocker 分類、retry disposition の返却だけを行い、暗黙の `refresh`、`compile`、`test`、保存、修復は行わない。
- `ucli compile`
  - Unity の script compilation、domain reload、compile diagnostics、最終 lifecycle を compile claim に変換する。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>` を受け付ける。
  - 専用 top-level command とし、`refresh` の拡張や primitive operation wrapper にはしない。
- `ucli play`
  - Unity Editor Play Mode の状態観測と明示遷移を行う。
  - `status` / `enter` / `exit` を持つ。
  - `--projectPath <string?>`、`--timeout <int>` を受け付ける。
  - Play Mode enter / exit は primitive operation や JSON request step として扱わない。
- `ucli verify`
  - 明示 profile に従って Unity 側 verifier を実行し、結果を claim packet に束ねる。
  - `--profile <name?>`、`--profilePath <path?>`、`--from <path?>`、`--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>` を受け付ける。
  - v1 は外部 tool の実行、外部 report ingest、外部 finding の再解釈を行わない。
- `ucli codes`
  - 公開 JSON 契約に現れる open code set の静的意味を返す。
  - `list` と `describe` を持つ。
  - error code、diagnostic code、reasonCode、claim code、risk code を扱う。
  - code value は種別を問わず uCLI 全体で一意とする。`kind` は分類と list filter のための metadata であり、code identity ではない。
- `ucli validate`
  - redirected `stdin` から JSON リクエストを読み、snapshot lint を返す。
  - `--projectPath <string?>` と `--readIndexMode <disabled|allowStale|requireFresh>` を受け付ける。
  - `--mode` / `--timeout` は受け付けず、Unity IPC に接続しない。
  - Play Mode 変更の runtime 条件、対象 live object、Prefab instance lineage、request-attributed property path は保証しない。これらは `plan --allowPlayMode` で検証する。
  - 成功時 payload は `project` と `readIndex` を返す。
  - `allowStale` では snapshot 欠落時に syntax-only へ縮退し、`requireFresh` では `READ_INDEX_BOOTSTRAP_FAILED` / `READ_INDEX_FORMAT_INVALID` / `READ_INDEX_FRESH_REQUIRED` を返す。
- `ucli resolve`
  - selector flags から 1 件だけ解決し、JSON request と `stdin` は受け付けない。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - selector は `--globalObjectId` / `--assetGuid` / `--assetPath` / `--projectAssetPath` / `--scene --hierarchyPath [--componentType]` / `--prefab --hierarchyPath` の exactly one とする。
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返す。
- `ucli query`
  - JSON request と `stdin` は受け付けず、型付きサブコマンドから固定 primitive operation 1 件を組み立てる。
  - サブコマンドの flags は operation Args contract 型に写像してから IPC payload へシリアライズする。
  - 全サブコマンドで `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--failFast` を受け付ける。
  - 一覧系の `assets find` と `scene tree` は `--limit`、`--after`、`--all` を受け付け、既定 `limit=100`、最大 `10000` とする。
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返す。
- `ucli plan`
  - redirected `stdin` から JSON リクエストを読み、static preflight 後に Unity IPC `plan` を実行する。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--readIndexMode <disabled|allowStale|requireFresh>`、`--allowPlayMode`、`--failFast` を受け付ける。
  - 成功時 payload は `project`、`requestId`、`opResults`、`readIndex`、`planToken` を返す。
  - `allowStale` では snapshot 欠落時に syntax-only へ縮退して継続し、`requireFresh` では snapshot 欠落・破損・非 fresh で失敗する。
  - `--allowPlayMode` と明示 `--readIndexMode` の併用は `INVALID_ARGUMENT` とし、`--readIndexMode` 未指定時だけ実効 readIndex mode を `disabled` とする。
- `ucli call`
  - redirected `stdin` から JSON リクエストを読み、static preflight 後に Unity IPC `call` を実行する。
  - `--projectPath <string?>`、`--mode <auto|daemon|oneshot>`、`--timeout <int>`、`--planToken <string?>`、`--withPlan`、`--allowDangerous`、`--allowPlayMode`、`--failFast` を受け付ける。
  - `--readIndexMode` は受け付けない。
  - 成功時 payload は `project`、`requestId`、`opResults`、必要時のみ `readPostcondition`、`postReadSource`、`plan` を返す。

## Lifecycle command contracts

### `ucli play`

`ucli play` は Unity Editor Play Mode を明示制御する lifecycle command family である。Play Mode enter / exit は uCLI content mutation を実行せず、`opResults[].touched` を返す content edit として扱わないため、primitive operation wrapper や JSON request step ではない。Play Mode 中に project code が起こす AssetDatabase、filesystem、static state、Editor state などへの副作用は、uCLI request-step mutation attribution の対象外である。

| Command | Meaning |
| --- | --- |
| `ucli play status` | 現在の Play Mode snapshot を返す。状態変更は行わない |
| `ucli play enter` | GUI Editor session を Play Mode に入れ、`EditorApplication.isPlaying == true` まで待つ |
| `ucli play exit` | Play Mode を終了し、通常 execution が再び可能な `lifecycleState=ready` まで待つ |

| Argument / Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象 Unity project path |
| `--timeout <int>` | - | 状態観測または transition wait の timeout milliseconds |

`ucli play` は Unity Editor process を起動しない。対象 project に既存の registered GUI daemon session が存在することを要求する。GUI daemon session が見つからない場合は `PLAYMODE_SESSION_NOT_AVAILABLE` を返す。GUI daemon session を起動または attach したい呼び出し側は、`ucli daemon start --editorMode gui` を明示的に実行する。

`ucli play` は GUI Editor session 専用であり、batchmode session では `PLAYMODE_REQUIRES_GUI_EDITOR` を返す。batchmode session の lifecycle 観測は `ucli status` または `ucli daemon status` で行う。

`play enter` の成功条件は `lifecycleState=playmode` だけではなく、Unity が `EditorApplication.isPlaying == true` を返すこととする。すでに Playing の場合は idempotent success として `alreadyEntered` を返す。`play exit` はすでに Edit Mode の場合に `alreadyExited` を返し、Play Mode から出る場合は exit 後の compile / domain reload / startup / busy を待って `ready` に戻るまでを command の成功条件とする。

Play Mode transition timeout は `PLAYMODE_TRANSITION_TIMEOUT` とし、transport timeout の `IPC_TIMEOUT` と区別する。timeout は no-op を意味しないため、可能な場合は latest observed lifecycle snapshot、`playMode`、transition result、application state を payload に含める。

## Assurance command contracts

### `ucli ready`

`ucli ready` は、Unity Editor が指定用途に対して次の要求を受け付けられるかを bounded wait 付きで判定し、readiness claim を返す。`status` は現在状態の snapshot であり、`ready` は次へ進めるかを判定する gate である。

| Argument / Option | Short | Description |
| --- | --- | --- |
| `--for <execution\|mutation\|test\|readIndex>` | - | readiness target。未指定時は `execution` |
| `--projectPath <string?>` | `-p` | 対象 Unity project path |
| `--mode <auto\|daemon\|oneshot>` | - | Unity execution mode |
| `--timeout <int>` | - | bounded wait の timeout milliseconds |
| `--readIndexMode <disabled\|allowStale\|requireFresh>` | - | `--for readIndex` の freshness 判定に使う readIndex mode |
| `--failFast` | - | waitable lifecycle state でも待たずに blocker として返す |

`ready` は次を行ってよい。

- Unity project identity の解決
- daemon / session 到達性の確認
- lifecycle / compile / domain reload / playmode / modal / safe mode / shutdown 状態の観測
- timeout budget 内の waitable state 待機
- blocker reason、retry disposition、readiness claim の返却

`ready` は次を行ってはならない。

- 暗黙の `refresh`
- 暗黙の `compile`
- 暗黙の `test`
- 暗黙の保存
- Unity project の修復

成功時 payload は `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`target`、`requestedMode`、`resolvedMode`、`sessionKind`、`timeoutMilliseconds`、`lifecycle`、`readIndex` を返す。`verifiers[]` には runtime lifecycle を観測した場合は `id=ready.lifecycle`、readIndex artifact を観測した場合は `id=ready.readIndex` の verifier を含め、`primaryClaims[]` は target に応じた ready claim code を含める。`claims[].verifierRef` はその `id` を参照する。`reports` は artifact が無い場合も空 object として返す。`claims[]` には target に応じて `UNITY_READY_EXECUTION`、`UNITY_READY_MUTATION`、`UNITY_READY_TEST`、`UNITY_READY_READ_INDEX` のいずれかを含める。ready claim は常に `validity` を持つ。readiness が成立しない場合でも、blocker を判定できた command は `status=ok` として `payload.verdict=fail` または `incomplete`、`claim.status=failed` または `indeterminate` を返してよい。transport failure、project resolution failure、payload validation failure は command failure として `status=error` を返す。

`--for readIndex` では `--readIndexMode allowStale` または `requireFresh` だけを受け付け、`disabled` は `INVALID_ARGUMENT` とする。readIndex readiness は persisted artifact の観測だけを行うため、明示 `--mode daemon` と `--mode oneshot` は `INVALID_ARGUMENT` とする。`--for execution`、`mutation`、`test` で `--readIndexMode` を指定した場合も `INVALID_ARGUMENT` とする。

`--mode daemon` は既存 daemon session の readiness だけを確認し、daemon が無ければ command failure とする。`--mode oneshot` は transient Unity session で readiness を確認し、persistent session を残さない。`--mode auto` は daemon があれば daemon readiness を確認し、daemon が無ければ oneshot readiness probe を実行する。payload は実効 runtime として `resolvedMode` と `sessionKind` を返す。`--for readIndex` では Unity runtime session を観測しないため、`resolvedMode=notApplicable`、`sessionKind=artifactOnly` を返す。

`sessionKind=transientProbe` の ready claim は、その probe session が指定 target に対して ready だったことだけを保証する。次の `call --mode auto` が同じ Unity process を再利用することは保証しない。この場合、claim の `subject.kind=unityReady` とし、`subject` には `target`、`requestedMode`、`resolvedMode`、`sessionKind` を含め、`validity.kind=probeOnly`、`validity.guaranteesReusableSession=false` を返す。daemon session に対する ready claim は `validity.kind=sessionBound`、`validity.guaranteesReusableSession=true` を返す。

### `ucli compile`

`ucli compile` は、Unity の script compilation、domain reload、compile diagnostics、最終 lifecycle を、次へ進んでよいか判断できる compile claim に変換する専用 top-level command である。`compile` は `refresh` の拡張ではなく、`ucli.project.compile` の primitive operation wrapper でもない。

`compile` の標準意味は、AssetDatabase refresh を起点に必要な script compilation を観測し、domain reload が発生する場合は reload 完了後の安定状態まで確認することである。`--waitForDomainReload false` のように保証強度を下げる option は標準経路に置かない。

| Argument / Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象 Unity project path |
| `--mode <auto\|daemon\|oneshot>` | - | Unity execution mode |
| `--timeout <int>` | - | refresh、compile、domain reload、最終 readiness の timeout milliseconds |

compile error が検出された場合、command 自体は compile verifier を実行できたため `status=ok` とし、`payload.verdict=fail`、`exitCode=1` を返す。IPC timeout、startup blocker、domain reload 追跡不能、diagnostics artifact 破損など、検証が成立しない場合は `status=error` を返す。

成功時 payload は `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`compile` を返す。`verifiers[]` には少なくとも `id=compile` の verifier を含め、`primaryClaims[]` は `UNITY_COMPILE_NO_ERRORS`、`UNITY_DOMAIN_RELOAD_SETTLED`、`UNITY_LIFECYCLE_READY_AFTER_COMPILE` を含める。`claims[].verifierRef` はその `id` を参照する。`compile` は少なくとも `refresh`、`scriptCompilation`、`domainReload` を含める。`refresh` は AssetDatabase refresh の実施有無と開始・完了時刻、`scriptCompilation` は開始・完了状態と diagnostic counts、`domainReload` は `reloadRequired`、`reloadObserved`、generation before / after を返す。`claims[]` には少なくとも次を含める。

- `UNITY_COMPILE_NO_ERRORS`
- `UNITY_DOMAIN_RELOAD_SETTLED`
- `UNITY_LIFECYCLE_READY_AFTER_COMPILE`

### `ucli verify`

`ucli verify` は、明示 profile に従って Unity 側 verifier を実行し、結果を `claim`、`evidence`、`coverage`、`residualRisk` に束ねる command である。便利な全部入り CI wrapper ではなく、閉じた verifier set から claim packet を生成する。

v1 の `verify` は外部 tool を含めない。外部 tool の実行、外部 report ingest、外部 finding の再解釈はいずれも行わない。
`ucli verify` v1 は Unity-local verifier である。cross-tool assurance aggregation は外部 supervisor の責務であり、uCLI は外部 tool の finding、verdict、coverage を再解釈しない。将来 profile extension を追加する場合も、外部 tool の report を uCLI の clean pass へ格上げする経路にしてはならない。

| Argument / Option | Short | Description |
| --- | --- | --- |
| `--profile <built-in:default\|built-in:mutation\|built-in:project\|built-in:script>` | - | built-in verify profile name。未指定時は `built-in:default` |
| `--profilePath <path?>` | - | verify profile file path |
| `--from <path?>` | - | 直前の uCLI mutation result JSON。`touched` と `readPostcondition` から post-read claim を生成する |
| `--projectPath <string?>` | `-p` | 対象 Unity project path |
| `--mode <auto\|daemon\|oneshot>` | - | Unity execution mode |
| `--timeout <int>` | - | profile 全体の timeout milliseconds |

verify flow の決定源は `verify profile` と `--from` だけである。自然言語の作業説明、エージェント判断、ログ文言 scraping で step を増減してはならない。

`--profile` は built-in profile だけを選ぶ。`--profilePath` は user-authored profile file だけを選ぶ。`--profile` と `--profilePath` の同時指定は `INVALID_ARGUMENT` とする。

profile 未指定時は `built-in:default` を使う。`built-in:default` は次の verifier を使う。

- `ready`
- `compile`
- `postRead`: `--from` があり、`readPostcondition` または `opResults[].applied` / `changed` / `touched` から post-read claim が必要な場合だけ実行する
- `test`: 既定では実行しない
- `logs`: failed または indeterminate claim がある場合だけ evidence として読む

`built-in:default` は `compile` を含むため、AssetDatabase refresh、script compilation、domain reload を起こし得る。これらの実効副作用は `payload.verifiers[].effects[]` に machine-readable に返す。

v1 の built-in profile 名は次の通り固定する。

| Profile | Verifiers | Meaning |
| --- | --- | --- |
| `built-in:default` | `ready`、`compile`、必要時 `postRead`、失敗時 `logs` | profile 未指定時の標準 project-level verification |
| `built-in:mutation` | `ready`、必要時 `postRead`、失敗時 `logs` | Scene / Prefab / Asset mutation 後の軽量 verification。`compile` は含めない |
| `built-in:project` | `ready`、`compile`、必要時 `postRead`、失敗時 `logs` | project-level verification。`built-in:default` と同じ verifier set |
| `built-in:script` | `ready`、`compile`、失敗時 `logs` | C# script 変更後の compile-focused verification |

profile 名は verify surface の一部であり、挙動変更は profile identity、profile digest、review surface の変更として扱う。`payload.profile.digest` は profile source、profile name、profile path、effective steps、verifier-specific args を含む canonical digest とする。

profile の step kind は closed enum とし、v1 は `ready`、`compile`、`test`、`postRead`、`logs` だけを許可する。uCLI は profile の宣言順ではなく、次の canonical order に正規化して実行する。

1. `ready`
2. `compile`
3. `postRead`
4. `test`
5. `logs`

profile は次を許可しない。

- 任意 shell
- 未知 step
- failed claim の pass 格上げ
- partial / indeterminate の full coverage 格上げ
- 外部 tool の実行
- 外部 report ingest
- 外部 finding / verdict / coverage の再解釈

profile step の入力は `kind`、`required`、verifier-specific args だけを正本とする。`effects[]` は profile author の申告値ではなく、uCLI が verifier kind と args から計算する effective value である。profile が `effects[]` を書く場合、uCLI はそれを信用せず、計算した effective effects と完全一致しなければ `INVALID_ARGUMENT` とする。

成功時 payload は `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` に加えて、`profile` と `profileDigest` を返す。`profile` は `source`、`name`、`path`、`digest` を持ち、`profileDigest` は `profile.digest` と同じ値の短縮 projection とする。`verifiers[]` は実行された verifier ごとに `id`、`kind`、`deterministic`、`required`、`primaryClaims[]`、uCLI が計算した `effects[]`、`reportRef` を返す。各 claim は `required` と `verifierRef` を持ち、`verifierRef` は `verifiers[].id` を参照する。

`verify --from` は、`--from` の `payload.project.projectFingerprint` と現在解決した project fingerprint を照合する。一致しない場合は `PROJECT_FINGERPRINT_MISMATCH` の command failure とし、postRead claim を生成しない。
`--from` input を読めない、または v1 の verify input として扱えない場合は command failure とする。標準 code は次のとおりである。

- `VERIFY_INPUT_SCHEMA_UNSUPPORTED`
- `VERIFY_INPUT_PROTOCOL_VERSION_MISMATCH`
- `VERIFY_INPUT_COMMAND_UNSUPPORTED`
- `VERIFY_INPUT_PAYLOAD_INVALID`
- `VERIFY_INPUT_PROJECT_MISSING`
- `PROJECT_FINGERPRINT_MISMATCH`

`postRead` verifier は `--from` の `readPostcondition`、`opResults[]`、`postReadSource.steps[]` を読んで必要な claim を組み立てる。ローカル保存済み request は参照しない。`postRead` claim は少なくとも `PERSISTENCE_UNIT_TOUCHED`、`READ_SURFACE_SAFE`、`POST_MUTATION_OBSERVED` の3種へ分ける。

`PERSISTENCE_UNIT_TOUCHED` は `changed=true` かつ永続化単位が期待される mutation で required claim とする。`READ_SURFACE_SAFE` は `readPostcondition.requirements[]` が存在する場合に required claim とする。`POST_MUTATION_OBSERVED` は edit DSL など、request から期待 post-state を決定論的に導ける場合だけ required claim とする。raw op や broad mutation など期待 post-state が定義されない場合は `outOfScope` または `required=false` とし、推測で required claim にしてはならない。

verify pass は、必須 claim がすべて `passed`、coverage が `full`、claim-local と payload-global のどちらにも `blocking=true` の residual risk が残らない場合だけ成立する。verifier が正常に実行されて fail を確認した場合は command failure ではなく verifier failure であり、`status=ok`、`payload.verdict=fail`、非 0 `exitCode` として返す。
`payload.verdict=pass` は Unity-local assurance の pass であり、外部 supervisor の reviewless green 十分条件ではない。外部 supervisor は intent scope、外部静的解析、scenario profile、dangerous / probeOnly / outOfScope / unverified の有無、non-blocking residual risk の扱いを別に判定する。

### Assurance verdict

`ready`、`compile`、`verify` は command-specific payload に `verdict` を必ず返す。

| `payload.verdict` | Meaning | Exit code |
| --- | --- | --- |
| `pass` | 必須 claim がすべて `passed`、coverage が `full`、claim-local と payload-global のどちらにも `blocking=true` の residual risk がない | `0` |
| `fail` | verifier が正常実行され、必須 claim の不成立を確認した | `1` |
| `incomplete` | 必須 claim に `partial`、`indeterminate`、`unverified`、`outOfScope`、または `coverage=none` が残る | `1` |

`status=error` は command 自体が成立しなかった状態であり、既存 failure kind の終了コードに従う。

### `ucli codes`

`ucli codes` は、公開 JSON 契約に現れる open code set の静的意味を返す語彙台帳である。code value は error、diagnostic、reasonCode、claim、risk の種別を問わず uCLI 全体で一意でなければならない。`kind` は分類と list filter のための metadata であり、code identity ではない。

対象 kind は次のとおりである。

| Kind | 対象 |
| --- | --- |
| `error` | `errors[].code` |
| `diagnostic` | `diagnosis.primaryDiagnostic.code` など診断根拠に近い code |
| `reason` | 予約済み。v1 標準 payload は `reasonCode` field を emit しない |
| `claim` | `claims[].id` |
| `risk` | `residualRisks[].code` |

`codes` は operation name、closed enum、JSON Schema field、Unity compiler diagnostics の正本にはならない。`lifecycleState`、`blockingReason`、`startupBlockingReason`、`retryDisposition`、`diagnosis.reason`、`fallbackReason` のような lowerCamel の分類値は codes 対象外である。v1 では `reason` kind は予約済みであり、標準 payload は `reasonCode` field を emit しない。将来、機械判定用の reason を codes 対象にする場合は、field 名を `reasonCode` とし、value は global unique な uppercase snake case code にし、その field path を `codes describe` の `appearsIn[]` に固定する。operation の正本は `ops describe`、compiler diagnostics の正本は Unity / compiler の diagnostic artifact である。

Code catalog は次の不変条件を持つ。

- `code` は catalog 内で global unique とする。
- code value は検索用の安定 machine token とし、uCLI 定義の code は uppercase snake case を基本形とする。claim の読みやすい主張は `claims[].statement` に置く。
- 同じ `code` を複数 field に出してよいのは、どの field でも静的意味が完全に同じ場合だけである。
- 意味が異なる場合は `kind` で分けず、code value 自体を分ける。
- code rename は breaking change として扱う。
- uCLI の JSON は bare `code` を返す。tool 横断の canonical identity は上位 supervisor が `ucli:<CODE>` のような `tool:code` として扱う。

#### `codes list`

| Option | Short | Description |
| --- | --- | --- |
| `--kind <string?>` | - | kind exact match filter |
| `--command <string?>` | - | 関連 command の exact / dot segment family match filter |

成功時 payload は `catalogVersion`、`source`、`kinds[]`、`codes[]` を返す。`codes[]` の各要素は `code`、`kind`、`category`、`summary` を持つ。

#### `codes describe`

| Argument / Option | Short | Description |
| --- | --- | --- |
| `<CODE>` | - | 説明対象の code。種別を問わず uCLI 全体で一意 |
| `<KIND:CODE>` | - | 期待 kind の検証付き alias。code が存在しても kind が一致しない場合は `INVALID_ARGUMENT` |
| `--requireKnown` | - | 未知 code を `INVALID_ARGUMENT` として失敗させる |

`describe` は静的な code 意味論だけを返す。run 固有の結論、実際のログ抜粋、source code の中身、pass へ格上げする条件は返さない。未知 code は既定で成功し、`known=false` と generic fallback descriptor を返す。

## 実行系コマンド共通規則

### 出力契約の参照先
- 公開 CLI 出力の種別、`CommandResult`、内部 IPC との関係は [uCLI.md](uCLI.md) を正本とする。
- 共通フィールドの shape は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。
- command-specific payload schema は top-level `command` で決まる。JSON Schema は構造検証だけを担い、`payload.verdict` の再計算、`claims[].verifierRef`、`primaryClaims[]`、`evidenceRef` / `reportRef` の解決などの保証整合性は semantic invariant validator と Golden tests で検証する。

### `--failFast`
- `--failFast` は CLI option のみであり、JSON request や `.ucli/config.json` には入れない。
- readiness gate を持つ実行系コマンドは、既定で `starting`, `busy`, `compiling` の解消を待ってから実行する。
- `domainReloading` は AppDomain reload を跨いで要求を再開しないため、既定でも待機せず `EDITOR_DOMAIN_RELOADING` を返す。
- `--failFast` 指定時だけ `lifecycleState != ready` を即時エラーとして返す。
- `blockedByModal`, `safeMode`, `playmode`, `shuttingDown` は待機中でも即時失敗する。
- `--allowPlayMode` 付きの `ucli plan` / `ucli call` は、GUI Editor session の `playmode` に限り Play Mode 変更として実行できる。この option は mutation request 用の明示ガードであり、`query` / `resolve` / `validate` / `ops` には適用しない。
- batchmode Editor session が観測・返却する非 ready 状態は `starting`, `busy`, `compiling`, `domainReloading`, `playmode`, `shuttingDown`。GUI Editor session は `blockedByModal` と `safeMode` も返す。
- 待機は既存の `--timeout` budget を消費し、budget を使い切った場合は `IPC_TIMEOUT` を返す。
- `ucli ops list` / `ucli ops describe` では live source fallback に対してのみ意味を持つ。readIndex hit では readiness wait を行わない。
- `ucli query assets find` では readIndex hit 時に Unity 接続も readiness wait も行わない。`ucli query scene tree` は daemon に dirty loaded scene があるかだけを軽く確認し、dirty loaded scene が見つかった場合は live source を優先する。live source fallback または Unity 専用 query では `--failFast` を IPC に渡す。
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
- Play Mode 変更専用エラー
  - `PLAYMODE_NOT_ACTIVE`
  - `PLAYMODE_REQUIRES_GUI_EDITOR`
  - `PLAYMODE_PERSISTENCE_FORBIDDEN`
- Play Mode 制御専用エラー
  - `PLAYMODE_SESSION_NOT_AVAILABLE`
  - `PLAYMODE_TRANSITION_TIMEOUT`
  - `PLAYMODE_TRANSITION_BLOCKED`
  - `PLAYMODE_ALREADY_CHANGING`
  - `PLAYMODE_ENTER_REJECTED`
  - `PLAYMODE_EXIT_REJECTED`
  - `PLAYMODE_STATE_UNKNOWN`
- daemon Editor mode エラー
  - `DAEMON_EDITOR_MODE_MISMATCH`
- Unity project lock エラー
  - `UNITY_PROJECT_ALREADY_OPEN`: 対象 project を開いている live Unity process が確認できる
  - `UNITY_PROJECT_LOCK_AMBIGUOUS`: `Temp/UnityLockfile` はあるが、所有 process の有無を安全に判定できない
  - `UNITY_PROJECT_LOCK_CLEANUP_FAILED`: stale lock と判定できたが、`Temp/UnityLockfile` の削除に失敗した
- operation contract エラー
  - `OPERATION_CONTRACT_VIOLATION`: operation の実行結果が `ops describe` の `assurance` facts と矛盾した。`status=error`、`errors[].opId` に該当 step id、`payload.contractViolations[]` に矛盾 details を返し、`applicationState=notApplied` 以外は再試行安全性を保証しない
- timeout
  - 既定待機の timeout も既存の `IPC_TIMEOUT` を使用する。

### `Temp/UnityLockfile` cleanup timing
- uCLI は `Temp/UnityLockfile` を定期的または background で掃除しない。
- uCLI が `Temp/UnityLockfile` の削除を試みるのは、Unity process を新規起動する直前の preflight と、uCLI が起動した Unity process の終了後 cleanup の2つに限定する。
- 起動直前の preflight で stale と断定できた場合は `Temp/UnityLockfile` を削除して起動を続行する。active lock、ambiguous lock、cleanup failed は起動を止め、上記の Unity project lock エラーを返す。
- 終了後 cleanup で stale と断定できた場合は `Temp/UnityLockfile` を削除する。この cleanup の成否だけで timeout、cancel、abnormal exit、artifact missing などの主エラー分類を変更しない。
- `logs unity read`、`logs daemon read`、`daemon status`、既存 daemon への IPC は Unity process を新規起動しない reader 経路であり、`Temp/UnityLockfile` を作成・削除しない。

### 失敗分類と終了コード
公開 CLI JSON は、失敗分類にかかわらず既存の共通エンベロープを返す。`status`、`exitCode`、`message`、`errors[]` の field shape は変わらない。内部失敗分類は、次の規則で既存の終了コードへ投影する。

| Failure kind | 代表例 | `exitCode` |
| --- | --- | --- |
| `InvalidInput` | option 不正、JSON request 不正、static validation failure | `3` |
| `ConfigurationError` | `.ucli` 設定不備、解決不能な設定値 | `3` |
| `EnvironmentError` | 実行環境の前提不足 | `4` |
| `UnityIpcFailure` | Unity IPC 応答失敗、lifecycle failure、daemon failure | `4` |
| `ExternalProcessFailure` | 外部プロセス起動・実行失敗 | `4` |
| `ContractViolation` | IPC 応答や内部契約の不整合、operation result と assurance facts の矛盾 | `4` |
| `Timeout` | IPC timeout、readiness wait timeout | `4` |
| `Canceled` | 実行キャンセル | `4` |
| `InternalError` | 予期しない内部障害 | `4` |

`ucli test run` だけはテスト実行結果を追加で区別し、テスト失敗は `exitCode=1`、Unity テスト実行基盤の失敗は `exitCode=2` を返す。機械判定用の詳細は終了コードだけでなく `errors[].code` を正とし、未知の `errors[].code` は汎用失敗として扱う。

## `ucli daemon`
デーモンの起動、停止、安全な残骸掃除、状態確認、登録一覧取得を行う管理コマンド。

### `daemon` 共通 options（`start` / `stop` / `cleanup` / `status` / `list`）
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |

### `daemon start` options
| Option | Short | Description |
| --- | --- | --- |
| `--editorMode <string?>` | - | `batchmode` or `gui`。未指定時は既存 running session、対象 project の既存 GUI Editor、batchmode 起動の順に選ぶ |
| `--onStartupBlocked <string?>` | - | `auto`、`keep`、`terminate`。endpoint 登録前に起動ブロックを検出したときの process 扱い。既定は `auto` |

`--onStartupBlocked=auto` は、user-owned GUI と CLI-owned GUI では `keep`、CLI-owned batchmode では `terminate` として扱う。user-owned GUI は `terminate` を明示しても終了対象にしない。

### `daemon start` Editor mode 契約
- `--editorMode=batchmode` は Unity を `-batchmode -nographics` で起動する。
- `--editorMode=gui` は既に開かれている対象 project の GUI Editor endpoint へ接続し、endpoint が未登録なら同じ GUI process の session 登録完了まで待機する。対象 GUI Editor が存在しない場合だけ GUI Editor を起動する。
- 既存 GUI Editor に接続した session は `ownerKind=user`、`canShutdownProcess=false` とする。CLI が新規起動した GUI Editor は `ownerKind=cli` とし、process 終了まで管理できる場合だけ `canShutdownProcess=true` とする。
- `--editorMode` 未指定時は既存 running session の editorMode を優先し、session が無くても対象 project の GUI Editor process を検出できる場合は GUI Editor session として接続待ちする。どちらも無い場合だけ `batchmode` を起動する。
- 明示した `--editorMode` と既存 running session または検出済み GUI Editor process の editorMode が一致しない場合は `DAEMON_EDITOR_MODE_MISMATCH` を返す。
- `daemon start` の成功は endpoint と session の登録完了を意味し、`lifecycleState=ready` を保証しない。成功 payload は `lifecycleState`、`canAcceptExecutionRequests`、`blockingReason` の snapshot を返す。
- `daemon stop` は `session.canShutdownProcess=false` の GUI Editor session では Unity process を終了せず、endpoint / session 登録と session token を無効化する。
- endpoint 登録前の起動観測、startup blocker、`--onStartupBlocked` の process policy は [daemon-startup-lifecycle.md](daemon-startup-lifecycle.md) を正とする。

### `daemon start` Unity Editor 解決
- `daemon start` が Unity process を新規起動する場合、`--editorMode=batchmode` / `--editorMode=gui` のどちらでも同じ Unity Editor path resolver を使う。
- Editor executable は `ProjectSettings/ProjectVersion.txt` の `m_EditorVersion` から Unity version を解決し、既定の Unity install search roots で一致する Editor を探索する。
- Editor executable を解決できない場合は `INVALID_ARGUMENT` を返す。
- 既存 GUI Editor へ attach する場合は Editor path resolver を使わない。対象 process の同一性は session probe、`Library/EditorInstance.json`、`projectFingerprint` で確定する。

### 既存 GUI Editor 検出
- 既存 GUI Editor 検出は valid GUI session probe と対象 project 配下の `Library/EditorInstance.json` を使う。
- project 同一性は endpoint probe の `projectFingerprint` と marker path で確定し、process 名、Unity version、最近開いた project 履歴だけを根拠に attach しない。
- `Library/EditorInstance.json` で GUI Editor process を検出したが uCLI endpoint が未登録の場合、同じ process の session 登録完了まで `--timeout` budget 内で待機しつつ startup blocker を観測する。
- 分類できる startup blocker は `DAEMON_STARTUP_BLOCKED` と diagnosis を返す。分類不能のまま timeout した場合だけ `IPC_TIMEOUT` と endpoint 未登録 diagnosis を返す。
- `--editorMode=batchmode` で既存 GUI Editor process を検出した場合は `DAEMON_EDITOR_MODE_MISMATCH` を返す。

### GUI session 保証境界
- GUI Editor session は物理 `UnityProjectRoot` 単位の project lifecycle lock に参加するが、同じ GUI Editor 内の手動操作は排他できない。
- `query` / `resolve` / `plan` は selection、active Scene、Prefab Stage、dirty state、Undo stack に観測由来の変更を残してはならない。
- 観測由来の Editor state を復元できない場合、その `query` / `resolve` / `plan` は成功として扱わない。

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
- `daemon start` の startup failure payload は `payload.blockingReason` を返さず、`payload.startup.startupBlockingReason` と `payload.retryDisposition` を返す。
- `daemon start` の startup observation と process 起動/終了判断は、対象 physical `UnityProjectRoot` の project lifecycle lock の下で直列化する。
- `payload` のフィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `daemon` のエラー契約
- `INVALID_ARGUMENT`
  - `--timeout` が空文字、空白、非数値、`0` 以下
  - `--projectPath` が Unity プロジェクトとして解決不能
  - `--editorMode` が `batchmode` / `gui` 以外
  - 新規 Unity process 起動時に Editor executable を解決できない
  - `ucli daemon list` の対象 project が Git worktree 配下にない
- `DAEMON_EDITOR_MODE_MISMATCH`
  - running session または検出済み GUI Editor process の `editorMode` と `daemon start --editorMode` の明示値が一致しない
- `DAEMON_STARTUP_BLOCKED`
  - endpoint 登録前に Safe Mode、compile error、package resolution failure、plugin dependency 欠落、precompiled assembly conflict などの起動ブロックを分類できた
- `DAEMON_START_PROCESS_EXITED`
  - endpoint 登録前に Unity process が終了し、session が成立しなかった
- `IPC_TIMEOUT`
  - `daemon start` が endpoint 登録前の状態を分類できないまま timeout budget を使い切った
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
ucli daemon start --projectPath ./UnityProject --editorMode gui
ucli daemon start --projectPath ./UnityProject --editorMode batchmode --onStartupBlocked terminate
ucli daemon stop --projectPath ./UnityProject --timeout 5000
ucli daemon cleanup --projectPath ./UnityProject
ucli daemon status --projectPath ./UnityProject
ucli daemon list --projectPath ./UnityProject
```

## `ucli logs`
ログ取得と GUI Editor の Unity Console 表示クリアのコマンド。`ucli logs unity read`、`ucli logs daemon read`、`ucli logs unity clear` を提供する。

### `logs` 出力契約
- `ucli logs unity read` と `ucli logs daemon read` の成功時は共通エンベロープを返さず、`stdout` にログイベントを逐次出力する。
- `--format json` は NDJSON とし、1イベントを1行のJSONオブジェクトで出力する。
- `--format text` は1イベントを1行のテキストで出力する。
- `--stream` 未指定時は取得条件に一致する範囲を出力して終了する。
- `--stream` 指定時は終了条件（`Ctrl+C`、`--idleTimeoutMilliseconds` 到達、`--until` 到達）まで継続出力する。
- 入力検証エラーなど、ストリーム開始前に失敗した場合は共通エンベロープの `status=error` を1件返して終了する。
- `ucli logs unity clear` の成功時は `command=logs.unity.clear`、共通エンベロープの `status=ok` を1件返す。

### `logs read` 共通 options（`unity read` / `daemon read`）
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

### `logs unity read` options
| Option | Short | Description |
| --- | --- | --- |
| `--source <string?>` | - | `compile` / `runtime` / `all` |
| `--stackTrace <string?>` | - | `none` / `error` / `all` |
| `--stackTraceMaxFrames <int?>` | - | スタックトレースの最大フレーム数（`1..512`） |
| `--stackTraceMaxChars <int?>` | - | スタックトレースの最大文字数（`256..131072`） |

### `logs daemon read` options
| Option | Short | Description |
| --- | --- | --- |
| `--category <string?>` | - | `lifecycle` / `ipc` / `auth` / `transport` / `health` / `all` |

### `logs unity clear` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |

### `logs` オプション規則
- `--after` と `--since` を同時指定した場合は `--after` を優先する。
- `--since` は `2026-03-05T10:30:00+09:00` や `2026-03-05T01:30:00Z` の形式を受け付ける。
- `--since` と `--until` を同時指定する場合は `since <= until` を必須とする。
- `--queryTarget=stack` と `--stackTrace=none` を同時指定した場合、stack 検索にはヒットしない。
- `ucli logs daemon read` で `--queryTarget=stack` を指定した場合は `INVALID_ARGUMENT` とする。
- `--stackTrace=none` の場合、`--stackTraceMaxFrames` と `--stackTraceMaxChars` は無効化される。
- `--category` は `ucli logs daemon read` でのみ指定可能とする。
- `--stream` はサーバープッシュではなく、`nextCursor` を使った増分ポーリングで実装する。
- `--pollIntervalMilliseconds` は `--stream` 指定時のみ有効とする。
- `--idleTimeoutMilliseconds` は `--stream` 指定時のみ有効とし、無通信時間が閾値を超えた時点で正常終了する。
- `--stream` と `--until` を同時指定した場合、`until` 到達時に正常終了する。
- `--format=json` は `--stream` 有無にかかわらず NDJSON を出力する。
- `ucli logs unity clear` は GUI Editor の Unity Console 表示を消す操作であり、daemon log、Unity log stream、`.ucli` 配下の物理ログファイルは削除しない。
- `ucli logs unity clear` は `--projectPath` と `--timeout` 以外の `logs` 取得 options を受け付けない。
- `ucli logs unity clear` の成功時 `payload` は `clearStatus` と `timeoutMilliseconds` を返す。field 定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する。

### `logs` 実行例
```bash
ucli logs unity read --projectPath ./UnityProject --tail 200 --level error --source runtime
ucli logs unity read --projectPath ./UnityProject --after "<cursor>" --stream --format text
ucli logs daemon read --projectPath ./UnityProject --since "2026-03-05T00:00:00+09:00" --format json
ucli logs daemon read --projectPath ./UnityProject --stream --pollIntervalMilliseconds 500 --idleTimeoutMilliseconds 60000 --category ipc
ucli logs unity read --projectPath ./UnityProject --since "2026-03-05T09:00:00+09:00" --until "2026-03-05T10:00:00+09:00"
ucli logs unity clear --projectPath ./UnityProject
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
- selector は exactly one とし、`stdin` は受け付けない。
- `--scene --hierarchyPath` かつ `--componentType` なしの場合、scene-tree-lite readIndex で解決できれば Unity IPC へ接続しない。
- `--globalObjectId`、asset 系 selector、`--componentType` 付き scene selector、prefab selector、readIndex miss は Unity IPC `execute(command=resolve)` へ fallback する。
- Unity fallback は `Validate -> Plan` を実行し、`planToken` は発行しない。
- `--failFast` は Unity fallback 時だけ適用する。readIndex hit では Unity readiness wait を行わない。

### `resolve` のレスポンス契約
- 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返す。
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

## `ucli query`
`ucli query` は検索・構造取得・スキーマ取得を型付きサブコマンドで実行する読取コマンドである。CLI は JSON request を読まず、サブコマンドと flags から固定の primitive operation 1 step を組み立てる。

### `query` 共通 options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--readIndexMode <string?>` | - | `disabled`, `allowStale`, or `requireFresh` |
| `--failFast` | - | Unity fallback または Unity 専用 query で `ready` になる前なら待機せず即失敗する |

### `query` サブコマンド
| Command | 固定 operation | Options |
| --- | --- | --- |
| `ucli query assets find` | `ucli.assets.find` | `--type <string?>` / `--pathPrefix <string?>` / `--nameContains <string?>` の 1 つ以上、`--limit <int?>`、`--after <string?>`、`--all` |
| `ucli query scene tree` | `ucli.scene.tree` | `--path <string>`、`--depth <int?>`、`--fullDepth`、`--limit <int?>`、`--after <string?>`、`--all` |
| `ucli query go describe` | `ucli.go.describe` | `--globalObjectId <string>` または `--scene <path> --hierarchyPath <path>` または `--prefab <path> --hierarchyPath <path>`、`--depth <int?>`、`--fullDepth` |
| `ucli query comp schema` | `ucli.comp.schema` | `--type <string>` |
| `ucli query asset schema` | `ucli.asset.schema` | `--type <string>` または `--globalObjectId <string>` または `--assetGuid <string>` または `--assetPath <path>` または `--projectAssetPath <path>` |

### `query` 実行契約
- `assets find` は readIndex lookup を優先し、必要時だけ live Unity source へ fallback する。
- `scene tree` は対象 scene が Unity daemon に dirty loaded state として存在する場合、その live state を readIndex より優先する。dirty loaded state がない場合は readIndex lookup を使い、必要時だけ live Unity source へ fallback する。
- `go describe`、`comp schema`、`asset schema` は Unity IPC `execute(command=query)` へ委譲し、Unity 側では `Validate -> Plan` を実行する。`planToken` は発行しない。
- `scene tree` の既定 depth は `1`、`go describe` の既定 depth は `0` とする。`--fullDepth` は depth を `null` として渡す。
- `scene tree` の source 優先順位は request-local temporary scene、loaded scene、persisted preview scene、readIndex の順とする。asset path を持たない完全未保存 scene は `--path` 契約の対象外とする。
- `--fullDepth` と `--depth` は同時指定できない。`--depth` は `0` 以上とする。
- `--all` は `--limit` / `--after` と同時指定できない。
- bounded window は command/query layer と primitive operation の両方で適用する。raw `kind:"op"` の `ucli.assets.find` と `ucli.scene.tree` も `limit` と `cursor` を受け付け、既定 `limit=100`、最大 `10000` とする。明示 opt-in なしに全件を stdout payload へ返してはならない。

### `query` のレスポンス契約
- 成功時 payload は `project`、`requestId`、`opResults`、`readIndex` を返す。
- `command` はサブコマンドごとに `query.assets.find`、`query.scene.tree`、`query.go.describe`、`query.comp.schema`、`query.asset.schema` のいずれかを返す。
- `assets find` の結果は `opResults[0].result.matches[]` と `opResults[0].result.window` に置く。
- `scene tree` の結果は `opResults[0].result.path`、`opResults[0].result.roots[]`、`opResults[0].result.sourceState`、`opResults[0].result.window` に置く。`sourceState.kind` は `temporaryScene`、`loadedScene`、`persistedPreview`、`readIndex` のいずれかで、`sourceState.isDirty` は読み取り時点の dirty state を示す。
- `window` は `limit`、`cursor`、`nextCursor`、`isComplete`、`totalCount` を返す。`after` は CLI option 名だけで使い、JSON result field には出さない。
- readIndex 完結時は `payload.readIndex.source=index`、`used=true` を返す。Unity fallback または Unity 専用 query では `source=unity`、`used=false` を返す。

### `query` 実行例
```bash
ucli query assets find --projectPath ./UnityProject --type "UnityEngine.Material, UnityEngine.CoreModule" --limit 100
ucli query scene tree --projectPath ./UnityProject --path Assets/Scenes/Main.unity --depth 1
ucli query go describe --projectPath ./UnityProject --scene Assets/Scenes/Main.unity --hierarchyPath Root/Player --fullDepth
ucli query comp schema --projectPath ./UnityProject --type "UnityEngine.Transform, UnityEngine.CoreModule"
ucli query asset schema --projectPath ./UnityProject --assetGuid 11111111111111111111111111111111
```

## `ucli plan`
`ucli plan` は最初の公開 request-driven execute コマンドである。  
CLI は JSON リクエストを redirected `stdin` から読み、static preflight を行ったうえで Unity IPC `execute(command=plan)` を 1 回だけ送る。

### `plan` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--readIndexMode <string?>` | - | `disabled`, `allowStale`, or `requireFresh` |
| `--allowPlayMode` | - | GUI Editor session の Play Mode 中に変更 plan を許可する |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `plan` 実行契約
- JSON リクエストは redirected `stdin` からのみ読む。
- ユーザー入力 JSON のトップレベルは `steps` のみを受け付ける。`protocolVersion` と `requestId` は CLI が Unity IPC 送信前に生成する。
- `payload.readIndex` は Unity 実行経路ではなく、Unity IPC `plan` 実行前の static preflight で readIndex をどう再利用したかを表す。
- `--readIndexMode=disabled` は validate と同じ syntax-only preflight に縮退し、`payload.readIndex` は `used=false`、`hit=false`、`source=index`、`freshness=probable`、`fallbackReason="readIndex disabled by mode."` を返す。
- `--readIndexMode=allowStale` は snapshot 欠落時に syntax-only preflight へ縮退し、Unity IPC `plan` を継続する。
- `--readIndexMode=requireFresh` は snapshot 欠落・破損・非 fresh なら Unity IPC 前に失敗する。
- `--allowPlayMode` と明示 `--readIndexMode` の併用は `INVALID_ARGUMENT` とし、`--readIndexMode` 未指定時だけ実効 readIndex mode を `disabled` とする。
- Unity へ送る execute request は `command=plan` とし、`IpcExecuteRequest.FailFast` に `--failFast` をそのまま写像する。request 側の `planToken` は常に送らない。
- `--allowPlayMode` 指定時だけ、GUI Editor session かつ `lifecycleState=playmode` の Play Mode 変更 plan を許可する。
- Play Mode 変更 plan は `kind:"edit"` と `on.scene` / `on.prefab` / `on.asset` / `on.project` の step だけを受け付ける。Scene context は `commit:"none"` のみ許可し、Scene asset へ保存しない。Scene context の `commit:"none"` は Scene 保存を行わない指定であり、`applyPrefabOverrides` による対象 Prefab asset 保存とは矛盾しない。Prefab / asset / project context は通常の `edit` と同じ commit 契約で扱う。`commit:"project"` は project-wide save であるため Play Mode 変更では拒否する。
- Scene context の Prefab instance override を Prefab asset へ反映する場合は、`applyPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` action を明示する。Prefab asset 値へ戻す場合は `revertPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` action を明示する。
- `applyPrefabOverrides` は Scene context から明示 Prefab asset へ保存する secondary persistence action であり、Scene asset は保存しない。
- `applyPrefabOverrides` / `revertPrefabOverrides` の `targetAssetPath` は既存の `Assets/.../*.prefab` で、current target の Prefab instance lineage / valid target chain に含まれる必要がある。
- `applyPrefabOverrides` / `revertPrefabOverrides` は、同一 edit step / 同一 current target の先行 `set` が effective changed にした exact property path だけを対象にする。`propertyPaths` 省略時は対象 path 全部、指定時は subset だけを許可する。`propertyPaths: []`、重複 path、先行 `set` に由来しない path、effective changed でない path、parent path 指定による child property 対象化は `INVALID_ARGUMENT` で拒否する。
- 同一 property を複数回 `set` した場合は最終 effective value だけを対象にし、最終値が pre-request 値と同じなら対象外にする。
- `revertPrefabOverrides` は同一 step の先行 `set` に由来する request-attributed override だけを Prefab asset 値へ戻し、pre-request 時点ですでに存在した override は拒否する。`applyPrefabOverrides` は pre-request override であっても、同一 step の先行 `set` が exact path を effective changed にした場合だけ許可する。
- apply / revert は全対象 property を preflight 検証してから実行する。検証エラーでは action 全体を適用せず、Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない。
- Play Mode 変更では raw `kind:"op"` を許可せず、Prefab apply / revert primitive は `edit` lowering から発生した場合だけ許可する。
- Play Mode 変更の Prefab context は opened stage を要求せず、runtime が対象 Prefab asset を編集用 context として開ける。
- Play Mode 変更 plan は Play Mode の live object を正本とし、readIndex を対象解決や scene / prefab / asset / project 観測に使わない。`--readIndexMode` 未指定時の `payload.readIndex` は `used=false`、`source=unity`、`fallbackReason="Play Mode mutation uses live Unity state."` を返す。
- `--mode` / `--timeout` が不正な場合でも、request parse と static preflight が完了していれば失敗 payload に `requestId` と `readIndex` を残す。

### `plan` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- 成功時 payload は `project`、`requestId`、`opResults`、`readIndex`、`planToken` を返す。
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
ucli plan --projectPath ./UnityProject --readIndexMode allowStale <<'JSON'
{"steps":[]}
JSON

ucli plan --projectPath ./UnityProject --mode daemon --failFast <<'JSON'
{"steps":[]}
JSON

ucli plan --projectPath ./UnityProject --mode daemon --allowPlayMode < playmode-mutation.json
```

## `ucli eval`
`ucli eval` は `ucli.cs.eval` を実行する convenience command である。  
CLI は source 入力から内部 request を組み立て、`call --withPlan` 相当の経路で Unity IPC `plan` と `call` を順に実行する。

### `eval` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--allowDangerous` | - | `ucli.cs.eval` の実行を明示許可する |
| `--allowPlayMode` | - | GUI Editor session の Play Mode 中に変更 call を許可する |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |
| `--source <string?>` | - | 評価する C# source |
| `--file <string?>` | - | 評価する C# source file path |

### `eval` 実行契約
- `ucli.cs.eval` は引き続き public raw `kind:"op"` の `mutation` / `dangerous` operation であり、`eval` はその専用 CLI フロントエンドである。
- source は `--source`、`--file`、または redirected stdin から読む。redirected stdin は `--source` と `--file` がどちらも無い場合だけ source として扱う。
- `--source` は shell history、process list、CI log に残り得るため、短い非機密 snippet 用とする。秘密情報を含む source は redirected stdin または権限制御した `--file` から渡す。
- `--source` と `--file` の併用は `INVALID_ARGUMENT` とする。
- source が未指定で stdin が redirect されていない場合、または読み取った source が空白のみの場合は `INVALID_ARGUMENT` とする。
- `--file` は absolute path または current working directory からの relative path を受け付ける。
- `--allowDangerous` が無い場合、既存 `call` の dangerous operation guard により `OPERATION_NOT_ALLOWED` で失敗する。
- CLI は `kind:"op"`、`id:"eval"`、`op:"ucli.cs.eval"`、`args.source` を持つ内部 request を生成する。
- source kind は CLI で指定しない。Unity 側が `compilationUnit` から `snippet` へ自動判定し、実際の form は `opResults[].result.sourceKind` に返す。
- `--plan`、`--withPlan`、`--planToken`、`--kind`、`--raw` は受け付けない。
- CLI は常に pre-plan を実行し、その結果を `payload.plan` に同梱する。発行された plan token は同一コマンド内の後続 call に転送する。

### `eval` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- 成功時 payload は `call` と同じ shape で、`project`、`requestId`、`opResults`、`plan` を返す。
- eval の戻り値は `payload.opResults[].result.returnValue` に格納される。

### `eval` の終了コード
| Code | Meaning |
| --- | --- |
| `0` | 成功 |
| `3` | 入力不正、static validation failure、dangerous operation guard failure |
| `4` | IPC timeout、lifecycle failure、daemon/tool/internal failure |

### `eval` 実行例
```bash
ucli eval --projectPath ./UnityProject --allowDangerous --source 'return UnityEngine.Application.unityVersion;'

ucli eval --projectPath ./UnityProject --mode daemon --allowDangerous --file ./eval.cs

ucli eval --projectPath ./UnityProject --allowDangerous <<'CS'
context.DeclareNoTouchedResources();
return new { ok = true };
CS
```

## `ucli call`
`ucli call` は request-driven execute コマンドであり、CLI は JSON リクエストを redirected `stdin` から読み、static preflight と dangerous operation 判定を行ったうえで Unity IPC `execute(command=call)` を送る。

### `call` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--planToken <string?>` | - | 既存 `plan` 実行で取得した plan token |
| `--withPlan` | - | `call` 前に CLI が `plan` を実行し、その結果を `payload.plan` に同梱する |
| `--allowDangerous` | - | `dangerous` operation の実行を明示許可する |
| `--allowPlayMode` | - | GUI Editor session の Play Mode 中に変更 call を許可する |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `call` 実行契約
- JSON リクエストは redirected `stdin` からのみ読む。
- ユーザー入力 JSON のトップレベルは `steps` のみを受け付ける。`protocolVersion` と `requestId` は CLI が Unity IPC 送信前に生成する。
- `--readIndexMode` は受け付けない。
- `dangerous` operation は、設定上許可されていても `--allowDangerous` が無ければ `OPERATION_NOT_ALLOWED` で失敗する。
- `--allowDangerous` は dangerous policy の gate だけを開き、edit lowering 専用 operation の exposure gate は解除しない。
- `kind:"edit"` step の dangerous 判定は、public DSL を lower した primitive operation 群に対して行う。
- `--withPlan` 指定時、CLI は Unity IPC `execute(command=plan)` を先に 1 回送り、その結果を `payload.plan` に保持する。
- `--planToken` 未指定で `--withPlan` が plan token を発行した場合、CLI はその token を後続の `call` request に転送する。
- `--planToken` 指定時はユーザー指定値を優先し、`payload.plan` は表示用としてのみ保持する。
- `call` 全体の timeout budget は 1 本であり、`--withPlan` 時は pre-plan と call で残り時間を順に消費する。
- `--allowPlayMode` 指定時だけ、GUI Editor session かつ `lifecycleState=playmode` の Play Mode 変更 call を許可する。
- Play Mode 変更 call は `kind:"edit"` と `on.scene` / `on.prefab` / `on.asset` / `on.project` の step だけを受け付ける。Scene context は `commit:"none"` のみ許可し、Scene asset へ保存しない。Scene context の `commit:"none"` は Scene 保存を行わない指定であり、`applyPrefabOverrides` による対象 Prefab asset 保存とは矛盾しない。Prefab / asset / project context は通常の `edit` と同じ commit 契約で扱い、明示 `commit` に従って保存できる。`commit:"project"` は project-wide save であるため Play Mode 変更では拒否する。
- Scene context の Prefab instance override を Prefab asset へ反映する場合は、`applyPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` action を明示する。Prefab asset 値へ戻す場合は `revertPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` action を明示する。
- `applyPrefabOverrides` は Scene context から明示 Prefab asset へ保存する secondary persistence action であり、Scene asset は保存しない。
- `applyPrefabOverrides` / `revertPrefabOverrides` の `targetAssetPath` は既存の `Assets/.../*.prefab` で、current target の Prefab instance lineage / valid target chain に含まれる必要がある。
- `applyPrefabOverrides` / `revertPrefabOverrides` は、同一 edit step / 同一 current target の先行 `set` が effective changed にした exact property path だけを対象にする。`propertyPaths` 省略時は対象 path 全部、指定時は subset だけを許可する。`propertyPaths: []`、重複 path、先行 `set` に由来しない path、effective changed でない path、parent path 指定による child property 対象化は `INVALID_ARGUMENT` で拒否する。
- 同一 property を複数回 `set` した場合は最終 effective value だけを対象にし、最終値が pre-request 値と同じなら対象外にする。
- `revertPrefabOverrides` は同一 step の先行 `set` に由来する request-attributed override だけを Prefab asset 値へ戻し、pre-request 時点ですでに存在した override は拒否する。`applyPrefabOverrides` は pre-request override であっても、同一 step の先行 `set` が exact path を effective changed にした場合だけ許可する。
- apply / revert は全対象 property を preflight 検証してから実行する。検証エラーでは action 全体を適用せず、Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない。
- Play Mode 変更では raw `kind:"op"` を許可せず、Prefab apply / revert primitive は `edit` lowering から発生した場合だけ許可する。
- Play Mode 変更の Prefab context は opened stage を要求せず、runtime が対象 Prefab asset を編集用 context として開ける。Prefab / asset / project の保存は対象永続化単位に限定し、open Scene を巻き込む一括 project save は使わない。
- Play Mode 変更 call は Play Mode の live object を正本とし、readIndex を対象解決や scene / prefab / asset / project 観測に使わない。

### `call` のレスポンス契約
- 出力は共通の `CommandResult` エンベロープを返す。
- 成功時 payload は `project`、`requestId`、`opResults`、必要時のみ `readPostcondition`、`postReadSource`、`plan` を返す。
- `payload.plan` は `project`、`requestId`、`opResults`、必要時のみ `planToken` を返す。
- `payload.readIndex` は返さない。
- Scene context の Play Mode 変更成功時、Prefab apply / revert を含まない場合の `opResults[].touched` は空配列になり、`readPostcondition` は返さない。`applyPrefabOverrides` で Prefab asset へ反映した場合は、その Prefab asset を `touched` に返し、必要な `readPostcondition` を返す。`revertPrefabOverrides` は Scene live object だけを戻すため、`touched` は空配列、`readPostcondition` は返さない。Prefab / asset / project context の保存を伴う Play Mode 変更は、通常の永続化変更と同じく保存した永続化単位を `touched` に返し、必要な `readPostcondition` を返す。
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
ucli call --projectPath ./UnityProject --planToken "<token>" <<'JSON'
{"steps":[]}
JSON

ucli call --projectPath ./UnityProject --withPlan --allowDangerous --mode daemon --failFast <<'JSON'
{"steps":[]}
JSON

ucli call --projectPath ./UnityProject --mode daemon --allowPlayMode < playmode-mutation.json
```

## `ucli refresh`
`ucli refresh` は独立コマンドであり、未公開の request 系 CLI surface の別名ではない。  
CLI は内部で固定の標準 `execute` リクエストを組み立て、Unity 側の既存 `ucli.project.refresh` に流す。`ucli.project.refresh` は `command` kind の標準 operation として扱う。

### `refresh` options
| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | 対象Unity project root path |
| `--mode <string?>` | - | `auto` (default), `daemon`, or `oneshot` |
| `--timeout <int?>` | - | IPC待機タイムアウト（ミリ秒）。`1..2147483647` |
| `--failFast` | - | `ready` になる前なら待機せず即失敗する |

### `refresh` 実行契約
- `stdin` は読まない。
- `--planToken` / `--withPlan` / `--readIndexMode` は指定できない。
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
- 成功時 payload は `project`、`requestId`、`opResults`、必要時のみ `readPostcondition`、`postReadSource` を返す。
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
