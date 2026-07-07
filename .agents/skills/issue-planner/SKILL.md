---
name: "issue-planner"
description: "タスクや仕様からGitHub Issue構造を決める。Issue分割、親子Issue、Pull Request単位の粒度、依存関係を整理し、単一Issue、または親Issueと子Issueの1階層にまとめ、文書前提を含むIssue本文作成に必要な入力を作る作業で使う。"
---

# issue-planner

## 目的
タスクや仕様を、実装単位として扱えるGitHub Issue構造へ整理する。
確定するIssue階層は、単一Issue、または親Issueと子Issueの1階層とする。

## 使うタイミング
- タスクや仕様からGitHub Issueを起票する前に、Issue分割、親子Issue、依存関係を整理する。
- Pull Request単位で完了できる粒度までタスクを分ける。
- 単一Issueで足りるか、親Issueと子Issueに分けるかを決める。

## 整理基準
Issue種別は、Issue構造上の `single`、`parent`、`child` のいずれかを指す。
GitHubラベルや開発種別は別項目として扱う。

| Issue種別 | 意味 | 使う条件 |
| --- | --- | --- |
| `single` | 単一Issue | 1つのPull Requestで完了できる。 |
| `parent` | 親Issue | 複数の子Issueが完了することで全体の完了状態を満たす。 |
| `child` | 子Issue | 親Issueに属し、1つのPull Requestで完了できる。 |

Issueの粒度は、1つの意図、1つの完了状態、1つのレビュー観点で決める。
別々にマージ、取り消し、完了判定できる変更は分ける。
互いに同じ成立条件を持つ変更は同じIssueにまとめる。

順序関係は階層ではなく依存関係として表す。

## フロー
### Phase 1: 入力を固定する
目的、参照元、対象リポジトリを確認し、Issue構造の判断に使う情報を固定する。
目的が未確定の場合は、確定に必要な判断材料を返す。

### Phase 2: 意図を分ける
入力タスクの意図を1文で表す。
意図が複数ある場合は、親Issue候補または単一Issue候補に分ける。
1つのPull Requestで完了できる場合は、単一Issue候補にする。

### Phase 3: 構造を検討する
親Issue候補ごとに子Issue候補を出す。
1つのPull Requestで完了できない子Issue候補はさらに分ける。
すべての末端タスクが1つのPull Requestで完了できるまで繰り返す。

検討中に階層が増えた場合も、最終的なIssue構造は単一Issue、または親Issueと子Issueの1階層に整理する。

### Phase 4: Issue構造を確定する
末端タスクが1つなら単一Issueにする。
複数の末端タスクが1つの意図を共有するなら、親Issueと子Issueにする。
末端タスクの意図が分かれるなら、親Issue候補を分ける。

Issue階層が1階層を超える場合は、意図と完了状態を見直して親Issue候補または子Issue候補を組み替える。

### Phase 5: 本文入力を作る
Issue本文作成に使う入力をIssueごとに揃える。
入力は次を含める。

- Issue構造
- 文書前提（対象、読者、媒体、言語、出力形式）
- 本文形式
- Issue種別（`single`、`parent`、`child`）
- タイトル案
- 目的
- 作業範囲または全体の完了条件
- 依存関係
- 参照元
- 親子関係

文書前提が未固定の場合は、`$writing` の文書前提に従って固定する。
媒体は GitHub Issue として扱う。

### Phase 6: 構造を確認する
Issue階層が1階層以内であることを確認する。
単一Issueと子Issueが1つのPull Requestで完了できることを確認する。
順序関係が依存関係として表されていることを確認する。

## 報告
Issue構造を確定できた場合は、起票対象のIssue一覧を、親子関係と依存関係が分かる順で返す。
各Issueには、タイトル案、目的、作業範囲または全体の完了条件を含める。
Issue構造が未確定の場合は、不足している判断材料を列挙する。

## Definition of Done
- Issue構造が `single`、または `parent` と `child` の1階層に収まっている。
- `single` と `child` は1つのPull Requestで完了できる粒度になっている。
- 依存関係が階層と混ざらずに表されている。
- Issue本文作成に使う入力がIssueごとに揃っている。
