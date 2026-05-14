# Ops Catalog

> [!NOTE]
> この文書は補助カタログである。
> 入力 DSL の正本は [json-request-spec.md](json-request-spec.md)、全体契約とコマンド仕様は [uCLI.md](uCLI.md) を参照する。
> operation ごとの agent 向け contract は `ops describe` の `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` で表す。`argsSchema` / `resultSchema` は `steps[].args` と `opResults[].result` の JSON 構造検証用 schema である。
> 1つの operation は1つのユーザー意図だけを表す。複数の意味を含む既存 operation は分割候補とし、同じ input の参照方法の差だけを `inputs[].variants[]` として扱う。

`ucli.prefab.applyOverrides` と `ucli.prefab.revertOverrides` は全モードで edit lowering 専用 primitive とする。ユーザー入力 JSON の raw `kind:"op"` から直接呼び出された場合は `INVALID_ARGUMENT` で拒否する。

## Catalog pipeline

Operation catalog は Unity 側で生成した operation 登録を論理 catalog へ変換し、live IPC、readIndex 永続化、CLI 表示、静的検証で共有する。永続化では `ops list` 用の軽量 descriptor と `ops describe` 用の detail artifact に分割し、公開 CLI payload は論理 catalog から投影する。

処理順は次の通りである。

```text
Unity 生成 -> 契約検証 -> source snapshot -> best-effort 永続化 -> persisted load + freshness -> access policy -> CLI projection
```

