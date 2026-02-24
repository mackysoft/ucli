---
name: verification-gate
description: コード変更が一通り終わった挙動確認時、またはPR作成前に、受け入れ判定に必要な検証のみを実行し、diff budget / spec drift を検出して止め、検証ログは条件に応じて `$log-update` で記録する。実装や設計検討が目的のときは使わない。
---

# 目的
- 「PR前に必ず通す検証ゲート」を固定し、検証漏れ・巨大差分・spec drift を機械的に検出して止める。
- 検証項目ごとに「今回の成果の受け入れ判定に必要か」を判定し、無関係な検証発火を防ぐ。

# 使うタイミング
- 変更が一通り終わった際の挙動確認
- PRを作る前の検証
- 「PRレビューに回す前に、最低限のformat・テスト・（必要なら）カバレッジを揃えたい」

# 使わないタイミング
- まだ設計が固まっていない、または仕様が未確定
- 変更途中で、検証結果を残す意味が薄い（ただし Smoke は可）

# 契約
- spec drift 比較対象は `docs/agents/<BRANCH_DIR>/spec.md` のみ。
- `spec.md` が無い場合、spec drift は `Skipped` にする。
- `spec.md` / `log.md` の有無は StartWork 実行済み判定に使わない。

# 参照
- ゲートレベル: [references/gate_levels.md](references/gate_levels.md)
- 閾値（diff budget）: [references/thresholds.md](references/thresholds.md)
- チェックリスト（更新対象）: [references/checklist.md](references/checklist.md)
- 失敗分類: [references/failure_triage.md](references/failure_triage.md)
- テンプレ:
  - log追記: [assets/log_entry.md](assets/log_entry.md)

# 入力（任意。無くても推定して進める）
- 目的（優先）:
  - 「挙動確認」 / 「PR前」 / 「最終」など
- 希望のゲートレベル（任意）:
  - `Smoke` / `Standard` / `Full`
- 検証実行方針（任意）:
  - `Auto` / `Force`
- 影響が大きいと感じる点（任意）:
  - 例: 「コアロジック変更」「データ形式変更」「UIフロー変更」など
- 検証対象（任意）:
  - format対象
  - test対象
  - coverage対象

# 出力
- 実行した検証の結果（format / tests / coverage / spec drift / diff budget）
- 各検証項目の判定（Run/Skip）と判定理由
- 失敗時: 失敗分類＋次の最短リトライ方針
- 検証ログの記録結果（`log.md` 存在時は `$log-update`、不在時は Skip 理由）

# 手順
## 1. ゲートレベルを決める（必須）
- ユーザーが明示した場合: そのレベルを採用
- 明示が無い場合: 目的と Task Size で決める
  - 「PR前」→ Standard（原則）
  - 「最終」→ Full
  - 「挙動確認」→ Smoke（原則）
- Task Size が取得できるなら `references/gate_levels.md` に従って補正する

## 2. 検証項目の実行要否を判定する（必須）
1. 判定対象は `format` / `tests` / `coverage` とする。
2. `検証実行方針 = Force` の場合は、判定を省略して全項目を `Run` にする。
3. `Auto`（既定）の場合、各項目は次を満たすときのみ `Run` にする。
   - ユーザーが当該項目の実行を明示している
   - 受け入れ条件または合意済み仕様に当該項目の実行要件がある
   - 当該項目を実行しないと、今回成果の受け入れ可否を判定できない
4. 上記を満たさない項目は `Skip` とし、理由を検証ログへ残す。

## 3. Diff Budget ゲート（必須: 巨大差分を止める）
1. `git diff --stat` / `--shortstat` から以下を取得する。
   - 変更ファイル数
   - 追加/削除行数
2. `references/thresholds.md` と照合し、想定サイズ（Small/Medium/Large）を逸脱していればゲートを **FAIL** にする。
3. FAIL の場合の扱い:
   - 原則: Issue の分割方針に従って子Issue/子PRへ分割する
   - 例外（どうしても分割できない）は、Issue本文に **Split waived**（分割免除）を理由付きで明記し、検証ログにも残す

## 4. Spec drift ゲート（必須: 仕様と実装のズレを止める）
1. 現在ブランチ名から `<BRANCH_DIR>` を算出する。
2. `docs/agents/<BRANCH_DIR>/spec.md` が存在する場合のみ、以下を実行する。
   - Issue番号が特定できる場合は `gh issue view` で Issue本文を取得する
   - `spec.md` の `## Issue Body` セクションと比較する
   - 不一致なら **FAIL**（spec-sync を要求）
3. `spec.md` が存在しない場合は、spec drift 判定を **Skipped** とし、理由を検証ログへ残す。
4. Issue番号を特定できない場合も **Skipped** とし、理由を検証ログへ残す。

## 5. Mechanical ゲート（条件付き: format）
1. `format = Run` の場合のみ、対象に適合する formatter を選択して実行する。
2. 対象を限定できる場合は限定して実行する。
3. `--verify-no-changes` 相当の確認が通る状態にする。
4. format によってファイルが更新された場合:
   - 以降のテストは続行してよいが、最終判定で「PR Ready」は出さない（コミットが必要）

## 6. テストゲート（条件付き）
1. `tests = Run` の場合のみ、ゲートレベルに応じて実行範囲を決める（gate_levels.md）。
2. 対象に適合するテストランナーを選択してテストを実行する。
3. 失敗した場合:
   - `references/failure_triage.md` で分類し、「最短の再実行方針」と「修正の当たり」を提示する
   - ゲートは **FAIL**

## 7. （必要なら）Coverage ゲート（任意）
`coverage = Run` かつ次の場合は coverage を実行する（Fullでは原則実行、Standardでは条件付き）。
- 受け入れ条件にカバレッジが含まれる
- 重要な分岐/計算/データ変換/永続化など、ロジック品質が主リスク
- 回帰が怖い領域（既知のバグ多発箇所）

## 8. 成果物更新（条件付き: log）
1. `docs/agents/<BRANCH_DIR>/log.md` が存在する場合は `$log-update` を呼び出し、検証ログを記録する。
2. `log.md` が存在しない場合は、ログ更新処理を Skip し、理由を出力へ残す。
3. チェックリスト更新要否を判定する。
   - 今回、手順漏れや新しい失敗パターンが発生したなら `references/checklist.md` を同一PR内で更新する（必須）

## 最終判定（PASS/FAIL）
- FAIL 条件:
  - Diff Budget 逸脱（免除なし）
  - spec drift で比較可能時に不一致
  - `Run` 判定になった format verify 失敗
  - `Run` 判定になったテスト失敗
- PASS（PR Ready）条件:
  - 上記が全てOK
  - かつ、作業ツリーがクリーン（`git status --porcelain` が空）
- PASS（Not Ready）:
  - 検証自体はOKだが、format等で未コミット変更が残っている

# Definition of Done
- 実行した検証の結果が出力として残っている
- `format/tests/coverage` それぞれに Run/Skip と理由が残っている
- `log.md` が存在する場合は `$log-update` で記録されている
- `log.md` が存在しない場合は、ログ更新処理が Skip され理由が記録されている
- FAIL の場合は次のアクション（spec-sync / 分割 / 再実行方針）が明記されている
