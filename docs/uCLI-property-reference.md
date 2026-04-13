> [!IMPORTANT]
> この文書は、uCLI の公開 CLI 出力契約、内部 IPC 契約、`payload`、status、readIndex catalog のプロパティ定義をまとめたリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、コマンドの option table と実行例は [uCLI-command-reference.md](uCLI-command-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。

## 公開 CLI 出力契約

公開 CLI 出力の種別と意味は [uCLI.md](uCLI.md) を正本とする。ここでは `request-response` 型で使う JSON shape だけを定義する。

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

### `IpcExecuteResponse.opResults[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `opId` | string | yes | 対応する実行単位の `id` |
| `op` | string | yes | 実行単位名。`kind:"edit"` step では `"edit"` を返す |
| `phase` | `validate \| plan \| call \| skipped` | yes | 実行フェーズ |
| `applied` | boolean | yes | step 内のどこかが適用済みか |
| `changed` | boolean | yes | step 内のどこかに実変更があったか |
| `touched` | array | yes | 影響した永続化単位の配列 |
| `result` | object | no | `kind:"op"` step の結果本体。`edit` では返さない |

#### `IpcExecuteResponse.opResults[].touched[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `scene \| prefab \| asset \| projectSettings` | yes | 永続化単位の種別 |
| `path` | string | yes | プロジェクトルート相対パス |
| `guid` | string | no | 取得可能な場合に付与する GUID |

#### `IpcExecuteResponse.opResults[].result` for `ucli.assets.find`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `matches` | array | yes | `assetPath` の ordinal 昇順で並ぶ一致結果。未一致時は空配列 |

`ucli.assets.find` の `touched` は常に空配列である。

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

## lifecycle 関連プロパティ

### lifecycle status フィールド
`ucli status` と `ucli daemon status` は、必要に応じて次の lifecycle 関連 field を `payload` に含める。各コマンドが返す field の subset は、コマンド別 `payload` 契約を正とする。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `daemonStatus` | `running \| notRunning \| stale` | yes | daemon 到達性と session 状態 |
| `unityVersion` | `string \| null` | yes | `ProjectSettings/ProjectVersion.txt` 由来の Unity バージョン |
| `serverVersion` | `string \| null` | yes | `daemonStatus = running` のときのみ値を持つ |
| `runtime` | `batchmode \| gui \| null` | yes | `daemonStatus = running` のときのみ値を持つ |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `daemonStatus = running` のときのみ値を持つ。current batchmode daemon は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset のみを返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | 実行を止めている理由。current batchmode daemon は `startup / busy / compile / domainReload / playMode / shutdown` の subset のみを返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 専用の状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | domain reload 完了ごとに変化する opaque な識別子 |
| `canAcceptExecutionRequests` | boolean | yes | `daemonStatus = running` かつ `lifecycleState = ready` のときのみ `true` |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |

## コマンド別 `payload`

### `ucli daemon`
`ucli daemon` はサブコマンドにかかわらず `payload.timeoutMilliseconds` を常に含む。

#### `daemon start`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `startStatus` | `started \| alreadyRunning` | yes | 起動結果 |
| `daemonStatus` | `running` | yes | daemon 状態 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | object | yes | セッション情報 |

#### `daemon stop`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `stopStatus` | `stopped \| notRunning` | yes | 停止結果 |
| `daemonStatus` | `notRunning` | yes | daemon 状態 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `null` | yes | 停止後は常に `null` |

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
| `runtime` | `batchmode \| gui \| null` | yes | `running` のときのみ値を持つ |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `running` のときのみ値を持つ。current batchmode daemon は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset のみを返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | `running` のときのみ意味を持つ。current batchmode daemon は `startup / busy / compile / domainReload / playMode / shutdown` の subset のみを返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | `running` のときのみ値を持つ |
| `canAcceptExecutionRequests` | boolean | yes | `running` かつ `ready` のときのみ `true` |
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
| `runtimeKind` | string | yes | runtime 種別 |
| `ownerKind` | string | yes | session 所有者種別 |
| `canShutdownProcess` | boolean | yes | 停止操作の可否 |
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

### `ucli validate`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `readIndex` | object | yes | この静的検証が参照した readIndex 情報。shape は `payload.readIndex` を参照 |

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
| `plan` | object | no | `--withPlan` 指定時の事前 `plan` 結果 |

#### `ucli call payload.plan`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requestId` | string | yes | 事前 `execute(command=plan)` リクエストの `requestId` |
| `opResults` | array | yes | `plan` 実行結果。failure 時は `[]` または部分結果 |
| `planToken` | string | no | 事前 `plan` 成功時に発行されたトークン。未発行時は field 自体を省略する |

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
| `scenePath` | string | yes | 対象 Scene の project-relative path |
| `sourceInputsHash` | string | yes | 対象 Scene 本体と `.meta` の内容ハッシュ |
| `roots` | array | yes | Scene hierarchy の root node 一覧 |

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
| `testPlatform` | `editmode \| playmode` | yes | テスト実行種別 |
| `buildTarget` | `string \| null` | yes | PlayMode 用 build target |
| `testFilter` | `string \| null` | yes | テスト名フィルタ |
| `testCategories` | array | yes | テストカテゴリ一覧 |
| `assemblyNames` | array | yes | テストアセンブリ一覧 |
| `testSettingsPath` | `string \| null` | yes | `TestSettings.json` のパス |
| `timeout` | integer | yes | タイムアウトミリ秒数 |
