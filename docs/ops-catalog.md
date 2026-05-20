# Ops Catalog

> [!NOTE]
> この文書は補助カタログである。
> 入力 DSL の正本は [json-request-spec.md](json-request-spec.md)、全体契約とコマンド仕様は [uCLI.md](uCLI.md) を参照する。
> operation ごとの agent 向け contract は `ops describe` の `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` で表す。`argsSchema` / `resultSchema` は `steps[].args` と `opResults[].result` の JSON 構造検証用 schema である。
> 1つの operation は1つのユーザー意図だけを表す。複数の意味を含む既存 operation は分割候補とし、同じ input の参照方法の差だけを `inputs[].variants[]` として扱う。

`ucli.prefab.applyOverrides` と `ucli.prefab.revertOverrides` は全モードで edit lowering 専用 primitive とする。ユーザー入力 JSON の raw `kind:"op"` から直接呼び出された場合は `INVALID_ARGUMENT` で拒否する。

Operation author は `policy` を指定しない。author が指定するのは operation の意味、Args / Result contract、semantic constraints、`declaredKind`、`assurance`、`codeContract`、`exposure` などの contract facts である。catalog builder はそれらの contract facts から `kind` と `policy` を決定論的に導出する。

## Catalog pipeline

Operation catalog は Unity 側で生成した operation 登録を論理 catalog へ変換し、live IPC、readIndex 永続化、CLI 表示、静的検証で共有する。永続化では `ops list` 用の軽量 descriptor と `ops describe` 用の detail artifact に分割し、公開 CLI payload は論理 catalog から投影する。

処理順は次の通りである。

```text
Unity 生成 -> contract facts 導出 -> kind / policy 検証 -> source snapshot -> best-effort 永続化 -> persisted load + freshness -> access policy -> CLI projection
```

- `ops.catalog.json` は public raw `kind:"op"` として呼べる operation だけの `name` / `kind` / `policy` / `description` と describe detail 参照情報を持ち、`ops list` と `ops describe` の事前 lookup に使う
- `ops.describe/<opKey>.json` は public raw operation の `description` / `inputs` / `resultContract` / `assurance` / `codeContract` / schema object を持ち、`ops describe` の単一 operation detail として使う
- `opKey` は operation name から決定論的に作る不透明な safe key であり、利用者は path を直接組み立てず `ucli ops describe <opName>` を正本として読む
- `ops list` は exposure が `public` の軽量 descriptor から `name` / `kind` / `policy` / `description` を表示用 model へ投影し、`--nameRegex` / `--kind` / `--maxPolicy` の AND 条件で絞り込める
- `editLoweringOnly` と `internal` の operation は内部 catalog validation の対象にするが、public `ops list` と public `ops describe` には出さない
- persisted catalog の読み込み失敗は access policy で分類し、CLI projection は永続化ファイルや freshness 計算の詳細へ依存しない

## Metadata derivation

author が指定する contract facts は次の通りである。

- `operationName`、`description`
- Args / Result CLR contract と属性
- input semantic constraints
- `declaredKind`
- `assurance.sideEffects`、`assurance.mayDirty`、`assurance.mayPersist`、`assurance.touchedKinds`、`assurance.planMode`
- source code を受け取る operation の `codeContract`
- public raw op として呼べるかを表す `exposure`
- destructiveness、外部 process、filesystem access などの追加 facts

catalog builder が自動生成または導出する値は次の通りである。

- `inputs[]`
- `resultContract`
- `argsSchema`
- `resultSchema`
- `opKey`
- `kind`
- `policy`
- `ops list` descriptor
- `ops describe` detail artifact

`policy` は入場制御であり、author が任意に付ける安全ラベルではない。catalog builder は contract facts から公開 `operation.policy` を導出する。author が `policy` を直接指定する API や、主観的に policy を厳しくする上書き指定は持たない。運用上より強い入場制御が必要な場合は、`externalProcess`、`filesystemWrite`、`arbitrarySourceExecution`、`destructiveScope`、`previewStateInPlan` のように policy 導出に使う contract fact として表す。導出過程は catalog validation と contract test の対象であり、public `ops describe` の独立 field にはしない。

