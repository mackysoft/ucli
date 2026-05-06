# uCLI Architecture

> [!IMPORTANT]
> この文書は、uCLI の project 責務境界、依存方向、型の配置判断の正本である。
> 設計原則は [uCLI-design-principles.md](uCLI-design-principles.md)、パッケージ公開と Unity 側依存復元の運用は [package-operations.md](package-operations.md)、公式 SKILL の詳細は [uCLI-skills.md](uCLI-skills.md) を参照する。

## 目的

uCLI の project 分割は、CLI hosting、application use case、共有契約、技術実装、Unity 実装、SKILL 配布ロジックを混在させないために定義する。

この文書は、後続の実装変更で次を判断する基準とする。

- 新しい型をどの project に置くか
- 既存型をどの project へ移すか
- project reference を追加してよいか
- architecture tests でどの境界を固定するか

この文書は実装移動そのものを行わない。CLI コマンド仕様、IPC JSON 契約、Unity operation 実装、パッケージ公開手順はそれぞれの正本文書に従う。

## Project 責務境界

| Project | 状態 | 責務 | 置かないもの |
| --- | --- | --- | --- |
| `Ucli` | 現行 | CLI entrypoint、command registration、composition root、CLI option 正規化、CLI JSON 出力投影、終了コード制御 | use case 本体、Unity 接続詳細、永続化詳細、UnityEngine / UnityEditor 依存 |
| `Ucli.Application` | 現行 | use case、preflight orchestration、request resolver、resolved request、service result / report model、外部境界 interface | ConsoleAppFramework、標準入出力、公開 JSON writer 実装、Unity 実装、filesystem / process 具体実装 |
| `Ucli.Contracts` | 現行 | CLI と Unity plugin が共有する IPC DTO、protocol constants、semantic value type、公開 JSON 構造の contract helper | CLI hosting、application use case、infrastructure 実装、Unity operation 実装、SKILL manifest |
| `Ucli.Infrastructure` | 現行 | path、hash、process、filesystem、storage、IPC transport helper などの汎用技術実装 | feature 固有 policy、CLI 出力、Unity operation 意味論、公開 JSON contract の正本 |
| `Ucli.Unity` | 現行 | Unity Editor plugin、UnityEngine / UnityEditor 依存、operation 実装、Unity IPC server、Unity runtime lifecycle 管理 | CLI host、application orchestration、CLI JSON 出力投影、SKILL 配布ロジック |
| `Ucli.Skills` | 現行 | SKILL 生成、検証、manifest、digest、host adapter、install / export / doctor の中核ロジック | IPC / operation contract の再定義、Unity 実装、CLI hosting、`Ucli.Contracts` / `Ucli.Infrastructure` への依存 |

`Ucli.Application` は公開パッケージ境界ではなく、CLI package 内部の application core として扱う。

## 責務境界ごとのやること

各 project は、型の置き場だけでなく実行時に引き受ける仕事を持つ。後続実装では、この「やること」を越境しないように分割する。

| Project | やること |
| --- | --- |
| `Ucli` | CLI command を登録する。CLI option と stdin を読み、application input へ変換する。composition root として concrete dependency を選ぶ。application service を呼ぶ。service result を公開 CLI JSON と終了コードへ投影し、標準出力へ書く。 |
| `Ucli.Application` | command の背後にある use case を実行する。project context、request、readIndex、timeout、execution mode などの実行条件を解決する。preflight と validation を順序付ける。外部境界 interface を通して Unity 実行、catalog/readIndex 参照、永続化、process 実行を要求する。CLI JSON へ変換する前の service result / report model を返す。 |
| `Ucli.Contracts` | IPC と operation の公開 wire shape を定義する。semantic value type、protocol constants、共有 DTO、公開 JSON 構造の最小 contract helper を提供する。CLI、Application、Unity plugin が同じ意味のデータを同じ型で扱えるようにする。 |
| `Ucli.Infrastructure` | filesystem、path、hash、process、storage、IPC transport helper など、feature に依存しない技術処理を実装する。Application や Unity plugin から渡された入力に対して、技術的な実行結果を返す。 |
| `Ucli.Unity` | Unity Editor 内で IPC request を受ける。Unity lifecycle を観測する。operation args を UnityEngine / UnityEditor API で検証・計画・実行する。AssetDatabase、Scene、Prefab、SerializedObject などの Unity 実体を扱い、IPC response を返す。 |
| `Ucli.Skills` | 公式 SKILL の source definition を読み、canonical output を生成する。manifest、digest、host artifact を検証する。host ごとの materialization、install、export、doctor を実行する。operation contract は複製せず、agent workflow だけを配布可能にする。 |

