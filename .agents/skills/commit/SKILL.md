---
name: commit
description: 変更を責務単位で分割し、Conventional Commits形式の包括的なメッセージでコミットを作成する。`docs/agents/<BRANCH_DIR>/log.md` がある場合のみ `$log-update` を実行する。
---

# 目的
- 1コミット=1責務を原則化し、レビューと障害調査の単位を明確にする。
- コミット本文に背景・変更内容・影響を残し、履歴だけで意図を追跡可能にする。

# 使うタイミング
- コミットしたいとき

# 契約
- `$log-update` は `docs/agents/<BRANCH_DIR>/log.md` が存在する場合のみ呼び出す。
- `log.md` が存在しない場合はログ更新を Skip し、コミット処理を継続する。
- `log.md` の有無は StartWork 実行済み判定に使わない。

# 参照
- コミット規則: [references/commit-rules.md](references/commit-rules.md)
- コミット本文テンプレート: [assets/commit-body-template.md](assets/commit-body-template.md)
- log追記テンプレート: [assets/log_entry.md](assets/log_entry.md)

# 入力（任意。無くても推定して進める）
- 対象範囲（全差分 / 一部差分）
- 優先したい分割方針（厳密分割 / 実務優先）

# 出力
- 責務単位に分割されたコミット
- 参照ルールに従った件名と本文
- ログ更新の実行結果（Run/Skip）

# 手順
## 1. 前提確認
1. `git status --porcelain` を確認し、変更が無ければ終了する。
2. detached HEAD なら停止し、作業ブランチでの実行へ切り替える。

## 2. 変更を分類し、コミット単位を確定する
1. 変更ファイル一覧を取得する。
2. `references/commit-rules.md` に従って `type` / `scope` / コミット単位を確定する。

## 3. コミットメッセージを生成する
1. `references/commit-rules.md` に従って件名を作成する。
2. `assets/commit-body-template.md` を使って本文を作成する。

## 4. コミットを作成する
1. コミット単位ごとに対象ファイルのみ `git add` する。
2. `git commit -m "<subject>" -m "<body>"` でコミットする。
3. 作成後に `git log --oneline -n <作成数>` で結果を確認する。

## 5. 作業ログを更新する（条件付き）
1. 現在ブランチ名から `<BRANCH_DIR>` を算出する（`/` を `_` に置換）。
2. `docs/agents/<BRANCH_DIR>/log.md` が存在する場合は `$log-update` を呼び、`assets/log_entry.md` 形式で追記する。
3. `log.md` が存在しない場合はログ更新を Skip して完了する（新規作成しない）。

# Definition of Done
- 変更がコミット種別単位で分割され、各コミットが単一種別を表している
- 各コミットが参照ルールに従った件名と本文を持つ
- `log.md` が存在する場合は `$log-update` で記録が残っている
- `log.md` が存在しない場合はログ更新が Skip され、コミット処理が完了している
