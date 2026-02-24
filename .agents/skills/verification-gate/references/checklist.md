# Verification Gate Checklist（canonical）

## Diff / Spec
- [ ] Diff Budget は閾値内、または Split waived（理由付き）が Issue にある
- [ ] spec drift は無い（Issue 本文と spec.md が一致）
- [ ] 受け入れ条件（Acceptance Criteria）が実装の変更をカバーしている（不足なら spec-sync）

## Run/Skip 判定
- [ ] format / tests / coverage の各項目について、Run/Skip と理由を記録した
- [ ] Run 判定の根拠が「受け入れ判定に必要」で説明できる

## Mechanical
- [ ] format が Run の場合のみ、formatter verify が通っている

## Tests
- [ ] tests が Run の場合のみ、影響範囲のテストを実行した
- [ ] tests が Run かつ失敗した場合、原因分類・再実行手順が log に残っている
