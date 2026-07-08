---
name: "branch-create"
description: "作業ブランチを規約準拠名で作成または再利用する。Issue Number or URL がある場合は `<type>/issue-<N>-<branch_name>`、Issue が無い場合は `<type>/<branch_name>` 形式で、現在作業を載せるブランチを確定する。"
---

# branch-create

## 目的
作業内容、Issue、指定値から規約準拠の作業ブランチを確定し、作業を載せる。

既存ブランチがある場合は再利用する。
新しい作業を既定ブランチから始める場合は、作成前に `$sync-latest` で起点を最新化する。

## フロー

### Phase 1: 作業状態を確認する
- `gh auth status` を確認する。
- 現在ブランチ、未コミット差分、worktree 使用状況を確認する。
- Issue が指定されている場合は `gh issue view` で存在と状態を確認し、open でなければ停止する。

### Phase 2: 起点を決める
base branch が指定されていれば採用する。
未指定ならリポジトリの既定ブランチを採用する。
採用した base branch が解決できなければ停止する。

現在の未コミット差分や detached HEAD の作業を保持する必要がある場合は、現在の `HEAD` を起点にする。
既定ブランチから新規作成する場合は、先に `$sync-latest` で起点を最新化する。

### Phase 3: ブランチ名を決める
ブランチ種別が指定されていれば採用する。
指定がなければ Issue、ユーザー指示、差分の目的から一般的な開発語彙に沿って選ぶ。

| 変更の性質 | branch type | 対応する Conventional Commit type |
| --- | --- | --- |
| 利用者に見える機能追加 | `feature` | `feat` |
| バグ修正 | `fix` | `fix` |
| 振る舞いを変えない構造整理 | `refactor` | `refactor` |
| 性能改善 | `perf` | `perf` |
| テスト | `test` | `test` |
| ドキュメント | `docs` | `docs` |
| CI 設定 | `ci` | `ci` |
| build、依存、リリース設定 | `build` | `build` |
| その他の保守作業 | `chore` | `chore` |

判断できない場合は `chore` を使う。

ブランチ名が指定されていれば slug 化して使う。
未指定なら Issue、ユーザー指示、差分の目的から slug を作る。

Issue がある場合は `<type>/issue-<N>-<branch_name>` にする。
Issue がない場合は `<type>/<branch_name>` にする。

slug は ASCII 小文字、数字、ハイフンで構成する。
空になった場合は `goal` を使う。

### Phase 4: ブランチを作成または再利用する
- 現在ブランチが確定名と一致する場合は再利用する。
- 同名ローカルブランチがある場合は、別 worktree で使用中でないことを確認して再利用する。
- 同名リモートブランチがある場合は tracking branch として再利用する。
- 既存ブランチが別 worktree で使用中なら停止する。
- 既存ブランチがなければ、Phase 2 の起点から作成する。

### Phase 5: 状態を残す
確定したブランチ名、起点、作成または再利用の結果、停止理由を残す。