- `ops.catalog.json` は `name` / `kind` / `policy` / `description` と describe detail 参照情報を持ち、`ops list` と `ops describe` の事前 lookup に使う
- `ops.describe/<opKey>.json` は `description` / `inputs` / `resultContract` / `assurance` / `codeContract` / schema object を持ち、`ops describe` の単一 operation detail として使う
- `opKey` は operation name から決定論的に作る不透明な safe key であり、利用者は path を直接組み立てず `ucli ops describe <opName>` を正本として読む
- `ops list` は軽量 descriptor から `name` / `kind` / `policy` / `description` を表示用 model へ投影し、`--nameRegex` / `--kind` / `--maxPolicy` の AND 条件で絞り込める
- persisted catalog の読み込み失敗は access policy で分類し、CLI projection は永続化ファイルや freshness 計算の詳細へ依存しない

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
> raw `ucli.asset.*` は `assetPath` に加えて `projectAssetPath` も扱う。`ucli.asset.create` が作れるのは `Assets/` 配下の既存 folder に置く `.asset` main asset だけで、`ProjectSettings/` 直下や directory 自動作成は行わない。
> `ucli.asset.set` の object reference 値は `Plan` で request-local selector を検証できるが、`Call` で persistent asset へ書き込めるのは live state または temporary alias に解決できる参照だけで、preview-only selector は拒否する。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.asset.create` | mutation | advanced | mvp-core | concrete な `ScriptableObject` main asset を `Assets/` 配下へ新規作成する。 | `AssetCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.asset.schema` | query | safe | mvp-support | `ScriptableObject` 型、既存 main asset、または `ProjectSettings/*` の project-scoped asset の設定可能項目を取得する。 | `AssetSchemaArgs` | `IndexSchemaEntryJsonContract` | 対象型または対象 asset の serialized property schema |
| `ucli.asset.set` | mutation | advanced | mvp-core | 既存 main asset または `ProjectSettings/*` の project-scoped asset のシリアライズ値を更新する。 | `AssetSetArgs` | `UcliNoResult` | result は返さない |

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
> raw `scene` / `prefab` selector を使う primitive op は、`Call` では対応する Scene が loaded、または Prefab が opened stage であることを前提にする。`Plan` では同一 request の先行 primitive や edit lowering が作った request-local plan state を観測でき、まだ存在しない場合でも現在の loaded Scene / opened Prefab Stage から request-local plan state を確保して評価する。
> `ucli.comp.set` の object reference 値は `Plan` で request-local selector を解決できるが、`Call` で live component へ書き込めるのは live state または temporary alias に解決できる参照だけで、preview-only selector は拒否する。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.comp.ensure` | mutation | advanced | mvp-core | 対象に指定コンポーネントが存在する状態を保証する。 | `ComponentEnsureArgs` | `UcliNoResult` | result は返さない |
| `ucli.comp.schema` | query | safe | mvp-support | コンポーネント型の設定可能項目を取得する。 | `ComponentTypeArgs` | `IndexSchemaEntryJsonContract` | 対象 component 型の serialized property schema |
| `ucli.comp.set` | mutation | advanced | mvp-core | 対象コンポーネントのシリアライズ値を更新する。 | `ComponentSetArgs` | `UcliNoResult` | result は返さない |

## cs

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.cs.eval` | mutation | dangerous | v1.0 | C# source を Unity Editor process 内で Roslyn によりインメモリコンパイルし、同期 entry point を呼び出す。 | `CsEvalArgs` | `CsEvalResult` | compile 情報、digest、call 時のログ、戻り値、touched resource 宣言 |

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
> raw `scene` / `prefab` selector を使う primitive op は、`Call` では対応する Scene が loaded、または Prefab が opened stage であることを前提にする。`Plan` では同一 request の先行 primitive や edit lowering が作った request-local plan state を観測でき、まだ存在しない場合でも現在の loaded Scene / opened Prefab Stage から request-local plan state を確保して評価する。prefab context で `ucli.go.delete` は prefab root 自身を削除できない。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.go.create` | mutation | advanced | mvp-core | 指定親配下、または loaded Scene の root に GameObject を作成する。 | `GoCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.go.delete` | mutation | advanced | mvp-core | 指定 GameObject を削除する。prefab root は対象にできない。 | `GoTargetArgs` | `UcliNoResult` | result は返さない |
| `ucli.go.describe` | query | safe | mvp-support | GameObjectの構造とコンポーネント情報を取得する。plan では request-local ensured component も観測対象に含む。 | `GoDescribeArgs` | `GameObjectDescriptionResult` | `name`, `globalObjectId`, `components[]`, `children[]` を返す |
| `ucli.go.reparent` | mutation | advanced | mvp-core | 指定 GameObject の親を付け替える。 | `GoReparentArgs` | `UcliNoResult` | result は返さない |

## prefab

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.prefab.create` | mutation | advanced | mvp-core | Loaded Scene 上の GameObject から Prefab を新規作成する。`target` 必須、空 Prefab は作らない。 | `PrefabCreateArgs` | `UcliNoResult` | result は返さない |
| `ucli.prefab.open` | command | safe | mvp-core | 指定 Prefab を編集コンテキストとして開く。 | `PrefabPathArgs` | `UcliNoResult` | result は返さない |
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
| `ucli.scene.open` | command | safe | mvp-core | 指定 Scene が loaded であることを保証する。既に loaded なら再オープンしない。閉じている Scene を live で開くときは `OpenSceneMode.Single` を使う。 | `ScenePathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.query` | query | safe | mvp-core | scene context 内で selection candidate を列挙する。`/` を含む GameObject 名は `hierarchyPath` で表現できないため candidate に含めない。 | `SceneQueryArgs` | `SceneQueryResult` | `scene` と `matches[]` を返し、match は `kind`, `hierarchyPath`, `componentType` を持つ |
| `ucli.scene.save` | mutation | advanced | mvp-core | loaded Scene に dirty または request-attributed change があるとき保存する。loaded scene 必須。`Plan` は request-local plan state と計画時に観測できる dirty を基に評価し、`Call` は保存時点の live dirty も保存し得る。 | `ScenePathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.tree` | query | safe | mvp-support | Sceneの階層構造を取得する。loaded dirty scene があれば作業途中の階層を読み、未ロードなら保存済み asset を preview scene として読む。raw op でも `limit` / `cursor` を受け付け、既定 `limit=100`、最大 `10000` とする。 | `SceneTreeArgs` | `SceneTreeResult` | `path`、`childrenState` 付き root GameObject tree の `roots[]`、読み取り元の `sourceState`、bounded window の `window` を返す |
