# Body Generation Rules

## Inputs
- `git diff --name-only <base>...HEAD`
- `git diff --shortstat <base>...HEAD`
- `git log --oneline <base>..HEAD`
- `verification-gate` の結果
- Issue本文（ある場合）

## Sections
1. `Summary`
   - 目的と変更の要点を2〜4行で記述する。
2. `Scope`
   - 主要変更ファイル群を責務単位で要約する。
3. `Verification`
   - format/tests/coverage（必要時）を結果付きで記述する。
4. `Risks / Rollback`
   - 既知リスクとロールバック方針を記述する。
5. `Checklist`
   - 未完了作業・レビュー観点をチェックボックスで列挙する。

## Footer
- Issueあり: 本文末尾に `Closes #<N>` を1行で出力する（Development連携用）
- Issueなし: `Issue: none (ad-hoc task)`
