# Daemon Startup Lifecycle

この文書は `ucli daemon start` の起動 lifecycle 正本である。command option と実行例は [uCLI-command-reference.md](uCLI-command-reference.md)、公開 JSON field の定義は [uCLI-property-reference.md](uCLI-property-reference.md)、NuGetForUnity を含む依存復元の運用手順は [package-operations.md](package-operations.md) を正とする。

## 基本契約
`daemon start` は Unity process を起動するだけのコマンドではなく、uCLI endpoint と session token を持つ検証可能な Unity Editor session を確立する操作である。

- 成功は endpoint 登録、session 永続化、session token の発行が完了した状態を意味する
- 成功時でも `lifecycleState = ready` は保証しない
- 成功 payload は endpoint 登録後の lifecycle snapshot を返す
- session 確立前に Unity startup が止まった場合は、daemon session は成立していないものとして扱う

`lifecycleState` は endpoint 登録後に Unity runtime が返す状態である。endpoint 登録前の起動観測は `startup`、`lastLaunchAttempt`、`diagnosis.startupPhase` で表し、`lifecycleState` に endpoint 未登録状態を混ぜない。

`payload.blockingReason` と `payload.startup.startupBlockingReason` は別の語彙である。`payload.blockingReason` は endpoint 登録済み session の lifecycle blocker を表し、`payload.startup.startupBlockingReason` は endpoint 登録前の startup blocker を表す。startup failure payload に `payload.blockingReason` を出してはならない。

## Startup Observation
`daemon start` は Unity process 起動から endpoint 登録までを startup observation phase として観測する。観測対象は Unity process、`Library/EditorInstance.json`、session artifact、Unity Editor log、package resolution log、起動 attempt metadata である。

startup observation、launch attempt artifact 作成、process policy 適用は、対象 physical `UnityProjectRoot` の project lifecycle lock の下で実行する。`launchAttemptId` は lock 取得前に発行してよいが、Unity process 起動、artifact commit、process 終了判断は lock 取得後に行う。同じ physical `UnityProjectRoot` を対象にする `daemon start`、oneshot、`test.run` は、worktree、storage root、`projectFingerprint` が異なっていても同じ lifecycle lock に参加する。

startup observation は次の状態を区別する。

| 状態 | 意味 |
| --- | --- |
| `none` | 直近の起動 attempt がない |
| `launching` | Unity process 起動前後の準備中 |
| `waitingForEndpoint` | process は観測できるが uCLI endpoint は未登録 |
| `blocked` | endpoint 登録前に高信頼の起動ブロックを検出した |
| `timeout` | 分類不能のまま timeout budget を使い切った |
| `failed` | process 起動失敗、process exit、artifact 書き込み失敗などで継続できない |
| `completed` | endpoint と session 登録が完了した |

`blocked` は timeout の別名ではない。Safe Mode、compile error、package resolution、plugin dependency 欠落など、待機を続けても endpoint 登録が見込めない状態を検出した場合に使う。

`completed` は endpoint と session 登録が完了した successful launch attempt の内部記録にだけ使う。`daemon start` failure payload と `daemon status.lastLaunchAttempt` では `completed` を返してはならない。session が成立している場合、起動結果は success payload の session snapshot で表し、`lastLaunchAttempt` は `null` とする。

## Daemon Status During Startup Failure
`daemon start` failure payload の `daemonStatus` は、session artifact の状態を表す。今回の launch attempt が endpoint 登録前に `blocked`、`timeout`、`failed` になっただけでは `stale` としない。

| 状態 | 条件 |
| --- | --- |
| `notRunning` | 既存 session artifact がない、または今回の launch attempt で session が成立しなかった |
| `stale` | 既存 session artifact は存在するが、endpoint probe 失敗、process 不在、token invalid などにより再利用できない |

既存 stale session を検出した後に新規 launch attempt も失敗した場合、`daemonStatus=stale` を返してよい。この場合でも `startup` は今回の launch attempt を表し、既存 stale session の詳細は `diagnosis` または cleanup 対象として扱う。

