---
name: spec-sync
description: 作業途中の意思決定変更（仕様/受け入れ条件/技術方針）の結果、Issueと実装・作業資料に差分が生じたときに、Issue（Source of Truth）を更新し、その本文を `spec.md` にミラーし、必要時のみ `log.md` へ同期記録を残す。実装やテスト実行が目的のときは使わない。
---

# 目的
- 「spec drift（Issueと実装の不一致）」を止める。
- ルール（spec.md 直接編集禁止、更新は Issue → spec へミラー）を守れるようにする。

# 使うタイミング
- 作業途中で決定が変わった（仕様/How/受け入れ条件/スコープ/Out of Scope）
- PR前の最終確認で Issue と現状がズレている
- 既に Issue は更新済みだが spec.md へのミラーが追いついていない

# 使わないタイミング
- Issue更新が不要（単なる実装の進捗）で spec drift がない
- Issueが closed

# 契約
- 参照・更新対象は `docs/agents/<BRANCH_DIR>/spec.md` と `log.md` のみ。
- `spec.md` / `log.md` が存在しない場合、該当するファイル処理は Skip する。
- ファイル有無は StartWork 実行済み判定に使わない。

# 参照
- 同期トリガー: [references/triggers.md](references/triggers.md)
- Issue更新戦略: [references/issue_update_strategy.md](references/issue_update_strategy.md)
- テンプレ:
  - spec: [assets/spec.md](assets/spec.md)
  - log 追記: [assets/log_entry.md](assets/log_entry.md)

# 入力（可能ならユーザーが与える）
- Issue番号/URL（最優先）
- 変更になった決定事項（箇条書き）
  - 例: 「受け入れ条件を2つ追加」「HowをA案→B案へ変更」「Out of Scope を削除」など

> 入力が無くても、可能な限りリポジトリ（spec.md / log.md / diff）から根拠を拾って進める。
> 根拠が無い内容は推測で書かない（未確定事項として Issue に質問として残す）。

# 出力
- GitHub Issue 本文の更新（必要な場合のみ）
- `docs/agents/<BRANCH_DIR>/spec.md` の更新（ファイル存在時のみ）
- 同期記録の追記（`log.md` 存在時のみ）

# 手順
## 0. 同期対象（Issue / spec.md / log.md）を特定する
1. 現在のブランチ名を取得する。
2. `<BRANCH_DIR>` を算出する（ブランチ名の `/` を `_` に置換）。
3. 以下のファイルを確認する。
   - `docs/agents/<BRANCH_DIR>/spec.md`
   - `docs/agents/<BRANCH_DIR>/log.md`
4. Issue番号を次の優先順で確定する。
   - ユーザー入力の Issue番号/URL
   - `spec.md` が存在する場合は `Issue Number:` または `Issue:` URL
   - ブランチ名が `issue-<N>-...` 形式なら `<N>`
5. ここまでで Issue が確定できない場合は中断する（推測で Issue を触らない）。
6. `gh issue view` で Issue の state を確認し、closed なら中断する。

## 1. spec drift の有無と変更点（根拠付き）を確定する
1. Issue本文（現行）を取得する。
2. 変更点の根拠を集める（推奨順）。
   - ユーザーが提示した決定事項
   - `log.md` が存在する場合は直近エントリ
   - `git diff`（実装済みなら実際の変更）
3. 変更点を次の分類で箇条書きにする。
   - What（変更内容）
   - How（技術方針）
   - Acceptance Criteria（受け入れ条件）
   - Out of Scope
   - Risk/Questions（未確定事項）

## 2. Issue を更新する（必要な場合のみ）
1. `references/issue_update_strategy.md` に従い、更新方式を決める。
2. 受け入れ条件の変更がある場合:
   - 追加はチェックボックス（未チェック）で追加
   - 既存条件の意図を変える場合は文言更新と理由追記
3. `gh issue edit` で Issue本文を更新する。
4. 更新後に再取得し、反映済みを確認する。

## 3. spec.md を Issue本文からミラーする（条件付き）
1. `spec.md` が存在する場合のみ実行する。
2. Issue URL / Task Size / Base branch を確定する。
3. `spec.md` の `## Issue Body` セクションへ Issue本文を全文反映する。
4. Task Size の記録は `Small|Medium|Large` を使用する。
5. `spec.md` が存在しない場合はミラー処理を Skip する（新規作成しない）。

## 4. log.md に同期記録を追記する（条件付き）
1. `log.md` が存在する場合のみ同期記録を追記する。
2. `log.md` が存在しない場合はログ追記を Skip する（新規作成しない）。

# Definition of Done
- （必要なら）Issue本文が更新され、変更点が根拠付きで反映されている
- `spec.md` が存在する場合は、Issue 本文の最新状態がミラーされている
- `log.md` が存在する場合は、同期の記録が追記されている
- 不存在ファイルは処理が Skip され、生成されていない
