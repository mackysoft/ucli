## 名称規則
- 正式名称：uCLI
- 名前空間・アセンブリ等（.NET / C#）：`Ucli`
- コマンド：`ucli`
## 対象
- CLI：.NET 8 以上
## コンセプト
**安全にUnityを編集できるCLIツール。**
- Unityを **ヘッドレス（batchmode）** で起動して実行できる（ワンショット）
- Unityを **常駐サーバ（デーモン）** として起動し、繰り返しリクエストを処理できる
- CLI起動、ユーザー起動のGUIインスタンス、両方でデーモンは起動する
- 変更は Unity Editor API（AssetDatabase / Scene / Prefab / SerializedObject 等）に限定する。YAML直編集を前提にしない
- すべての入出力はJSONを基本にする。
## アーキテクチャ
- コア：.NET製 CLI（`ucli`）
- サーバ：Unity Editor プラグイン（`Ucli.Unity`）
### 実行モード
- **デーモン（常駐）**
    - Unity Editor（GUI/ヘッドレスいずれも可）上で ucli サーバを起動
    - CLIはプロジェクト単位のエンドポイントへ接続してRPCを実行
- **ワンショット**
    - CLIが Unity Editor を `-batchmode` で起動
    - `-executeMethod` をエントリポイントとして JSONリクエストを処理
    - 実行後にUnityを終了
## 機能
- Editorで可能な基本操作を、**operation（オペレーション）** として提供
    - シーン操作（open/save/構造取得 等）
    - GameObject / Component 操作（作成、追加、プロパティ編集 等）
    - Prefab / Asset 操作（構造取得、プロパティ編集 等）
- `SerializedObject / SerializedProperty` ベースで **シリアライズ可能なプロパティ** を編集
    - `SerializeReference`（ManagedReference）用の操作も別opとして提供
- 参照解決は原則 **GlobalObjectId**（GlobalObjectIdが安定しない場面において GUID / asset path / hierarchy path を補助）
- 返すレスポンスは基本JSON。Unityのログはコンソールに流れない。
- allowlistで使用可能オペレーションを管理できる
## `plan` / `call` の基本入力
`plan` と `call` は JSONリクエストを受け取る。
パッチの実行は主に既定のopを束ねることで行う。または、Unity側で新しいopを追加し、より複雑なopを実装することが出来る。
リクエストは原則として1本ずつの直列実行。
- `plan`：リクエストを **実変更なし**（または最小の観測のみ）で検証・解決し、差分見積りを返す
- `call`：同じリクエストを **実際に適用して保存**する
    - `call` は実行前に `plan` 相当（validate/resolve/差分計算/実行可能性確認）の検証を挟む（oneshot時の二重起動を避けるため）
    - uCLIでの操作による永続化は`call`でのみ可能
## 入力方法（CLI）
- 基本：**stdin のJSONを読む**
    - `ucli plan < request.json`
    - `ucli call < request.json`
- オプション：`--requestPath <jsonPath>` で JSONパッチファイルを指定可能
## オペレーション
オペレーションは、JSONの `ops[]` に並ぶ **最小ステップ**の処理単位。  
`plan`/`call` は同じ `ops[]` を、実行フェーズ（plan/call）だけ変えて回す。
### 命名規約
- コア：`ucli.<domain>.<verb>`（例：`ucli.scene.open`, `ucli.comp.set`）
- 拡張：`<org>.<domain>.<verb>`（例：`myorg.navmesh.bake`）`
### allowlist
allowlistで使用可能opを定義する。正規表現を使用可能。
ucli.はデフォルトで使用可能。
### opのメタデータ
- `name`：op名（例：`ucli.comp.set`）
	- デフォルトではdangerousは使用不可（設定で変更する）
	- 基本的な操作はsafeだけでも回るようにする
- `kind`：`query | mutation`
- `policy : safe | advanced | dangerous`
	- dangerousは既定で無効
- `argsSchema`：引数 `args` のJSONスキーマ
### 実行フェーズ
各opは内部的に次の3フェーズを持つ（`call`は事前にplan相当を必ず行う）。
- **Validate**：引数・型・存在・許可プロファイル等の検証（副作用なし）
- **Plan**：対象解決（GlobalObjectId化）、差分見積り、影響範囲（touch）算出（保存しない）
- **call**：Unity Editor APIで変更→必要に応じて保存
### 登録方式
Unity起動時に、Editorアセンブリ内の op 実装を走査して **オペレーションレジストリに登録**する。
- op実装クラスに `[UcliOperation]` 属性を付与
- `IUcliOperation`を実装
- 起動時に列挙し、`OperationRegistry` に追加
- これにより `ucli ops list/describe` が **動的にopを列挙**できる
### 暫定設計構想
```cs
public interface IUcliOperation
{
    OperationMeta Meta { get; }

