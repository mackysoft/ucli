---
name: branch-create
description: 作業ブランチを規約準拠名で作成または再利用する。Issue Number or URL がある場合は `<type>/issue-<N>-<branch_name>`、Issue が無い場合は `<type>/<branch_name>` 形式で、現在作業を載せるブランチを確定する。
---

# 目的
- 入力パラメータ、Issue、現在作業のいずれかから、規約準拠ブランチ名を一意に確定する。
- 既存ブランチを優先再利用する。
- 現在ブランチが無い作業や未コミット差分を、以降の `$commit` / `$push` / `$pr-create` が扱える現在ブランチへ載せる。

# 使う/使わない
## 使う
- 対象Issueが確定しており、規約準拠ブランチを作成または再利用したい。
- Issueなしの現在作業に対して、規約準拠ブランチを作成したい。
- 現在ブランチが無い作業を、作業ブランチへ載せたい。
- `Branch Type` / `Base Branch` / `Branch Name` を含む入力から、最終ブランチ名を一意に確定したい。
## 使わない
- Issueの特定・作成が主目的。

# 入力
## 任意
- `Issue Number or URL`
- `Branch Type`
- `Base Branch`
- `Branch Name`
- `Source Ref`（未指定時は現在作業を保持できる起点を選ぶ）

# 出力
- `Branch Name`
- `Base Branch`
- `Created New Branch`（`Yes/No`）

# 参照
- 推論規約: [references/branch_policy.md](references/branch_policy.md)

# 手順
## 0. 前提チェック（必須）
1. `gh auth status` が成功すること（失敗したら中断）。
2. `Issue Number or URL` が入力されている場合のみ、`gh issue view` を実行し、対象Issueが open であることを確認する。
3. 現在の `git status --porcelain` と `git symbolic-ref --quiet HEAD` を確認する。

## 1. Base Branch を確定する
1. `Base Branch` が入力されていれば採用する。
2. 未指定なら `gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name'` の結果を採用する。
3. 確定した `Base Branch` が解決できない場合は中断する。

## 2. type を確定する
1. `Branch Type` が許可値 `feature|fix|refactor|docs|chore|ci` のいずれかなら採用する。
2. Issue がある場合は、`references/branch_policy.md` の規約で Issue ラベル優先・本文キーワード次点で推論する。
3. Issue が無い場合は、ユーザー指示、未コミット差分、変更ファイルから推論する。
4. 推論不能なら `chore` を採用する。

## 3. branch_name を確定する
1. `Branch Name` が入力されていれば、`references/branch_policy.md` の slug ルールで正規化して採用する。
2. Issue がある場合は、Issue本文の `変更内容（What）` と `受け入れ条件（Acceptance Criteria）` を優先し、候補が不足する場合はタイトルから slug を生成する。
3. Issue が無い場合は、ユーザー指示、変更ファイル、差分の目的から slug を生成する。
4. 正規化後に空文字なら `goal` を採用する。

## 4. ブランチ名を組み立てる
1. Issue がある場合は `<type>/issue-<N>-<branch_name>` とする。
2. Issue が無い場合は `<type>/<branch_name>` とする。

## 5. ブランチを作成または再利用する
1. 現在ブランチが確定ブランチ名と一致する場合は、そのまま再利用する（`Created New Branch = No`）。
2. 同名ローカルブランチが存在する場合は、別 worktree で使用中でないことを `git worktree list --porcelain` で確認してから checkout して再利用する（`Created New Branch = No`）。
3. ローカルに無く同名リモートブランチが存在する場合は tracking で checkout して再利用する（`Created New Branch = No`）。
4. どちらにも無い場合は新規作成して checkout する（`Created New Branch = Yes`）。
   - `Source Ref` が入力されている場合は、その ref から作成する。
   - 現在ブランチが無い作業または未コミット差分がある現在作業を保持する場合は、現在の `HEAD` から作成する。
   - それ以外は `Base Branch` から作成する。
5. 既存ブランチが別 worktree で使用中の場合は、その worktree パスを示して停止する。確認なしに別名ブランチを作成しない。

## 6. 出力を固定する
1. `Branch Name` に確定名を設定する。
2. `Base Branch` に確定値を設定する。
3. `Created New Branch` は `Yes` または `No` で返す。

# Definition of Done
- Issueありなら `<type>/issue-<N>-<branch_name>`、Issueなしなら `<type>/<branch_name>` 形式で確定している。
- `type` が許可値・推論順・フォールバック規約に従って決定されている。
- 既存ブランチがある場合は再利用され、別名ブランチが作成されていない。
- 現在ブランチが無い作業または未コミット差分がある現在作業では、現在の `HEAD` からブランチが作成されている。
- 出力3項目がすべて返却されている。
