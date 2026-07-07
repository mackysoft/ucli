---
name: "push"
description: "push 指示を受けたときに、未コミット差分があれば `$commit` で責務単位コミットを作成し、現在ブランチを upstream 優先で安全に push する。upstream が無い場合は `origin/<current_branch>` を `-u` で設定して push する。"
---

# push

## 目的
現在ブランチをリモートへ反映する。

未コミット差分がある場合は `$commit` を使う。
push 前の最新化は `$sync-latest` に委譲する。

## フロー

### Phase 1: 作業状態を確認する
- detached HEAD では停止する。
- 現在ブランチ、`origin`、upstream を確認する。
- `--force` と `--force-with-lease` は使わない。

### Phase 2: コミットする
未コミット差分がある場合は `$commit` を使い、責務単位でコミットする。
コミット対象がない場合は作成しない。

### Phase 3: 最新化する
push 前の最新化は `$sync-latest` に委譲する。
`$sync-latest` が同期不要と判断した場合は、そのまま進む。
同期で衝突、未解決事項、未コミット差分の保護が必要な状態になった場合は push しない。

### Phase 4: push する
upstream がある場合は `git push` を実行する。
upstream がない場合は `git push -u origin <current_branch>` を実行する。

push 対象がない場合は、その理由を残して停止する。
push が non-fast-forward で失敗した場合は `$sync-latest` を実行し、同期できた場合だけ1回再試行する。

### Phase 5: 状態を残す
作成したコミット、同期結果、push 先、push 結果、停止理由を残す。