`assurance.mayDirty` と `assurance.mayPersist` は author が指定する一次 fact ではなく、`Ucli.Contracts` の sideEffect descriptor table から生成される public projection である。public `ops describe` と persisted describe artifact には読みやすさのために残すが、保存値が descriptor projection と一致しない catalog は validation failure とする。`mayPersist` は Unity save だけでなく direct filesystem persistence も含む広義の永続化 projection である。`mayPersist=true` の operation は永続化境界を `touchedKinds` で 1 種類以上宣言しなければならない。sideEffect descriptor の required touchedKinds は、その side effect が要求する最小種別であり、この operation 全体の非空要求に加えて検証する。

`kind` は operation の意味論に近いため、author は `declaredKind` を指定する。ただし公開 `kind` は validation 済みの値であり、catalog builder は `declaredKind` を自動昇格・降格してはならない。次の矛盾は catalog validation failure とする。

- `declaredKind = query` かつ derived `mayDirty = true`
- `declaredKind = query` かつ derived `mayPersist = true`
- `declaredKind = query` かつ `touchedKinds` が空でない
- `declaredKind = query` かつ Editor state / AssetDatabase state を変える side effect がある
- `declaredKind = command` かつ Scene / Prefab / Asset / ProjectSettings content mutation が主目的である

policy 導出の基準は次の通りである。

| Policy | Conditions |
| --- | --- |
| `safe` | bounded observation。`mayDirty=false`、`mayPersist=false`、Editor state change なし、任意コード実行なし、外部 process / filesystem write なし |
| `advanced` | Unity Editor API 経由の決定論的 write、`mayDirty=true` または `mayPersist=true`、Scene / Prefab / Asset / ProjectSettings 変更、AssetDatabase refresh / import / compile など broader project effect |
| `dangerous` | arbitrary C# execution、arbitrary shell / process / filesystem write、unbounded delete / overwrite、typed context / save boundary / touched contract を十分に保証できない escape hatch |

`mayCreatePreviewState` は Plan が review gate 前に一時 Scene / Prefab Stage などの状態を作るため、`policy` を最低 `advanced` に導出する。public raw catalog では `mayCreatePreviewState` を禁止し、metadata / catalog validation failure とする。preview state が必要な primitive は `editLoweringOnly` または `internal` に置く。

`sideEffects` は public JSON では string tag として出るが、仕様上は `Ucli.Contracts` 内の descriptor-backed closed vocabulary である。descriptor は minimum policy、dirty/persist projection、query 許可、required touchedKinds を持つ。複数の side effect がある場合、policy は最も厳しい minimum policy を採用する。

| sideEffects value | Minimum policy | Notes |
| --- | --- | --- |
| `observesUnityState` | `safe` | read-only observation |
| `editorStateChange` | `advanced` | active scene、Prefab Stage、selection、mode などを変え得る |
| `opensSceneInEditor` | `advanced` | Scene を Editor に open / load し得る |
| `opensPrefabStage` | `advanced` | Prefab editing stage を open し得る |
| `assetDatabaseRefresh` | `advanced` | AssetDatabase refresh により import / project state 更新が起き得る |
| `assetImport` | `advanced` | AssetDatabase import を発生させ得る |
| `scriptCompilation` | `advanced` | compile / reload boundary をまたぎ得る |
| `domainReload` | `advanced` | lifecycle transition を発生させ得る |
| `sceneContentMutation` | `advanced` | Scene content を dirty にし得る |
| `prefabContentMutation` | `advanced` | Prefab asset content を dirty にし得る |
| `assetContentMutation` | `advanced` | AssetDatabase 管理下の asset content を dirty にし得る |
| `projectSettingsMutation` | `advanced` | ProjectSettings 配下の setting を dirty にし得る |
| `sceneSave` | `advanced` | Scene file へ永続化し得る |
| `prefabSave` | `advanced` | Prefab asset file へ永続化し得る |
| `assetSave` | `advanced` | project asset file へ永続化し得る |
| `projectSave` | `advanced` | project-wide save を実行し得る |
| `externalProcess` | `dangerous` | shell / process execution |
| `filesystemWrite` | `dangerous` | Unity Editor API の save boundary 外で filesystem write を行い得る |
| `arbitrarySourceExecution` | `dangerous` | 利用者が渡した source code を実行し得る |
| `destructiveScope` | `dangerous` | delete / overwrite など破壊的で、対象境界を十分に保証できない |

