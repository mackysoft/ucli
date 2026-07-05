---
name: "unity-authoring-rules"
description: "Unity プロジェクトの C# コード、ゲームコード、Editor 拡張、テスト、Asset、Scene、Prefab、asmdef、プロジェクト構成を実装、修正、レビューするときに、C# 一般ルールと併用して Unity 固有の判断規則を適用する。Runtime、Editor、Test の境界、MonoBehaviour、SerializeField、UnityEngine.Object の寿命、Unity が管理する状態、生成物、ScriptableObject、Domain Reload、Unity メインスレッド、永続化を伴う副作用、依存方向の判断で使う。"
---

# unity-authoring-rules

## 目的
Unity プロジェクトの実装、修正、レビューで適用する Unity 固有のルールを定める。
Unity の実行モデル、シリアライズ、UnityEngine.Object の寿命、生成物、Asset、Scene、Prefab、ScriptableObject、asmdef の制約を確認する。

## 優先順位
1. プロジェクト固有の明示規約を優先する。
2. Unity 固有の制約と C# 一般の設計判断が衝突する場合は、Unity 固有の制約を満たす。
3. 既存コードが責務境界や依存境界を崩している場合は、現状を正当化せず、変更範囲で改善できる境界を選ぶ。

## C# 一般ルールとの関係
Unity 固有の制約がない通常の C# クラス、ドメインロジック、変換、判定、テスト対象コードには、$csharp-authoring-rules も適用する。
Unity の実行モデル、シリアライズ、Inspector、Scene、Prefab、Asset、Editor API、Unity のライフサイクルに関わる判断では、Unity 固有の制約を満たしたうえで C# としての責務境界を保つ。

## ルール

### Runtime と Editor の境界
Runtime コードから Editor 専用 API へ依存させない。
Editor 拡張は Editor 側に閉じる。
Runtime と Editor の両方で使う契約は、どちらにも依存しない場所へ分ける。
Editor 用の処理を条件分岐だけで Runtime 側へ混在させない。
Test の都合を Runtime の依存方向へ逆流させない。

### MonoBehaviour の責務
`MonoBehaviour` には Unity のライフサイクル、Scene との接続、Inspector から設定される状態を置く。
純粋な計算、判定、変換、ドメインロジックは通常の C# クラスへ分ける。
`MonoBehaviour` を、すべての依存を集める場所として使わない。
Scene 上の接続責務とアプリケーション方針を同じコンポーネントへ混在させない。

### 通常 C# クラスとの分担
Unity に依存しない処理は、通常の C# クラスとして表す。
通常の C# クラスに `GameObject`、`Transform`、`Component` への操作を広げる場合は、Unity 境界として扱う。
Unity 境界を越える処理は、呼び出し元と副作用が追える形にする。

### Unity が管理する状態
Scene、Prefab、Asset、Inspector、Serialization が管理する状態と、コードが管理する状態を分ける。
Inspector で設定される値は、実行時にどこで確定するかを明確にする。
実行時状態を `SerializeField` に逃がさない。
永続化される状態と Play Mode 中だけの状態を混同しない。

### UnityEngine.Object の寿命と null
`UnityEngine.Object` 派生型は、通常の C# 参照型と同じ null 規則で扱わない。
`GameObject`、`Component`、`MonoBehaviour`、`ScriptableObject`、Asset などの生存判定では `?.`、`??`、`??=`、`is null`、`is not null`、`ReferenceEquals` を使わない。
Unity オブジェクトを条件式にそのまま置く暗黙的な bool 変換も使わない。
Unity オブジェクトの生存判定は `== null` または `!= null` を使う。
条件演算子 `?:` で分岐する場合も、条件式に `== null` または `!= null` を明示する。
破棄済み、参照先が失われた状態、Unity 側から切り離された状態の可能性がある参照は、保持元、解除元、再取得条件を明確にする。

### 生成物と手書きコード
自動生成、インポート、変換で作られるコードや Asset は、手書きコードと同じ責務として扱わない。
生成物を編集して設計を成立させない。
生成物に依存する手書きコードは、生成元、再生成条件、失敗時の扱いを追える形にする。
生成物を変更対象に含める場合は、生成元の契約も同じ変更範囲で確認する。

### `SerializeField` と公開 API
`SerializeField` は Inspector 入力契約として扱う。
設定必須、未設定時の扱い、Prefab override の可否、実行時変更の有無を明確にする。
Inspector から設定するためだけにフィールドを `public` にしない。
外部コードから操作させる必要がない値は `SerializeField` と非公開フィールドで表す。
公開 API は、Inspector 用ではなくコード上の契約として必要な場合だけ公開する。
Inspector 表示のための構造と、実行時の契約を同じ型へ無理に詰め込まない。
`RequireComponent` は、同じ `GameObject` 上に必要なコンポーネントを置く設計契約として使う。
既存インスタンス、Prefab、既存 Scene、手動変更で欠落する可能性を無視しない。
`RequireComponent` の対象コンポーネントは Inspector 入力として重複管理せず、原則として `Awake` などで `GetComponent` して保持する。
欠落時の扱いが実行時契約に影響する場合は、検証または早期失敗を入れる。