## Application 境界の意図

`Ucli.Application` は CLI 以外の host を公開するための SDK ではない。`MackySoft.Ucli` global tool の内部で、CLI 入出力から独立して実行判断をテストできる application core として扱う。

`Ucli.Application` の責務は、入力を実行可能な文脈へ解決し、preflight を行い、外部境界 interface を通して必要な観測や実行を要求し、CLI JSON へ投影する前の service result を返すことである。

Application code は次の入力を受け取ってよい。

- CLI が正規化した command input
- request JSON text または parsed request model
- project path input、execution mode、timeout、readIndex mode などの実行条件
- `Ucli.Contracts` が定義する IPC / operation contract 型

Application code は次を直接扱わない。

- `ConsoleAppFramework`
- `Console.Out` / `Console.Error` / process exit code
- `CommandResult`、`CommandError`、匿名型による公開 JSON shape
- UnityEngine / UnityEditor
- `File`、`Directory`、`Process`、socket などの具体 API を呼ぶ永続化・実行実装

外部境界は Application が必要とする能力として interface を定義する。たとえば Unity へ request を送る能力は `IUnityRequestExecutor` のような interface として Application 側に置き、IPC transport、batchmode 起動、socket 接続、response read の具体実装は adapter 側に置く。

## 現行コードからの移動判断

後続の project 分離では、現行 `src/Ucli` の名前や directory だけで移動先を決めない。型が持つ責務で判断する。

| 現行の例 | 分離後の配置 | 判断理由 |
| --- | --- | --- |
| `Hosting/Cli/**Command.cs` | `Ucli` | ConsoleAppFramework command、CLI option、stdout 書き込み、終了コードを扱う |
| `Hosting/Cli/**CommandResultFactory.cs` | `Ucli` | service result を公開 CLI JSON envelope へ投影する |
| `Hosting/Cli/Common/Contracts/CommandResult.cs` | `Ucli` | CLI stdout contract であり、application service result ではない |
| `Features/**/UseCases/**Service.cs` | `Ucli.Application` | command の背後にある use case 本体、preflight、orchestration を担う |
| `Features/**/UseCases/**ServiceResult.cs` | `Ucli.Application` | CLI JSON へ投影する前の application result を表す |
| `Features/**/Common/Contracts/*ExecutionOutput.cs` | 原則 `Ucli.Application` | use case が返す report model。公開 JSON writer そのものではない |
| `Features/Requests/Shared/Preparation/**` | `Ucli.Application` | request preparation、resolved request、readIndex preflight の application policy を担う |
| `Shared/Context/**` | policy は `Ucli.Application`、具体実装は adapter 側 | project context resolution は use case 入力解決だが、filesystem / git detail は境界外へ分ける |
| `Shared/Execution/UnityRequest/IUnityRequestExecutor.cs` | `Ucli.Application` | Unity 実行要求という外部境界 interface を表す |
| `UnityIntegration/Ipc/Execution/UnityIpcRequestExecutor.cs` | adapter 側または `Ucli` | IPC transport を使う具体実装であり Application には置かない |
| `UnityIntegration/Project/Resolution/**` | interface / policy は `Ucli.Application`、Unity project 判定実装は adapter 側 | project 解決 policy と filesystem / Unity project detail を分ける |
| `Shared/Execution/Process/ProcessRunner.cs` | `Ucli.Infrastructure` または adapter 側 | process 実行の具体実装であり Application には置かない |
| `Features/**/Persistence/File*Store.cs` | `Ucli.Infrastructure` または adapter 側 | file 永続化の具体実装であり Application には置かない |
| `Features/Requests/**/Projection/*ExecutionOutputFactory.cs` | 内容で分ける | application report 生成なら `Ucli.Application`、公開 JSON shape 生成なら `Ucli` |