## Startup Blocker
endpoint 登録前に起動が止まった場合、分類できるものは `IPC_TIMEOUT` ではなく `DAEMON_STARTUP_BLOCKED` として返す。`IPC_TIMEOUT` は、高信頼の blocker を分類できないまま timeout した場合の fallback である。

代表的な blocker は次の通り。

| 分類 | 代表 code | diagnosis reason | retryDisposition |
| --- | --- | --- | --- |
| Safe Mode prompt | `DAEMON_STARTUP_BLOCKED` | `editorUserActionRequired` | `manualActionRequired` |
| Safe Mode または script compile error | `DAEMON_STARTUP_BLOCKED` | `unityScriptCompilationFailed` | `retryAfterFix` |
| Unity package resolution failure | `DAEMON_STARTUP_BLOCKED` | `unityPackageResolutionFailed` | `retryAfterFix` |
| uCLI plugin dependency 欠落 | `DAEMON_STARTUP_BLOCKED` | `ucliPluginDependencyMissing` | `retryAfterFix` |
| uCLI plugin compile failure | `DAEMON_STARTUP_BLOCKED` | `ucliPluginCompileFailed` | `retryAfterFix` |
| precompiled assembly conflict | `DAEMON_STARTUP_BLOCKED` | `precompiledAssemblyConflict` | `retryAfterFix` |
| endpoint 登録前の process exit | `DAEMON_START_PROCESS_EXITED` | `unityEditorExitedBeforeBootstrap` | `unknown` |
| 分類不能 timeout | `IPC_TIMEOUT` | `guiEndpointNotRegistered` または `endpointNotRegistered` | `unknown` |

NuGetForUnity は現在の Unity plugin 配布経路の一つであり、uCLI plugin 起動失敗全体の代表 reason にはしない。`NUGET_FOR_UNITY_RESTORE_FAILED` は NuGetForUnity 固有ログを検出できた場合だけ、`primaryDiagnostic.code` などの詳細診断で表す。missing DLL、compile error、precompiled assembly conflict だけを根拠に NuGetForUnity 固有失敗と断定しない。

`DAEMON_STARTUP_BLOCKED` は広い代表 code であるため、実際の復帰判断は failure payload の `retryDisposition`、`safeToRetryImmediately`、`diagnosis.reason`、`diagnosis.primaryDiagnostic` を優先する。`retryDisposition` は `retryImmediately`、`waitThenRetry`、`retryAfterFix`、`manualActionRequired`、`doNotRetry`、`unknown` のいずれかを返す。

`safeToRetryImmediately` は `retryDisposition=retryImmediately` のときだけ `true` とする。`waitThenRetry`、`retryAfterFix`、`manualActionRequired`、`doNotRetry`、`unknown` では常に `false` とする。

| retryDisposition | 意味 | 代表例 |
| --- | --- | --- |
| `retryImmediately` | 何も修正せず即時再試行できる | stale marker cleanup 済み、process 起動前の一時的 lock acquisition failure |
| `waitThenRetry` | 待機後に再試行できる | endpoint 未登録だが process が正常起動中で blocker signal がない観測状態 |
| `retryAfterFix` | project または依存関係を修正してから再試行する | compile error、package resolution failure、uCLI plugin dependency 欠落、precompiled assembly conflict |
| `manualActionRequired` | GUI または外部操作が必要 | GUI Safe Mode prompt、modal dialog |
| `doNotRetry` | 同じ入力では再試行しても成功しない | Unity executable 解決失敗、project path invalid、permission hard failure |
| `unknown` | 安全な再試行判断ができない | 分類不能 timeout、endpoint 登録前 process exit |

final `daemon start` failure payload では、原則として `waitThenRetry` を返さない。`daemon start` は endpoint 登録完了、blocker 検出、timeout、process exit、preflight failure のいずれかまで待つ entry stream なしの command であるため、途中観測状態は最終 failure として返さない。`waitThenRetry` は `daemon status.lastLaunchAttempt` 以外の進行中 observation、将来の watch/diagnose 系 command、または timeout 前に観測途中状態を返す command のために予約する。

