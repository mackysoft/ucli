# Ops Catalog

> [!NOTE]
> この文書は補助カタログである。
> 入力 DSL の正本は [json-request-spec.md](json-request-spec.md)、全体契約とコマンド仕様は [uCLI.md](uCLI.md) を参照する。
> operation ごとの機械可読な正本は `ops describe` の `argsSchema` / `resultSchema` であり、これらは `MackySoft.Ucli.Contracts` の Args/Result contract 型から生成される。

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
| `ucli.assets.find` | query | safe | mvp-support | `Assets/` 配下の persistent main asset を検索する。`Plan` は request-local planned asset と asset shadow を観測し、`Call` は live Unity state のみを観測する。primitive op 自体は limit / cursor を持たない。 | `AssetsFindArgs` | `AssetsFindResult` | `matches[]` に `assetPath`, `assetGuid`, `name`, `typeId` を返す |

- `type` は stable `typeId` を受け取り、runtime type が指定型へ assignable な main asset を一致とみなす
- `pathPrefix` は `Assets` またはその配下を受け取り、ordinal prefix で比較する
- `nameContains` は main asset 名に対する大小文字無視の部分一致で評価する
- `result.matches[]` は `assetPath`, `assetGuid`, `name`, `typeId` を返し、`assetPath` の ordinal 昇順で並ぶ
- `touched` は常に空配列で、結果フィールド定義は [uCLI-property-reference.md](uCLI-property-reference.md) を参照する

## comp

> [!NOTE]
> raw `scene` / `prefab` selector を使う primitive op は、`Call` では対応する Scene が loaded、または Prefab が opened stage であることを前提にする。`Plan` では同一 request の先行 primitive や edit lowering が作った request-local plan state を観測でき、まだ存在しない場合でも現在の loaded Scene / opened Prefab Stage から request-local plan state を確保して評価する。
> `ucli.comp.set` の object reference 値は `Plan` で request-local selector を解決できるが、`Call` で live component へ書き込めるのは live state または temporary alias に解決できる参照だけで、preview-only selector は拒否する。

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.comp.ensure` | mutation | advanced | mvp-core | 対象に指定コンポーネントが存在する状態を保証する。 | `ComponentEnsureArgs` | `UcliNoResult` | result は返さない |
| `ucli.comp.schema` | query | safe | mvp-support | コンポーネント型の設定可能項目を取得する。 | `TypeArgs` | `IndexSchemaEntryJsonContract` | 対象 component 型の serialized property schema |
| `ucli.comp.set` | mutation | advanced | mvp-core | 対象コンポーネントのシリアライズ値を更新する。 | `ComponentSetArgs` | `UcliNoResult` | result は返さない |

## cs

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.cs.invoke` | mutation | dangerous | experimental | 指定エントリポイントの任意コードを呼び出す。 | 予定 | 予定 | 予定 |

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
| `ucli.prefab.open` | query | safe | mvp-core | 指定 Prefab を編集コンテキストとして開く。 | `PathArgs` | `UcliNoResult` | result は返さない |
| `ucli.prefab.save` | mutation | advanced | mvp-core | opened Prefab に dirty または request-attributed change があるとき保存する。opened stage 必須。`Plan` は request-local plan state と計画時に観測できる dirty を基に評価し、`Call` は保存時点の live dirty も保存し得る。 | `PathArgs` | `UcliNoResult` | result は返さない |

## project

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.project.refresh` | mutation | advanced | mvp-support | AssetDatabase更新やインポートを実行する。 | `UcliEmptyArgs` | `UcliNoResult` | result は返さない |
| `ucli.project.save` | mutation | advanced | mvp-support | request 中に追跡した open Scene / opened Prefab の変更と、Unity の project save が扱う asset / project settings を保存する。`Plan` は既知の request-attributed resource だけを返し、実際の asset / project settings touched は `Call` 観測に依存する。保存はトランザクションではなく、失敗時でも先行保存が残り得る。 | `UcliEmptyArgs` | `UcliNoResult` | result は返さない |

## resolve

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.resolve` | query | safe | mvp-core | セレクタを対象オブジェクト参照へ解決する。 | `ResolveSelectorArgs` | `IpcResolveOperationResult` | 解決済み `globalObjectId` を返す |

## scene

| op | kind | policy | status | 概要 | Args | Result | result 概要 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ucli.scene.open` | query | safe | mvp-core | 指定 Scene が loaded であることを保証する。既に loaded なら再オープンしない。閉じている Scene を live で開くときは `OpenSceneMode.Single` を使う。 | `PathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.query` | query | safe | mvp-core | scene context 内で selection candidate を列挙する。`/` を含む GameObject 名は `hierarchyPath` で表現できないため candidate に含めない。 | `SceneQueryArgs` | `SceneQueryResult` | `scene` と `matches[]` を返し、match は `kind`, `hierarchyPath`, `componentType` を持つ |
| `ucli.scene.save` | mutation | advanced | mvp-core | loaded Scene に dirty または request-attributed change があるとき保存する。loaded scene 必須。`Plan` は request-local plan state と計画時に観測できる dirty を基に評価し、`Call` は保存時点の live dirty も保存し得る。 | `PathArgs` | `UcliNoResult` | result は返さない |
| `ucli.scene.tree` | query | safe | mvp-support | Sceneの階層構造を取得する。 | `SceneTreeArgs` | `SceneTreeResult` | `path` と root GameObject tree の `roots[]` を返す |