`CommandPreflight` という名前の型は、名前だけで `Ucli` に残さない。CLI option parse error 時の出力維持だけを担うなら `Ucli`、project context、request preparation、readIndex preflight のような application policy を担うなら `Ucli.Application` へ移す。両方を持つ場合は、CLI wrapper と application preflight service に分割する。

## Host と Adapter の分離

`Ucli` は CLI host と最終 composition root を持つ。`Ucli.Application` は service registration を提供してよいが、具体実装の選択は `Ucli` の composition root で行う。

外部境界 interface と具体実装は次の関係にする。

| 能力 | Interface の配置 | 具体実装の配置 |
| --- | --- | --- |
| Unity IPC request 実行 | `Ucli.Application` | `Ucli` の adapter 領域、または将来の専用 adapter project |
| project context 解決 | policy interface は `Ucli.Application` | filesystem / git / Unity project 判定は `Ucli.Infrastructure` または adapter 側 |
| readIndex / catalog 参照 | use case が必要とする reader interface は `Ucli.Application` | file store、snapshot loader、Unity fallback client は adapter 側 |
| process 実行 | 必要なら能力 interface は `Ucli.Application` | `Ucli.Infrastructure` の process 実装 |
| filesystem 永続化 | 必要なら repository / store interface は `Ucli.Application` | `Ucli.Infrastructure` または adapter 側の file store |

Adapter 側は Application の interface を実装してよい。Application 側から adapter namespace を参照してはならない。

## Read Model 境界

readIndex は、公開 JSON contract、Application policy、adapter 実装を分離する。`index` JSON 形式と CLI 出力、Unity plugin 側の index 生成処理は `Ucli.Contracts` の既存 contract を正本とし、Application の都合で変更しない。

`Ucli.Application` は read model の access policy を持つ。具体的には、readIndex mode、freshness policy、fallback 判断、mutation read postcondition 判定、persisted artifact の read 成否に基づく source fallback の判断を行う。ops catalog、asset-search、guid-path、scene-tree-lite は同じ流れで扱い、persisted snapshot load、freshness 判定、read postcondition 判定、source fallback、best-effort persistence の結果反映を揃える。Application は `IReadIndexArtifactReader`、`IReadIndexFreshnessEvaluator`、source refresh port、scene source probe などの port だけを呼び、filesystem、storage path、hash 計算、Unity IPC の具体実装を持たない。

`Ucli` adapter は read model の技術実装を持つ。具体的には、`.ucli/local/.../index/` 配下の storage path 解決、artifact JSON の file read / atomic write、input fingerprint と scene source hash の計算、Unity IPC による source read、refresh 後の best-effort persistence を実装する。source refresh adapter は source result を失敗させず、永続化失敗を fallback reason に含める。

`Ucli.Contracts` は既存の index JSON contract だけを持つ。freshness 判定、fallback policy、local storage path、hash 計算、Unity IPC source read は `Ucli.Contracts` に置かない。

## 命名方針

CLI package の source project 名は `Ucli` のままとする。`Ucli.Cli` へ rename しない。

理由は、`MackySoft.Ucli` package、`ucli` command、README、既存 docs の正面玄関が `Ucli` であり、rename は責務分離よりも公開名・assembly 名・package metadata の移行コストを増やすためである。

