---
name: pr-merge
description: 現在ブランチのPRを安全にマージし、worktree競合を回避しながらブランチクリーンアップまで実行する。Open PRが無い場合のみ `$pr-create` でPR作成後に進み、Open PRがあり未コミット/未push差分がある場合は `$push` で反映してからマージする。Draft解除、必須チェック確認、自動マージ設定、保護ルール起因失敗時の `--admin` 再試行確認を行う。
---

# 目的
- PRマージ手順を固定し、PR未作成・差分未反映・チェック未確認のままマージする事故を防ぐ。
- 既存PR運用では `$pr-create` を使わず、役割分離した最小手順で安全に完了する。

# 使うタイミング
- 「現在の作業PRをマージして」のように、現在ブランチのPRマージ完了が要求されたとき
- PRが未作成の可能性、または既存PRへの差分反映有無が不明な状態でマージしたいとき

# 入力（任意。無くても推定して進める）
- 対象PR（番号/URL/ブランチ。未指定時は現在ブランチ）
- 保護ルール起因失敗時の `--admin` 再試行可否

# 出力
- 対象PR URL
- `$pr-create` 実行有無
- `$push` 実行有無
- 即時マージ or 自動マージ設定
- リモートブランチ削除結果
- ローカルブランチ削除結果（削除 or Skip理由）
- 停止した場合の理由

# 手順

## 1. 前提を確認する
1. `git symbolic-ref --quiet HEAD` を実行し、detached HEAD なら停止する。
2. `gh auth status` を確認し、失敗したら停止する。

## 2. 対象PRを確認する
1. 現在ブランチ名を取得する。
2. `gh pr list --state open --head <current_branch>` を実行して Open PR を確認する。
3. Open PR が複数ある場合は対象PRをユーザー確認して1件に確定する。

## 3. 差分状態を確認する
1. `git status --porcelain` で未コミット差分有無を確認する。
2. upstream がある場合は `git rev-list --count @{upstream}..HEAD` で未push件数を確認する。
3. upstream が無い場合は、未push差分ありとして扱う。

## 4. 分岐して必要なスキルを実行する
1. Open PR が無い場合のみ `$pr-create` を実行する。
2. Open PR がある場合、未コミット差分または未push差分があれば `$push` を実行する。
3. Open PR があり、差分が無ければ追加スキルを呼ばずに続行する。
4. `$pr-create` または `$push` 実行後は `gh pr list --state open --head <current_branch>` で対象PRを再取得する。
5. 対象PRを再取得できない場合は停止する。

## 5. PR状態を確認する
1. `gh pr view --json state,isDraft,headRefOid,headRefName,url,number` を実行する。
2. `state != OPEN` の場合は停止する。
3. 既存PR分岐では PR本文を更新しない。

## 6. Draft を解除する
1. `isDraft=true` の場合は `gh pr ready` を実行して Ready にする。

## 7. 必須チェックを確認する
1. `gh pr checks --required` を実行する。
2. 失敗チェックがある場合は停止する。
3. 未完了チェックは許容し、`--auto` でマージ設定する。

## 8. マージを実行する
1. 次のコマンドでマージを実行する。  
   `gh pr merge <number> --merge --auto --match-head-commit <headRefOid>`
2. `already merged` が返る場合は、マージ済みとして後続のクリーンアップへ進む。

## 9. 失敗時の管理者再試行
1. ブランチ保護ルール起因で失敗した場合のみ、ユーザーへ `--admin` 再試行可否を確認する。
2. 承認された場合に限り `--admin` を付けて1回だけ再試行する。
3. 未承認または再試行失敗時は停止する。

## 10. ブランチをクリーンアップする（worktree対応）
1. `<headRefName>` を対象にリモートブランチ削除を実行する。  
   `git push origin --delete <headRefName>`
2. リモート側に対象ブランチが存在しない場合は「既に削除済み」として扱う。
3. `git worktree list --porcelain` を確認し、`branch refs/heads/<headRefName>` が存在するか判定する。
4. 存在しない場合のみ、ローカルブランチ削除を実行する。  
   `git branch -d <headRefName>`
5. worktree使用中で削除できない場合は、削除を実行せず Skip理由を出力する。

# 安全規約
1. マージ方式は常に `--merge` を使用する。
2. 既存PRがある場合に `$pr-create` を呼ばない。
3. 既存PRで差分反映が必要な場合は `$push` のみを使う。
4. 既存PR分岐で `gh pr edit` を使った本文更新は行わない。
5. worktree 競合回避のため、マージ本体では `--delete-branch` を使わない。
6. ブランチ削除は `git push origin --delete` と `git worktree list` 判定を使って段階的に実行する。

# Definition of Done
- PR未作成時のみ `$pr-create` が実行されている
- 既存PRで差分あり時のみ `$push` が実行されている
- 必須チェック失敗時に停止できる
- 手順で定義したマージコマンドで実行される
- 保護ルール起因失敗時のみ `--admin` を都度確認し、承認時に1回だけ再試行する
- リモートブランチが削除されている（または既に削除済みとして扱われている）
- ローカルブランチ削除は実行されるか、worktree使用中の理由付きでSkipされている
