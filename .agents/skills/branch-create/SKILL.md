---
name: branch-create
description: Issue から規約準拠ブランチを作成または再利用する。Issue Number or URL と任意の Branch Type/Base Branch/Branch Name を受け取り、`<type>/issue-<N>-<branch_name>` 形式でブランチを確定する。
---

# 目的
- Issueと入力パラメータから、規約準拠ブランチ名を一意に確定する。
- 既存ブランチを優先再利用し、不要な別名ブランチ作成を防ぐ。

# 使う/使わない
## 使う
- 対象Issueが確定しており、規約準拠ブランチを作成または再利用したい。
- `Branch Type` / `Base Branch` / `Branch Name` を含む入力から、最終ブランチ名を一意に確定したい。
## 使わない
- Issueの特定・作成が主目的（この場合は `$issue-writer` を使う）。
- タスクサイズ判定やIssue分割が主目的（この場合は別スキルで行う）。

# 入力
## 必須
- `Issue Number or URL`

## 任意
- `Branch Type`
- `Base Branch`
- `Branch Name`

# 出力
- `Branch Name`
- `Base Branch`
- `Created New Branch`（`Yes/No`）
- `Branch Directory Name`（`Branch Name` の `/` を `_` に置換）

# 参照
- 推論規約: [references/branch_policy.md](references/branch_policy.md)

# 手順
## 0. 前提チェック（必須）
1. `gh auth status` が成功すること（失敗したら中断）。
2. `Issue Number or URL` で `gh issue view` を実行し、対象Issueが open であることを確認する。

## 1. Base Branch を確定する
1. `Base Branch` が入力されていれば採用する。
2. 未指定なら `gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name'` の結果を採用する。
3. 確定した `Base Branch` が解決できない場合は中断する。

## 2. type を確定する
1. `Branch Type` が許可値 `feature|fix|refactor|docs|chore|ci` のいずれかなら採用する。
2. 未指定または不正値なら、`references/branch_policy.md` の規約で Issue ラベル優先・本文キーワード次点で推論する。
3. 推論不能なら `feature` を採用する。

## 3. branch_name を確定する
1. `Branch Name` が入力されていれば、`references/branch_policy.md` の slug ルールで正規化して採用する。
2. 未指定なら、Issue本文の `変更内容（What）` と `受け入れ条件（Acceptance Criteria）` を優先し、候補が不足する場合はタイトルから slug を生成する。
3. 正規化後に空文字なら `goal` を採用する。

## 4. ブランチ名を組み立てる
- 形式は必ず `<type>/issue-<N>-<branch_name>` とする。

## 5. ブランチを作成または再利用する
1. 同名ローカルブランチが存在する場合は checkout して再利用する（`Created New Branch = No`）。
2. ローカルに無く同名リモートブランチが存在する場合は tracking で checkout して再利用する（`Created New Branch = No`）。
3. どちらにも無い場合は `Base Branch` から新規作成して checkout する（`Created New Branch = Yes`）。
4. 既存ブランチがある場合は別名ブランチを作成しない。

## 6. 出力を固定する
1. `Branch Name` に確定名を設定する。
2. `Base Branch` に確定値を設定する。
3. `Created New Branch` は `Yes` または `No` で返す。
4. `Branch Directory Name` は `Branch Name` の `/` を `_` に変換して返す。

# Definition of Done
- ブランチ名が `<type>/issue-<N>-<branch_name>` 形式で確定している。
- `type` が許可値・推論順・フォールバック規約に従って決定されている。
- 既存ブランチがある場合は再利用され、別名ブランチが作成されていない。
- 出力4項目がすべて返却されている。