複数 signal が同時に検出された場合、`startupBlockingReason` は次の優先順位で 1 つに正規化する。より具体的で復帰行動が限定される分類を優先し、残りの根拠は `secondaryDiagnostics` または `detectedSignals` に残す。

1. `modalDialog` / GUI Safe Mode prompt requiring user action
2. `precompiledAssemblyConflict`
3. `ucliPlugin`
4. `packageResolution`
5. `compile`
6. `processExit`
7. `endpointNotRegistered`
8. `unknown`

GUI Safe Mode prompt と compile error が同時に見える場合、GUI で人間操作が必要なら `startupBlockingReason=safeMode`、`diagnosis.reason=editorUserActionRequired`、`retryDisposition=manualActionRequired` を返す。batchmode または headless 起動で compile error が主因なら `startupBlockingReason=compile`、`diagnosis.reason=unityScriptCompilationFailed`、`retryDisposition=retryAfterFix` を返す。

代表的な検出 signal は次の通りである。実装はこの表を基準に `detectedSignals[]` と `primaryDiagnostic` を構成する。

| Signal | startupBlockingReason | diagnosis.reason | primaryDiagnostic.kind | confidence |
| --- | --- | --- | --- | --- |
| Safe Mode prompt / `Entering Safe Mode` | `safeMode` | `editorUserActionRequired` | `unityDialog` | `high` |
| `Scripts have compiler errors` | `compile` | `unityScriptCompilationFailed` | `compiler` | `high` |
| UPM package resolve failure | `packageResolution` | `unityPackageResolutionFailed` | `packageResolution` | `high` |
| NuGetForUnity restore failure log | `packageResolution` | `unityPackageResolutionFailed` | `packageResolution` with `code=NUGET_FOR_UNITY_RESTORE_FAILED` | `high` |
| missing `MackySoft.Ucli.Contracts` or `MackySoft.Ucli.Infrastructure` | `ucliPlugin` | `ucliPluginDependencyMissing` | `pluginDependency` | `medium` |
| `Multiple precompiled assemblies with the same name` | `precompiledAssemblyConflict` | `precompiledAssemblyConflict` | `compiler` | `high` |
| process exit before endpoint registration | `processExit` | `unityEditorExitedBeforeBootstrap` | `processExit` | `high` |

## Process Policy
起動ブロックを検出したとき、process を終了するかどうかは `ownerKind`、`editorMode`、`canShutdownProcess`、`--onStartupBlocked` で決める。

| 起動形態 | 既定動作 |
| --- | --- |
| user-owned GUI | keep。uCLI は Unity process を終了しない |
| CLI-owned GUI | keep。GUI は人間が Safe Mode や modal を解消する場として残す |
| CLI-owned batchmode | terminate。証拠を保存してから graceful shutdown を試み、必要なら force kill する |

`--onStartupBlocked=auto` は上記の既定に従う。`keep` は process を残す。`terminate` は `canShutdownProcess=true` の CLI-owned process だけを終了対象にする。user-owned GUI は option に関わらず終了対象にしない。

## Diagnosis And Artifacts
起動 attempt は `.ucli/local/fingerprints/<projectFingerprint>/launch-attempts/<launchAttemptId>/` 配下へ記録する。保存対象は bounded な Unity log snapshot、UPM/package resolution log、launch metadata、process metadata、startup diagnosis JSON である。

diagnosis は次の用途を持つ。

- `daemon start` の失敗 payload で、起動がどの phase で止まったかを返す
- `daemon status` の `lastLaunchAttempt` で、直近の session 未成立 startup failure を再取得できるようにする
- agent と CI が「再試行」ではなく「compile error 修正」「package 復元」「GUI Safe Mode 解消」を選べるようにする

artifact は process cleanup とは独立して保持する。CLI-owned batchmode を終了しても、診断に必要なログと metadata は削除しない。

