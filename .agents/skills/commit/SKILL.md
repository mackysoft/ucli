---
name: commit
description: 変更を責務単位で分割し、Conventional Commits形式の包括的なメッセージでコミットを作成する。コミットしたいときに使う。
---

# 目的
- 1コミット=1責務を原則化し、レビューと障害調査の単位を明確にする。
- コミット本文に背景・変更内容・影響を残し、履歴だけで意図を追跡可能にする。

# 使うタイミング
- コミットしたいとき

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
- `$log-update` を使った `log.md` 追記

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

## 5. 作業ログを更新する
1. `$log-update` を呼び、`assets/log_entry.md` 形式で `log.md` を追記する。
2. 記録には分割方針、作成コミット一覧、残課題を含める。

# Definition of Done
- 変更がコミット種別単位で分割され、各コミットが単一種別を表している
- 各コミットが参照ルールに従った件名と本文を持つ
- `log.md` に `$log-update` で記録が残っている
