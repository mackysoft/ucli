---
name: pr-create
description: PR作成依頼を受けたときに、検証・`$push`・PR作成までを一気通貫で実行する。Issueがある場合はIssueを優先して本文とbaseを決定し、Issueが特定できない場合は確認なしでIssueなし運用として継続する。既存Open PRがある場合は更新か新規作成を確認する。
---

# 目的
- PR作成手順を固定し、検証漏れ・push漏れ・本文品質のばらつきを防ぐ。
- Issue起点タスクとIssueなし即興タスクの両方で、安全にPR作成できる状態を作る。

# 使うタイミング
- 「PRを作成して」「PRまで進めて」のように、PR作成完了が要求されたとき

# 契約
- `docs/agents/<BRANCH_DIR>/spec.md` が存在する場合のみ、Issue解決の補助入力として参照する。
- `spec.md` が無い場合は spec 参照手順を Skip し、他の解決経路で継続する。
- `spec.md` の有無は StartWork 実行済み判定に使わない。
- Issueが解決できた場合は、PR本文末尾に必ず `Closes #<N>` を入れて Development へ紐付ける。

# 参照
- 本文生成ルール: [references/body-generation.md](references/body-generation.md)
- テンプレ:
  - PR本文: [assets/pr_body_template.md](assets/pr_body_template.md)

# 入力（任意。無くても推定して進める）
- Issue番号/URL
- Base branch
- PRタイトル
- 既存Open PRがあるときの方針（更新/新規）

# 出力
- 検証結果（PASS/FAIL）
- `$push` 実行結果（コミット実行有無を含む）
- 作成または更新したPR URL

# 手順
## 1. 前提を確認する
1. `git status --porcelain` で未コミット差分の有無を確認する（停止条件には使わない）。
2. `gh auth status` を確認する。失敗したら停止する。

## 2. Issueとbase branchを確定する
1. 現在ブランチ名から `<BRANCH_DIR>` を算出する（`/` を `_` に置換）。
2. Issue番号は次の優先順で解決する。
   - 明示入力
   - `docs/agents/<BRANCH_DIR>/spec.md` が存在する場合は `Issue Number` / `Issue URL`
   - ブランチ名の `issue-<N>-...`
3. `spec.md` が存在しない場合は、spec 参照手順を Skip して次の解決経路へ進む。
4. Issueが解決できた場合は、Issue本文の `Base branch` 指定を優先して base を確定する。
5. Issueが解決できない場合は、確認せず Issueなし運用で継続する。
6. base branch が確定できない場合は、リポジトリ既定ブランチを採用する。
7. PR対象差分を確認する。
   - `git rev-list --count <base_branch>..HEAD` でコミット差分を確認する。
   - コミット差分が `0` かつ `git status --porcelain` も空なら、PR対象差分が無いので停止する。

## 3. 既存Open PR有無を確認する
1. `gh pr list --state open --head <current_branch>` を実行する。
2. Open PRがある場合は、ユーザーへ「既存PR更新 / 新規PR作成」を確認して分岐する。

## 4. 検証を実行する
1. `$verification-gate` を `PR前` 用途で実行する。
2. FAILなら停止し、PRを作成しない。

## 5. pushを実行する
1. `$push` を呼び、必要に応じたコミット作成と push を実行する。

## 6. PR本文を生成する
1. `references/body-generation.md` に従い、差分・コミット履歴・検証結果から本文を生成する。
2. Issueありの場合は本文末尾に `Closes #<N>` を入れる。
3. Issueなしで継続した場合は、`Closes` を入れずに「Issueなし運用」を明記する。

## 7. PRを作成または更新する
1. PR種別は次で決める。
   - Issueがあり、`verification-gate` PASS かつ IssueのAcceptance Criteriaが全完了: 通常PR
   - Issueがあり、Acceptance Criteriaが未完了または未定義: Draft PR
   - Issueなしで継続し、`verification-gate` PASS: 通常PR
2. 新規作成分岐・既存更新分岐のどちらでも、Step 6で生成した本文をそのまま使用する。
3. 新規作成分岐なら `gh pr create` を実行する。
   - Draft PR の場合は `--draft` を付ける。
4. 既存更新分岐なら `gh pr edit` で本文/タイトル/base を更新する。
5. 既存更新分岐では `gh pr view --json isDraft` で現在状態を確認し、必要なときだけ Draft 状態を同期する。
   - 判定が Draft PR で現在が通常PRなら `gh pr ready --undo` を実行する。
   - 判定が通常PRで現在が Draft PRなら `gh pr ready` を実行する。

# Definition of Done
- `verification-gate` FAIL時に停止できる
- `push` / PR作成または更新が完了している
- Issueあり/なしの分岐ルールに従って本文が生成されている
- IssueありでPR作成/更新した場合、本文末尾に `Closes #<N>` が含まれている
- `spec.md` 不在時に spec 参照手順を Skip して継続できる
