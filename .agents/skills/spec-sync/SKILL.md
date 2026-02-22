---
name: spec-sync
description: 作業途中の意思決定変更（仕様/受け入れ条件/技術方針）の結果、Issueと実装・作業資料に差分が生じたときに、Issue（Source of Truth）を更新し、その本文を spec.md にミラーし、`$log-update` を呼び出して同期記録を残す。実装やテスト実行が目的のときは使わない。
---

# 目的
- 「spec drift（Issueと実装の不一致）」を止める。
- ルール（spec.md 直接編集禁止、更新は Issue → spec へミラー）を守れるようにする。

# 使うタイミング
- 作業途中で決定が変わった（仕様/How/受け入れ条件/スコープ/Out of Scope）
- PR前の最終確認で Issue と現状がズレている。
- 既に Issue は更新済みだが spec.md へのミラーが追いついていない。

# 使わないタイミング
- Issue更新が不要（単なる実装の進捗）で spec drift がない。
- Issueが閉じている

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
- `Documents/agents/<BRANCH_DIR>/spec.md` の更新（Issue本文ミラー）
- `$log-update` 呼び出しによる同期記録の追記（同期した事実＋変更点）

# 手順

## 0. 同期対象（Issue / spec.md / log.md）を特定する
1. 現在のブランチ名を取得する
2. `<BRANCH_DIR>` を算出する  
   - ルール：ブランチ名の `/` を `_` に置換  
3. 次のファイルが存在することを確認する  
   - `Documents/agents/<BRANCH_DIR>/spec.md`
   - `Documents/agents/<BRANCH_DIR>/log.md`
   - 無い場合：このskillは中断する

4. Issue番号を確定する（優先順位）
   - ユーザー入力の Issue番号/URL
   - `spec.md` の `Issue Number:` または `Issue:` URL
   - ブランチ名が `issue-<N>-...` 形式なら `<N>`
   - ここまでで確定できない場合：中断（推測で Issue を触らない）

5. `gh issue view` で Issue の state を確認する
   - closed の場合：中断（再オープン/作り直しの判断が必要）

## 1. spec drift の有無と「変更点（根拠付き）」を確定する
1. Issue本文（現行）を取得する
2. 変更点の根拠を集める（推奨順）
   - ユーザーが提示した決定事項
   - `log.md` の直近エントリ（設計判断が書かれているか）
   - `git diff`（実装済みなら実際の変更が根拠になる）
3. 変更点を次の分類で箇条書きにする
   - What（変更内容）
   - How（技術方針）
   - Acceptance Criteria（受け入れ条件）
   - Out of Scope
   - Risk/Questions（未確定事項）

> 不確かな内容は「決定済み」として書かない。`Risk/Questions` として Issue に残す。

## 2. Issue を更新する（必要な場合のみ）
1. `references/issue_update_strategy.md` に従い、更新方式を決める
   - Issue本文がテンプレ形式なら「該当セクションを更新」
   - そうでなければ「Decision Update セクションを追記」
2. 受け入れ条件の変更がある場合
   - 追加はチェックボックス（未チェック）で追加する
   - 既存条件の意図を変える場合は、条件の文言を更新し、必要なら理由を追記する
3. 更新した Issue本文を `gh issue edit` で反映する
4. Issue本文の更新が完了したら、もう一度 `gh issue view` で本文を取得して「反映済み」を確認する

## 3. spec.md を Issue本文からミラーする（必須）
1. Issue の最新本文を取得する（反映後の本文が必須）
2. Issue URL / Task Size / Base branch を確定する
   - `spec.md` の `## Issue Body` セクションへ Issue本文を全文貼り付ける
   - Task Size が S だが、内容が S の定義を超えている場合：
     - Issue の Task Size を M に上げる（根拠を `Risk/Questions` に残す）

3. spec.md を更新する（ルール）
   - spec.md は Issue 本文のスナップショット（ミラー）であり、spec.md 側で内容を編集しない
   - 変更が必要なら Issue を更新し直し、再ミラーする

## 4. log.md に同期記録を追記する（必須）
1. `log.md`に以下を埋めて追記する
   - Issue番号/URL
   - 同期理由（どのトリガーに該当したか）
   - Issueで変更した項目（What/How/AC/Out of Scope）
   - spec.md をミラーした事実
   - 触ったファイル

# Definition of Done
- （必要なら）Issue本文が更新され、変更点が根拠付きで反映されている
- `spec.md` が Issue 本文の最新状態をミラーしている（サイズに応じた運用）
- `log.md` に同期の記録が追記されている
