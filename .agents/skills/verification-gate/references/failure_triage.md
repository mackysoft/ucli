# Failure triage（失敗分類）

## dotnet format 失敗
- 原因:
  - .editorconfig違反
  - analyzer警告（設定次第）
- 対処:
  - apply → verify の順で再実行
  - 変更が出たらコミットしてから再ゲート

## テスト失敗
- 分類:
  - ロジック不整合（期待値/仕様）
  - 初期化順序・実行順序依存
  - 環境依存（ファイル/時間/順序/乱数）
  - 不安定（Flaky）
- 対処:
  - 失敗テスト名で絞って再実行（filter）
  - Flaky疑いは原因を特定し、再現条件を log.md に残す
  - 仕様解釈が原因なら spec-sync を先に行う

## Diff budget 逸脱
- 原因:
  - 分割漏れ（設計/土台/実装/整理が混ざっている）
- 対処:
  - Issue の分割方針に従って子Issue/子PRへ分割
  - 例外は Split waived（理由付き）を Issue に明記
