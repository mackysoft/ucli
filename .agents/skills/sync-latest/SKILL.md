---
name: "sync-latest"
description: "作業開始前、push 前、PR 作成前、PR 更新前、PR マージ前、non-fast-forward 解消など、現在ブランチの最新性が作業の前提になる場面で、origin から最新状態を取得し、現在ブランチ、upstream、open PR、既定ブランチ、指定ブランチを確認して、必要な場合だけ安全に同期する。"
---

# sync-latest

## 目的
現在ブランチの最新性が作業の前提になる場面で状態を確認し、必要な場合だけ適切な基準を取り込む。

## フロー

### Phase 1: 作業状態を確認する
- detached HEAD では停止する。
- `origin` が存在することを確認する。
- 現在ブランチ、upstream、open PR、既定ブランチ、指定ブランチ、未コミット差分を確認する。
- 同期で上書きされうる未コミット差分がある場合は停止し、先に `$commit` で保護する。

### Phase 2: 最新状態を取得する
`git fetch origin --prune` で remote-tracking branch を更新する。
判断に必要な場合だけ tag も取得する。
前提確認なしに `git pull` を使わない。

### Phase 3: 最新化の要否と基準を決める

| 状態 | 扱い |
| --- | --- |
| ブランチが指定されている | 指定ブランチを同期基準にする。 |
| upstream があり、現在ブランチが behind している | upstream branch を同期基準にする。 |
| open PR があり、現在ブランチが PR base に対して behind している | PR の base branch を同期基準にする。 |
| 現在ブランチが既定ブランチであり、`origin/<default>` に対して behind している | `origin/<default>` を同期基準にする。 |
| 作業ブランチに open PR がなく、`origin/<default>` に未取り込みの commit がある | `origin/<default>` を同期基準にする。 |
| 取り込むべき基準がない | 同期不要として進む。 |

複数該当する場合は、upstream を先に同期し、その後に PR base または既定ブランチを同期する。

### Phase 4: 同期する
- 現在ブランチが同期基準そのものなら fast-forward のみ行う。
- 作業ブランチでは同期基準を現在ブランチへ merge する。
- 同期不要の場合は merge しない。
- 既に共有済みのブランチでは履歴を書き換えない。
- `--force` と `--force-with-lease` は使わない。
- 衝突した場合は、同期後に予定していた後続操作へ進まない。

### Phase 5: 状態を残す
同期した基準ブランチ、同期前後の ahead / behind、作成された merge commit の有無、衝突または未解決事項、次に進める作業を残す。