`Ucli` の責務は、名前ではなく中身で CLI host に限定する。CLI 境界であることは namespace の `Hosting.Cli`、command class、composition root、package metadata で表す。

## 依存方向

許可する production project reference は次を基準とする。

```text
Ucli -> Ucli.Application
Ucli -> Ucli.Contracts
Ucli -> Ucli.Infrastructure
Ucli -> Ucli.Skills

Ucli.Application -> Ucli.Contracts

Ucli.Infrastructure -> Ucli.Contracts

Ucli.Unity -> Ucli.Contracts
Ucli.Unity -> Ucli.Infrastructure
```

`Ucli.Contracts` は他の uCLI project へ依存しない。`Ucli.Application` は `Ucli`、`Ucli.Infrastructure`、`Ucli.Unity` へ依存しない。`Ucli.Unity` は `Ucli` と `Ucli.Application` へ依存しない。

`Ucli.Skills` は operation catalog を同梱せず、実行時に `ucli ops describe` を読む workflow layer を生成する。そのため原則として他の uCLI production project へ依存しない。`ucli skills` CLI entrypoint は `Ucli` に置き、必要な場合だけ `Ucli` から `Ucli.Skills` を参照する。

テスト project は対応する production project と `Tests.Helper` を参照してよい。architecture tests だけは、project file と source tree を検査するために必要な範囲で複数 project の path を読む。

## 型と処理の配置ルール

Command class、CLI option 型、標準入出力、終了コード、CLI JSON 出力 writer は `Ucli` に置く。CLI 表示のための整形は application result を受け取る投影として扱い、use case 本体へ混ぜない。

Use case 本体、preflight orchestration、request input の解決、resolved request、service result、report model、外部境界 interface は `Ucli.Application` に置く。外部境界 interface は application core が必要とする能力を表し、filesystem、process、Unity IPC などの具体実装は持たない。

IPC wire DTO、operation Args / Result contract、protocol constants、複数 project で共有する semantic value type は `Ucli.Contracts` に置く。JSON Schema generator、contract reader、validation engine は、公開 JSON 構造の正本として必要な最小範囲だけを置く。application use case、CLI output policy、Unity 実行 policy を `Ucli.Contracts` へ入れない。

Filesystem、path normalization、hash、process liveness、storage path、IPC frame / transport path などの汎用技術実装は `Ucli.Infrastructure` に置く。feature 固有の判断、user-facing error message、CLI 出力 shape、Unity operation の意味論は置かない。

UnityEngine / UnityEditor 依存コード、operation handler、Unity object 解決、AssetDatabase / Scene / Prefab / SerializedObject 操作、Unity IPC server は `Ucli.Unity` に閉じる。Unity plugin は CLI host と application orchestration を持たない。

SKILL template、manifest、digest、host adapter、materialization、install / export / doctor のロジックは `Ucli.Skills` に置く。SKILL は operation contract の正本ではなく、operation args、result、assurance は実行時の `ucli ops describe` を正本にする。

## Ucli.Contracts 内の配置基準

`Ucli.Contracts` は公開 contract の正本であり、実行 host や use case の都合を持たない。ここに置いてよい処理は、共有 contract 型だけで完結し、CLI、Application、Infrastructure、Unity 実装へ依存しないものに限る。

`Ucli.Contracts` に残すものは次の通り。

- IPC wire DTO、operation Args / Result contract、protocol constants
- semantic value type、contract attribute、contract metadata DTO
- public schema generator、contract attribute に基づく structural validation
- JSON を共有 contract model へ読む internal contract reader
- edit step contract を primitive operation contract へ対応づける共有 structural lowering helper
- 公開 JSON 構造を維持するための最小 contract helper

`Ucli.Contracts` に置かないものは次の通り。

- project context、operation catalog 取得、readIndex freshness、config allowlist などの application policy
- user-facing error、CLI JSON envelope、標準入出力、終了コード
- filesystem、process、socket、hash、storage path などの infrastructure 実装
- UnityEngine / UnityEditor、Unity object 解決、operation handler、Unity lifecycle

