---
name: "pr-merge"
description: "現在作業または指定PRを安全にマージする。現在作業をブランチへ載せ、PRを取得または作成し、push、Draft解除、auto-merge設定、CI監視、マージ、リモートブランチクリーンアップまで進める。admin merge や bypass が明示された場合は `privileged-merge` として確認なしで使う。"
---

# pr-merge

## 目的
現在作業または指定 PR を、安全にマージする。

対象 PR を確定し、必要な更新、push、Ready 化、auto-merge 設定、CI 監視、マージ、リモートブランチ後始末まで進める。

## 用語
`privileged-merge` は、ユーザーが admin merge または bypass を明示した状態を指す。
これは強い権限指定であり、明示された場合だけ使う。
`gh pr merge` では `--admin` に対応する。

## フロー

### Phase 1: 対象 PR を確定する
- `gh auth status` を確認する。
- PR 番号、URL、ブランチが指定されている場合は、その PR を使う。
- 指定がない場合は、現在ブランチの open PR を探す。
- 現在ブランチに PR がなく、現在作業から PR を作れる場合は `$pr-submit` を使う。
- 複数候補がある場合は停止し、対象を確認する。
- 対象 PR を確定したら `gh pr view` で状態を確認し、open でなければ停止する。
- `privileged-merge` が明示されている場合は状態に残す。

### Phase 2: PR を更新可能な状態にする
- 未コミット差分または未 push コミットがある場合は `$push` を使う。
- PR が behind、または最新化が必要な場合は `$sync-latest` を使い、必要なら `$push` で反映する。
- 同期で衝突した場合はマージへ進まない。
- 既存 PR の本文は、マージのためだけには更新しない。

### Phase 3: Ready にする
Draft PR の場合は Ready にする。
Ready 化できない理由がある場合は停止する。

### Phase 4: マージ経路を決める
最新の head SHA を取得する。
チェック結果と保護ルールの状態を確認し、次に進む先を決める。
チェック結果は必須チェックだけでなく、PR に表示されているチェックも見る。
PR 更新または Ready 化の直後でチェックがまだ表示されていない場合は、短く検出待ちしてから判定する。

| 状態 | 次に進む先 |
| --- | --- |
| `privileged-merge` が明示されている | Phase 7 で `--admin` 付きのマージを実行する。 |
| マージ条件を満たしている | Phase 7 で通常のマージを実行する。 |
| 必須チェック、表示済みチェック、merge queue、保護ルールの完了待ち | Phase 5 で auto-merge を設定する。 |
| チェックが未表示で検出待ちが必要 | Phase 6 で監視する。 |
| 失敗、キャンセル、タイムアウトしたチェックがある | 停止する。 |

### Phase 5: auto-merge を設定する
auto-merge はまず `gh pr merge <number> --auto --merge --delete-branch --match-head-commit <headRefOid>` で設定する。

| 結果 | 次に進む先 |
| --- | --- |
| auto-merge を設定できた | 後でマージされる状態として Phase 9 へ進む。 |
| merge queue がマージ方式の指定を受け付けない | `--merge` を外して再実行し、結果をこの表で判定する。 |
| auto-merge を設定できないが、チェック監視で続行できる | Phase 6 へ進む。 |
| head SHA 不一致、認証不足、権限不足、PR 状態の不整合で失敗した | 停止する。 |

### Phase 6: CI を監視する
Phase 6 は、チェックの検出待ちまたは監視が必要な場合に実行する。

チェックが未表示の場合は短く検出待ちする。
必須チェックが検出できる場合は `gh pr checks <number> --required --watch` で監視する。
必須チェックが検出できない場合は、表示されたチェックを監視する。
チェックが通ったら最新の head SHA を取り直し、Phase 7 へ進む。
失敗、キャンセル、タイムアウトが残る場合は停止する。

### Phase 7: マージする
マージコマンドは次で選ぶ。

| 状態 | コマンド |
| --- | --- |
| `privileged-merge` が明示されている | `gh pr merge <number> --merge --admin --delete-branch --match-head-commit <headRefOid>` |
| `privileged-merge` が明示されていない | `gh pr merge <number> --merge --delete-branch --match-head-commit <headRefOid>` |

保護ルール起因で失敗した場合も、`privileged-merge` の明示がなければ停止する。

### Phase 8: ブランチ削除を確認する
実マージが完了している場合だけ、リモートブランチとローカルブランチの残存を確認する。
リモートブランチが残っている場合だけ `git push origin --delete <headRefName>` を実行する。
ローカルブランチが残っており、現在ブランチでない場合だけ `git branch -d <headRefName>` を実行する。

### Phase 9: 状態を残す
PR URL、更新に使ったスキル、チェック結果、マージ経路、`privileged-merge` 指定の有無、auto-merge 設定結果、マージ結果、ブランチ削除結果、停止理由を残す。
