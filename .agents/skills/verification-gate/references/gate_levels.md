# Gate Levels（検証の強さ）

## Smoke（挙動確認・短時間）
- `Run` 判定になった format を最小範囲で実行
- `Run` 判定になったテストを最小範囲で実行
- spec drift は比較対象がある場合のみ評価し、対象が無ければ `Skipped`

用途:
- 変更直後の挙動確認
- Small想定の小変更

## Standard（PR前の基本）
- Diff Budget ゲート（必須）
- Spec drift ゲート（比較対象がある場合は必須）
- `Run` 判定になった format（apply + verify）
- `Run` 判定になったテスト（影響範囲）＋必要なら追加テスト

用途:
- 通常のPR前

## Full（最終/高リスク）
- Standard の全て
- `Run` 判定になったテストを広く実行（原則フル）

用途:
- 高リスク変更
- 重要フロー変更
- 最終確認