    void Validate(JsonElement args, OpContext ctx);
    OpResult Plan(JsonElement args, OpContext ctx);
    OpResult Apply(JsonElement args, OpContext ctx);
}

public sealed class OpContext  
{  
	public string ProjectPath { get; }  
	public UcliPhase Phase { get; }  
	public OperationSafety Profile { get; }
	  
	public ValueStore Values { get; }  
	public IResolver Resolver { get; }  
	  
	public CancellationToken CancellationToken { get; }
}

public sealed class ValueStore  
{  
	private readonly Dictionary<string, UcliValue> values = new();
}
public enum UcliValueKind  
{  
	UnityObject, // UnityEngine.Object  
	Type, // System.Type  
	Json, // JsonElement (object/array含む)  
	String,  
	Number,  
	Bool,  
	Null  
}  
  
public sealed class UcliValue  
{  
	public UcliValueKind Kind { get; }  
	public object? Value { get; }
}

public sealed record VarRef(string Name);  
  
public enum SelectorKind  
{  
	GlobalObjectId, AssetGuid, AssetPath, HierarchyPath, Query  
}

public abstract record UcliRef  
{  
	public sealed record Var(VarRef Ref) : UcliRef;  
	public sealed record Selector(SelectorRef Ref) : UcliRef;  
}

public interface IResolver  
{  
	UnityEngine.Object ResolveUnityObject(UcliRef reference, OpContext ctx);  
	T ResolveUnityObject<T>(UcliRef reference, OpContext ctx) where T : UnityEngine.Object;
}  
  
public sealed record TypeRef(string Name, string? Assembly = null, bool ExpectUnique = true);
```
## JSONパッチ（リクエスト）のスキーマ概要
### 全体構造（最小）
- `requestId`：追跡用UUID
- `ops`：実行するオペレーション配列（順次実行）
### op（1要素）の最小構造
- `id`：op識別子（ログ・結果参照用）
- `op`：op名（例：`ucli.scene.open`）
- `args`：op引数（`ops describe`が返すschemaで検証）
- `as`：後続参照用の変数名（任意）
- `expect`：件数・成功条件（特に `resolve/query` 系で誤爆防止）
### 参照表現
- 変数参照：`{ "var": "<name>" }`（例：`{ "var": "go" }`）
- セレクタは `ucli.resolve` の `args` として表現し、`plan` 時点で一意化する運用を基本とする
### 任意コード呼び出し（`dangerous`）
`UcliEntryPoint`属性の関数を呼び出し、uCLIの規定opでの処理が難しいフローを実行できる`ucli.cs.invoke`op。
JSONがレスポンスとして返る。
1. エージェントが使い捨てのコードをプロジェクトに実装（または既存のコードでも可）
2. コードが追加されているなら、通常コンパイルに乗せる（ドメインリロード）
3. Reflectionで実行
コンパイル、デーモンの観点から基本的には`--oneshot`推奨
```cs
using System.Text.Json;
using UnityEditor;

