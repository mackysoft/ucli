# Branch Policy

## Type
- 許可値: `feature|fix|refactor|docs|chore|ci`
- 決定順:
1. `Branch Type` が許可値ならそのまま採用する。
2. Issue がある場合は、Issueラベルを優先して推論する。
3. Issue がありラベルで決まらない場合は、Issueタイトルと本文から推論する。
4. Issue が無い場合は、ユーザー指示、変更ファイル、差分の目的から推論する。
5. 判定不能なら `chore` を採用する。
- 推論時の優先順: `fix > ci > docs > refactor > chore > feature`

## Branch Name Slug
- 生成元の優先順:
1. 入力 `Branch Name`
2. Issue本文の `変更内容（What）`
3. Issue本文の `受け入れ条件（Acceptance Criteria）`
4. Issueタイトル
5. ユーザー指示
6. 変更ファイル名または差分の目的
- 生成ルール:
  - 小文字 kebab-case に正規化する。
  - 英数字以外は区切りとして扱う。
  - 2〜4語を優先する。
  - 実装手段語（`refactor`、`rename`、`tmp`、`wip` など）は除外する。
