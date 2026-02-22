# Verification Gate Checklist（canonical）

## Diff / Spec
- [ ] Diff Budget は閾値内、または Split waived（理由付き）が Issue にある
- [ ] spec drift は無い（Issue 本文と spec.md が一致）
- [ ] 受け入れ条件（Acceptance Criteria）が実装の変更をカバーしている（不足なら spec-sync）

## Mechanical
- [ ] dotnet format が通る（verify-no-changes）

## Tests
- [ ] 影響範囲のテストを実行した
- [ ] 失敗があれば原因分類・再実行手順が log に残っている

## Artifacts
- [ ] `$log-update` を呼び出して log.md に検証ログを追記した
- [ ] 新しい失敗パターンが出たら、この checklist 自体を更新した
