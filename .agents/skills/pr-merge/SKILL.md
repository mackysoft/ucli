---
name: pr-merge
description: 現在作業または指定PRを安全にマージする。現在作業をブランチへ載せ、PRを取得または作成し、push、Draft解除、CI待機、ワークフロー未起動原因の確認、必須チェック確認、マージ、リモートブランチクリーンアップまで進める。
---

# 目的
- ブランチ、PR、push、CI、マージ状態を順に整える。
- 「マージ」指示だけで、現在作業からマージ完了まで必要な手順を実行する。
- 対象ブランチのPRがある場合は、そのPRを使ってマージへ進める。

# 使うタイミング
- 現在作業をPRとしてマージしたいとき
- 指定PRをチェック通過後にマージしたいとき

# 入力（任意。無くても推定して進める）
- 対象PR（番号/URL/ブランチ。省略時は現在作業）
- 作業ブランチ名（省略時は差分内容から生成）

# 出力
- 作業ブランチ作成有無
- 対象PR URL
- `$pr-create` 実行有無
- `$push` 実行有無
- CI待機結果
- CI未検出時の調査結果
- 即時マージ結果
- リモートブランチ削除結果
- 停止した場合の理由

# 手順

## 1. 前提を確認する
1. `gh auth status` を確認し、失敗したら停止する。
2. `git symbolic-ref --quiet HEAD` を実行し、現在ブランチの有無を確認する。
3. 現在ブランチが無い場合は、Step 2 で作業ブランチを作成する。

## 2. 対象作業をブランチへ載せる
1. 対象PRが番号/URLで明示されている場合は、ブランチ作成を行わず `gh pr view <target>` で対象PRを確定して Step 6 へ進む。
2. 現在ブランチがある場合は、そのブランチを対象作業ブランチにする。
3. 現在ブランチが無い場合は、マージ対象の有無を確認する。
   - `git status --porcelain` で未コミット差分を確認する。
   - リポジトリ既定ブランチを `gh repo view --json defaultBranchRef` で確認する。
   - `git rev-list --count <default_branch>..HEAD` で HEAD 上の未取り込みコミット数を確認する。
   - 未コミット差分が無く、未取り込みコミット数も `0` なら、マージ対象が無いので停止する。
4. マージ対象がある場合は `$branch-create` を呼び、現在作業を載せるブランチを作成または再利用する。
5. `$branch-create` の完了後に現在ブランチ名を再取得し、以降はそのブランチを対象作業ブランチにする。

## 3. 対象PRを確認する
1. 現在ブランチ名を取得する。
2. `gh pr list --state open --head <current_branch>` を実行して対象ブランチのPRを確認する。
3. 複数のPRが返った場合は対象PRをユーザー確認して1件に確定する。

## 4. 差分状態を確認する
1. `git status --porcelain` で未コミット差分有無を確認する。
2. upstream がある場合は `git rev-list --count @{upstream}..HEAD` で未push件数を確認する。
3. upstream が無い場合は、未push差分ありとして扱う。

## 5. 分岐して必要なスキルを実行する
1. 対象PRを取得できない場合のみ `$pr-create` を実行する。
2. 対象PRがあり、未コミット差分または未push差分があれば `$push` を実行する。
3. 対象PRがあり、差分が無ければ追加スキルを呼ばずに続行する。
4. `$pr-create` または `$push` 実行後は `gh pr list --state open --head <current_branch>` で対象PRを再取得する。
5. 対象PRを再取得できない場合は停止する。

## 6. PR状態を確認する
1. `gh pr view --json state,isDraft,headRefOid,headRefName,url,number` を実行する。
2. `state != OPEN` の場合は停止する。
3. 既存PR分岐では PR本文を更新しない。

## 7. Draft を解除する
1. `isDraft=true` の場合は `gh pr ready` を実行して Ready にする。

## 8. CI と必須チェックを待機する
1. `gh pr view --json headRefName,headRefOid,baseRefName,number` を実行し、以降のCI確認で使う head branch / head SHA / base branch を確定する。
2. `gh pr checks <number> --required` を実行し、必須チェックの有無と状態を確認する。
3. 必須チェックが存在する場合は、次のコマンドで完了まで待機する。
   `gh pr checks <number> --required --watch --fail-fast --interval 10`
4. 必須チェックが未設定または未検出の場合は、CI検出待機とワークフロー状態確認を行う。
   - 最大60秒、10秒間隔で `gh pr checks <number>` を再実行し、CIチェックが表示されるか確認する。
   - あわせて `gh run list --branch <headRefName> --commit <headRefOid> --json databaseId,name,workflowName,status,conclusion,headSha,event,createdAt,updatedAt,url --limit 20` を実行し、head SHA のワークフロー実行有無を確認する。
   - `queued` / `pending` / `requested` / `waiting` / `in_progress` の実行がある場合は、CI未起動やCIなしと判定しない。対象 run を `gh run watch <databaseId> --interval 10 --exit-status` で待機し、完了後に `gh pr checks <number>` を再確認する。
   - head SHA の実行が無くても、`gh run list --status queued --limit 20` と `gh run list --status in_progress --limit 20` でリポジトリ全体の滞留を確認する。過去のワークフロー実行によりキューが詰まっている可能性がある場合は、その状態を停止理由または待機理由として記録し、管理者マージへ進まない。
   - CIチェックが表示された場合は、次のコマンドで全チェックの完了まで待機する。
     `gh pr checks <number> --watch --fail-fast --interval 10`