新しい `sideEffects` value を追加する場合は、同じ変更で descriptor と contract test fixture を追加しなければならない。side effect value だけを追加し、policy / projection 導出が未定義になる catalog は validation failure とする。

`exposure` は raw `kind:"op"` から呼べるかを表す contract fact である。

| exposure | Meaning |
| --- | --- |
| `public` | public raw `kind:"op"` と edit lowering の両方から利用できる |
| `editLoweringOnly` | edit DSL lowering からだけ利用でき、public raw `kind:"op"` では拒否する |
| `internal` | catalog projection と public request からは利用できない内部 primitive |

public `ops list` と public `ops describe` は `exposure=public` の operation だけを返す。`editLoweringOnly` は edit lowering から呼ばれた場合だけ使用でき、public raw `kind:"op"` から呼ばれた場合は `INVALID_ARGUMENT` とする。`internal` は public request surface から到達できない。

operation の valid args によって policy や sideEffects、planMode、result 解釈が変わる場合、その operation は分割対象である。分割できない場合は valid args 全体の worst-case policy を採用する。

実行結果も assurance facts と整合しなければならない。`changed=true` なのに `mayDirty=false`、内部 execution trace が永続化を観測したのに `mayPersist=false`、`touched[].kind` が `assurance.touchedKinds` に含まれない、`declaredKind=query` なのに `applied=true`、`changed=true`、または `touched[]` が non-empty になる場合は `OPERATION_CONTRACT_VIOLATION` とする。この failure は operation 実装または metadata の不整合であり、未適用を意味しない。公開 payload は矛盾ごとに `contractViolations[]` を返し、適用状態を `applicationState` として `notApplied`、`applied`、`indeterminate`、`unknown` のいずれかで保持する。

catalog validation、golden、contract tests は、少なくとも次の matrix を固定する。

- public `ops list` は `exposure=public` の operation だけを返す
- public `ops describe` は `exposure=public` の operation だけを返し、operation field set を `name`、`kind`、`policy`、`description`、`inputs[]`、`resultContract`、`assurance`、`codeContract?`、`argsSchema`、`resultSchema` に固定する
- `editLoweringOnly` を public raw `kind:"op"` で呼ぶと `INVALID_ARGUMENT`
- `internal` operation は public request surface から到達できない
- derived `mayDirty=false`、derived `mayPersist=false`、advanced / dangerous side effect なしは `safe`
- `assetDatabaseRefresh`、`assetImport`、`scriptCompilation`、`domainReload` は `advanced`
- derived `mayDirty=true` または derived `mayPersist=true` の Unity Editor API write は `advanced`
- arbitrary source execution、external process、filesystem write、unbounded destructive scope は `dangerous`
- `planMode=mayCreatePreviewState` は最低 `advanced` に導出し、public raw catalog では validation failure
- `declaredKind=query` かつ `touchedKinds` が空でない場合は catalog validation failure
- derived `mayPersist=true` かつ `touchedKinds` が空の場合は catalog validation failure
- Query operation の golden は `applied=false`、`changed=false`、`touched=[]` を固定する
- `changed=true` かつ `mayDirty=false` は `OPERATION_CONTRACT_VIOLATION`
- `touched[].kind` が `assurance.touchedKinds` に含まれない場合は `OPERATION_CONTRACT_VIOLATION`
- `OPERATION_CONTRACT_VIOLATION` の public payload は `contractViolations[]` を 1 件以上返す
- 信頼できる operation envelope が観測済みなら `OPERATION_CONTRACT_VIOLATION` の `payload.opResults[]` に残し、信頼できる envelope が無い場合は `payload.opResults: []` とする
- `applicationState=notApplied` 以外は retry safe と扱わない
- 新しい `sideEffects` value は descriptor と contract test fixture を同時に持つ
- execution result validation は `assurance` と `opResults[]` の不整合を contract violation として検出する
- `dangerous` policy の raw operation は `--allowDangerous` 無しでは拒否し、`--allowDangerous` 有りでは policy gate だけを通過する
- `--allowDangerous` は exposure gate を解除しない。`editLoweringOnly` と `internal` は `--allowDangerous` があっても public raw `kind:"op"` から呼べない

