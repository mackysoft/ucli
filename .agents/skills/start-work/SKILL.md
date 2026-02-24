---
name: start-work
description: 計画完了後の新規タスク開始時に、タスク性質（Issueベース/アドホック）とタスクサイズ（Small/Medium/Large）を確定し、必要なら分割し、Issueベース時のみ `$branch-create` と `docs/agents` 初期化を実行して作業開始状態を作る。実装・レビュー・テスト実行・フォーマット実行が主目的のときは使わない。
---

# 目的
- 作業開始前にタスク性質と規模を確定し、実装境界を固定する。
- 大きい作業は着手前に分割し、レビュー不能な巨大変更を防ぐ。
- Issueベース作業では、ブランチと作業ファイルを揃えて開始状態を作る。

# 使う/使わない
## 使う
- 計画が完了し、新規タスクに着手する時点。
- 非アドホックで、Issueベース運用として進めるとき。

## 使わない
- ユーザーがアドホック作業を明示している。
- ユーザーが「Issueなしで継続」を選択した。
- 実装・レビュー・テスト・format が主目的。

# 契約
- StartWork 実行可否は「新規開始か」「アドホックか」「Issue運用か」で判定する。
- ブランチ作成・再利用は `$branch-create` に委譲し、StartWork 内で独自に命名しない。

# 参照
- タスクサイズ定義: [references/task_analyze.md](references/task_analyze.md)
- Large分割ルール: [references/split_policy.md](references/split_policy.md)
- テンプレ:
  - Issue本文: [assets/issue.md](assets/issue.md)
  - spec.md: [assets/spec.md](assets/spec.md)
  - log.md: [assets/log.md](assets/log.md)

# 手順
## 0. 実行対象を判定する（必須）
1. 「新規タスク開始時点」かを確認する。新規開始でなければ中断する。
2. `git status --porcelain` が空であることを確認する。未コミット変更がある場合は中断する。
3. 次のいずれかに該当する場合は「アドホック」と判定し、このスキルは不実行で終了する。
   - ユーザーがアドホック作業を明示した
   - ユーザーが Issueなし継続を選択した
4. `gh auth status` が成功することを確認する。失敗したら中断する。

## 1. Issue を特定または作成する（非アドホック時）
- Issue番号/URLがある: `gh issue view` で対象を確定する
- キーワードのみ: `gh issue list --search` で open issue を探索して確定する
- 見つからない: `assets/issue.md` をテンプレに Issue を作成し、`analyzed` ラベルを付与する

## 2. タスク性質とタスクサイズを確定する（必須）
1. タスク性質を `Issue-based` に確定する（非アドホックのみ）。
2. `references/task_analyze.md` に従って `Small|Medium|Large` を決める。
3. Issue本文へ `Task Size: Small|Medium|Large` を反映して確定する。

## 3. Large の場合は分割を完了してから先へ進む（必須）
- `references/split_policy.md` に従って子Issueを作成する
- 実装対象を最小の子Issueへ切り替える（親Issueで実装しない）

## 4. `$branch-create` でブランチを確定する（必須）
1. `$branch-create` を呼び出し、`Issue Number or URL` を渡す。
2. 返却された `Branch Name` / `Base Branch` / `Branch Directory Name` を採用する。

## 5. 作業ファイルを必要時のみ作成する（Issueベース時）
1. `<BRANCH_DIR>` は `Branch Directory Name` を使用する。
2. `docs/agents/<BRANCH_DIR>/` を作成する。
3. `assets/spec.md` / `assets/log.md` をテンプレとして配置する。
4. `spec.md` は Issue本文ミラーとして扱い、直接仕様編集をしない。

# Definition of Done
- 非アドホック時:
  - 対象Issueが確定し、`Task Size` が `Small|Medium|Large` で記録されている
  - `Large` の場合は子Issueへ分割され、作業対象が子Issueへ切り替わっている
  - `$branch-create` の出力でブランチが確定している
  - `docs/agents/<BRANCH_DIR>/spec.md` と `log.md` が作成されている
- アドホック時:
  - StartWork を不実行で終了し、Issue作成・ブランチ作成・作業ファイル作成を行っていない