5. 検出待機後もチェックと head SHA のワークフロー実行が無い場合は、CI未起動の理由を調査する。
   - `gh workflow list --all` で有効/無効なワークフローを確認する。
   - `.github/workflows` がある場合は、PR対象差分と `on` / `pull_request` / `push` / `branches` / `paths` 条件を確認し、今回のPRで起動対象か判断する。
   - 有効なワークフローが存在し、今回のPRで起動対象と判断できる場合は「CIが起動しなかった」として停止し、確認したワークフロー名、head SHA、直近run、滞留状況を報告する。
   - 起動対象のワークフローが無く、必須チェックも無い場合のみ「CIチェックなし」として続行する。
6. 失敗、キャンセル、タイムアウトしたチェックまたはワークフローがある場合は停止する。
7. チェック待機後に `gh pr view --json headRefOid` を再実行し、`headRefOid` を最新化する。

## 9. マージを実行する
1. 次のコマンドでマージを実行する。  
   `gh pr merge <number> --merge --match-head-commit <headRefOid>`
2. `already merged` が返る場合は、マージ済みとして後続のクリーンアップへ進む。

## 10. マージ失敗時の扱い
1. ブランチ保護ルール、未完了チェック、未起動CI、権限不足で失敗した場合は、Step 8 の確認結果と `gh pr view --json mergeStateStatus,statusCheckRollup` の状態を添えて停止する。
2. `--admin` は使用しない。ユーザーへ管理者マージの可否を確認しない。
3. head SHA の更新だけが原因で失敗した場合は、`headRefOid` を再取得して1回だけ通常マージを再試行する。

## 11. リモートブランチをクリーンアップする
1. リポジトリの自動ブランチ削除設定を確認する。
   - `name_with_owner=$(gh repo view --json nameWithOwner --jq '.nameWithOwner')`
   - `auto_delete=$(gh api "repos/${name_with_owner}" --jq '.delete_branch_on_merge')`
2. `git ls-remote --exit-code --heads origin <headRefName>` でリモートブランチの存在を確認する。
3. 自動ブランチ削除が有効な場合は、最大60秒、5秒間隔でリモートブランチが消えるか確認する。
4. リモートブランチが存在しない場合は「自動削除済み」または「既に削除済み」として扱う。
5. 待機後もリモートブランチが存在する場合のみ、`git push origin --delete <headRefName>` を実行する。
6. 手動削除時にリモート側へ対象ブランチが存在しないエラーになった場合は「既に削除済み」として扱う。

# 安全規約
1. マージ方式は常に `--merge` を使用する。
2. ブランチ作成が必要な場合は `$branch-create` を実行する。
3. 既存PRがある場合に `$pr-create` を呼ばない。
4. 既存PRで差分反映が必要な場合は `$push` のみを使う。
5. 既存PR分岐で `gh pr edit` を使った本文更新は行わない。
6. 現在ブランチが無い場合は、差分や未取り込みコミットがある限り `$branch-create` で現在ブランチを用意してから `$pr-create` へ進む。
7. CIチェックが存在する場合は、完了を待たずに `gh pr merge` へ進まない。
8. `--auto` は CI待機の代替として使わない。チェック未完了、失敗、タイムアウト時は停止する。
9. マージ本体では `--delete-branch` を使わない。
10. リモートブランチ削除前に、リポジトリの自動ブランチ削除設定と現在のリモートブランチ存在有無を確認する。
11. 自動ブランチ削除が有効な場合は、反映待ち後も残っているときだけ `git push origin --delete` を実行する。
12. 管理者権限によるマージは禁止する。`gh pr merge --admin` を実行せず、ユーザーにも提案しない。

# Definition of Done
- 現在作業が対象ブランチに載り、必要なPR作成へ進めている
- 対象PRを取得できない場合のみ `$pr-create` が実行されている
- 既存PRで差分あり時のみ `$push` が実行されている
- CIチェックがある場合は完了まで待機し、失敗時に停止できる
- CIチェックが未検出の場合は、head SHA のワークフロー実行、リポジトリ全体の滞留、有効ワークフロー、起動条件を確認してから停止または続行している
- 手順で定義したマージコマンドで実行される
- 管理者権限によるマージを使用していない
- リモートブランチが削除されている（自動削除済み、手動削除済み、または既に削除済み）