## Play Mode 変更での扱い

`--allowPlayMode` 付きの Play Mode 変更では、`kind:"edit"` の lowering から発生する Scene / GameObject / Component / Prefab / Asset / ProjectSettings 操作だけを許可する。公開 query-only request を許可するための契約ではない。

- Scene context は Play Mode の live object への変更だけを許可し、`ucli.scene.save` へ lower される形は許可しない
- Prefab / asset / project context は通常の `edit` と同じ primitive、commit、dangerous guard を適用し、明示 `commit` に従って保存できる
- `commit:"project"` は project-wide save であるため Play Mode 変更では許可しない
- Play Mode 変更では raw `kind:"op"` を許可せず、Prefab apply / revert primitive は `edit` lowering から発生した場合だけ許可する
- Scene context の Prefab instance override を Prefab asset へ反映する場合は `applyPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` から `ucli.prefab.applyOverrides` へ lower する
- Scene context の Prefab instance override を Prefab asset 値へ戻す場合は `revertPrefabOverrides(targetAssetPath:"...", propertyPaths:[...])` から `ucli.prefab.revertOverrides` へ lower する
- Prefab / asset / project の保存は対象永続化単位に限定し、open Scene を巻き込む一括 project save は使わない
- readIndex は対象解決や scene / prefab / asset / project 観測に使わない
- Scene context の `opResults[].touched` は永続化単位を表さないため、Prefab apply を含まない場合は空配列を返す

## asset

