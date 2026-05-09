> [!IMPORTANT]
> この文書は、uCLI の公開 CLI 出力契約、内部 IPC 契約、`payload`、status、readIndex catalog のプロパティ定義をまとめたリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、コマンドの option table と実行例は [uCLI-command-reference.md](uCLI-command-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。

## 公開 CLI 出力契約

公開 CLI 出力の種別と意味は [uCLI.md](uCLI.md) を正本とする。ここでは `request-response` 型で使う JSON shape だけを定義する。
公開 CLI JSON の固定対象は専用 JSON writer、command output DTO、Golden files であり、この文書は property の意味と参照先を説明する。

### request-response 型の共通エンベロープ
`request-response` 型の CLI JSON 出力は、次の共通エンベロープを返す。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `protocolVersion` | integer | yes | プロトコルメジャーバージョン |
| `command` | string | yes | コマンド識別子。例: `test.run` |
| `status` | `ok \| error` | yes | 実行成否 |
| `exitCode` | integer | yes | プロセス終了コード |
| `message` | string | yes | 人間向け説明 |
| `payload` | object | yes | コマンド固有結果 |
| `errors` | array | yes | CLI エラー配列。正常時は空配列 |

`requestId` は CLI 共通エンベロープには含めない。必要な場合だけ各コマンドの `payload` に含める。

### CLI エラーオブジェクト

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | 機械判定用エラーコード |
| `message` | string | yes | 説明 |
| `opId` | `string \| null` | yes | 該当実行単位の `id`。該当なしは `null` |

`code` は open code set である。既知コード一覧にない値でも JSON 契約上は有効であり、利用側は未知値を汎用失敗として扱う。C# 契約では機械判定用エラーコードを `UcliErrorCode` で扱い、既知コードは責務別の typed code definition として定義する。JSON wire shape は文字列のままとする。

内部の失敗分類は CLI エンベロープへ投影される診断モデルであり、公開 JSON field は追加しない。利用側は引き続き `status`、`exitCode`、`message`、`errors[].code`、`errors[].message`、`errors[].opId` だけを読めばよい。

## 内部 IPC 契約

### `IpcResponse`
CLI と Unity runtime の間では、共通 CLI エンベロープではなく次の IPC エンベロープを使用する。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `protocolVersion` | integer | yes | IPC プロトコルメジャーバージョン |
| `requestId` | string | yes | IPC リクエスト相関 ID |
| `status` | `ok \| error` | yes | IPC 実行成否 |
| `payload` | object | yes | IPC method-specific payload |
| `errors` | array | yes | IPC エラー配列。正常時は空配列 |

ここでの `status` は IPC 層の成否である。

### `IpcExecuteResponse`
`execute` 系 IPC の `payload` は次の構造を返す。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `opResults` | array | yes | 各 public step の結果 |
| `planToken` | string | no | `plan` 実行時に発行されたトークン。未発行時は field 自体を省略する |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |

### `IpcExecuteResponse.opResults[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `opId` | string | yes | 対応する実行単位の `id` |
| `op` | string | yes | 実行単位名。`kind:"edit"` step では `"edit"` を返す |
| `phase` | `validate \| plan \| call \| skipped` | yes | 実行フェーズ |
| `applied` | boolean | yes | step 内のどこかが適用済みか |
| `changed` | boolean | yes | step 内のどこかに実変更があったか |
| `touched` | array | yes | 影響した永続化単位の配列 |
| `result` | object | no | `kind:"op"` step の operation 固有結果本体。`edit` と `UcliNoResult` operation では返さない |

`result` は operation ごとの Result contract 型に対応する。`phase`、`applied`、`changed`、`touched`、`errors` は envelope 側の情報であり、`result` の中には含めない。

#### `IpcExecuteResponse.opResults[].touched[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `scene \| prefab \| asset \| projectSettings` | yes | 永続化単位の種別 |
| `path` | string | yes | プロジェクトルート相対パス |
| `guid` | string | no | 取得可能な場合に付与する GUID |

#### Play Mode 変更時の `IpcExecuteResponse.opResults[]`
`--allowPlayMode` 付きの Play Mode 変更では、Play Mode の live object を変更対象とする。

- `applied` / `changed` は live object への適用結果を表す。
- Scene context の `touched` は永続化単位を表さないため、Prefab apply / revert を含まない場合は空配列を返す。
- `changed = true` かつ `touched = []` は、Play Mode の live object は変わったが永続化単位は保存していない状態を表す。
- Scene context では Prefab apply / revert を含まない場合、`readPostcondition` は返さない。
- Scene context の `applyPrefabOverrides` で Prefab asset へ反映した場合は、その Prefab asset を `touched` に返し、必要な `readPostcondition` を返す。
- Scene context の `revertPrefabOverrides` は Scene live object だけを戻すため、`touched` は空配列、`readPostcondition` は返さない。
- Prefab apply / revert の preflight 検証エラーでは action 全体を適用せず、成功扱いの `touched` / `readPostcondition` は返さない。
- Prefab apply / revert の Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない。
- Prefab / asset / project context の保存を伴う変更では、通常の永続化変更と同じく保存した永続化単位を `touched` に返し、必要な `readPostcondition` を返す。
- `result` は通常の `edit` step と同じく省略する。
- `--readIndexMode` 未指定時の `plan` の `payload.readIndex` は `used=false`、`source=unity`、`fallbackReason="Play Mode mutation uses live Unity state."` を返す。

#### `IpcExecuteResponse.readPostcondition`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requirements` | array | yes | stale 化した read surface ごとの safe 条件 |

#### `IpcExecuteResponse.readPostcondition.requirements[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `surface` | `assetSearch \| guidPath \| sceneTreeLite` | yes | 対象 read surface |
| `scenePath` | string | no | `surface = sceneTreeLite` のときだけ使用する。指定時は normalize 済み scene path、未指定時は全 Scene が対象 |
| `minSafeGeneratedAtUtc` | string | yes | この時刻以降に生成された readIndex だけを safe とみなす UTC 時刻 |

matching requirement がある場合、safe 判定は `payload.readIndex.generatedAtUtc >= minSafeGeneratedAtUtc` を満たすか、または live Unity 再観測であることを要求する。`generatedAtUtc` 欠落または requirement より古い readIndex は unsafe であり、live fallback が必要である。matching requirement が無い surface にだけ既存の `freshness` 契約を適用する。

#### `IpcExecuteResponse.opResults[].result` for `ucli.assets.find`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `matches` | array | yes | `assetPath` の ordinal 昇順で並ぶ一致結果。未一致時は空配列 |

`ucli.assets.find` の `touched` は常に空配列である。

#### `IpcExecuteResponse.opResults[].result` for `ucli.resolve`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `globalObjectId` | string | yes | 解決済み GlobalObjectId |

`ucli.resolve` の `touched` は常に空配列である。

##### `IpcExecuteResponse.opResults[].result.matches[]` for `ucli.assets.find`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetPath` | string | yes | `Assets/` から始まる persistent main asset path |
| `assetGuid` | string | yes | `AssetDatabase.AssetPathToGUID(assetPath)` の結果 |
| `name` | string | yes | main asset 名 |
| `typeId` | string | yes | main asset runtime type の stable `typeId` |

### `IpcError`
現在の `IpcError` は CLI の `errors[]` と同じ field shape を持つが、契約の層は別である。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | 機械判定用エラーコード |
| `message` | string | yes | 説明 |
| `opId` | `string \| null` | yes | 該当実行単位の `id`。該当なしは `null` |

`code` は CLI `errors[]` と同じ open code set の値を文字列として運ぶ。IPC 受信側は未知値を破棄せず保持する。

## lifecycle 関連プロパティ

### lifecycle status フィールド
`ucli status` と `ucli daemon status` は、必要に応じて次の lifecycle 関連 field を `payload` に含める。各コマンドが返す field の subset は、コマンド別 `payload` 契約を正とする。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `daemonStatus` | `running \| notRunning \| stale` | yes | daemon 到達性と session 状態 |
| `unityVersion` | `string \| null` | yes | `ProjectSettings/ProjectVersion.txt` 由来の Unity バージョン |
| `serverVersion` | `string \| null` | yes | `daemonStatus = running` のときのみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | `daemonStatus = running` のときのみ値を持つ。`gui` は Unity GUI Editor 内の uCLI endpoint を表す |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `daemonStatus = running` のときのみ値を持つ。batchmode Editor session は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset を返し、GUI Editor session は `blockedByModal` と `safeMode` も返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | 実行を止めている理由。batchmode Editor session は `startup / busy / compile / domainReload / playMode / shutdown` の subset を返し、GUI Editor session は `modalDialog` と `safeMode` も返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 専用の状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | domain reload 完了ごとに変化する opaque な識別子 |
| `canAcceptExecutionRequests` | boolean | yes | 通常実行要求を受け付けられるときのみ `true`。`--allowPlayMode` の例外可否は含めず、`lifecycleState = playmode` では `false` |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |

## コマンド別 `payload`

### `ucli daemon`
`ucli daemon` はサブコマンドにかかわらず `payload.timeoutMilliseconds` を常に含む。

#### `daemon start`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `startStatus` | `started \| alreadyRunning \| attached` | yes | 起動結果。`attached` は既存 GUI Editor session の endpoint に接続した状態 |
| `daemonStatus` | `running` | yes | daemon 状態 |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown` | yes | 起動または attach 直後の lifecycle snapshot。`daemon start` 成功は `ready` を保証しない |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | 起動または attach 直後に通常実行を止めている理由 |
| `canAcceptExecutionRequests` | boolean | yes | 起動または attach 直後に通常実行要求を受け付けられるか。Play Mode 例外は含めない |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | object | yes | セッション情報 |

`daemon start` の成功は endpoint と session の登録完了を意味し、`lifecycleState = ready` を保証しない。

#### `daemon stop`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `stopStatus` | `stopped \| notRunning` | yes | 停止結果。ユーザー起動 GUI Editor session では endpoint 登録解除までを `stopped` とする |
| `daemonStatus` | `notRunning` | yes | daemon 状態 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `null` | yes | 停止後は常に `null` |

ユーザー起動 GUI Editor session の `daemon stop` は Unity process を終了せず、endpoint / session 登録と session token を無効化する。旧 session token では再接続できない。

#### `daemon cleanup`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `cleanupStatus` | `completed \| skipped` | yes | cleanup 結果 |
| `skipReason` | `null \| running \| unsafeInvalidSession \| uncertainReachability` | yes | cleanup を見送った理由 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |

#### `daemon status`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `daemonStatus` | `running \| notRunning \| stale` | yes | daemon 状態 |
| `serverVersion` | `string \| null` | yes | `running` のときのみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | `running` のときのみ値を持つ |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `running` のときのみ値を持つ。batchmode Editor session は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset を返し、GUI Editor session は `blockedByModal` と `safeMode` も返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | `running` のときのみ意味を持つ。batchmode Editor session は `startup / busy / compile / domainReload / playMode / shutdown` の subset を返し、GUI Editor session は `modalDialog` と `safeMode` も返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | `running` のときのみ値を持つ |
| `canAcceptExecutionRequests` | boolean | yes | 通常実行要求を受け付けられるときのみ `true`。`--allowPlayMode` の例外可否は含めず、`playmode` では `false` |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `object \| null` | yes | `running` / `stale` は object、`notRunning` は `null` |
| `diagnosis` | `object \| null` | yes | 保存済みまたは推定された diagnosis |

#### `daemon list`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `timeoutMilliseconds` | integer | yes | コマンド全体に適用する実効タイムアウト |
| `projectRelativePath` | string | yes | Git worktree root から見た対象 Unity project の相対パス |
| `isComplete` | boolean | yes | 対象 worktree 群を最後まで観測できたか |
| `completionReason` | `null \| timeout` | yes | 不完全終了の理由 |
| `remainingWorktreeCount` | integer | yes | 未観測の worktree 件数 |
| `items` | array | yes | 永続化済み daemon 登録の一覧 |

#### `daemon session`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `projectFingerprint` | string | yes | project fingerprint |
| `issuedAtUtc` | string | yes | 発行時刻 |
| `editorMode` | `batchmode \| gui` | yes | Editor mode |
| `ownerKind` | `cli \| user` | yes | session 所有者種別。`user` はユーザーが起動済みの GUI Editor を表す |
| `canShutdownProcess` | boolean | yes | `daemon stop` が Unity process を終了してよいか。ユーザー起動 GUI Editor session では `false` |
| `endpointTransportKind` | string | yes | IPC transport 種別 |
| `endpointAddress` | string | yes | IPC endpoint |
| `processId` | `integer \| null` | yes | daemon process ID |

#### `daemon diagnosis`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `reason` | string | yes | 診断理由 |
| `message` | string | yes | 人間向け説明 |
| `reportedBy` | `unity \| cli` | yes | 診断報告元 |
| `isInferred` | boolean | yes | CLI が後から推定して合成した診断か |
| `updatedAtUtc` | string | yes | 更新時刻 |
| `processId` | `integer \| null` | yes | 関連 process ID |
| `editorInstancePath` | `string \| null` | yes | 関連する `Library/EditorInstance.json` の path。該当なしは `null` |

既存 GUI Editor process を検出したが endpoint 登録が timeout まで完了しない場合、`IPC_TIMEOUT` の diagnosis は `reason = guiEndpointNotRegistered`、`reportedBy = cli`、`isInferred = true` とし、`editorInstancePath` と `processId` を返す。

#### `daemon list items[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `worktreePath` | string | yes | Git worktree root path |
| `branchRef` | `string \| null` | yes | attached 時の branch ref |
| `head` | string | yes | worktree の HEAD commit hash |
| `projectPath` | string | yes | Unity project root path |
| `projectFingerprint` | string | yes | worktree ローカル storage root で解決した fingerprint |
| `state` | `running \| stale \| error` | yes | item の状態 |
| `reason` | `null \| staleSession \| invalidSession \| probeTimeout \| probeFailed` | yes | item 状態の理由 |
| `issuedAtUtc` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `processId` | `integer \| null` | yes | valid session を読めた場合のみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | valid session を読めた場合のみ値を持つ |
| `ownerKind` | `cli \| user \| null` | yes | valid session を読めた場合のみ値を持つ |
| `canShutdownProcess` | `boolean \| null` | yes | valid session を読めた場合のみ値を持つ |
| `endpointTransportKind` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `endpointAddress` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `diagnosis` | `object \| null` | yes | `running` は `null` |

### `ucli init`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `configPath` | string | yes | 生成した `.ucli/config.json` の絶対パス |
| `gitignorePath` | string | yes | 生成した `.ucli/.gitignore` の絶対パス |

### `ucli refresh`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 内部 `execute` リクエストの `requestId` |
| `opResults` | array | yes | `ucli.project.refresh` の `call` 結果 |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |

### `ucli resolve`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 内部 `execute(command=resolve)` または index 解決単位の `requestId` |
| `opResults` | array | yes | `ucli.resolve` の `plan` 結果。成功時は単一要素 |
| `readIndex` | object | yes | 最終結果の観測元。shape は `payload.readIndex` を参照 |

### `ucli ops list`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `operations` | array | yes | 利用可能な operation の一覧。絞り込み条件に該当しない場合は空配列 |
| `readIndex` | object | yes | catalog / detail の観測元。shape は `payload.readIndex` を参照 |

#### `ucli ops list payload.operations[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | operation 名 |
| `kind` | string | yes | `query`、`command`、または `mutation` |
| `policy` | string | yes | `safe`、`advanced`、または `dangerous` |

`ucli ops list` は一覧と絞り込みに必要な `name` / `kind` / `policy` だけを返す。`description`、`inputs`、`resultContract`、`assurance`、`argsSchema`、`resultSchema` は含めない。operation の詳細契約は `ucli ops describe <opName>` を参照する。

### `ucli ops describe`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `operation` | object | yes | 対象 operation の詳細 |
| `readIndex` | object | yes | catalog / detail の観測元。shape は `payload.readIndex` を参照 |

`ucli ops describe` は指定した単一 operation の detail を返す。readIndex の永続化形式が list descriptor と describe detail に分かれていても、公開 payload の shape は変えない。

#### `ucli ops describe payload.operation`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | operation 名 |
| `kind` | string | yes | `query`、`command`、または `mutation`。`command` は Editor 状態や AssetDatabase 状態を変えるが、永続化対象の内容変更を主目的にしない |
| `policy` | string | yes | `safe`、`advanced`、または `dangerous` |
| `description` | string | yes | operation の目的、使いどころ、注意点 |
| `inputs` | array | yes | ユーザー入力から `steps[].args` を組み立てるための主契約。shape は `payload.operation.inputs[]` を参照 |
| `resultContract` | object | yes | `opResults[].result` の有無、Result contract 型名、読み方。shape は `payload.operation.resultContract` を参照 |
| `codeContract` | object | no | source code を受け取る operation の entry point 署名、uCLI が提供する source-visible API、戻り値制約。shape は `payload.operation.codeContract` を参照 |
| `assurance` | object | yes | 副作用と plan / touched の保証情報。shape は `payload.operation.assurance` を参照 |
| `argsSchema` | object | yes | `steps[].args` の JSON 構造検証用 JSON Schema |
| `resultSchema` | object \| null | yes | `opResults[].result` の JSON 構造検証用 JSON Schema。`UcliNoResult` operation では `null` |

`argsSchema` / `resultSchema` は検証用 schema であり、agent 向けの主契約ではない。operation 選択、`args` の組み立て、結果解釈は `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` を参照する。source code を受け取る operation の C# API は `codeContract` を参照する。schema には説明文や意味制約を置かない。

#### `ucli ops describe payload.operation.inputs[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | input 名。通常は対応する args property 名 |
| `description` | string | yes | 入力値の意味 |
| `valueType` | string | yes | JSON 値型。`string`、`boolean`、`integer`、`number`、`object`、`array` のいずれか |
| `constraints` | array | yes | 入力値の意味制約。shape は `payload.operation.inputs[].constraints[]` を参照 |
| `argsPath` | string | no | 対応する `steps[].args` 内 JSON path。省略時は `$.<name>` |
| `variants` | array | no | selector / reference の表現方法。shape は `payload.operation.inputs[].variants[]` を参照 |

`valueType` は IPC JSON の値型だけを表す。C# contract では `SceneAssetPath`、`PrefabAssetPath`、`UnityHierarchyPath`、`UnityGlobalObjectId`、`UnityAssetGuid`、`UnityTypeId` などの semantic value type を使えるが、`ops describe` ではそれらも JSON wire shape に合わせて `valueType:"string"` として出す。意味の違いは型名をそのまま外へ出すのではなく、`description` と `constraints` に展開する。

必須性は `argsSchema.required` で表す。optional input は `argsSchema.required` に含めない。`inputs[]` には `required` field を持たせない。

`argsPath` は例外用である。input 名と args property 名が一致する通常ケースでは省略する。root object 全体を 1 input として扱う場合や、input 名と JSON property 名が一致しない場合だけ指定する。

`constraints` は常に出す。意味制約がない input は `constraints: []` とする。

`argsPath` と `variants[].fields[].argsPath` は JSONPath ではなく uCLI args path である。`inputs[].argsPath` の許可形は `$`、`$.property`、`$.property.nestedProperty` だけとする。`variants[].fields[].argsPath` は具体的な field path なので、許可形は `$.property`、`$.property.nestedProperty` だけとする。各 property segment は ASCII 英数字と `_` だけを使い、全体は 256 文字以内、最大 16 segment とする。配列添字、wildcard、filter、quoted property name は扱わない。`var` segment は request-local alias 用の内部 branch なので、public `ops describe` には出さない。`variants[].fields[].argsPath` は同じ input の `argsPath`、または省略時の `$.<name>` と同じ path か、その descendant path でなければならない。

`argsPath` を指定する例:

```json
{
  "name": "patch",
  "valueType": "object",
  "description": "Serialized property values to apply.",
  "constraints": [
    { "kind": "serializedProperty", "access": "write" }
  ],
  "argsPath": "$.values"
}
```

この例では agent-facing input 名は `patch` だが、実際の `steps[].args` では `values` property を組み立てる。

#### `ucli ops describe payload.operation.inputs[].variants[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | variant 名 |
| `description` | string | yes | この表現方法の意味 |
| `fields` | array | yes | この variant を成立させるために埋める field 群。shape は `payload.operation.inputs[].variants[].fields[]` を参照 |

variant は operation の意味差ではなく、同じ input を表現する方法だけを表す。同一 `input` 内の `variants` は相互排他である。operation の `description` が複数のユーザー意図を説明する場合、その operation は分割対象である。

variant を選ぶ場合、agent はその `variants[].fields[]` に列挙された field をすべて埋める。variant 固有の optional field は `fields[]` には含めず、`argsSchema` の optional property と operation / input の `description` で表す。

path と constraint は field object に閉じ込める。variant 直下に constraint を置かない。

#### `ucli ops describe payload.operation.inputs[].variants[].fields[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | variant 内 field 名 |
| `argsPath` | string | yes | この field が対応する `steps[].args` 内 path |
| `description` | string | yes | field 値の意味 |
| `constraints` | array | yes | field 値の意味制約。shape は `payload.operation.inputs[].constraints[]` と同じ |

field の `constraints` は常に出す。意味制約がない field は `constraints: []` とする。

field の `name` は `argsPath` の最後の property segment と一致しなければならない。たとえば `argsPath:"$.target.globalObjectId"` の field 名は `globalObjectId` である。

#### `ucli ops describe payload.operation.inputs[].constraints[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | 制約の種類 |
| `min` | number | no | `kind:"range"` の下限 |
| `max` | number | no | `kind:"range"` の上限 |
| `assetKind` | string | no | `asset`、`scene`、`prefab`、または `projectSettings` |
| `targetKind` | string | no | `asset`、`gameObject`、または `component` |
| `typeKind` | string | no | `component` |
| `access` | string | no | `write` |

`constraints` は入力値の意味を表す機械判定用 contract である。自由文ではなく、定義済み `kind` と必要なパラメータで表す。

初期 vocabulary は次に限定する。

| kind | Parameters | Meaning |
| --- | --- | --- |
| `nonEmpty` | none | 空文字、空配列、空 object を受け付けない |
| `range` | `min?`, `max?` | 数値が指定範囲に入る。下限だけ、上限だけ、両方を同じ kind で表す |
| `projectRelativePath` | none | Unity project 相対 path として扱う |
| `assetExists` | `assetKind` | 指定 kind の既存 asset を指す |
| `assetCreatable` | `assetKind` | 指定 kind の asset 作成先として使える |
| `globalObjectId` | none | Unity GlobalObjectId として解決できる |
| `assetGuid` | none | Unity asset GUID として解釈する |
| `hierarchyPath` | none | scene / prefab hierarchy path として解釈する |
| `referenceResolvable` | `targetKind` | 指定 kind の Unity 参照へ解決できる |
| `typeExists` | none | Unity runtime type として解決できる |
| `typeAssignableTo` | `typeKind` | 指定 kind に代入可能な Unity type である |
| `serializedProperty` | `access` | serialized property path として書き込みに使える |

kind ごとの parameter 規則:

| kind | Required parameters | Forbidden parameters |
| --- | --- | --- |
| `nonEmpty` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `range` | `min` または `max` の少なくとも一方 | `assetKind`, `targetKind`, `typeKind`, `access` |
| `projectRelativePath` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `assetExists` | `assetKind` | `min`, `max`, `targetKind`, `typeKind`, `access` |
| `assetCreatable` | `assetKind` | `min`, `max`, `targetKind`, `typeKind`, `access` |
| `globalObjectId` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `assetGuid` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `hierarchyPath` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `referenceResolvable` | `targetKind` | `min`, `max`, `assetKind`, `typeKind`, `access` |
| `typeExists` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `typeAssignableTo` | `typeKind` | `min`, `max`, `assetKind`, `targetKind`, `access` |
| `serializedProperty` | `access` | `min`, `max`, `assetKind`, `targetKind`, `typeKind` |

#### `ucli ops describe payload.operation.resultContract`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `emitted` | boolean | yes | `opResults[].result` が出るか |
| `resultType` | string | yes | result の contract 型名。result を返さない operation は `UcliNoResult` |
| `description` | string | yes | result の意味と読み方 |

`resultContract` は `opResults[].result` の読み方だけを表す。step 間データフロー、binding、durable output はこの property では表さない。

`emitted:false` の operation は `opResults[].result` property 自体を出さない。`null` は返さない。この場合、`resultType` は `UcliNoResult`、`resultSchema` は `null` になる。

`UcliNoResult` は `{}` または `null` を意味しない。operation 固有の result field を出力しないことを意味する。

#### `ucli ops describe payload.operation.codeContract`

`codeContract` は source code を受け取る operation だけが返す任意 property である。JSON wire の `args` / `result` 構造ではなく、利用者が渡す source code の entry point と uCLI が提供する API を説明する。Unity API や project assembly の参照可否を列挙する allowlist ではない。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `language` | string | yes | source language。`ucli.cs.eval` は `csharp` |
| `entryPoint` | object | yes | 利用者 source に必要な entry point。shape は `payload.operation.codeContract.entryPoint` を参照 |
| `sourceForms` | array | yes | 受け付ける source form。shape は `payload.operation.codeContract.sourceForms[]` を参照 |
| `apiTypes` | array | yes | uCLI が利用者 source 向けに公開した API 型。shape は `payload.operation.codeContract.apiTypes[]` を参照 |

`apiTypes` の説明は対象 API の `[UcliDescription]` から生成する。説明を持たない public member または parameter がある API 型は `codeContract` 生成に失敗する。

#### `ucli ops describe payload.operation.codeContract.entryPoint`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `signature` | string | yes | source に実装する entry point の C# 署名 |
| `matchRule` | string | yes | source 内から entry point を選択する規則 |
| `requiredStatic` | boolean | yes | entry point が `static` でなければならないか |
| `parameterTypes` | string[] | yes | entry point parameter の完全修飾型名 |
| `returnValue` | string | yes | 戻り値制約 |

#### `ucli ops describe payload.operation.codeContract.sourceForms[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | source form 名。`ucli.cs.eval` は `compilationUnit` または `snippet` |
| `description` | string | yes | source form の書き方と制約 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | API 型名 |
| `fullName` | string | yes | API 型の完全修飾名 |
| `description` | string | yes | API 型の説明 |
| `members` | array | yes | public API member。shape は `payload.operation.codeContract.apiTypes[].members[]` を参照 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[].members[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `method \| property` | yes | member 種別 |
| `name` | string | yes | member 名 |
| `description` | string | yes | member の説明 |
| `type` | string | property のみ | property 型 |
| `returnType` | string | method のみ | method 戻り値型 |
| `parameters` | array | method のみ | method parameter。shape は `payload.operation.codeContract.apiTypes[].members[].parameters[]` を参照 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[].members[].parameters[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | parameter 名 |
| `type` | string | yes | parameter 型 |
| `description` | string | yes | parameter の説明 |

#### `ucli ops describe payload.operation.assurance`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `sideEffects` | string[] | yes | `call` で起こり得る副作用の分類 |
| `mayDirty` | boolean | yes | `call` が Unity object や project state を dirty にし得るか |
| `mayPersist` | boolean | yes | `call` が project file へ永続化し得るか |
| `touchedKinds` | string[] | yes | `touched[]` に出現し得る resource kind |
| `planMode` | string | yes | `validationOnly`、`observesLiveUnity`、または `mayCreatePreviewState` |

`sideEffects` は閉じた語彙で表す。副作用がない operation は `sideEffects: []` とする。

| sideEffects value | Meaning |
| --- | --- |
| `opensSceneInEditor` | Unity Editor で Scene を開く |
| `opensPrefabStage` | Prefab editing stage を開く |
| `refreshesAssetDatabase` | AssetDatabase refresh を実行する |
| `writesScene` | Scene file を保存または dirty にし得る |
| `writesPrefab` | Prefab asset を保存または dirty にし得る |
| `writesAsset` | project asset を保存または dirty にし得る |
| `writesProjectSettings` | ProjectSettings 配下を保存または dirty にし得る |

`touchedKinds` は閉じた語彙で表す。touched resource がない operation は `touchedKinds: []` とする。

| touchedKinds value | Meaning |
| --- | --- |
| `scene` | Scene resource |
| `prefab` | Prefab resource |
| `asset` | AssetDatabase 管理下の project asset |
| `projectSettings` | ProjectSettings 配下の project setting |

`planMode` は次のいずれかに限定する。

| planMode value | Meaning |
| --- | --- |
| `validationOnly` | Plan は typed args と静的 contract の検証だけを行う |
| `observesLiveUnity` | Plan は live Unity state または readIndex を観測して結果を作る |
| `mayCreatePreviewState` | Plan が preview scene や prefab stage など一時状態を作り得る |

`kind` と `assurance` の整合は次を満たす。

| kind | Assurance rule | Examples |
| --- | --- | --- |
| `query` | 観測のみ。`mayDirty:false`、`mayPersist:false`、`sideEffects:[]` とする。読み取った resource を `touchedKinds` で表す場合がある | `ucli.scene.tree`, `ucli.assets.find` |
| `command` | Editor 状態や AssetDatabase 状態を変え得る。永続化対象の内容変更を主目的にしないが、import や AssetDatabase refresh によって dirty / persist が起こり得る場合は `mayDirty` / `mayPersist` を `true` にし、副作用は `sideEffects` で表す | `ucli.scene.open`, `ucli.prefab.open`, `ucli.project.refresh` |
| `mutation` | Scene / Prefab / Asset / ProjectSettings を dirty または保存し得る。対象種別は `touchedKinds`、永続化可能性は `mayPersist` で表す | `ucli.comp.set`, `ucli.scene.save` |

#### `ucli ops describe payload.operation.argsSchema`

`argsSchema` は `steps[].args` の JSON 構造だけを表す。使用する語彙は `type`、`properties`、`required`、`additionalProperties:false`、`items`、`$ref`、`$defs` に限定する。`type` は string または string array で、値は `object`、`array`、`string`、`integer`、`number`、`boolean`、`null` のいずれかである。

schema には説明文や意味制約を出さない。説明は `description`、input 全体の意味制約は `inputs[].constraints`、variant field の意味制約は `inputs[].variants[].fields[].constraints` に置く。

#### `ucli ops describe payload.operation.resultSchema`

`argsSchema` / `resultSchema` は JSON Schema の完全実装ではなく、uCLI-supported JSON Schema subset である。この subset は JSON object の構造検証だけを contract し、外部 JSON Schema validator への完全互換入力として扱えることは保証しない。

subset で使用できる語彙は `type`、`properties`、`required`、`additionalProperties:false`、`items`、`$ref`、`$defs` に限定する。`type` は string または string array で、nullable property は `["<type>","null"]` で表す。`$schema` は出力しない。closed value set は schema の `enum` ではなく、この property reference の語彙表、`inputs[].constraints`、または `inputs[].variants[].fields[].constraints` で表す。composition、condition、default、example、format、scalar constraint 系の JSON Schema keyword は公開 contract として使用しない。

`resultSchema` は `opResults[].result` の JSON 構造だけを表す。result を返さない operation では `null` になる。配列 property は `items` で要素構造を表し、ネスト型や再帰型は `$defs` の名前付き schema と `$ref` で表す。

`$defs` は配列ではなく object である。複数定義は `"SceneTreeNode"` や `"AssetReference"` のような名前を key にして並べ、参照側は `"$ref":"#/$defs/SceneTreeNode"` の形で参照する。

#### `ucli ops describe` no result command 例

```json
{
  "operation": {
    "name": "ucli.scene.open",
    "kind": "command",
    "policy": "safe",
    "description": "Open a Unity scene in the Editor before reading or editing scene objects.",
    "inputs": [
      {
        "name": "path",
        "valueType": "string",
        "description": "Project-relative path to a scene asset.",
        "constraints": [
          { "kind": "nonEmpty" },
          { "kind": "assetExists", "assetKind": "scene" }
        ]
      }
    ],
    "resultContract": {
      "emitted": false,
      "resultType": "UcliNoResult",
      "description": "No operation-specific result is emitted."
    },
    "assurance": {
      "sideEffects": [
        "opensSceneInEditor"
      ],
      "mayDirty": false,
      "mayPersist": false,
      "touchedKinds": [
        "scene"
      ],
      "planMode": "observesLiveUnity"
    },
    "argsSchema": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "path": {
          "type": "string"
        }
      },
      "required": [
        "path"
      ]
    },
    "resultSchema": null
  },
  "readIndex": {
    "used": true,
    "hit": true,
    "source": "index",
    "freshness": "fresh",
    "generatedAtUtc": "2026-05-03T00:00:00Z",
    "fallbackReason": null
  }
}
```

#### object result と nested `$defs` 例

```json
{
  "operation": {
    "name": "ucli.scene.tree",
    "kind": "query",
    "policy": "safe",
    "description": "Read the hierarchy tree for a loaded or indexed Unity scene.",
    "inputs": [
      {
        "name": "path",
        "valueType": "string",
        "description": "Scene asset path to inspect.",
        "constraints": [
          { "kind": "nonEmpty" },
          { "kind": "projectRelativePath" },
          { "kind": "assetExists", "assetKind": "scene" }
        ]
      },
      {
        "name": "depth",
        "valueType": "integer",
        "description": "Maximum hierarchy depth to include; null means unbounded.",
        "constraints": [
          { "kind": "range", "min": 0 }
        ]
      }
    ],
    "resultContract": {
      "emitted": true,
      "resultType": "SceneTreeResult",
      "description": "Scene hierarchy tree for the requested scene."
    },
    "assurance": {
      "sideEffects": [],
      "mayDirty": false,
      "mayPersist": false,
      "touchedKinds": ["scene"],
      "planMode": "observesLiveUnity"
    },
    "argsSchema": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "path"
      ],
      "properties": {
        "path": {
          "type": "string"
        },
        "depth": {
          "type": [
            "integer",
            "null"
          ]
        }
      }
    },
    "resultSchema": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "path",
        "roots",
        "sourceState"
      ],
      "properties": {
        "path": {
          "type": "string"
        },
        "roots": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/IndexSceneTreeLiteNodeJsonContract"
          }
        },
        "sourceState": {
          "$ref": "#/$defs/SceneTreeSourceState"
        }
      },
      "$defs": {
        "IndexSceneTreeLiteNodeJsonContract": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "name",
            "globalObjectId",
            "children"
          ],
          "properties": {
            "name": {
              "type": "string"
            },
            "globalObjectId": {
              "type": "string"
            },
            "children": {
              "type": "array",
              "items": {
                "$ref": "#/$defs/IndexSceneTreeLiteNodeJsonContract"
              }
            }
          }
        },
        "SceneTreeSourceState": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "kind",
            "isDirty"
          ],
          "properties": {
            "kind": {
              "type": "string"
            },
            "isDirty": {
              "type": "boolean"
            }
          }
        }
      }
    }
  },
  "readIndex": {
    "used": true,
    "hit": true,
    "source": "index",
    "freshness": "fresh",
    "generatedAtUtc": "2026-05-03T00:00:00Z",
    "fallbackReason": null
  }
}
```

`depth` は optional input なので `argsSchema.required` に含めない。`inputs[]` に `required` は置かない。

#### object input と selector variants 例

```json
{
  "input": {
    "name": "target",
    "valueType": "object",
    "description": "Object reference to resolve.",
    "argsPath": "$.target",
    "constraints": [
      { "kind": "referenceResolvable", "targetKind": "gameObject" }
    ],
    "variants": [
      {
        "name": "globalObjectId",
        "description": "Use when an exact Unity GlobalObjectId is already known.",
        "fields": [
          {
            "name": "globalObjectId",
            "argsPath": "$.target.globalObjectId",
            "description": "Resolved Unity GlobalObjectId.",
            "constraints": [
              { "kind": "globalObjectId" }
            ]
          }
        ]
      },
      {
        "name": "sceneHierarchy",
        "description": "Use when the scene path and hierarchy path are known.",
        "fields": [
          {
            "name": "scene",
            "argsPath": "$.target.scene",
            "description": "Scene asset path for a hierarchy selector.",
            "constraints": [
              { "kind": "assetExists", "assetKind": "scene" }
            ]
          },
          {
            "name": "hierarchyPath",
            "argsPath": "$.target.hierarchyPath",
            "description": "Unity hierarchy path inside the selected scene or prefab.",
            "constraints": [
              { "kind": "hierarchyPath" }
            ]
          }
        ]
      }
    ]
  },
  "argsSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "target"
    ],
    "properties": {
      "target": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "globalObjectId": {
            "type": "string"
          },
          "scene": {
            "type": "string"
          },
          "hierarchyPath": {
            "type": "string"
          }
        }
      }
    }
  }
}
```

object input の内部構造は `argsSchema.properties` で表す。selector の表現差は `variants` に置くが、operation の意味差は variant にしない。

selector の各 variant は同じ `target` object の異なる表現方法を説明する。`variants[].fields[]` はその variant を選ぶときに埋める leaf property と、各 property に掛かる説明・意味制約を表す。`argsSchema.properties.target.properties` はそれらの leaf property の JSON 構造だけを表す。

複数表現の排他性は `inputs[].variants[]` の仕様で表し、JSON Schema の `oneOf` では表さない。

#### root object input の `argsPath` 例

```json
{
  "input": {
    "name": "request",
    "valueType": "object",
    "description": "Complete operation argument object.",
    "constraints": [],
    "argsPath": "$"
  },
  "argsSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "path",
      "values"
    ],
    "properties": {
      "path": {
        "type": "string"
      },
      "values": {
        "type": "object"
      }
    }
  }
}
```

`argsPath:"$"` は input が `steps[].args` 全体に対応することを表す。通常の property input では `argsPath` を省略する。

#### no constraints 例

```json
{
  "name": "includeInactive",
  "valueType": "boolean",
  "description": "Whether inactive objects are included.",
  "constraints": []
}
```

意味制約がない input でも `constraints` は省略しない。

#### 複数 `$defs` 例

```json
{
  "resultSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "matches"
    ],
    "properties": {
      "matches": {
        "type": "array",
        "items": {
          "$ref": "#/$defs/AssetMatch"
        }
      },
      "selected": {
        "$ref": "#/$defs/AssetReference"
      }
    },
    "$defs": {
      "AssetMatch": {
        "type": "object",
        "additionalProperties": false,
        "required": [
          "assetPath",
          "assetGuid"
        ],
        "properties": {
          "assetPath": {
            "type": "string"
          },
          "assetGuid": {
            "type": "string"
          }
        }
      },
      "AssetReference": {
        "type": "object",
        "additionalProperties": false,
        "required": [
          "assetGuid"
        ],
        "properties": {
          "assetGuid": {
            "type": "string"
          }
        }
      }
    }
  }
}
```

`$defs` は名前付き schema の object であり、複数定義を持つ場合も配列にはしない。

#### 禁止される表現

`description` が複数のユーザー意図を説明する operation は分割対象である。`variants` は同じ input の参照方法だけに使う。

schema の property に説明文や意味制約を置かない。説明は `inputs[].description` または `inputs[].variants[].fields[].description`、意味制約は `inputs[].constraints` または `inputs[].variants[].fields[].constraints` に置く。空文字禁止は該当 input または field の `constraints` の `{ "kind": "nonEmpty" }` で表す。

#### 不正な result 例

```json
{
  "resultContract": {
    "emitted": false,
    "resultType": "UcliNoResult",
    "description": "No operation-specific result is emitted."
  },
  "resultSchema": null,
  "opResults": [
    {
      "id": "openMainScene",
      "phase": "call",
      "applied": true,
      "changed": false,
      "result": null
    }
  ]
}
```

`emitted:false` では `opResults[].result` property 自体を出さない。`null` は返さない。

### `ucli validate`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `readIndex` | object | yes | この静的検証が参照した readIndex 情報。shape は `payload.readIndex` を参照 |

`validate` は Play Mode 変更の runtime 条件、対象 live object、Prefab instance lineage、request-attributed property path を保証しない。これらは `plan --allowPlayMode` で検証する。

### `ucli plan`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 内部 `execute(command=plan)` リクエストの `requestId` |
| `opResults` | array | yes | `plan` 実行結果。failure 時は `[]` または部分結果 |
| `readIndex` | object | yes | Unity IPC `plan` 実行前の static preflight で再利用した readIndex 情報。shape は `payload.readIndex` を参照 |
| `planToken` | string | no | `plan` 成功時に発行されたトークン。failure 時は field 自体を省略する |

### `ucli call`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 内部 `execute(command=call)` リクエストの `requestId` |
| `opResults` | array | yes | `call` 実行結果。failure 時は `[]` または部分結果 |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |
| `plan` | object | no | `--withPlan` 指定時の事前 `plan` 結果 |

#### `ucli call payload.plan`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 事前 `execute(command=plan)` リクエストの `requestId` |
| `opResults` | array | yes | `plan` 実行結果。failure 時は `[]` または部分結果 |
| `planToken` | string | no | 事前 `plan` 成功時に発行されたトークン。未発行時は field 自体を省略する |

`payload.plan` は `readPostcondition` を含まない。mutation 後 read の safe 条件は常に top-level の `payload.readPostcondition` にだけ出力する。

### readIndex

#### `payload.readIndex`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `used` | boolean | yes | readIndex を利用したか |
| `hit` | boolean | yes | 既存 index を利用できたか |
| `source` | string | yes | 参照元 |
| `freshness` | `fresh \| probable \| stale` | yes | 索引の鮮度 |
| `generatedAtUtc` | `string \| null` | yes | index 生成時刻 |
| `fallbackReason` | `string \| null` | yes | フォールバック理由 |

#### `types.catalog.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | catalog schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | 入力ハッシュ |
| `entries` | array | yes | type エントリ一覧 |

#### `types.catalog.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `typeId` | string | yes | 型識別子 |
| `displayName` | string | yes | 表示名 |
| `namespace` | `string \| null` | yes | 名前空間 |
| `assemblyName` | string | yes | アセンブリ名 |
| `baseTypeId` | `string \| null` | yes | 基底型識別子 |
| `flags.isAbstract` | boolean | yes | abstract 型か |
| `flags.isGenericDefinition` | boolean | yes | generic definition か |
| `flags.isUnityObject` | boolean | yes | UnityEngine.Object 派生か |
| `flags.isComponent` | boolean | yes | Component か |
| `flags.isScriptableObject` | boolean | yes | ScriptableObject か |
| `flags.isSerializeReferenceCandidate` | boolean | yes | SerializeReference 候補か |

#### `schemas.catalog.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | catalog schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | 入力ハッシュ |
| `entries` | array | yes | schema エントリ一覧 |

#### `schemas.catalog.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaKey` | string | yes | `comp:<typeId>` または `asset:<typeId>` |
| `kind` | `comp \| asset` | yes | schema 種別 |
| `typeId` | string | yes | 型識別子 |
| `displayName` | string | yes | 表示名 |
| `properties` | array | yes | プロパティ一覧 |

#### `schemas.catalog.json entries[].properties[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `path` | string | yes | SerializedProperty path |
| `propertyType` | string | yes | `SerializedPropertyType` と意味対応する enum literal |
| `declaredTypeId` | `string \| null` | yes | 宣言型識別子 |
| `isArray` | boolean | yes | 配列か |
| `elementTypeId` | `string \| null` | yes | `isArray=true` のときのみ非 `null` |
| `isReadOnly` | boolean | yes | 読み取り専用か |

#### `asset-search.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | `assetSearchHash` と一致する入力ハッシュ |
| `entries` | array | yes | asset 検索エントリ一覧 |

#### `asset-search.lookup.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetPath` | string | yes | `Assets/` 配下の persistent main asset path |
| `assetGuid` | string | yes | asset GUID |
| `name` | string | yes | main asset 名 |
| `typeId` | string | yes | runtime 型識別子 |
| `searchTypeIds` | array | yes | runtime 型から `UnityEngine.Object` までの型識別子連鎖 |

#### `guid-path.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | `guidPathHash` と一致する入力ハッシュ |
| `entries` | array | yes | GUID-path エントリ一覧 |

#### `guid-path.lookup.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetGuid` | string | yes | asset GUID |
| `assetPath` | string | yes | `Assets/` 配下の persistent main asset path |

#### `lookups/scene-tree-lite/*.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `scenePath` | string | yes | persisted scene asset を指す project-relative path |
| `sourceInputsHash` | string | yes | 対象 Scene 本体と `.meta` の内容ハッシュ |
| `roots` | array | yes | persisted preview として読んだ Scene hierarchy の root node 一覧 |

#### `lookups/scene-tree-lite/*.lookup.json roots[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | GameObject 名 |
| `globalObjectId` | string | yes | 解決できたときの GlobalObjectId。解決不能時は空文字列 |
| `children` | array | yes | 子 node 一覧 |

#### `inputs/manifest.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | manifest schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `scriptAssembliesHash` | string | yes | ScriptAssemblies ハッシュ |
| `packagesManifestHash` | string | yes | `Packages/manifest.json` ハッシュ |
| `packagesLockHash` | string | yes | `Packages/packages-lock.json` ハッシュ |
| `assemblyDefinitionHash` | string | yes | `.asmdef/.asmref` ハッシュ |
| `assetsContentHash` | string | yes | `Assets/` 配下の asset 本体と `.meta` の内容ハッシュ |
| `assetSearchHash` | string | yes | `asset-search.lookup.json` の鮮度判定に使う結合ハッシュ |
| `guidPathHash` | string | yes | `guid-path.lookup.json` の鮮度判定に使う結合ハッシュ |
| `combinedHash` | string | yes | 結合ハッシュ |

### `ucli test run`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `result` | `pass \| fail \| null` | yes | テスト結果。`status=error` のときは `null` |
| `errorKind` | `invalidInput \| infraError \| toolError \| null` | yes | 失敗種別 |
| `runId` | string | yes | 実行 ID |
| `artifactsDir` | string | yes | 成果物ディレクトリ |
| `summaryJsonPath` | string | yes | サマリー JSON のパス |

### テストプロファイル JSON

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | profile schema version |
| `projectPath` | string | yes | 対象 Unity project path |
| `unityVersion` | `string \| null` | yes | Unity バージョン |
| `unityEditorPath` | `string \| null` | yes | Unity Editor 実行ファイルまたはディレクトリ |
| `testPlatform` | `editmode \| playmode \| <BuildTarget>` | yes | テスト実行先。`playmode` は現在対象の PlayMode、`<BuildTarget>` は player 実行先 |
| `testFilter` | `string \| null` | yes | テスト名フィルタ |
| `testCategories` | array | yes | テストカテゴリ一覧 |
| `assemblyNames` | array | yes | テストアセンブリ一覧 |
| `testSettingsPath` | `string \| null` | yes | `TestSettings.json` のパス |
| `timeout` | integer | yes | タイムアウトミリ秒数 |
