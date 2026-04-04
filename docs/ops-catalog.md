# Ops Catalog

> [!NOTE]
> この文書は補助カタログである。
> 入力 DSL の正本は [json-request-spec.md](json-request-spec.md)、全体契約とコマンド仕様は [uCLI.md](uCLI.md) を参照する。

## asset

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.asset.create` | mutation | advanced | mvp-core | concrete な `ScriptableObject` Asset を新規作成する。 | `{ type, path }` |
| `ucli.asset.schema` | query | safe | mvp-support | `ScriptableObject` 型、または既存 main asset の設定可能項目を取得する。 | `{ type }` または `{ target }` |
| `ucli.asset.set` | mutation | advanced | mvp-core | 既存 main asset のシリアライズ値を更新する。 | `{ target, sets[] }` |

## assets

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.assets.find` | query | safe | mvp-support | 条件に一致するAssetを検索する。 | 予定 |

## comp

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.comp.ensure` | mutation | advanced | mvp-core | 対象に指定コンポーネントが存在する状態を保証する。 | 予定 |
| `ucli.comp.schema` | query | safe | mvp-support | コンポーネント型の設定可能項目を取得する。 | 予定 |
| `ucli.comp.set` | mutation | advanced | mvp-core | 対象コンポーネントのシリアライズ値を更新する。 | 予定 |

## cs

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.cs.invoke` | mutation | dangerous | experimental | 指定エントリポイントの任意コードを呼び出す。 | 予定 |

## go

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.go.create` | mutation | advanced | mvp-core | 指定親配下にGameObjectを作成する。 | 予定 |
| `ucli.go.delete` | mutation | advanced | mvp-core | 指定 GameObject を削除する。 | 予定 |
| `ucli.go.describe` | query | safe | mvp-support | GameObjectの構造とコンポーネント情報を取得する。 | 予定 |
| `ucli.go.reparent` | mutation | advanced | mvp-core | 指定 GameObject の親を付け替える。 | 予定 |

## prefab

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.prefab.create` | mutation | advanced | mvp-core | Loaded Scene 上の GameObject から Prefab を新規作成する。`target` 必須、空 Prefab は作らない。 | 予定 |
| `ucli.prefab.open` | query | safe | mvp-core | 指定 Prefab を編集コンテキストとして開く。 | 予定 |
| `ucli.prefab.save` | mutation | advanced | mvp-core | 現在開いている指定 Prefab を保存する。 | 予定 |

## project

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.project.refresh` | mutation | advanced | mvp-support | AssetDatabase更新やインポートを実行する。 | 予定 |
| `ucli.project.save` | mutation | advanced | mvp-support | プロジェクト内の未保存変更を保存する。 | 予定 |

## resolve

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.resolve` | query | safe | mvp-core | セレクタを対象オブジェクト参照へ解決する。 | 予定 |

## scene

| op | kind | policy | status | 概要 | argsSchema |
| --- | --- | --- | --- | --- | --- |
| `ucli.scene.open` | query | safe | mvp-core | 指定Sceneを開いて編集対象にする。 | 予定 |
| `ucli.scene.query` | query | safe | mvp-core | scene context 内で selection candidate を列挙する。 | `{ scene, pathPrefix?, componentType? }` |
| `ucli.scene.save` | mutation | advanced | mvp-core | 指定Sceneの変更を保存する。 | 予定 |
| `ucli.scene.tree` | query | safe | mvp-support | Sceneの階層構造を取得する。 | 予定 |