> [!NOTE]
> `ucli.asset.schema` は `assetPath` に加えて `projectAssetPath` も扱う。`ucli.asset.create` / `ucli.asset.set` は edit lowering 専用 primitive である。`ucli.asset.create` が作れるのは `Assets/` 配下の既存 folder に置く `.asset` main asset だけで、`ProjectSettings/` 直下や directory 自動作成は行わない。
> `ucli.asset.set` の object reference 値は `Plan` で request-local selector を検証できるが、`Call` で persistent asset へ書き込めるのは live state または temporary alias に解決できる参照だけで、preview-only selector は拒否する。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.asset.create` | mutation | advanced | mvp-core | Edit lowering 専用。concrete な `ScriptableObject` main asset を `Assets/` 配下へ新規作成する。 | `AssetCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.asset.schema` | query | safe | mvp-support | `ScriptableObject` 型、既存 main asset、または `ProjectSettings/*` の project-scoped asset の設定可能項目を取得する。 | `AssetSchemaArgs` | `IndexSchemaEntryJsonContract` | 対象型または対象 asset の serialized property schema |
| `ucli.asset.set` | mutation | advanced | mvp-core | Edit lowering 専用。既存 main asset または `ProjectSettings/*` の project-scoped asset のシリアライズ値を更新する。 | `AssetSetArgs` | `UcliNoResult` | result は返さない |

## assets

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.assets.find` | query | safe | mvp-support | `Assets/` 配下の persistent main asset を検索する。`Plan` は request-local planned asset と asset shadow を観測し、`Call` は live Unity state のみを観測する。primitive op 自体も `limit` / `cursor` を持ち、bounded-by-default とする。 | `AssetsFindArgs` | `AssetsFindResult` | `matches[]` に `assetPath`, `assetGuid`, `name`, `typeId` を返す |

- `type` は stable `typeId` を受け取り、runtime type が指定型へ assignable な main asset を一致とみなす
- `pathPrefix` は `Assets` またはその配下を受け取り、ordinal prefix で比較する
- `nameContains` は main asset 名に対する大小文字無視の部分一致で評価する
- `result.matches[]` は `assetPath`, `assetGuid`, `name`, `typeId` を返し、`assetPath` の ordinal 昇順で並ぶ
- `limit` は既定 `100`、最大 `10000` とする。続きがある場合は `cursor` を返し、明示 opt-in なしに全件を stdout payload へ返さない
- `touched` は常に空配列で、結果フィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する

## comp

> [!NOTE]
> raw `scene` / `prefab` selector を使う primitive op は、`Call` では対応する Scene が loaded、または Prefab が opened stage であることを前提にする。preview state が必要な component 編集は edit lowering 専用 primitive で扱う。
> `ucli.comp.set` の object reference 値は `Plan` で request-local selector を解決できるが、`Call` で live component へ書き込めるのは live state または temporary alias に解決できる参照だけで、preview-only selector は拒否する。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.comp.ensure` | mutation | advanced | mvp-core | Edit lowering 専用。対象に指定コンポーネントが存在する状態を保証する。 | `ComponentEnsureArgs` | `UcliNoResult` | result は返さない |
| `ucli.comp.schema` | query | safe | mvp-support | コンポーネント型の設定可能項目を取得する。 | `ComponentTypeArgs` | `IndexSchemaEntryJsonContract` | 対象 component 型の serialized property schema |
| `ucli.comp.set` | mutation | advanced | mvp-core | Edit lowering 専用。対象コンポーネントのシリアライズ値を更新する。 | `ComponentSetArgs` | `UcliNoResult` | result は返さない |

## cs

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.cs.eval` | mutation | dangerous | v1.0 | C# source を Unity Editor process 内で Roslyn によりインメモリコンパイルし、同期 entry point を呼び出す。 | `CsEvalArgs` | `CsEvalResult` | compile 情報、digest、call 時のログ、戻り値、touched resource 宣言 |

`ucli.cs.eval` は metadata と internal execution を維持するが、`arbitrarySourceExecution` を持つため v1 の public raw catalog には出さない。public `ops list` / `ops describe` / public request validation の対象ではない。

- `args.source` は完全な C# コンパイル単位、または `Run` method body だけを書く snippet のどちらかである
- 完全なコンパイル単位では source 内の `public static object? Run(UcliCsEvalContext context)` に一致するメソッドを自動解決する。一致数が 1 件以外の場合は失敗する
- snippet は先頭の `using`、statement、明示 `return`、返り値なし、単一 expression を受け付ける。返り値なし snippet は `returnValue.kind = "null"` になる
- `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>`、`object?` 以外の戻り値、インスタンスメソッド、引数なしメソッド、JSON 化できない戻り値は失敗する
- `plan` は source をコンパイル・検証するだけで entry point を呼び出さない
- `call` は直前に plan 相当の検証を再実行し、成功した場合だけ entry point を呼び出す
- `CsEvalResult.sourceKind` は `compilationUnit` または `snippet` を返す
- `CsEvalResult.resolvedEntryPoint` は一意解決された entry point を監査用に返す。解決できない場合は省略される
- touched resource 宣言は監査情報である。`call` 後の read index invalidation は宣言内容に関係なく安全側に倒して扱う
- `call` の timeout / cancel は entry point 呼び出し前の検証と IPC 待機には作用するが、同期 entry point 実行中の使用者コードを強制停止しない
- `codeContract` は source forms、entry point、uCLI が提供する Context 型、Context の public API、戻り値制約を構造化して返す。Unity API と project assembly は source から直接参照でき、`codeContract.apiTypes` は参照可能型の完全な allowlist ではない

## go

> [!NOTE]
> raw `scene` / `prefab` selector を使う primitive op は、`Call` では対応する Scene が loaded、または Prefab が opened stage であることを前提にする。public raw `ucli.go.delete` / `ucli.go.reparent` の `Plan` は live Unity state を観測するだけで、新しい request-local plan state は確保しない。prefab context で `ucli.go.delete` は prefab root 自身を削除できない。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.go.create` | mutation | advanced | mvp-core | Edit lowering 専用。指定親配下、または loaded Scene の root に GameObject を作成する。 | `GoCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.go.delete` | mutation | advanced | mvp-core | 指定 GameObject を削除する。prefab root は対象にできない。 | `GoTargetArgs` | `UcliNoResult` | result は返さない |
| `ucli.go.describe` | query | safe | mvp-support | GameObjectの構造とコンポーネント情報を取得する。plan では request-local ensured component も観測対象に含む。 | `GoDescribeArgs` | `GameObjectDescriptionResult` | `name`, `globalObjectId`, `components[]`, `children[]` を返す |
| `ucli.go.reparent` | mutation | advanced | mvp-core | 指定 GameObject の親を付け替える。 | `GoReparentArgs` | `UcliNoResult` | result は返さない |

## prefab

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.prefab.create` | mutation | advanced | mvp-core | Edit lowering 専用。Loaded Scene 上の GameObject から Prefab を新規作成する。`target` 必須、空 Prefab は作らない。 | `PrefabCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.prefab.open` | command | advanced | mvp-core | 指定 Prefab を編集コンテキストとして開く。 | `PrefabPathArgs` | `UcliNoResult` | result は返さない |
| `ucli.prefab.save` | mutation | advanced | mvp-core | opened Prefab に dirty または request-attributed change があるとき保存する。opened stage 必須。`Plan` は request-local plan state と計画時に観測できる dirty を基に評価し、`Call` は保存時点の live dirty も保存し得る。 | `PrefabPathArgs` | `UcliNoResult` | result は返さない |
| `ucli.prefab.applyOverrides` | mutation | advanced | mvp-core | Edit lowering 専用。raw `kind:"op"` では呼び出せない。Scene 上の Prefab instance に対する request-attributed property override を明示した Prefab asset へ反映する。 | `{ target, targetAssetPath, propertyPaths[] }` | `UcliNoResult` | result は返さない |
| `ucli.prefab.revertOverrides` | mutation | advanced | mvp-core | Edit lowering 専用。raw `kind:"op"` では呼び出せない。Scene 上の Prefab instance に対する request-attributed property override を Prefab asset 値へ戻す。 | `{ target, targetAssetPath, propertyPaths[] }` | `UcliNoResult` | result は返さない |

## project

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.project.refresh` | command | advanced | mvp-support | AssetDatabase更新やインポートを実行する。 | `UcliEmptyArgs` | `UcliNoResult` | result は返さない |
| `ucli.project.save` | mutation | advanced | mvp-support | request 中に追跡した open Scene / opened Prefab の変更と、Unity の project save が扱う asset / project settings を保存する。`Plan` は既知の request-attributed resource だけを返し、実際の asset / project settings touched は `Call` 観測に依存する。保存はトランザクションではなく、失敗時でも先行保存が残り得る。 | `UcliEmptyArgs` | `UcliNoResult` | result は返さない |

## resolve

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.resolve` | query | safe | mvp-core | セレクタを対象オブジェクト参照へ解決する。 | `ResolveSelectorArgs` | `IpcResolveOperationResult` | 解決済み `globalObjectId` を返す |

## scene

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.scene.open` | command | advanced | mvp-core | 指定 Scene が loaded であることを保証する。既に loaded なら再オープンしない。閉じている Scene を live で開くときは `OpenSceneMode.Single` を使う。 | `ScenePathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.query` | query | safe | mvp-core | scene context 内で selection candidate を列挙する。`/` を含む GameObject 名は `hierarchyPath` で表現できないため candidate に含めない。 | `SceneQueryArgs` | `SceneQueryResult` | `scene` と `matches[]` を返し、match は `kind`, `hierarchyPath`, `componentType` を持つ |
| `ucli.scene.save` | mutation | advanced | mvp-core | loaded Scene に dirty または request-attributed change があるとき保存する。loaded scene 必須。`Plan` は request-local plan state と計画時に観測できる dirty を基に評価し、`Call` は保存時点の live dirty も保存し得る。 | `ScenePathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.tree` | query | safe | mvp-support | Sceneの階層構造を取得する。loaded dirty scene があれば作業途中の階層を読み、未ロードなら保存済み asset を preview scene として読む。raw op でも `limit` / `cursor` を受け付け、既定 `limit=100`、最大 `10000` とする。 | `SceneTreeArgs` | `SceneTreeResult` | `path`、`childrenState` 付き root GameObject tree の `roots[]`、読み取り元の `sourceState`、bounded window の `window` を返す |

- `ucli.scene.query` の `result.matches[]` は hierarchy traversal order で並ぶ。root は Unity の scene root order、子は `Transform.GetChild(index)` 昇順の深さ優先 pre-order とする
