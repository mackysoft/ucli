---
name: test-writer
description: 契約と不変条件を根拠に、新規または更新の自動テストを1件ずつ設計・作成する。TDDで進め、同型テストを統合し、全テストへサイズ属性を必須付与する。実行・フォーマット・検証ゲートの実施が主目的のときは使わない。
---

# 目的
- 契約（Contract）と不変条件を、最小のテスト集合で検証する。
- テストを変更容易性の基盤として育て、回帰検出の根拠を明確にする。
- レビュー対象を「契約の妥当性」と「証拠の十分性」に集中させる。

# 非目的
- テスト実行、フォーマット実行、検証ゲート実行。
- カバレッジ数値の最大化だけを目的にしたテスト量産。
- 実装詳細への過剰結合（内部呼び出し順、private構造への依存）。

# 入力
- 変更要求（バグ修正、API/CLI変更、データ契約変更、リファクタなど）。
- 現行の仕様または契約情報。
- 対象コードと既存テスト。

# 出力
- 契約に対応した新規/更新テスト。
- `assets/report-template.md` に従ったレポート。
- 必要時に `assets/execution-handoff.md` を使った引き継ぎメモ。

# 参照
- 契約の定義と記録形式: [references/contract-inventory.md](references/contract-inventory.md)
- 変更タイプ別の選択基準: [references/test-selection-matrix.md](references/test-selection-matrix.md)
- サイズ分類と付与規約: [references/test-size-policy.md](references/test-size-policy.md)
- 信頼性とフレーク対策: [references/reliability-policy.md](references/reliability-policy.md)
- 同型ケース統合規則: [references/equivalence-rules.md](references/equivalence-rules.md)
- 既存テスト更新規約: [references/update-policy.md](references/update-policy.md)
- テストリスト: [assets/test-list-template.md](assets/test-list-template.md)
- レポート雛形: [assets/report-template.md](assets/report-template.md)
- 実行引き継ぎ: [assets/execution-handoff.md](assets/execution-handoff.md)
- flaky隔離メモ: [assets/flaky-quarantine-note.md](assets/flaky-quarantine-note.md)

# 手順
## 1. Contract Inventory を作成する
- `references/contract-inventory.md` の形式で、今回守る契約を列挙する。

## 2. テストリストを作成する
- `assets/test-list-template.md` を使い、候補ケースを列挙する。

## 3. 1ケースだけ選ぶ
- TDDとして、1ループで扱うケースを1つに限定する。
- 選んだケースに、契約IDと失敗分類を紐づける。

## 4. サイズ属性を先に決める
- サイズ分類、記法、許容値、運用上の制約は `references/test-size-policy.md` の定義に従う。

## 5. Red -> Green -> Refactor で作成する
- 先に失敗条件（Red）を定義し、最小差分で通す（Green）。
- 契約を壊さない範囲で、テストと実装の重複を整理する（Refactor）。

## 6. DRY統合判定を行う
- 同型判定、統合条件、分離条件、優先順位は `references/equivalence-rules.md` の定義に従う。

## 7. 信頼性チェックを行う
- 偽陽性/偽陰性の対策と flaky の扱いは `references/reliability-policy.md` に従う。
- 隔離が必要な場合は `assets/flaky-quarantine-note.md` を使用する。

## 8. 5点レポートを作成する
- `assets/report-template.md` の全項目を埋める。

## 9. 実行は別工程へ引き継ぐ
- 本スキルではテスト実行を行わない。
- 実行が必要な場合は `assets/execution-handoff.md` を作成し、`verification-gate` へ委譲する。

# 受け入れシナリオ
1. バグ修正では、修正前に失敗する回帰ケースを先に定義できる。
2. API/CLI変更では、正常系と異常系の契約テストを両方作成できる。
3. パーサ/正規化/シリアライズ変更では、プロパティテストまたは軽量ファズ方針を含められる。
4. 同型ケースが複数ある場合、重複を作らず統合できる。
5. 新規/更新テストにサイズ属性を必須付与できる。
6. flaky要因がある場合、隔離方針を出力できる。
7. 実行依頼が含まれていても、作成のみ実施して引き継ぎへ切り替えられる。

# Definition of Done
- 影響契約を列挙し、各契約に少なくとも1つの証拠テストを紐づけている。
- 主要な失敗系（入力不正、境界、例外/exit code）を押さえている。
- 本質的に同じテストが統合され、重複が残っていない。
- 5点レポートと必要な引き継ぎメモが作成されている。
