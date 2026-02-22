# Issue Update Strategy（壊さず更新する）

## 原則
- Issue がSource of Truth（spec.md はスナップショット）
- 推測で仕様を書かない
- 既存の文脈や履歴を壊さない（無理にテンプレへ変換しない）

## 更新方式の選択
### A. テンプレ形式（推奨）
Issue本文に以下のようなセクションが明確に存在する場合：
- 背景（Why）
- 変更内容（What）
- 技術仕様（How）
- スコープ外（Out of Scope）
- 受け入れ条件（Acceptance Criteria）
- ベースブランチ（Base branch）
- タスクサイズ（Task Size）

→ **該当セクションだけを更新**し、他は維持する。

### B. 非テンプレ形式（安全策）
Issue本文が自由形式で、セクション境界が曖昧な場合：
→ 本文を大改造せず、末尾に以下を追記する。

#### 追記する見出し例
## Decision Update (YYYY-MM-DD HH:MM)

- Changed:
  - What:
  - How:
  - Acceptance Criteria:
  - Out of Scope:
- Rationale:
- Open Questions

## 受け入れ条件の扱い
- 新規条件はチェックボックスで追加
- 既存条件の意味を変える場合は、理由を追記