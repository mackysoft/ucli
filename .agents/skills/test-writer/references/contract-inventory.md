# Contract Inventory

## 目的
- テストが何を保証するかを、利用者視点で固定する。

## 記録単位
各契約は次の形式で記録する。

- Contract: 契約名
- What: 何を保証するか
- Why: なぜ重要か（破ったときの影響）
- Scope: 適用範囲（入力、環境、機能）
- Evidence: 根拠テスト（テスト名、サイズ、種別）

## 最小テンプレート
```markdown
- Contract: <name>
  - What: <behavior guarantee>
  - Why: <impact if broken>
  - Scope: <where it applies>
  - Evidence:
    - <test-id> (<Small|Medium|Large>)
```

## ルール
- 契約は実装詳細ではなく外部から観測できる振る舞いで表現する。
- 仕様不明点は推測で確定しない。
- Evidence が空の契約は未完了として扱う。