### Asset、Scene、Prefab への副作用
Asset、Scene、Prefab を変更する処理は、いつ、何を、どの単位で変更するかを明確にする。
Editor 処理で永続化される変更と、Play Mode 中だけの変更を混同しない。
自動生成やインポート処理は、手書きコードと責務を分ける。
永続化を伴う Editor 操作では、Undo、dirty 状態、保存範囲を確認する。
Prefab や Scene に保存される参照を変える場合は、コード変更ではなく永続化された状態の変更として扱う。
Unity にコードファイルや Asset を追加、削除、移動した場合は、Unity 正規の `meta` 生成と更新を前提にする。
`meta` 更新を避けるために、別ファイルにすべき型や責務を同じファイルへ押し込まない。

### Play Mode と Edit Mode
Play Mode でだけ成立する状態と、Edit Mode でも成立する状態を分ける。
Edit Mode で実行される処理は、永続化される変更と一時的な変更を区別する。
Play Mode の初期化結果を、Scene や Asset の初期状態として扱わない。
実行環境が変わると結果が変わる処理は、どの環境を契約にするかを明確にする。

### Scene と Prefab の依存
コードが Scene 上の配置、名前、階層、Inspector 参照に依存する場合は、その依存を接続責務として扱う。
Scene や Prefab に置かれた参照を、通常 C# クラスの暗黙の依存にしない。
Prefab の再利用範囲を超える方針や状態を、Prefab 内のコンポーネントへ閉じ込めない。
Scene ごとの接続と、ゲーム全体の方針を同じ場所へ混在させない。

### ScriptableObject の責務
`ScriptableObject` は、共有設定、データ Asset、Editor 上の入力単位としての責務を明確にする。
実行時に変わる状態を、共有 Asset の状態として扱わない。
データ定義とサービス実行を同じ `ScriptableObject` へ混在させない。
Asset として共有される値を変更する処理では、変更が他の参照元へ波及することを確認する。
実行時に変更する値は、共有 Asset を変更しているのか、実行時インスタンスを変更しているのかを分ける。
共有 Asset を変更しない設計では、複製、セッション中の状態、保存データの境界を明確にする。

### ライフサイクル
`Awake`、`OnEnable`、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnDisable`、`OnDestroy`、`OnValidate`、`Reset` の責務を混ぜない。
初期化、購読、実行時更新、解除を対応させる。
購読解除や破棄が必要な処理は、生成元と解放元が追える形にする。
毎フレーム更新、物理更新、追従更新、Inspector 値の検証、コンポーネント追加時の既定値設定を同じ処理へ混在させない。
毎フレーム処理に、初期化や探索を混在させない。
`OnValidate` に重い処理や永続副作用を置かない。

### Domain Reload と static 状態
Domain Reload の有効、無効で挙動が変わる static フィールド、static イベント、シングルトン、キャッシュ、ID 生成器を導入または変更するときは、Play Mode 間の初期化とリセット責務を確認する。
Domain Reload 無効時に前回の Play Mode の状態が残っても正しく動くようにする。
static イベントの購読は、購読元と解除元を対応させる。

### async と Unity メインスレッド
Unity API、Scene、Asset、Prefab、GameObject、Component、Transform に触れる処理は、Unity メインスレッドで実行する。
バックグラウンドスレッドや async 処理では、Unity オブジェクトを直接読まない、作らない、破棄しない、Scene を変更しない。
async、Coroutine、Job System の選択では、Unity オブジェクトに触る境界、フレームをまたぐ責務、停止条件を確認する。
Play Mode 終了、Scene の unload、GameObject の destroy、Component の disable で非同期処理が停止または解除されるか確認する。
Play Mode 終了で停止する処理は、`Application.exitCancellationToken` と結びつける。
コンポーネント、Scene、サービスの寿命で止める処理は、その寿命に対応するキャンセル契約を用意する。

### 割り当て
毎フレーム呼ばれる処理で不要な割り当てを増やさない。
頻繁に実行される処理では、コレクション生成、LINQ、文字列生成、クロージャ生成を必要性で判断する。
最適化のために責務境界や可読性を壊さない。

### asmdef と依存方向
asmdef がある場合は、Runtime、Editor、Test、パッケージ境界の依存方向を確認する。
Editor assembly から Runtime assembly への依存は許容できるが、Runtime から Editor へ依存させない。
テスト assembly の依存を本体コードへ逆流させない。
パッケージ境界を越える依存は、公開契約を通す。

### テスト
Unity が必要な検証と、通常の C# テストで足りる検証を分ける。
Unity のライフサイクル、Scene、Prefab、Asset が関係しないロジックは通常の C# テストを優先する。
Unity 上でしか成立しない挙動は、Unity の実行環境で検証する。
テストのために Runtime と Editor の境界を崩さない。

## 適用結果
変更後またはレビュー後に、適用した Unity 固有ルール、影響した永続化状態、未確認事項を残す。