[UcliEntryPoint("MyOrg.Tools.RebuildNavmesh.Run")]
public static class RebuildNavmesh
{
    // 同期で完結（oneshotで -quit できる）
    public static UcliExecResult Run(UcliExecContext ctx, JsonElement args)
    {
        // ...任意のEditor API呼び出し...
        return UcliExecResult.Ok(new { changed = true });
    }
}
```
### パッチJSON例
```json
{
  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
  "ops": [
    { "id": "open", "op": "ucli.scene.open", "args": { "path": "Assets/Scenes/Main.unity" } },

    { "id": "resolveGO", "op": "ucli.resolve", "as": "go",
      "args": { "scene": "Assets/Scenes/Main.unity", "hierarchyPath": "Root/Enemies/Spawner" },
      "expect": { "count": 1 }
    },

    { "id": "ensureComp", "op": "ucli.comp.ensure", "as": "spawner",
      "args": { "target": { "var": "go" }, "type": "Game.EnemySpawner, Assembly-CSharp" }
    },

    { "id": "setSpawner", "op": "ucli.comp.set",
      "args": { "target": { "var": "spawner" }, "mode": "atomic", "sets": [  
{ "path": "spawnInterval", "value": 3.0 },  
{ "path": "maxCount", "value": 10 },  
{ "path": "weights.Array.data[0]", "value": 0.25 }  
]
    },

    { "id": "save", "op": "ucli.scene.save", "args": { "path": "Assets/Scenes/Main.unity" } }
  ]
}
```
## コマンド
すべてのコマンド（`daemon`を除く）は共通で `--oneshot` を受け取れる（デーモンを使わず `-batchmode` 起動→実行→終了）
`--oneshot`ではない場合にデーモンが見つからない場合はエラーになる。
CWDがUnityプロジェクトと判定可能な場合はそれを使う。そうでない場合は、`--projectPath`を指定する必要がある。
- `ucli validate`：JSONリクエストの静的検証（スキーマ/必須項目/許可op等）
- `ucli plan`：対象解決・差分見積り（実変更なし、または最小）を返す
- `ucli call`：Unityへリクエストを送って実行し、保存する（実行前にplan相当の検証を挟む）
    - `--withPlan`：callレスポンスにplan相当（resolved/diff等）を同梱する（任意）
- `ucli resolve`：セレクタ（例：scene+hierarchyPath 等）を GlobalObjectId 等へ解決する
- `ucli query`：検索・構造取得・スキーマ取得（規定操作）
    - 例：`scene.tree` / `go.describe` / `comp.schema` / `asset.schema` / `assets.find` / `scenes.findComponents`
- `ucli refresh`：AssetDatabase更新、インポート、コンパイル等でプロジェクトを最新化する
- `ucli ops`：利用可能なオペレーション一覧・詳細
    - `list`：利用可能なオペレーション一覧
    - `describe <opName>`：特定オペレーションの引数スキーマ
- `ucli status`：
	- CWDか`--projectPath`でプロジェクト指定
	- `--oneshot`であればエディタ解決、実行可能かどうか確認。そうでない場合は、接続を試行して結果を返す。`mode: gui/headless`, `unityVersion`, `ucliUnityVersion` `compileState` 等
- `ucli daemon`：常駐サーバの管理
    - `start`：デーモンを起動（ヘッドレス/GUIインスタンスへのattach方針は実装で規定）
    - `stop`：デーモンを停止
    - `status`：デーモンの状態取得
	    - daemon startで起動した管理対象の情報を返す
	    - ユーザーがGUIで起動しているUnityサーバーはmanagedではない
    - `logs`：デーモンログを取得
    - `--projectPath <path>`：対象Unityプロジェクトを指定
## サーバー
別ディレクトリのworktreeは別fingerprintの別デーモンでなければならない。
同一ディレクトリであれば同一デーモン。
### 識別
- `projectRoot = realpath(CWD or --projectPath)`
- `projectFingerprint = SHA256(normalize(projectRoot))`（OS差分を吸収した正規化を必須）
### エンドポイント
OSごとに最適なローカルIPCを選ぶ。
Windows：NamedPipe
Mac・Linux：Unix domain socket
フォールバック：TCP
### デーモン起動
#### CLI
- プロジェクト解決
- エンドポイント接続試行
- 接続できない場合は、ヘッドレス・oneshotでUnity起動
#### Unity
- サーバー起動（GUI・ヘッドレス両方）
- リクエスト受信->メインスレッド実行キュー->レスポンス
- ドメインリロード後の復帰