diagnosis は粗い `reason` と具体的な `primaryDiagnostic.code` を分ける。`reason` は `unityPackageResolutionFailed` や `ucliPluginDependencyMissing` のような復帰カテゴリを表し、`primaryDiagnostic.code` は `NUGET_FOR_UNITY_RESTORE_FAILED` のような検出根拠に近い詳細 code を表す。検出根拠は `detectedSignals` と `topErrors` に残し、agent が message scraping に依存しなくても復帰判断できるようにする。

`errors[].code` は command failure の代表 code であり、`diagnosis.primaryDiagnostic.code` は診断根拠に近い詳細 code である。`primaryDiagnostic.code` は `errors[].code` と同じ open code set の値を使ってよいが、必ず `errors[]` にも載るとは限らない。agent は command outcome の分類には `errors[].code` と `retryDisposition` を使い、復帰カテゴリの詳細化には `diagnosis.reason` と `diagnosis.primaryDiagnostic` を使う。

## Artifact Retention
launch attempt artifact は project fingerprint ごとに直近 20 件を保持する。`daemon cleanup` は stale session と安全に削除できる古い launch attempt を削除できるが、直近失敗 diagnosis は明示 option がない限り残す。cleanup が launch attempt artifact を削除した場合、payload は `deletedLaunchAttemptCount` を返す。

artifact 書き込み失敗は startup blocker とは別の失敗であり、分類結果の有無で扱いを分ける。

- blocker 分類前に artifact 書き込み失敗だけが発生した場合は `startupStatus=failed`、`startupBlockingReason=unknown`、`artifactPath=null`、`retryDisposition=unknown` を返す
- blocker 分類後に artifact 永続化へ失敗した場合は、分類済み blocker を維持して `startupStatus=blocked` を返し、artifact failure は `secondaryDiagnostics` に `code=STARTUP_ARTIFACT_WRITE_FAILED` として載せる

分類済み blocker がある場合、artifact 永続化失敗だけを理由に主診断を捨ててはならない。

## Shared Startup Observation
この文書は `daemon start` の payload semantics を定義する。startup blocker の分類器、artifact model、process observation は Unity process を起動する他の command でも共有する。

- `daemon start` は session 未成立の `startup`、`lastLaunchAttempt`、`diagnosis` として投影する
- oneshot 実行系 command は request 未成立の startup failure として各 command の payload に投影してよい
- `test.run` は test runner 未起動の startup failure または infrastructure failure として投影してよい

他 command が同じ startup diagnosis を使う場合でも、stdout contract は各 command の最終 `CommandResult` envelope を維持する。

## Contract Test Requirements
startup lifecycle の契約は golden file または contract tests で固定する。

- startup failure payload に `payload.blockingReason`、`payload.lifecycleState`、`payload.canAcceptExecutionRequests` を出さない
- startup failure payload と `daemon status.lastLaunchAttempt` に `startupStatus=completed` を出さない
- `safeToRetryImmediately` は `retryDisposition=retryImmediately` のときだけ `true` になる
- final `daemon start` failure payload では `retryDisposition=waitThenRetry` を返さない
- 複数 blocker signal が同時に出た場合、`startupBlockingReason` は定義済み優先順位に従う
- startup observation、artifact commit、process policy は project lifecycle lock の下で直列化する
- endpoint 登録済みで `lifecycleState=compiling` の場合は `daemon start` を成功として返す
- user-owned GUI は `--onStartupBlocked=terminate` でも process を終了しない
- CLI-owned batchmode は hard blocker 時に artifact 保存後 terminate policy を適用する
- NuGetForUnity 固有ログがある場合だけ `primaryDiagnostic.code=NUGET_FOR_UNITY_RESTORE_FAILED` を返す
- missing DLL だけでは NuGetForUnity 固有失敗と断定しない
- 分類済み blocker と artifact 書き込み失敗が同時に起きた場合、blocker 情報を維持して artifact failure を secondary diagnostic にする

## Output Contract
`daemon start` は entry stream を持たず、stdout には最終 `CommandResult` を 1 件だけ出力する。Unity 起動進行、blocker 検出、log tail 要約は stderr に出してよいが、stderr を機械判定の正本にしない。

長い起動を観測する利用者は `daemon status` の `lastLaunchAttempt` と `logs` を併用する。
