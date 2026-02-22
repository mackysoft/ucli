---
name: start-work
description: GitHub Issue を起点に、タスクサイズ（S/M/L）を確定し、必要な分割（Lのみ）を行い、作業ブランチと Documents/agents 配下の spec.md と log.md を初期化して「作業開始状態」を作る。実装・レビュー・テスト実行・フォーマット実行が主目的のときは使わない。
---

# 目的
- 「小さい作業を小さく始める」ために、開始時点での境界（S/M/L）と成果物を固定する。
- 作業開始時点で、Issue / ブランチ / agentsドキュメントが揃っている状態にする。

# 使う/使わない
## 使う
- 新規に着手する（Issueが未特定・ブランチ未作成・agentsドキュメント未作成）
## 使わない
- すでに Issue / ブランチ / Documents/agents が揃っている。
- 実装・レビュー・テスト・format が主目的。

# 参照
- タスクサイズ定義: [references/task_analyze.md](references/task_analyze.md)
- L分割ルール: [references/split_policy.md](references/split_policy.md)
- テンプレ:
  - Issue本文: [assets/issue.md](assets/issue.md)
  - spec.md: [assets/spec.md](assets/spec.md)
  - log.md: [assets/log.md](assets/log.md)

# 手順
## 0. 前提チェック（必須）
1. `git status --porcelain` が空であること（未コミット変更があるなら、このスキルは中断）
2. `gh auth status` が成功すること（失敗するなら、このスキルは中断）

## 1. Issue を特定 or 作成
- Issue番号/URLがある：`gh issue view` で本文を取得し、対象を確定する
- キーワードのみ：`gh issue list --search` で open issue を探して確定する
- 見つけた場合：`analyzed`ラベルが付与されていない場合、`assets/issue.md` をテンプレにしてIssueを更新する
- 見つからない：`assets/issue.md` を本文テンプレにして Issue を作成し、`analyzed`ラベルを付与する

## 2. タスクサイズ（S/M/L）を確定し、Issue に明記（必須）
- `references/task_analyze.md`に従って S/M/L を決める
- Issue本文に `Task Size: S/M/L` を入れて確定する

## 3. L の場合は分割を完了してから先へ進む（必須）
- `references/split_policy.md` に従い、子Issueを作る
- 作業対象を「最も小さい子Issue」に切り替える（親Issueで実装しない）

## 4. ベースブランチを確定
- Issue本文にブランチ指定があればそれを採用
- 無ければデフォルトブランチを採用

## 5. ブランチ作成（<type>/<branch_name>）
- type:
  - `feature`
  - `fix`
  - `refactor`
  - `docs`
  - `chore`
  - `ci`
- branch_name: `issue-<N>-<short-slug>`
- 既存ならそのブランチを使う（勝手に別名を作らない）

## 6. Documents/agents を初期化（必須）
- `<BRANCH_DIR>` = ブランチ名の `/` を `_` に置換
- `Documents/agents/<BRANCH_DIR>/` を作成
- `assets/spec.md` / `assets/log.md` をテンプレにして作成
- spec.md は Issue本文をミラー（直接編集しない）

# Definition of Done
- 対象Issueが特定され、Issue本文に `Task Size` が確定している
- Lなら、子Issueに分割済みで作業対象が子Issueに切り替わっている
- ブランチが作成されチェックアウトされている
- `Documents/agents/<BRANCH_DIR>/spec.md log.md` が作成されている