contract reader は `MackySoft.Ucli.Contracts.Ipc.ContractReading` に置く。reader は JSON の構造を contract model へ読む責務だけを持ち、application validation の判断、CLI 向け文言、Unity 実体解決を持たない。

edit step lowering helper は `MackySoft.Ucli.Contracts.Ipc.EditSteps` に置く。helper は validated edit step contract から primitive operation name、structural target category、implicit save operation を導出する責務だけを持ち、operation catalog、readIndex、Unity object 解決、実行 lifecycle を持たない。

既存の public 型が `MackySoft.Ucli.Contracts.Ipc.Validation` namespace で公開されている場合は、公開 API 維持のため旧 namespace に残す。新規 internal reader 型を旧 namespace へ追加してはならない。

operation schema generator と operation contract validator は公開 helper として `MackySoft.Ucli.Contracts.Ipc` に残す。ただし責務は operation Args / Result contract の JSON Schema subset 生成と contract attribute / semantic value に基づく structural validation に限定する。

## Package Visibility

Project 分割と公開パッケージ境界は一致させない。公開パッケージの運用正本は [package-operations.md](package-operations.md) とする。

公開 NuGet package は次に限定する。

| Package | Source | 公開上の役割 |
| --- | --- | --- |
| `MackySoft.Ucli` | `src/Ucli` | .NET global tool として `ucli` command を提供する |
| `MackySoft.Ucli.Contracts` | `src/Ucli.Contracts` | CLI、Unity plugin、外部 tooling が共有する IPC / operation contract を提供する |
| `MackySoft.Ucli.Infrastructure` | `src/Ucli.Infrastructure` | CLI と Unity plugin が共有する技術実装を提供する |
| `MackySoft.Ucli.Unity` | `src/Ucli.Unity` | NuGetForUnity 用 Unity Editor plugin を提供する |

`Ucli.Application` は公開パッケージにしない。`MackySoft.Ucli` の内部 assembly として use case を分離し、利用者が直接参照する API にはしない。

`Ucli.Skills` は公開パッケージにしない。SKILL 配布物は `MackySoft.Ucli` package、release artifact、または `ucli skills install/export` の出力として扱う。

`Ucli.Unity` は `Ucli.Application` に依存しない。Unity 側の復元対象は `MackySoft.Ucli.Contracts` と `MackySoft.Ucli.Infrastructure` を基準とし、CLI orchestration を Unity plugin へ持ち込まない。

## Architecture Tests 方針

後続の architecture tests は、この文書を実装境界の期待値として扱う。最小限、次を固定する。

- production project reference が許可リストに一致すること
- `Ucli` が CLI host と composition root として `Ucli.Application`、`Ucli.Contracts`、`Ucli.Infrastructure`、`Ucli.Skills` 以外の production project へ依存しないこと
- `Ucli.Contracts` が `Ucli`、`Ucli.Application`、`Ucli.Infrastructure`、`Ucli.Unity`、`Ucli.Skills` に依存しないこと
- `Ucli.Application` が ConsoleAppFramework、標準入出力、CLI JSON writer 実装、Unity 実装、filesystem / process 具体実装へ依存しないこと
- `Ucli.Infrastructure` が CLI output、feature 固有 policy、Unity operation 実装へ依存しないこと
- `Ucli.Unity` が `Ucli` と `Ucli.Application` へ依存しないこと
- `Ucli.Skills` が `Ucli.Contracts` と `Ucli.Infrastructure` へ project reference を持たないこと
- source namespace の禁止参照が必要な境界では、`using` だけでなく fully qualified name も検出対象にすること

Architecture tests は構造の侵食を検出するための small test とする。個別機能の振る舞い、公開 JSON の詳細、パッケージ metadata、Unity 実行結果は、それぞれの feature test、contract test、package test、Unity test で検証する。
