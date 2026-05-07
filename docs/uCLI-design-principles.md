> [!IMPORTANT]
> この文書は、uCLI の設計原則の正本である。
> 製品概要とコマンド契約は [uCLI.md](uCLI.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。

## 目的

uCLI は、Unity を外部から操作するための単なる便利 CLI ではない。  
uCLI の目的は、**Unity に対する変更・観測・保存・検証を、保証可能な手続きとして扱える実行基盤を提供すること**である。

uCLI は次を同時に満たすことを目指す。
- Unity を外部から扱えること
- Unity の意味論を壊さずに扱えること
- AI / エージェントが使っても、人間が保証責務を引き取れること
- GUI 前提に閉じないこと
- worktree / CI / headless 実行に耐えること
## 基本的な立ち位置
uCLI は、次のどれでもない。
- 単なる YAML 編集ツール
- Unity Editor を遠隔操作するだけのブリッジ
- 任意コード実行を中心にした汎用 RPC
- 便利さ最優先の自動化レイヤ
    
uCLI は、**Unity 変更の実行プロトコル**である。  
主眼は「何ができるか」より、**何を前提に、何を変え、どこまで確認し、どう保存したかを明示できること**にある。
## 最上位原則

### Context-first
すべての操作は、まず **どの責任境界に属するか** を持たなければならない。  
uCLI における基本境界は次である。
- Scene
- Prefab
- Asset
- Project

uCLI は object 操作系 DSL ではなく、**context-bound execution model** を採る。
### Primitive-first
高級構文や sugar を導入しても、primitive operation を捨てない。  
Scene を開く、保存する、resolve する、describe する、refresh する、といった低レベル操作は first-class のまま維持する。
### Assurance-first
uCLI は「便利に触れること」より、**保証できること**を優先する。  
変更可能性ではなく、変更の説明可能性・再現可能性・監査可能性を重視する。
### Lowerable
すべての高級構文は、機械的に primitive operation に lower できなければならない。  
lower 不能な sugar は採用しない。
## 操作モデル原則
### Query と Mutation を分離する
検索・観測と変更を混ぜない。  
query の結果はそのまま mutation に流さず、必ず **selection / stable target** に正規化してから編集する。
### Mutation は live Unity 実体を正とする
local index や cache は観測補助であり、mutation の正本ではない。
`call` 実行時は必ず live Unity 実体で再解決・再検証する。
### Modify と Persist を分離する
変更と保存を分ける。  
uCLI では **modify != persist** を原則とし、保存境界は常に明示する。
### One step, one context
1つの編集単位は、1つの context に閉じる。  
複数 Scene や複数 Prefab をまたぐ処理は、1つの巨大操作にしない。request / step の列として構成する。
### Multi-target は制限付きで許可する
一括編集は許可するが、一般化しすぎない。  
許容するのは、**単一 context 内・同一型・同一 patch の一括適用**に限る。
## 参照と識別の原則

### Human-friendly reference は入力専用
自然言語的・検索的な指定は入力として許可する。  
ただし変更前には、必ず **durable identity** に正規化する。
### Stable ref を中核に置く
uCLI の内部・結果・再実行では、曖昧参照に依存しない。  
会話的参照や一時 ID を長寿命の識別子に使わない。
### Local binding only
`as` のような束縛機構は残してよいが、**局所束縛**に限定する。  
step をまたぐ受け渡しは durable value のみとする。
## 保存・責任境界の原則

### 保存境界は Context に従う
保存は target 単位ではなく、context 単位で定義する。  
Scene は Scene、Prefab は Prefab、Project は Project として保存責任を持つ。
### Project は上位境界として扱う
Project は単なるファイルの集合ではなく、**整合・更新・保存の上位責任境界**である。  
Project 配下で扱うのは、project-scoped settings object、singleton、project-wide mutable state とする。
### Asset と ProjectAsset を混同しない
通常 asset は asset context に属する。  
project-scoped settings は project context に属する。  
両者を同じ編集モデルに潰さない。
## 実行と接続の原則

### 接続は状態機械として扱う
接続は単なる `接続中 / 非接続` ではなく、状態として扱う。  
少なくとも、稼働中・再読込中・非互換・利用不能などを区別する。
### Domain reload / compile / reimport は第一級イベント
再読込や再コンパイルは異常ではなく、Unity の通常ライフサイクルである。  
uCLI はこれを例外でなく、仕様化された状態遷移として扱う。
### Single project lifecycle lock per physical UnityProjectRoot
同一物理 `UnityProjectRoot` に対する Unity process 起動は、常に 1 つの project lifecycle lock で直列化する。
daemon / oneshot / `test.run` は worktree / storage root / `projectFingerprint` が異なっても同じ lifecycle lock に参加する。
`daemon stop` / `daemon cleanup` は Unity process を起動しないが、session 操作を直列化するため同じ lifecycle lock に参加する。
`projectFingerprint` は session / IPC / artifact / readIndex の識別に使い、project lifecycle lock の identity と混同しない。
### GUI と headless を同列に扱う
uCLI は GUI 依存にしてはならない。  
ただし headless だから偉いのではなく、**どの runtime で動いても同じ契約を守る**ことを優先する。
### Idle は inert である
idle 時の runtime は zero-ish work を原則とする。
index refresh、package check、health sweep のような背景作業は、明示コマンドか厳密な TTL でしか走らせない。
### Reload-safe に資源を扱う
domain reload や異常終了は通常事象として扱う。
file、log、session、artifact は reload generation をまたいで reopen できる前提で設計する。
### 暗黙起動を増やさない
便利さのための自動起動・自動接続・自動修復は慎重に扱う。  
暗黙挙動は負債化しやすいため、uCLI では明示操作を優先する。
## 互換性と拡張性の原則
### Version より Capability を重視する
単純な version string 一致だけで可否を決めない。  
利用可能な機能、transport、runtime、schema を明示的に扱う。
### Transport は意味を変えない
transport は搬送路であり、意味論を変える理由ではない。
差異がある場合は、黙って欠落させず capability として明示する。
### Contract 型を唯一の正本にする
README、help、skill、tool description を別々に人手で保守しない。  
operation ごとの Args/Result contract 型と operation metadata を公開 contract の正本にする。複数 operation で同じ意味を持つ scalar 入力/出力は C# contract 上の semantic value type として表し、IPC JSON では primitive JSON 値のまま扱う。asset path、hierarchy path、GlobalObjectId、asset GUID、type identifier のような値は、必要な制約属性を持つ dedicated value type に寄せる。入力ごとの説明と意味制約は Args property または semantic value type の属性に置き、`inputs[]` はそこから生成する。operation 全体の description / resultContract / assurance metadata は operation metadata に置く。request-local alias は `edit` lowering の内部 primitive 間だけで使い、public raw `op` の contract には出さない。
`argsSchema` / `resultSchema` はその contract から生成される JSON 構造検証用 schema とし、agent 向けの主契約にはしない。意味制約は JSON Schema の低レベル keyword ではなく Args 属性から生成される `inputs[].constraints` と `inputs[].variants[].fields[].constraints` に置く。
### Operation は1つのユーザー意図を表す
1つの operation に複数の意味 variant を持たせない。
description が「A または B」になる operation、必須入力セットによってユーザー意図が変わる operation、result 解釈や policy / sideEffects / planMode が変わる operation は分割対象とする。

同じ対象を指定する複数の方法は operation variant ではなく input variant として扱う。たとえば `globalObjectId`、`sceneHierarchy`、`prefabHierarchy` は `GameObjectReferenceArgs` などの reference input の表現方法であり、`ucli.go.delete` 自体の意味を増やすものではない。
### Unsafe path は隔離する
任意コード実行や危険操作は認めてもよいが、safe 系の主経路とは分ける。  
危険操作に safe と同じ保証を与えない。
### Safe core は typed operation で閉じる
safe 系主経路は `typed reference + typed op + explicit context` を基準にする。
generic setter や任意コード実行を safe core の前提にしない。
selector は `GameObjectReferenceArgs`、`ComponentReferenceArgs`、`AssetReferenceArgs` などの contract 型で受け取り、解決済み `UnityEngine.Object` は Unity 実装層の内部状態に閉じ込める。
operation 本体は `JsonElement` 起点ではなく typed Args を受け取り、JSON は IPC と schema validation の境界に閉じ込める。
### Sugar は正本ではない

短縮構文は許容してよい。  
ただし正本は primitive とその lower 規則であり、sugar に設計の中心を置かない。

## 証拠と結果の原則

### Evidence-first
uCLI は成功/失敗だけを返すべきではない。  
何を前提に、何を触り、何が変わり、何が保存され、何が観測されたかを返すべきである。
### Failure は not-applied と indeterminate を区別する
失敗時は「未適用」と「状態不明」を混同しない。
timeout / disconnect / reload 中断では、適用可否と証拠を機械可読に返すことを優先する。
### 機械可読を優先する

ログ断片や人間向けメッセージではなく、構造化された結果を優先する。  
人間向け説明は必要だが、正本は機械可読データとする。
### AI向け列挙は bounded-by-default
AI が消費する一覧・ツリー・ログは、既定で shallow / deterministic order / incremental retrieval とする。
全件列挙は opt-in とし、bounded でない出力を主経路にしない。

### touched を第一級に扱う

影響した永続化単位は必ず把握可能であるべきである。  
uCLI は「何を触ったか」を曖昧にしない。
## 設計負債に対する方針

### 負債は契約の曖昧さとして捉える

uCLI の負債は「コードが汚いこと」より、

- 接続境界
- 保存境界
- 参照境界
- 互換性境界
- 証拠境界  
が曖昧になることから発生する。
### 暗黙を増やさない
設計負債の大半は暗黙挙動の増殖から来る。  
uCLI は「便利そうだから暗黙でやる」を抑制する。
### 例外を個別実装で吸収しない
再読込、接続断、バージョン差、保存差などは例外処理でなく、仕様で扱う。  
仕様に上がらない例外は、将来必ず負債化する。
## 非目標
uCLI は次を直接の目標としない。
- Unity を何でも自然言語で操作できること
- もっとも短い記述で何でもできること
- 任意コード実行を万能な逃げ道にすること
- Editor 内 UX のみを最適化すること
uCLI は、**万能さより契約性**を優先する。
