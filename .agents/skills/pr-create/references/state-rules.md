# State Rules

## Issue state
1. Issueが特定できる場合は Issue優先で進める。
2. Issueが特定できない場合は、確認せず Issueなし運用で継続する。

## Existing PR state
1. 同一headのOpen PRが無ければ新規作成する。
2. 同一headのOpen PRがある場合は、必ずユーザー確認後に `更新/新規` を決定する。

## PR kind state
1. `verification-gate` が FAIL の場合は停止する。
2. PASS かつ IssueのAcceptance Criteriaが全完了なら通常PRを作成/更新する。
3. PASS かつ Issueが無い（即興タスク）の場合は通常PRを作成/更新する。
4. PASS でも、Issueがあり次のどちらかに該当する場合はDraft PRにする。
   - Acceptance Criteria が未完了
   - Acceptance Criteria が未定義
